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

    public static void ApplyCurrentTheme()
    {
        if (Application.Current == null)
            return;

        bool dark = IsDarkTheme;
        bool translucent = CanUseTransparency;
        SetBrush("FocusTextBrush", dark ? "#FFF5F7FF" : "#FF1D2433");
        SetBrush("FocusMutedTextBrush", dark ? "#FFAEB5C8" : "#FF667085");
        SetBrush("FocusSurfaceBrush", translucent
            ? (dark ? "#40191C24" : "#55FFFFFF")
            : (dark ? "#FF191C24" : "#FFF6F8FC"));
        SetBrush("FocusSurfaceStrongBrush", translucent
            ? (dark ? "#7A222630" : "#8AFFFFFF")
            : (dark ? "#FF222630" : "#FFFFFFFF"));
        SetBrush("FocusSurfaceSoftBrush", translucent
            ? (dark ? "#382C313D" : "#45FFFFFF")
            : (dark ? "#FF2C313D" : "#FFE9EDF5"));
        SetBrush("FocusStrokeBrush", dark ? "#24FFFFFF" : "#1F0B1220");
        SetBrush("FocusHoverBrush", dark ? "#24FFFFFF" : "#160B1220");
        SetBrush("FocusAccentBrush", "#FF7C8CFF");
        SetBrush("FocusAccentBrightBrush", dark ? "#FFA9B4FF" : "#FF5868E8");
        SetBrush("FocusDangerBrush", dark ? "#FFFF6B7A" : "#FFD92D4B");
        SetBrush("PrimaryHueMidBrush", "#FF7C8CFF");
        SetBrush("PrimaryHueLightBrush", dark ? "#FFA9B4FF" : "#FF5868E8");
        SetBrush("MaterialDesignBody", dark ? "#FFF5F7FF" : "#FF1D2433");
        SetBrush("MaterialDesignBodyLight", dark ? "#FFAEB5C8" : "#FF667085");
        SetBrush("MaterialDesignPaper", dark ? "#F0222630" : "#F2FFFFFF");
        SetBrush("MaterialDesignDivider", dark ? "#33FFFFFF" : "#260B1220");
        SetBrush("MaterialDesignCardBackground", dark ? "#802C313D" : "#BDE9EDF5");
    }

    private static void SetBrush(string key, string colorText)
    {
        Color color = (Color)ColorConverter.ConvertFromString(colorText);
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
