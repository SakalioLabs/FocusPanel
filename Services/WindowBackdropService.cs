using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace FocusPanel.Services;

internal static class WindowBackdropService
{
    private const int DwmaUseImmersiveDarkMode = 20;
    private const int DwmaWindowCornerPreference = 33;
    private const int DwmaBorderColor = 34;
    private const int DwmaSystemBackdropType = 38;
    private const int DwmcpRound = 2;
    private const int DwmsbtNone = 1;
    private const int DwmsbtTransientWindow = 3;

    internal static bool Apply(
        Window window,
        bool updateThemeState = false)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return false;

        bool backdropActive = false;
        try
        {
            int cornerPreference = DwmcpRound;
            DwmSetWindowAttribute(
                hwnd,
                DwmaWindowCornerPreference,
                ref cornerPreference,
                sizeof(int));

            int borderColor = unchecked((int)0xFFFFFFFE);
            DwmSetWindowAttribute(
                hwnd,
                DwmaBorderColor,
                ref borderColor,
                sizeof(int));

            int darkMode = ThemeService.IsDarkTheme ? 1 : 0;
            DwmSetWindowAttribute(
                hwnd,
                DwmaUseImmersiveDarkMode,
                ref darkMode,
                sizeof(int));

            int backdrop = ThemeService.CanUseTransparency
                ? DwmsbtTransientWindow
                : DwmsbtNone;
            if (backdrop == DwmsbtTransientWindow)
            {
                var margins = new Margins(-1, -1, -1, -1);
                int frameResult =
                    DwmExtendFrameIntoClientArea(
                        hwnd,
                        ref margins);
                int backdropResult =
                    DwmSetWindowAttribute(
                        hwnd,
                        DwmaSystemBackdropType,
                        ref backdrop,
                        sizeof(int));
                backdropActive =
                    frameResult == 0
                    && backdropResult == 0;
            }
            else
            {
                DwmSetWindowAttribute(
                    hwnd,
                    DwmaSystemBackdropType,
                    ref backdrop,
                    sizeof(int));
            }

            if (HwndSource.FromHwnd(hwnd)
                is HwndSource source)
            {
                source.CompositionTarget.BackgroundColor =
                    Colors.Transparent;
            }
        }
        catch (DllNotFoundException)
        {
            backdropActive = false;
        }
        catch (EntryPointNotFoundException)
        {
            backdropActive = false;
        }

        if (updateThemeState)
            ThemeService.SetNativeBackdropActive(backdropActive);

        return backdropActive;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Margins
    {
        internal readonly int Left;
        internal readonly int Right;
        internal readonly int Top;
        internal readonly int Bottom;

        internal Margins(
            int left,
            int right,
            int top,
            int bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int size);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmExtendFrameIntoClientArea(
        IntPtr hwnd,
        ref Margins margins);
}
