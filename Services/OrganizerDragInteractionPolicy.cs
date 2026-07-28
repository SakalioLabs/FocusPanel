using System;

namespace FocusPanel.Services;

internal static class OrganizerDragInteractionPolicy
{
    internal const double DefaultEdgeBand = 64;
    internal const double DefaultMaximumStep = 12;

    internal static bool HasExceededDragThreshold(
        double startX,
        double startY,
        double currentX,
        double currentY,
        double minimumHorizontalDistance,
        double minimumVerticalDistance)
        => Math.Abs(currentX - startX) >= minimumHorizontalDistance
            || Math.Abs(currentY - startY) >= minimumVerticalDistance;

    internal static double GetAutoScrollStep(
        double pointerY,
        double viewportHeight,
        double verticalOffset,
        double scrollableHeight,
        double edgeBand = DefaultEdgeBand,
        double maximumStep = DefaultMaximumStep)
    {
        if (!double.IsFinite(pointerY)
            || !double.IsFinite(viewportHeight)
            || !double.IsFinite(verticalOffset)
            || !double.IsFinite(scrollableHeight)
            || viewportHeight <= 0
            || edgeBand <= 0
            || maximumStep <= 0
            || pointerY < 0
            || pointerY >= viewportHeight)
        {
            return 0;
        }

        double effectiveBand = Math.Min(edgeBand, viewportHeight / 2);
        if (pointerY < effectiveBand && verticalOffset > 0)
        {
            double intensity = (effectiveBand - pointerY) / effectiveBand;
            return -Math.Min(verticalOffset, Math.Max(1, maximumStep * intensity));
        }

        if (pointerY > viewportHeight - effectiveBand
            && verticalOffset < scrollableHeight)
        {
            double intensity =
                (pointerY - (viewportHeight - effectiveBand)) / effectiveBand;
            return Math.Min(
                scrollableHeight - verticalOffset,
                Math.Max(1, maximumStep * intensity));
        }

        return 0;
    }
}
