using System;

namespace FocusPanel.Services;

internal readonly record struct CompactTaskbarScrollState(
    bool ShowsOverflowControls,
    bool CanScrollUp,
    bool CanScrollDown,
    int HiddenAboveCount,
    int HiddenBelowCount);

internal static class CompactTaskbarScrollPolicy
{
    private const double EdgeTolerance = 0.5;

    internal static CompactTaskbarScrollState GetState(
        double verticalOffset,
        double scrollableHeight,
        double extentHeight = 0,
        int itemCount = 0)
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
                && safeOffset < safeScrollableHeight - EdgeTolerance,
            HiddenAboveCount: GetHiddenItemCount(
                safeOffset,
                extentHeight,
                itemCount),
            HiddenBelowCount: GetHiddenItemCount(
                Math.Max(
                    0,
                    safeScrollableHeight
                    - safeOffset),
                extentHeight,
                itemCount));
    }

    internal static double GetRevealOffset(
        double currentOffset,
        double itemTop,
        double itemHeight,
        double viewportHeight,
        double scrollableHeight,
        double leadingInset,
        double trailingInset)
    {
        double maximumOffset =
            NormalizeNonNegative(
                scrollableHeight);
        double offset = Math.Clamp(
            NormalizeNonNegative(
                currentOffset),
            0,
            maximumOffset);
        double viewport =
            NormalizeNonNegative(
                viewportHeight);
        double height =
            NormalizeNonNegative(
                itemHeight);
        if (!double.IsFinite(itemTop)
            || viewport <= EdgeTolerance
            || height <= EdgeTolerance)
        {
            return offset;
        }

        double safeTop = Math.Min(
            viewport,
            NormalizeNonNegative(
                leadingInset));
        double safeBottom = Math.Max(
            safeTop,
            viewport
            - NormalizeNonNegative(
                trailingInset));
        double itemBottom =
            itemTop + height;
        double target = offset;

        if (height > safeBottom - safeTop)
        {
            target += itemTop - safeTop;
        }
        else if (itemTop < safeTop)
        {
            target -= safeTop - itemTop;
        }
        else if (itemBottom > safeBottom)
        {
            target += itemBottom - safeBottom;
        }

        return Math.Clamp(
            target,
            0,
            maximumOffset);
    }

    internal static string FormatHiddenCount(
        int count) =>
        count > 99
            ? "99+"
            : Math.Max(0, count)
                .ToString();

    private static double NormalizeNonNegative(
        double value) =>
        double.IsFinite(value)
            ? Math.Max(0, value)
            : 0;

    private static int GetHiddenItemCount(
        double hiddenHeight,
        double extentHeight,
        int itemCount)
    {
        if (itemCount <= 0
            || hiddenHeight <= EdgeTolerance
            || !double.IsFinite(extentHeight)
            || extentHeight <= EdgeTolerance)
        {
            return 0;
        }

        double itemPitch =
            extentHeight / itemCount;
        if (!double.IsFinite(itemPitch)
            || itemPitch <= EdgeTolerance)
        {
            return 0;
        }

        int hiddenCount = (int)Math.Ceiling(
            Math.Max(
                0,
                hiddenHeight
                - EdgeTolerance)
            / itemPitch);
        return Math.Clamp(
            hiddenCount,
            0,
            itemCount);
    }
}
