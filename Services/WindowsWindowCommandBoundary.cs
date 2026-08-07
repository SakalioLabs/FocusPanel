using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

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

    public bool TryGetRestoredBounds(
        IntPtr handle,
        out Rectangle bounds)
    {
        var placement = new NativeMethods.WindowPlacement
        {
            Length = Marshal.SizeOf<
                NativeMethods.WindowPlacement>()
        };
        if (!NativeMethods.GetWindowPlacement(
                handle,
                ref placement))
        {
            bounds = Rectangle.Empty;
            return false;
        }

        NativeMethods.Rect rect =
            placement.NormalPosition;
        bounds = Rectangle.FromLTRB(
            rect.Left,
            rect.Top,
            rect.Right,
            rect.Bottom);
        return bounds.Width > 0
            && bounds.Height > 0;
    }

    public Rectangle GetWorkingArea(
        IntPtr handle) =>
        handle == IntPtr.Zero
            ? Rectangle.Empty
            : Forms.Screen
                .FromHandle(handle)
                .WorkingArea;

    public bool SetRestoredBounds(
        IntPtr handle,
        Rectangle bounds)
    {
        var placement = new NativeMethods.WindowPlacement
        {
            Length = Marshal.SizeOf<
                NativeMethods.WindowPlacement>()
        };
        if (!NativeMethods.GetWindowPlacement(
                handle,
                ref placement))
        {
            return false;
        }

        placement.NormalPosition =
            new NativeMethods.Rect(
                bounds.Left,
                bounds.Top,
                bounds.Right,
                bounds.Bottom);
        return NativeMethods.SetWindowPlacement(
            handle,
            ref placement);
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            internal int X;
            internal int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            internal Rect(
                int left,
                int top,
                int right,
                int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowPlacement
        {
            internal int Length;
            internal int Flags;
            internal int ShowCommand;
            internal Point MinPosition;
            internal Point MaxPosition;
            internal Rect NormalPosition;
        }

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

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowPlacement(
            IntPtr hwnd,
            ref WindowPlacement placement);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPlacement(
            IntPtr hwnd,
            ref WindowPlacement placement);
    }
}
