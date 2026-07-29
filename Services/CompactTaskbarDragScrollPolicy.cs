using System;

namespace FocusPanel.Services;

internal readonly record struct
    CompactTaskbarDragScrollDecision(
        double TargetOffset,
        bool ShouldScroll);

internal static class
    CompactTaskbarDragScrollPolicy
{
    internal const int ScrollIntervalMilliseconds =
        45;
    private const double ActivationDepth = 56;
    private const double MinimumStep = 4;
    private const double MaximumStep = 18;
    private const double OffsetTolerance = 0.5;

    internal static CompactTaskbarDragScrollDecision
        GetDecision(
            double currentOffset,
            double scrollableHeight,
            double pointerY,
            double viewportHeight,
            bool canScrollUp,
            bool canScrollDown,
            bool intervalElapsed)
    {
        double maximumOffset =
            NormalizeNonNegative(
                scrollableHeight);
        double offset = Math.Clamp(
            NormalizeNonNegative(
                currentOffset),
            0,
            maximumOffset);
        if (!intervalElapsed
            || !double.IsFinite(pointerY))
        {
            return new(
                offset,
                false);
        }

        double viewport =
            NormalizeNonNegative(
                viewportHeight);
        if (viewport <= OffsetTolerance
            || maximumOffset <= OffsetTolerance)
        {
            return new(
                offset,
                false);
        }

        double zone = Math.Min(
            ActivationDepth,
            viewport / 2);
        double clampedY = Math.Clamp(
            pointerY,
            0,
            viewport);
        double delta = 0;
        if (clampedY < zone
            && canScrollUp)
        {
            delta = -GetStep(
                1 - clampedY / zone);
        }
        else if (clampedY > viewport - zone
                 && canScrollDown)
        {
            delta = GetStep(
                1
                - (viewport - clampedY)
                / zone);
        }

        double target = Math.Clamp(
            offset + delta,
            0,
            maximumOffset);
        return new(
            target,
            Math.Abs(target - offset)
                > OffsetTolerance);
    }

    internal static bool IsScrollDue(
        long previousTick,
        long currentTick,
        int intervalMilliseconds =
            ScrollIntervalMilliseconds)
    {
        if (previousTick < 0)
            return true;
        if (currentTick < previousTick)
            return true;

        return currentTick - previousTick
            >= Math.Max(
                0,
                intervalMilliseconds);
    }

    private static double GetStep(
        double depth)
    {
        double normalized =
            Math.Clamp(depth, 0, 1);
        return MinimumStep
            + (MaximumStep - MinimumStep)
            * normalized;
    }

    private static double NormalizeNonNegative(
        double value) =>
        double.IsFinite(value)
            ? Math.Max(0, value)
            : 0;
}
