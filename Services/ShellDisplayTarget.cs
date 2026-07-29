using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Forms = System.Windows.Forms;

namespace FocusPanel.Services;

internal readonly record struct ShellDisplaySnapshot(
    Rectangle Bounds,
    bool IsPrimary);

internal static class ShellDisplayTarget
{
    internal static Rectangle GetBounds()
    {
        ShellDisplaySnapshot[] displays = Forms.Screen.AllScreens
            .Select(screen => new ShellDisplaySnapshot(
                screen.Bounds,
                screen.Primary))
            .ToArray();
        return Select(displays)?.Bounds ?? Rectangle.Empty;
    }

    internal static ShellDisplaySnapshot? Select(
        IReadOnlyCollection<ShellDisplaySnapshot> displays)
    {
        if (displays.Count == 0)
            return null;

        return displays
            .OrderByDescending(display => display.Bounds.Right)
            .ThenByDescending(display => display.IsPrimary)
            .ThenBy(display => display.Bounds.Top)
            .ThenByDescending(display => display.Bounds.Width)
            .First();
    }
}
