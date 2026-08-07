using System;
using System.Drawing;

namespace FocusPanel.Services;

internal static class WindowDisplayMovePolicy
{
    internal static bool CanMove(
        Rectangle sourceWorkArea,
        Rectangle targetWorkArea) =>
        IsValid(sourceWorkArea)
        && IsValid(targetWorkArea)
        && sourceWorkArea != targetWorkArea;

    internal static Rectangle CalculateBounds(
        Rectangle windowBounds,
        Rectangle sourceWorkArea,
        Rectangle targetWorkArea)
    {
        if (!CanMove(
                sourceWorkArea,
                targetWorkArea)
            || !IsValid(windowBounds))
        {
            return Rectangle.Empty;
        }

        int width = Math.Min(
            windowBounds.Width,
            targetWorkArea.Width);
        int height = Math.Min(
            windowBounds.Height,
            targetWorkArea.Height);
        double horizontalRatio = PositionRatio(
            windowBounds.Left
                - sourceWorkArea.Left,
            sourceWorkArea.Width
                - windowBounds.Width);
        double verticalRatio = PositionRatio(
            windowBounds.Top
                - sourceWorkArea.Top,
            sourceWorkArea.Height
                - windowBounds.Height);
        int left = targetWorkArea.Left
            + (int)Math.Round(
                Math.Max(
                    0,
                    targetWorkArea.Width
                        - width)
                * horizontalRatio);
        int top = targetWorkArea.Top
            + (int)Math.Round(
                Math.Max(
                    0,
                    targetWorkArea.Height
                        - height)
                * verticalRatio);

        return new Rectangle(
            left,
            top,
            width,
            height);
    }

    private static double PositionRatio(
        int offset,
        int available) =>
        available <= 0
            ? 0.5
            : Math.Clamp(
                (double)offset / available,
                0,
                1);

    private static bool IsValid(
        Rectangle bounds) =>
        bounds.Width > 0
        && bounds.Height > 0;
}
