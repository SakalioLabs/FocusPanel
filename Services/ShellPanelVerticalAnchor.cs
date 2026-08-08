using System;

namespace FocusPanel.Services;

internal enum ShellPanelVerticalAnchor
{
    Top,
    Center,
    Bottom
}

internal static class ShellPanelVerticalAnchorPolicy
{
    internal const string TopValue = "Top";
    internal const string CenterValue = "Center";
    internal const string BottomValue = "Bottom";

    internal static ShellPanelVerticalAnchor Parse(
        string? value) =>
        value?.Trim() switch
        {
            TopValue => ShellPanelVerticalAnchor.Top,
            BottomValue => ShellPanelVerticalAnchor.Bottom,
            _ => ShellPanelVerticalAnchor.Center
        };

    internal static string NormalizeValue(
        string? value) =>
        Parse(value).ToString();

    internal static int CalculateTop(
        int availableTop,
        int availableHeight,
        int panelHeight,
        string? anchorValue)
    {
        int freeSpace = Math.Max(
            0,
            availableHeight - panelHeight);
        return Parse(anchorValue) switch
        {
            ShellPanelVerticalAnchor.Top => availableTop,
            ShellPanelVerticalAnchor.Bottom =>
                availableTop + freeSpace,
            _ => availableTop + freeSpace / 2
        };
    }
}
