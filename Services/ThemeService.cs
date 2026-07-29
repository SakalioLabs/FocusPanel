using System;
using System.Windows;
using System.Windows.Media;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace FocusPanel.Services;

public static class ThemeService
{
    private const int SmRemoteSession = 0x1000;
    private static string _currentMode = "System";
    private static bool _nativeBackdropActive;

    public static string CurrentMode => _currentMode;

    public static bool IsDarkTheme
    {
        get
        {
            if (SystemParameters.HighContrast)
                return true;

            if (_currentMode.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                return true;
            if (_currentMode.Equals("Light", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int lightTheme && lightTheme == 0;
            }
            catch
            {
                return true;
            }
        }
    }

    public static bool CanUseTransparency
    {
        get
        {
            if (SystemParameters.HighContrast || GetSystemMetrics(SmRemoteSession) != 0)
                return false;

            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("EnableTransparency") is int transparencyEnabled
                    && transparencyEnabled == 0)
                    return false;
            }
            catch
            {
                return false;
            }

            return DwmIsCompositionEnabled(out bool enabled) == 0 && enabled;
        }
    }

    public static void SetMode(string? mode)
    {
        _currentMode = mode is "Light" or "Dark" ? mode : "System";
        ApplyCurrentTheme();
    }

    public static void SetNativeBackdropActive(bool active)
    {
        if (_nativeBackdropActive == active)
            return;

        _nativeBackdropActive = active;
        ApplyCurrentTheme();
    }

    public static void ApplyCurrentTheme()
    {
        if (Application.Current == null)
            return;

        bool dark = IsDarkTheme;
        bool translucent = CanUseTransparency;
        Color accent = SystemParameters.WindowGlassColor;
        if (accent.A == 0)
            accent = Color.FromRgb(0x00, 0x6F, 0xC4);
        accent.A = 0xFF;
        Color accentBright = Color.FromRgb(
            (byte)Math.Min(255, accent.R + 48),
            (byte)Math.Min(255, accent.G + 48),
            (byte)Math.Min(255, accent.B + 48));

        SetBrush("FocusTextBrush", dark ? "#FFF5F7FF" : "#FF1D2433");
        SetBrush("FocusMutedTextBrush", dark ? "#FFAEB5C8" : "#FF667085");
        SetBrush("FocusShellTintBrush", _nativeBackdropActive && translucent
            ? (dark ? "#10191C24" : "#14FFFFFF")
            : (dark ? "#FF191C24" : "#FFF6F8FC"));
        SetBrush("FocusSurfaceBrush", translucent
            ? (dark ? "#521E222B" : "#66FFFFFF")
            : (dark ? "#FF191C24" : "#FFF6F8FC"));
        SetBrush("FocusSurfaceStrongBrush", translucent
            ? (dark ? "#D020242C" : "#E6FFFFFF")
            : (dark ? "#FF222630" : "#FFFFFFFF"));
        SetBrush(
            "FocusPopupSurfaceBrush",
            dark ? "#FF20242C" : "#FFFFFFFF");
        SetBrush("FocusSurfaceSoftBrush", translucent
            ? (dark ? "#302C313D" : "#38FFFFFF")
            : (dark ? "#FF2C313D" : "#FFE9EDF5"));
        SetBrush("FocusStrokeBrush", dark ? "#1CFFFFFF" : "#180B1220");
        SetBrush("FocusHoverBrush", dark ? "#18FFFFFF" : "#120B1220");
        SetBrush("FocusAccentBrush", accent);
        SetBrush("FocusAccentBrightBrush", accentBright);
        Color accentSoft = accent;
        accentSoft.A = dark ? (byte)0x2E : (byte)0x24;
        SetBrush("FocusAccentSoftBrush", accentSoft);
        SetBrush(
            "FocusKeyboardFocusBrush",
            SystemParameters.HighContrast
                ? SystemColors.HighlightColor
                : accentBright);
        Color danger = SystemParameters.HighContrast
            ? SystemColors.HighlightColor
            : dark
                ? Color.FromRgb(0xFF, 0x6B, 0x7A)
                : Color.FromRgb(0xD9, 0x2D, 0x4B);
        SetBrush("FocusDangerBrush", danger);
        Color dangerSoft = SystemParameters.HighContrast
            ? SystemColors.WindowColor
            : danger;
        if (!SystemParameters.HighContrast)
            dangerSoft.A = dark ? (byte)0x26 : (byte)0x20;
        SetBrush("FocusDangerSoftBrush", dangerSoft);
        Color warning = SystemParameters.HighContrast
            ? SystemColors.HighlightColor
            : dark
                ? Color.FromRgb(0xFF, 0xC8, 0x57)
                : Color.FromRgb(0x9A, 0x67, 0x00);
        SetBrush("FocusWarningBrush", warning);
        Color warningSoft = SystemParameters.HighContrast
            ? SystemColors.WindowColor
            : dark
                ? Color.FromArgb(0x33, 0xFF, 0xB0, 0x20)
                : Color.FromArgb(0x24, 0xB7, 0x79, 0x1F);
        SetBrush("FocusWarningSoftBrush", warningSoft);
        Color warningText = SystemParameters.HighContrast
            ? SystemColors.HighlightTextColor
            : dark
                ? Color.FromRgb(0xF5, 0xF7, 0xFF)
                : Color.FromRgb(0x49, 0x30, 0x00);
        SetBrush("FocusWarningTextBrush", warningText);
        SetBrush("FocusOverlayBrush",
            dark ? "#50000000" : "#380B1220");
        Color edgeIndicator = SystemParameters.HighContrast
            ? SystemColors.HighlightColor
            : dark
                ? Color.FromArgb(0xC8, 0xFF, 0xFF, 0xFF)
                : accentBright;
        SetBrush("FocusEdgeIndicatorBrush", edgeIndicator);
    }

    private static void SetBrush(string key, string colorText)
    {
        Color color = (Color)ColorConverter.ConvertFromString(colorText);
        if (Application.Current.Resources[key] is SolidColorBrush brush && !brush.IsFrozen)
            brush.Color = color;
        else
            Application.Current.Resources[key] = new SolidColorBrush(color);
    }

    private static void SetBrush(string key, Color color)
    {
        if (Application.Current.Resources[key] is SolidColorBrush brush && !brush.IsFrozen)
            brush.Color = color;
        else
            Application.Current.Resources[key] = new SolidColorBrush(color);
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
