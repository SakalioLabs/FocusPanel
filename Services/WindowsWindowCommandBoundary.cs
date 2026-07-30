using System;
using System.Runtime.InteropServices;

namespace FocusPanel.Services;

internal sealed class WindowsWindowCommandBoundary :
    IWindowCommandBoundary
{
    private const uint WmClose = 0x0010;

    public bool IsWindow(IntPtr handle) =>
        NativeMethods.IsWindow(handle);

    public IntPtr GetForegroundWindow() =>
        NativeMethods.GetForegroundWindow();

    public bool IsIconic(IntPtr handle) =>
        NativeMethods.IsIconic(handle);

    public bool IsZoomed(IntPtr handle) =>
        NativeMethods.IsZoomed(handle);

    public void ShowWindow(
        IntPtr handle,
        int command) =>
        NativeMethods.ShowWindow(handle, command);

    public bool SetForegroundWindow(IntPtr handle) =>
        NativeMethods.SetForegroundWindow(handle);

    public bool PostClose(IntPtr handle) =>
        NativeMethods.PostMessage(
            handle,
            WmClose,
            IntPtr.Zero,
            IntPtr.Zero);

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr hwnd);

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
        internal static extern bool ShowWindow(
            IntPtr hwnd,
            int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(
            IntPtr hwnd);

        [DllImport(
            "user32.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessage(
            IntPtr hwnd,
            uint message,
            IntPtr wParam,
            IntPtr lParam);
    }
}
