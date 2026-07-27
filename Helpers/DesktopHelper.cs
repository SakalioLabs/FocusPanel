using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FocusPanel.Helpers;

public static class DesktopHelper
{
    private const int SwHide = 0;
    private const int SwShow = 5;
    private const int ShcneAssocChanged = 0x08000000;
    private const uint ShcnfIdList = 0x0000;
    private const uint SmtoNormal = 0x0000;
    private const uint WmSpawnWorkerW = 0x052C;

    public static void ToggleDesktopIcons(bool show)
    {
        IntPtr handle = GetDesktopListViewHandle();
        if (handle != IntPtr.Zero)
            ShowWindow(handle, show ? SwShow : SwHide);
    }

    public static bool IsDesktopIconsVisible()
    {
        IntPtr handle = GetDesktopListViewHandle();
        return handle == IntPtr.Zero || IsWindowVisible(handle);
    }

    public static void RefreshDesktop()
        => SHChangeNotify(ShcneAssocChanged, ShcnfIdList, IntPtr.Zero, IntPtr.Zero);

    public static bool IsCursorOverDesktop()
    {
        if (!GetCursorPos(out Point point))
            return false;

        IntPtr window = WindowFromPoint(point);
        var className = new StringBuilder(128);
        while (window != IntPtr.Zero)
        {
            className.Clear();
            int length = GetClassName(window, className, className.Capacity);
            if (length > 0 && DesktopDropTargetPolicy.IsDesktopWindowClass(className.ToString()))
                return true;

            window = GetParent(window);
        }

        return false;
    }

    private static IntPtr GetDesktopListViewHandle()
    {
        IntPtr handle = TryGetDesktopListViewHandle();
        if (handle != IntPtr.Zero)
            return handle;

        IntPtr progman = FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            SendMessageTimeout(
                progman,
                WmSpawnWorkerW,
                IntPtr.Zero,
                IntPtr.Zero,
                SmtoNormal,
                1000,
                out _);
            RefreshDesktop();
        }

        return TryGetDesktopListViewHandle();
    }

    private static IntPtr TryGetDesktopListViewHandle()
    {
        IntPtr progman = FindWindow("Progman", null);
        IntPtr shellView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (shellView == IntPtr.Zero)
        {
            IntPtr worker = IntPtr.Zero;
            while ((worker = FindWindowEx(IntPtr.Zero, worker, "WorkerW", null)) != IntPtr.Zero)
            {
                shellView = FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                    break;
            }
        }

        return shellView == IntPtr.Zero
            ? IntPtr.Zero
            : FindWindowEx(shellView, IntPtr.Zero, "SysListView32", "FolderView");
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(
        IntPtr parent,
        IntPtr childAfter,
        string className,
        string? windowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr window,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeout,
        out IntPtr result);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(
        int eventId,
        uint flags,
        IntPtr item1,
        IntPtr item2);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Point
    {
        public readonly int X;
        public readonly int Y;
    }
}

public static class DesktopDropTargetPolicy
{
    public static bool IsDesktopWindowClass(ReadOnlySpan<char> className)
        => className.Equals("Progman", StringComparison.Ordinal)
           || className.Equals("WorkerW", StringComparison.Ordinal)
           || className.Equals("SHELLDLL_DefView", StringComparison.Ordinal)
           || className.Equals("SysListView32", StringComparison.Ordinal);
}
