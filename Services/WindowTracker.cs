using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using FocusPanel.Helpers;
using FocusPanel.Models;
using Forms = System.Windows.Forms;

namespace FocusPanel.Services;

public sealed class WindowTracker : IWindowTracker
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectShow = 0x8002;
    private const uint EventObjectHide = 0x8003;
    private const uint EventObjectNameChange = 0x800C;
    private const uint WineventOutOfContext = 0;
    private const int ObjidWindow = 0;
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const int SwMinimize = 6;
    private const int SwRestore = 9;
    private const uint WmClose = 0x0010;
    private const int DwmwaCloaked = 14;

    private readonly DispatcherTimer _refreshDebounce;
    private readonly NativeMethods.WinEventDelegate _callback;
    private readonly List<IntPtr> _hooks = new();
    private readonly IAppIdentityResolver _identityResolver;
    private IReadOnlyList<WindowTaskItem> _snapshot = Array.Empty<WindowTaskItem>();
    private volatile bool _trackingActive = true;

    public WindowTracker() : this(new AppIdentityResolver())
    {
    }

    internal WindowTracker(IAppIdentityResolver identityResolver)
    {
        _identityResolver = identityResolver;
        _callback = OnWinEvent;
        _refreshDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        _refreshDebounce.Tick += (_, _) =>
        {
            _refreshDebounce.Stop();
            if (_trackingActive)
                RefreshSnapshot();
        };

        AddHook(EventSystemForeground, EventSystemForeground);
        AddHook(EventObjectShow, EventObjectHide);
        AddHook(EventObjectNameChange, EventObjectNameChange);
        RefreshSnapshot();
    }

    public event EventHandler? SnapshotChanged;

    public IReadOnlyList<WindowTaskItem> GetSnapshot() => _snapshot;

    public void SetTrackingActive(bool isActive)
    {
        bool wasActive = _trackingActive;
        if (wasActive == isActive)
            return;

        _trackingActive = isActive;
        _refreshDebounce.Stop();
        if (WindowTrackingActivityPolicy.ShouldRefreshAfterActivityChange(
                wasActive,
                isActive))
        {
            RefreshSnapshot();
        }
    }

    public void ActivateOrMinimize(WindowTaskItem task)
    {
        if (task.PrimaryHandle == IntPtr.Zero)
            return;

        if (NativeMethods.GetForegroundWindow() == task.PrimaryHandle)
            NativeMethods.ShowWindow(task.PrimaryHandle, SwMinimize);
        else
            Activate(task.PrimaryHandle);
    }

    public void Activate(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return;

        if (NativeMethods.IsIconic(handle))
            NativeMethods.ShowWindow(handle, SwRestore);
        NativeMethods.SetForegroundWindow(handle);
    }

    public void Close(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
            NativeMethods.PostMessage(handle, WmClose, IntPtr.Zero, IntPtr.Zero);
    }

    public bool IsForegroundFullscreen()
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero || !NativeMethods.GetWindowRect(foreground, out NativeRect rect))
            return false;

        NativeMethods.GetWindowThreadProcessId(foreground, out uint processId);
        var className = new StringBuilder(256);
        NativeMethods.GetClassName(foreground, className, className.Capacity);
        Forms.Screen screen = Forms.Screen.FromHandle(foreground);
        var windowBounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        long style = NativeMethods.GetWindowLongPtr(foreground, GwlStyle).ToInt64();
        bool hasStandardFrame = (style & (WsCaption | WsThickFrame)) != 0;
        return FullscreenWindowPolicy.IsFullscreenApplication(
            className.ToString(),
            processId,
            (uint)Environment.ProcessId,
            windowBounds,
            screen.Bounds,
            NativeMethods.IsZoomed(foreground),
            hasStandardFrame);
    }

    private void RefreshSnapshot()
    {
        var windows = new List<WindowEntry>();
        IntPtr foreground = NativeMethods.GetForegroundWindow();

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!IsTaskWindow(hwnd))
                return true;

            NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == Environment.ProcessId)
                return true;

            string title = GetWindowTitle(hwnd);
            if (title.Length == 0)
                return true;

            string? executablePath = null;
            string processName = title;
            try
            {
                using Process process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
                executablePath = process.MainModule?.FileName;
            }
            catch
            {
                // Protected processes still remain usable through their window handle.
            }

            ResolvedAppIdentity identity = _identityResolver.ResolveWindow(
                hwnd,
                processId,
                executablePath);
            windows.Add(new WindowEntry(
                hwnd,
                title,
                processName,
                identity.ExecutablePath ?? executablePath,
                identity.Key,
                identity.ApplicationUserModelId,
                hwnd == foreground));
            return true;
        }, IntPtr.Zero);

        _snapshot = windows
            .GroupBy(item => item.IdentityKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                WindowEntry first = group.First();
                string? resolvedExecutable = group
                    .Select(item => item.ExecutablePath)
                    .FirstOrDefault(value => value != null);
                return new WindowTaskItem
                {
                    AppKey = group.Key,
                    IdentityKey = group.Key,
                    ApplicationUserModelId = group
                        .Select(item => item.ApplicationUserModelId)
                        .FirstOrDefault(value => value != null),
                    DisplayName = first.ProcessName,
                    ExecutablePath = resolvedExecutable,
                    Icon = resolvedExecutable == null ? null : IconHelper.GetIcon(resolvedExecutable),
                    Windows = group.Select(item => new WindowReference(item.Handle, item.Title)).ToList(),
                    IsActive = group.Any(item => item.IsActive)
                };
            })
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        SnapshotChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsTaskWindow(IntPtr hwnd)
    {
        if (!NativeMethods.IsWindowVisible(hwnd))
            return false;

        long exStyle = NativeMethods.GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        if ((exStyle & (WsExToolWindow | WsExNoActivate)) != 0)
            return false;

        if (NativeMethods.GetWindow(hwnd, 4) != IntPtr.Zero)
            return false;

        if (NativeMethods.DwmGetWindowAttribute(hwnd, DwmwaCloaked, out int cloaked, sizeof(int)) == 0
            && cloaked != 0)
            return false;

        return true;
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        int length = NativeMethods.GetWindowTextLength(hwnd);
        if (length <= 0)
            return string.Empty;

        var builder = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString().Trim();
    }

    private void AddHook(uint eventMin, uint eventMax)
    {
        IntPtr hook = NativeMethods.SetWinEventHook(
            eventMin,
            eventMax,
            IntPtr.Zero,
            _callback,
            0,
            0,
            WineventOutOfContext);
        if (hook != IntPtr.Zero)
            _hooks.Add(hook);
    }

    private void OnWinEvent(
        IntPtr hook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint eventThread,
        uint eventTime)
    {
        if (eventType >= EventObjectShow && idObject != ObjidWindow)
            return;
        if (!WindowTrackingActivityPolicy.ShouldProcessWindowEvent(
                _trackingActive))
        {
            return;
        }

        void RestartDebounce()
        {
            if (!_trackingActive)
                return;

            _refreshDebounce.Stop();
            _refreshDebounce.Start();
        }

        if (_refreshDebounce.Dispatcher.CheckAccess())
        {
            RestartDebounce();
        }
        else
        {
            _refreshDebounce.Dispatcher.BeginInvoke(RestartDebounce);
        }
    }

    public void Dispose()
    {
        _refreshDebounce.Stop();
        foreach (IntPtr hook in _hooks)
            NativeMethods.UnhookWinEvent(hook);
        _hooks.Clear();
    }

    private sealed record WindowEntry(
        IntPtr Handle,
        string Title,
        string ProcessName,
        string? ExecutablePath,
        string IdentityKey,
        string? ApplicationUserModelId,
        bool IsActive);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private static class NativeMethods
    {
        internal delegate bool EnumWindowsDelegate(IntPtr hwnd, IntPtr lParam);
        internal delegate void WinEventDelegate(
            IntPtr hook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint eventThread,
            uint eventTime);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumWindowsDelegate callback, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindow(IntPtr hwnd, uint command);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        internal static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

        [DllImport("user32.dll")]
        internal static extern int GetWindowTextLength(IntPtr hwnd);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsZoomed(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(IntPtr hwnd, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

        [DllImport("user32.dll")]
        internal static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr module,
            WinEventDelegate callback,
            uint processId,
            uint threadId,
            uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWinEvent(IntPtr hook);
    }
}
