using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Forms = System.Windows.Forms;

namespace FocusPanel.Services;

internal enum ShellDisplayTargetMode
{
    OutermostRight,
    Primary
}

internal readonly record struct ShellDisplaySnapshot(
    Rectangle Bounds,
    bool IsPrimary);

internal static class ShellDisplayTarget
{
    internal const string OutermostRightValue =
        "OutermostRight";
    internal const string PrimaryValue =
        "Primary";

    internal static Rectangle GetBounds(
        ShellDisplayTargetMode mode =
            ShellDisplayTargetMode.OutermostRight)
    {
        ShellDisplaySnapshot[] displays = Forms.Screen.AllScreens
            .Select(screen => new ShellDisplaySnapshot(
                screen.Bounds,
                screen.Primary))
            .ToArray();
        return Select(displays, mode)?.Bounds
            ?? Rectangle.Empty;
    }

    internal static ShellDisplaySnapshot? Select(
        IReadOnlyCollection<ShellDisplaySnapshot> displays,
        ShellDisplayTargetMode mode =
            ShellDisplayTargetMode.OutermostRight)
    {
        if (displays.Count == 0)
            return null;

        if (mode == ShellDisplayTargetMode.Primary)
        {
            foreach (ShellDisplaySnapshot display
                     in displays)
            {
                if (display.IsPrimary)
                    return display;
            }
        }

        return displays
            .OrderByDescending(display => display.Bounds.Right)
            .ThenByDescending(display => display.IsPrimary)
            .ThenBy(display => display.Bounds.Top)
            .ThenByDescending(display => display.Bounds.Width)
            .First();
    }

    internal static ShellDisplayTargetMode Parse(
        string? value) =>
        string.Equals(
            value,
            PrimaryValue,
            System.StringComparison.Ordinal)
            ? ShellDisplayTargetMode.Primary
            : ShellDisplayTargetMode.OutermostRight;

    internal static string NormalizeValue(
        string? value) =>
        Parse(value) == ShellDisplayTargetMode.Primary
            ? PrimaryValue
            : OutermostRightValue;
}
