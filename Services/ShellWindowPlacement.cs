using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace FocusPanel.Services;

internal readonly record struct PhysicalWindowBounds(
    int Left,
    int Top,
    int Width,
    int Height);

internal static class ShellWindowPlacement
{
    internal static PhysicalWindowBounds CalculatePanel(
        Rectangle screenBounds,
        uint dpi,
        double widthDip,
        double marginDip)
    {
        double scale = NormalizeDpi(dpi) / 96.0;
        int width = Math.Max(
            1,
            (int)Math.Round(widthDip * scale));
        int margin = Math.Max(
            0,
            (int)Math.Round(marginDip * scale));
        int height = Math.Max(
            (int)Math.Round(320 * scale),
            screenBounds.Height - margin * 2);
        return new PhysicalWindowBounds(
            screenBounds.Right - width - margin,
            screenBounds.Top + margin,
            width,
            height);
    }

    internal static PhysicalWindowBounds CalculateIndicator(
        Rectangle screenBounds,
        int widthPhysicalPixels) =>
        new(
            screenBounds.Right
                - Math.Max(1, widthPhysicalPixels),
            screenBounds.Top,
            Math.Max(1, widthPhysicalPixels),
            Math.Max(1, screenBounds.Height));

    internal static uint GetWindowDpi(IntPtr hwnd)
    {
        uint dpi = hwnd == IntPtr.Zero
            ? 0
            : NativeMethods.GetDpiForWindow(hwnd);
        if (dpi == 0)
            dpi = NativeMethods.GetDpiForSystem();
        return NormalizeDpi(dpi);
    }

    internal static uint GetTargetDpi(
        Rectangle screenBounds,
        IntPtr fallbackWindow)
    {
        var rect = new NativeMethods.Rect(
            screenBounds.Left,
            screenBounds.Top,
            screenBounds.Right,
            screenBounds.Bottom);
        IntPtr monitor = NativeMethods.MonitorFromRect(
            ref rect,
            NativeMethods.MonitorDefaultToNearest);
        if (monitor != IntPtr.Zero)
        {
            try
            {
                int result = NativeMethods.GetDpiForMonitor(
                    monitor,
                    NativeMethods.MdtEffectiveDpi,
                    out uint dpiX,
                    out _);
                if (result == 0)
                    return NormalizeDpi(dpiX);
            }
            catch (DllNotFoundException)
            {
                // Windows 8.1+ normally provides Shcore; keep a
                // window/system DPI fallback for restricted hosts.
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        return GetWindowDpi(fallbackWindow);
    }

    internal static bool Apply(
        IntPtr hwnd,
        PhysicalWindowBounds bounds)
    {
        if (hwnd == IntPtr.Zero)
            return false;

        return NativeMethods.SetWindowPos(
            hwnd,
            new IntPtr(-1),
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            NativeMethods.SwpNoActivate
                | NativeMethods.SwpNoOwnerZOrder);
    }

    private static uint NormalizeDpi(uint dpi) =>
        dpi is >= 48 and <= 768 ? dpi : 96u;

    private static class NativeMethods
    {
        internal const uint MonitorDefaultToNearest = 0x00000002;
        internal const int MdtEffectiveDpi = 0;
        internal const uint SwpNoActivate = 0x0010;
        internal const uint SwpNoOwnerZOrder = 0x0200;

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct Rect
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

            internal readonly int Left;
            internal readonly int Top;
            internal readonly int Right;
            internal readonly int Bottom;
        }

        [DllImport("user32.dll")]
        internal static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        internal static extern uint GetDpiForSystem();

        [DllImport("user32.dll")]
        internal static extern IntPtr MonitorFromRect(
            ref Rect rect,
            uint flags);

        [DllImport("shcore.dll")]
        internal static extern int GetDpiForMonitor(
            IntPtr monitor,
            int dpiType,
            out uint dpiX,
            out uint dpiY);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(
            IntPtr hwnd,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);
    }
}
