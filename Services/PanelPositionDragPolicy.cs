using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace FocusPanel.Services;

internal sealed record PanelPositionDragTarget(
    string DisplayTarget,
    string PanelEdge,
    string VerticalAnchor,
    Rectangle DisplayBounds);

internal static class PanelPositionDragPolicy
{
    internal static PanelPositionDragTarget? FromCursor(
        Point cursor,
        IReadOnlyCollection<ShellDisplaySnapshot>
            displays)
    {
        ShellDisplaySnapshot[] eligible =
            displays
                .Where(candidate =>
                    candidate.Bounds.Width > 0
                    && candidate.Bounds.Height > 0
                    && !string.IsNullOrWhiteSpace(
                        candidate.DeviceName))
                .ToArray();
        if (eligible.Length == 0)
            return null;

        ShellDisplaySnapshot display = eligible
            .OrderBy(candidate =>
                DistanceSquared(
                    cursor,
                    candidate.Bounds))
            .ThenByDescending(candidate =>
                candidate.Bounds.Contains(cursor))
            .ThenByDescending(candidate =>
                candidate.IsPrimary)
            .ThenBy(candidate =>
                candidate.DeviceName,
                StringComparer.OrdinalIgnoreCase)
            .First();
        Rectangle bounds = display.Bounds;
        string edge = cursor.X
                      < bounds.Left
                        + bounds.Width / 2d
            ? ShellPanelEdgePolicy.LeftValue
            : ShellPanelEdgePolicy.RightValue;
        return new PanelPositionDragTarget(
            ShellDisplayTarget.NormalizeValue(
                ShellDisplayTarget.DevicePrefix
                + display.DeviceName),
            edge,
            PanelVerticalAnchorDragPolicy
                .FromCursor(
                    cursor.Y,
                    bounds),
            bounds);
    }

    private static long DistanceSquared(
        Point point,
        Rectangle bounds)
    {
        if (bounds.Width <= 0
            || bounds.Height <= 0)
        {
            return long.MaxValue;
        }

        long deltaX = point.X < bounds.Left
            ? (long)bounds.Left - point.X
            : point.X >= bounds.Right
                ? (long)point.X - bounds.Right + 1
                : 0;
        long deltaY = point.Y < bounds.Top
            ? (long)bounds.Top - point.Y
            : point.Y >= bounds.Bottom
                ? (long)point.Y - bounds.Bottom + 1
                : 0;
        return deltaX * deltaX
               + deltaY * deltaY;
    }
}
