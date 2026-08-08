using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace FocusPanel.Services;

internal sealed record ShellDisplayPresentation(
    string DeviceName,
    string CompactName,
    string DisplayName,
    Rectangle Bounds,
    Rectangle WorkArea,
    bool IsPrimary,
    int OrderIndex);

internal static class ShellDisplayPresentationPolicy
{
    internal static IReadOnlyList<
        ShellDisplayPresentation> Create(
        IReadOnlyCollection<ShellDisplaySnapshot>
            displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        ShellDisplaySnapshot[] ordered = displays
            .Where(display =>
                display.Bounds.Width > 0
                && display.Bounds.Height > 0
                && !string.IsNullOrWhiteSpace(
                    display.DeviceName))
            .OrderBy(display =>
                display.Bounds.Left)
            .ThenBy(display =>
                display.Bounds.Top)
            .ThenBy(display =>
                display.DeviceName,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var presentations = new List<
            ShellDisplayPresentation>(
            ordered.Length);
        for (int index = 0;
             index < ordered.Length;
             index++)
        {
            ShellDisplaySnapshot display =
                ordered[index];
            Rectangle workArea =
                display.WorkingArea.Width > 0
                && display.WorkingArea.Height > 0
                    ? display.WorkingArea
                    : display.Bounds;
            string primary = display.IsPrimary
                ? " · 主屏"
                : string.Empty;
            string compactName =
                $"显示器 {index + 1}{primary}";
            presentations.Add(
                new ShellDisplayPresentation(
                    display.DeviceName,
                    compactName,
                    compactName + " · "
                    + $"{display.Bounds.Width}×"
                    + $"{display.Bounds.Height} · "
                    + $"({display.Bounds.Left},"
                    + $"{display.Bounds.Top})",
                    display.Bounds,
                    workArea,
                    display.IsPrimary,
                    index));
        }

        return presentations;
    }
}
