using System;

namespace FocusPanel.Services;

internal readonly record struct CompactTaskbarScrollState(
    bool ShowsOverflowControls,
    bool CanScrollUp,
    bool CanScrollDown);

internal static class CompactTaskbarScrollPolicy
{
    private const double EdgeTolerance = 0.5;

    internal static CompactTaskbarScrollState GetState(
        double verticalOffset,
        double scrollableHeight)
    {
        double safeOffset = double.IsFinite(verticalOffset)
            ? Math.Max(0, verticalOffset)
            : 0;
        double safeScrollableHeight = double.IsFinite(scrollableHeight)
            ? Math.Max(0, scrollableHeight)
            : 0;
        bool hasOverflow = safeScrollableHeight > EdgeTolerance;

        return new CompactTaskbarScrollState(
            ShowsOverflowControls: hasOverflow,
            CanScrollUp: hasOverflow && safeOffset > EdgeTolerance,
            CanScrollDown: hasOverflow
                && safeOffset < safeScrollableHeight - EdgeTolerance);
    }
}
