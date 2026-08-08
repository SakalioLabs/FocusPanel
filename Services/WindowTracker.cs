using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using FocusPanel.Helpers;
using FocusPanel.Models;
using Forms = System.Windows.Forms;

namespace FocusPanel.Services;

public sealed class WindowTracker : IWindowTracker
{
    private const uint WineventOutOfContext = 0;
    private const uint WineventSkipOwnProcess = 0x0002;
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsExTopmost = 0x00000008L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExAppWindow = 0x00040000L;
    private const long WsExNoActivate = 0x08000000L;
    private const int DwmwaCloaked = 14;
    private static readonly IntPtr HwndMessage =
        new(-3);

    private readonly DispatcherTimer _refreshDebounce;
    private readonly Dispatcher _uiDispatcher;
    private readonly CoalescingBackgroundRefresh<
        PendingWindowSnapshot> _snapshotRefresh;
    private readonly NativeMethods.WinEventDelegate _callback;
    private readonly List<IntPtr> _hooks = new();
    private readonly IAppIdentityResolver _identityResolver;
    private readonly WindowCommandExecutor _commands;
    private readonly WindowAttentionState
        _attention = new();
    private readonly int _currentSessionId;
    private readonly string _windowsDirectory;
    private readonly ResilientSnapshotStore<WindowTaskItem>
        _snapshotStore = new();
    private volatile bool _trackingActive = true;
    private volatile bool _disposed;
    private long _snapshotRevision;

    public WindowTracker() : this(new AppIdentityResolver())
    {
    }

    internal WindowTracker(IAppIdentityResolver identityResolver)
    {
        _identityResolver = identityResolver;
        using (Process current = Process.GetCurrentProcess())
            _currentSessionId = current.SessionId;
        _windowsDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.Windows);
        _uiDispatcher = Dispatcher.CurrentDispatcher;
        _commands = new WindowCommandExecutor(
            new WindowsWindowCommandBoundary());
        _snapshotRefresh =
            new CoalescingBackgroundRefresh<
                PendingWindowSnapshot>(
                CapturePendingSnapshot,
                ApplySnapshotAsync,
                ex => Debug.WriteLine(
                    "Window snapshot refresh failed; "
                    + "keeping the last valid snapshot: "
                    + ex.Message));
        _callback = OnWinEvent;
        _refreshDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        _refreshDebounce.Tick +=
            RefreshDebounce_Tick;

        AddHook(
            WindowTrackingEventPolicy.EventSystemAlert,
            WindowTrackingEventPolicy.EventSystemAlert);
        AddHook(
            WindowTrackingEventPolicy.EventSystemForeground,
            WindowTrackingEventPolicy.EventSystemForeground);
        AddHook(
            WindowTrackingEventPolicy.EventSystemMinimizeStart,
            WindowTrackingEventPolicy.EventSystemMinimizeEnd);
        AddHook(
            WindowTrackingEventPolicy.EventObjectCreate,
            WindowTrackingEventPolicy.EventObjectHide);
        AddHook(
            WindowTrackingEventPolicy.EventObjectLocationChange,
            WindowTrackingEventPolicy.EventObjectLocationChange);
        AddHook(
            WindowTrackingEventPolicy.EventObjectNameChange,
            WindowTrackingEventPolicy.EventObjectNameChange);
        RequestSnapshotRefresh();
    }

    public event EventHandler? SnapshotChanged;

    public IReadOnlyList<WindowTaskItem> GetSnapshot() =>
        _snapshotStore.Current;

    public void RequestRefresh() =>
        RequestSnapshotRefresh();

    public void SetTrackingActive(bool isActive)
    {
        if (_disposed)
            return;

        bool wasActive = _trackingActive;
        if (wasActive == isActive)
            return;

        _trackingActive = isActive;
        _refreshDebounce.Stop();
        if (!isActive)
            _attention.ClearAll();
        if (WindowTrackingActivityPolicy.ShouldRefreshAfterActivityChange(
                wasActive,
                isActive))
        {
            RequestSnapshotRefresh();
        }
    }

    public bool ActivateOrMinimize(
        WindowTaskItem task) =>
        _commands.ActivateOrMinimize(task);

    public bool Activate(IntPtr handle) =>
        _commands.Activate(handle);

    public bool Minimize(IntPtr handle)
    {
        bool succeeded =
            _commands.Minimize(handle);
        if (succeeded)
            RequestSnapshotRefresh();
        return succeeded;
    }

    public bool Maximize(IntPtr handle)
    {
        bool succeeded =
            _commands.Maximize(handle);
        if (succeeded)
            RequestSnapshotRefresh();
        return succeeded;
    }

    public bool Restore(IntPtr handle)
    {
        bool succeeded =
            _commands.Restore(handle);
        if (succeeded)
            RequestSnapshotRefresh();
        return succeeded;
    }

    public bool Arrange(
        IntPtr handle,
        WindowLayoutTarget target)
    {
        bool succeeded =
            _commands.Arrange(
                handle,
                target);
        if (succeeded)
        {
            RequestSnapshotRefresh();
            ScheduleSnapshotRefresh();
        }
        return succeeded;
    }

    public bool CanMoveToDisplay(
        IntPtr handle,
        Rectangle targetWorkArea) =>
        _commands.CanMoveToDisplay(
            handle,
            targetWorkArea);

    public bool MoveToDisplay(
        IntPtr handle,
        Rectangle targetWorkArea)
    {
        bool succeeded =
            _commands.MoveToDisplay(
                handle,
                targetWorkArea);
        if (succeeded)
            RequestSnapshotRefresh();
        return succeeded;
    }

    public bool SetTopmost(
        IntPtr handle,
        bool isTopmost)
    {
        bool succeeded =
            _commands.SetTopmost(
                handle,
                isTopmost);
        if (succeeded)
        {
            RequestSnapshotRefresh();
            ScheduleSnapshotRefresh();
        }
        return succeeded;
    }

    public bool Close(IntPtr handle) =>
        _commands.Close(handle);

    public bool IsForegroundFullscreen()
    {
        if (_disposed)
            return false;

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

    private void RefreshDebounce_Tick(
        object? sender,
        EventArgs e)
    {
        _refreshDebounce.Stop();
        if (_trackingActive && !_disposed)
            RequestSnapshotRefresh();
    }

    private void RequestSnapshotRefresh()
    {
        if (_disposed || !_trackingActive)
            return;

        Interlocked.Increment(
            ref _snapshotRevision);
        _snapshotRefresh.Request();
    }

    private PendingWindowSnapshot
        CapturePendingSnapshot() =>
        new(
            Interlocked.Read(
                ref _snapshotRevision),
            CaptureSnapshot());

    private async Task ApplySnapshotAsync(
        PendingWindowSnapshot pending,
        CancellationToken cancellationToken)
    {
        await _uiDispatcher.InvokeAsync(
            () =>
            {
                if (!WindowSnapshotApplyPolicy.CanApply(
                        pending.Revision,
                        Interlocked.Read(
                            ref _snapshotRevision),
                        _trackingActive,
                        _disposed,
                        cancellationToken
                            .IsCancellationRequested))
                {
                    return;
                }

                if (!_snapshotStore.TryRefresh(
                        () => pending.Items,
                        out Exception? failure))
                {
                    Debug.WriteLine(
                        "Window snapshot commit failed; "
                        + "keeping the last valid snapshot: "
                        + failure?.Message);
                    return;
                }

                PublishSnapshotChangedSafely();
            },
            DispatcherPriority.Background,
            cancellationToken);
    }

    private IReadOnlyList<WindowTaskItem> CaptureSnapshot()
    {
        var windows = new List<WindowEntry>();
        var backgroundOwners =
            new Dictionary<uint, BackgroundAppObservation>();
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        _attention.Clear(foreground);

        bool enumerated =
            NativeMethods.EnumWindows((hwnd, _) =>
        {
            try
            {
                CaptureBackgroundOwner(
                    hwnd,
                    backgroundOwners);
                CaptureWindow(
                    hwnd,
                    foreground,
                    windows);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Skipping window 0x{hwnd.ToInt64():X}: "
                    + ex.Message);
            }

            return true;
        }, IntPtr.Zero);
        if (!enumerated)
        {
            int error = Marshal.GetLastWin32Error();
            throw error == 0
                ? new InvalidOperationException(
                    "Windows 未能枚举顶层窗口。")
                : new Win32Exception(
                    error,
                    "Windows 未能枚举顶层窗口。");
        }

        CaptureMessageOnlyBackgroundOwners(
            backgroundOwners);

        List<WindowTaskItem> taskWindows = windows
            .GroupBy(item => item.IdentityKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                WindowEntry first = group.First();
                bool isActive = group.Any(
                    item => item.IsActive);
                if (isActive)
                {
                    foreach (WindowEntry item in group)
                        _attention.Clear(item.Handle);
                }
                string? resolvedExecutable = group
                    .Select(item => item.ExecutablePath)
                    .FirstOrDefault(value => value != null);
                ImageSource? icon = null;
                if (resolvedExecutable != null)
                {
                    try
                    {
                        icon = IconHelper.GetIcon(
                            resolvedExecutable);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            "Window icon extraction failed: "
                            + ex.Message);
                    }
                }

                return new WindowTaskItem
                {
                    AppKey = group.Key,
                    IdentityKey = group.Key,
                    ApplicationUserModelId = group
                        .Select(item => item.ApplicationUserModelId)
                        .FirstOrDefault(value => value != null),
                    DisplayName = first.ProcessName,
                    ExecutablePath = resolvedExecutable,
                    Icon = icon,
                    Windows = group
                        .Select(item =>
                            new WindowReference(
                                item.Handle,
                                item.Title,
                                item.IsActive,
                                item.State,
                                item.IsTopmost,
                                !isActive
                                && item.IsAttentionRequested))
                        .ToList(),
                    IsActive = isActive
                };
            })
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        _attention.Retain(
            windows.Select(item => item.Handle));
        return BackgroundAppSnapshotComposer.Append(
            taskWindows,
            backgroundOwners.Values);
    }

    private void CaptureMessageOnlyBackgroundOwners(
        IDictionary<uint, BackgroundAppObservation>
            owners)
    {
        try
        {
            IReadOnlyList<IntPtr> messageWindows =
                MessageOnlyWindowEnumerator.Enumerate(
                    previous =>
                        NativeMethods.FindWindowEx(
                            HwndMessage,
                            previous,
                            null,
                            null));
            foreach (IntPtr hwnd in messageWindows)
            {
                try
                {
                    CaptureBackgroundOwner(
                        hwnd,
                        owners);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        "Skipping message-only window "
                        + $"0x{hwnd.ToInt64():X}: "
                        + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                "Message-only window enumeration failed; "
                + "visible windows remain available: "
                + ex.Message);
        }
    }

    private void CaptureBackgroundOwner(
        IntPtr hwnd,
        IDictionary<uint, BackgroundAppObservation>
            owners)
    {
        NativeMethods.GetWindowThreadProcessId(
            hwnd,
            out uint processId);
        if (processId == 0
            || processId == Environment.ProcessId
            || owners.ContainsKey(processId))
        {
            return;
        }

        try
        {
            using Process process =
                Process.GetProcessById(
                    (int)processId);
            string? executablePath =
                process.MainModule?.FileName;
            if (!BackgroundAppVisibilityPolicy
                    .ShouldInclude(
                        processId,
                        Environment.ProcessId,
                        process.SessionId,
                        _currentSessionId,
                        executablePath,
                        _windowsDirectory))
            {
                return;
            }

            ResolvedAppIdentity identity =
                _identityResolver.ResolveWindow(
                    hwnd,
                    processId,
                    executablePath);
            ImageSource? icon = null;
            try
            {
                icon = IconHelper.GetIcon(
                    executablePath!);
            }
            catch
            {
                // Text and launch target remain useful when icon extraction fails.
            }

            string? description = null;
            try
            {
                description = process.MainModule
                    ?.FileVersionInfo
                    .FileDescription;
            }
            catch
            {
                // Protected metadata falls back to the process name.
            }

            owners.Add(
                processId,
                new BackgroundAppObservation(
                    processId,
                    BackgroundAppVisibilityPolicy
                        .GetDisplayName(
                            process.ProcessName,
                            description),
                    identity.ExecutablePath
                        ?? executablePath!,
                    identity.Key,
                    identity.ApplicationUserModelId,
                    icon));
        }
        catch
        {
            // Protected, short-lived and cross-session processes are skipped.
        }
    }

    private void CaptureWindow(
        IntPtr hwnd,
        IntPtr foreground,
        ICollection<WindowEntry> windows)
    {
        if (!IsTaskWindow(hwnd))
            return;

        NativeMethods.GetWindowThreadProcessId(
            hwnd,
            out uint processId);
        if (processId == Environment.ProcessId)
            return;

        string title = GetWindowTitle(hwnd);
        if (title.Length == 0)
            return;

        string? executablePath = null;
        string processName = title;
        try
        {
            using Process process =
                Process.GetProcessById(
                    (int)processId);
            processName = process.ProcessName;
            executablePath =
                process.MainModule?.FileName;
        }
        catch
        {
            // Protected processes remain usable through their window handle.
        }

        ResolvedAppIdentity identity;
        try
        {
            identity =
                _identityResolver.ResolveWindow(
                    hwnd,
                    processId,
                    executablePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                "Window identity resolution failed: "
                + ex.Message);
            identity = new ResolvedAppIdentity(
                AppIdentityResolver
                    .BuildTemporaryWindowKey(
                        hwnd,
                        processId),
                null,
                executablePath);
        }

        windows.Add(new WindowEntry(
            hwnd,
            title,
            processName,
            identity.ExecutablePath
                ?? executablePath,
            identity.Key,
            identity.ApplicationUserModelId,
            NativeMethods.IsIconic(hwnd)
                ? TrackedWindowState
                    .Minimized
                : NativeMethods.IsZoomed(hwnd)
                    ? TrackedWindowState
                        .Maximized
                    : TrackedWindowState
                        .Normal,
            hwnd == foreground,
            (NativeMethods.GetWindowLongPtr(
                    hwnd,
                    GwlExStyle)
                .ToInt64()
             & WsExTopmost) != 0,
            _attention.IsRequested(hwnd)));
    }

    private void PublishSnapshotChangedSafely()
    {
        EventSubscriberIsolation.Publish(
            SnapshotChanged,
            this,
            ex =>
                Debug.WriteLine(
                    "Window snapshot subscriber failed: "
                    + ex.Message));
    }

    private static bool IsTaskWindow(IntPtr hwnd)
    {
        long exStyle = NativeMethods
            .GetWindowLongPtr(
                hwnd,
                GwlExStyle)
            .ToInt64();
        bool isCloaked =
            NativeMethods.DwmGetWindowAttribute(
                hwnd,
                DwmwaCloaked,
                out int cloaked,
                sizeof(int)) == 0
            && cloaked != 0;
        return TaskWindowVisibilityPolicy
            .ShouldInclude(
                NativeMethods.IsWindowVisible(hwnd),
                (exStyle & WsExToolWindow) != 0,
                (exStyle & WsExNoActivate) != 0,
                NativeMethods.GetWindow(hwnd, 4)
                    != IntPtr.Zero,
                (exStyle & WsExAppWindow) != 0,
                isCloaked);
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
            WineventOutOfContext
            | WineventSkipOwnProcess);
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
        if (_disposed)
            return;

        if (!WindowTrackingEventPolicy.ShouldQueueRefresh(
                eventType,
                idObject))
        {
            return;
        }
        if (!WindowTrackingActivityPolicy.ShouldProcessWindowEvent(
                _trackingActive))
        {
            return;
        }

        IntPtr attentionWindow =
            NormalizeAttentionWindow(hwnd);
        _attention.Observe(
            eventType,
            attentionWindow,
            NativeMethods
                .GetForegroundWindow());

        ScheduleSnapshotRefresh();
    }

    private static IntPtr NormalizeAttentionWindow(
        IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return IntPtr.Zero;

        IntPtr rootOwner =
            NativeMethods.GetAncestor(hwnd, 3);
        return rootOwner == IntPtr.Zero
            ? hwnd
            : rootOwner;
    }

    private void ScheduleSnapshotRefresh()
    {
        void RestartDebounce()
        {
            if (!_trackingActive
                || _disposed
                || _refreshDebounce.Dispatcher
                    .HasShutdownStarted
                || _refreshDebounce.Dispatcher
                    .HasShutdownFinished)
            {
                return;
            }

            _refreshDebounce.Stop();
            _refreshDebounce.Start();
        }

        Dispatcher dispatcher =
            _refreshDebounce.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            RestartDebounce();
        }
        else if (!dispatcher.HasShutdownStarted
                 && !dispatcher.HasShutdownFinished)
        {
            try
            {
                dispatcher.BeginInvoke(
                    RestartDebounce);
            }
            catch (InvalidOperationException)
            {
                // Dispatcher shutdown raced the native WinEvent callback.
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _trackingActive = false;
        _attention.ClearAll();
        _snapshotRefresh.Dispose();
        _refreshDebounce.Stop();
        _refreshDebounce.Tick -=
            RefreshDebounce_Tick;
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
        TrackedWindowState State,
        bool IsActive,
        bool IsTopmost,
        bool IsAttentionRequested);

    private sealed record PendingWindowSnapshot(
        long Revision,
        IReadOnlyList<WindowTaskItem> Items);

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

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumWindowsDelegate callback, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(IntPtr hwnd);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindow(IntPtr hwnd, uint command);

        [DllImport(
            "user32.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        internal static extern IntPtr FindWindowEx(
            IntPtr parent,
            IntPtr childAfter,
            string? className,
            string? windowName);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetAncestor(
            IntPtr hwnd,
            uint flags);

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
        internal static extern bool IsZoomed(IntPtr hwnd);

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
