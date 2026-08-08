using System.Drawing;

namespace FocusPanel.Services;

internal static class PanelVerticalAnchorDragPolicy
{
    internal static string FromCursor(
        int cursorY,
        Rectangle screenBounds)
    {
        if (screenBounds.Height <= 0)
        {
            return ShellPanelVerticalAnchorPolicy
                .CenterValue;
        }

        int relativeY = cursorY
                        - screenBounds.Top;
        if (relativeY
            < screenBounds.Height / 3)
        {
            return ShellPanelVerticalAnchorPolicy
                .TopValue;
        }

        if (relativeY
            >= screenBounds.Height * 2 / 3)
        {
            return ShellPanelVerticalAnchorPolicy
                .BottomValue;
        }

        return ShellPanelVerticalAnchorPolicy
            .CenterValue;
    }

    internal static string GetNext(
        string? current) =>
        ShellPanelVerticalAnchorPolicy
            .Parse(current) switch
        {
            ShellPanelVerticalAnchor.Top =>
                ShellPanelVerticalAnchorPolicy
                    .CenterValue,
            ShellPanelVerticalAnchor.Center =>
                ShellPanelVerticalAnchorPolicy
                    .BottomValue,
            _ => ShellPanelVerticalAnchorPolicy
                .TopValue
        };
}
