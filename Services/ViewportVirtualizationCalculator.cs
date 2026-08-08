using System;

namespace FocusPanel.Services;

internal sealed record ViewportVirtualizationLayout(
    int ItemsPerRow,
    int RowCount,
    double ExtentHeight,
    int FirstRealizedIndex,
    int LastRealizedIndex)
{
    internal bool HasRealizedItems =>
        FirstRealizedIndex >= 0
        && LastRealizedIndex >= FirstRealizedIndex;
}

internal static class ViewportVirtualizationCalculator
{
    internal static double GetArrangedCellWidth(
        double panelWidth,
        int itemsPerRow,
        double requestedCellWidth,
        bool wrap)
    {
        requestedCellWidth = NormalizeLength(
            requestedCellWidth,
            1);
        panelWidth = NormalizeLength(
            panelWidth,
            requestedCellWidth);
        return wrap
            ? Math.Min(panelWidth, requestedCellWidth)
            : panelWidth;
    }

    internal static double ResolvePanelWidth(
        double availableWidth,
        double actualWidth,
        double parentWidth,
        double viewportWidth,
        double fallbackWidth)
    {
        double resolved = 0;
        resolved = MaxFinitePositive(
            resolved,
            availableWidth);
        resolved = MaxFinitePositive(
            resolved,
            actualWidth);
        resolved = MaxFinitePositive(
            resolved,
            parentWidth);
        resolved = MaxFinitePositive(
            resolved,
            viewportWidth);
        return resolved > 0
            ? resolved
            : NormalizeLength(
                fallbackWidth,
                1);
    }

    internal static ViewportVirtualizationLayout
        Calculate(
            int itemCount,
            double panelWidth,
            double itemWidth,
            double itemHeight,
            double visibleTop,
            double visibleBottom,
            bool wrap,
            int overscanRows = 1)
    {
        itemCount = Math.Max(0, itemCount);
        itemWidth = NormalizeLength(
            itemWidth,
            1);
        itemHeight = NormalizeLength(
            itemHeight,
            1);
        panelWidth = NormalizeLength(
            panelWidth,
            itemWidth);
        int itemsPerRow = wrap
            ? Math.Max(
                1,
                (int)Math.Floor(
                    panelWidth / itemWidth))
            : 1;
        int rowCount = itemCount == 0
            ? 0
            : (itemCount + itemsPerRow - 1)
                / itemsPerRow;
        double extentHeight =
            rowCount * itemHeight;
        if (itemCount == 0
            || visibleBottom <= 0
            || visibleTop >= extentHeight
            || visibleBottom <= visibleTop)
        {
            return new ViewportVirtualizationLayout(
                itemsPerRow,
                rowCount,
                extentHeight,
                -1,
                -1);
        }

        int safeOverscan =
            Math.Max(0, overscanRows);
        int firstRow = Math.Max(
            0,
            (int)Math.Floor(
                Math.Max(0, visibleTop)
                / itemHeight)
            - safeOverscan);
        int lastRow = Math.Min(
            rowCount - 1,
            (int)Math.Ceiling(
                Math.Max(
                    0,
                    visibleBottom)
                / itemHeight)
            - 1
            + safeOverscan);
        int firstIndex =
            firstRow * itemsPerRow;
        int lastIndex = Math.Min(
            itemCount - 1,
            ((lastRow + 1) * itemsPerRow) - 1);

        return new ViewportVirtualizationLayout(
            itemsPerRow,
            rowCount,
            extentHeight,
            firstIndex,
            lastIndex);
    }

    private static double NormalizeLength(
        double value,
        double fallback)
        => double.IsNaN(value)
            || double.IsInfinity(value)
            || value <= 0
                ? fallback
                : value;

    private static double MaxFinitePositive(
        double current,
        double candidate)
        => double.IsNaN(candidate)
            || double.IsInfinity(candidate)
            || candidate <= 0
                ? current
                : Math.Max(current, candidate);
}
