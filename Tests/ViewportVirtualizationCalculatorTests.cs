using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ViewportVirtualizationCalculatorTests
{
    [Fact]
    public void WrappedCells_KeepStableDesktopSpacing()
    {
        double width =
            ViewportVirtualizationCalculator
                .GetArrangedCellWidth(
                    panelWidth: 650,
                    itemsPerRow: 5,
                    requestedCellWidth: 110,
                    wrap: true);

        Assert.Equal(110, width);
    }

    [Fact]
    public void PanelWidth_UsesTheLargestRealViewportCandidate()
    {
        double width =
            ViewportVirtualizationCalculator
                .ResolvePanelWidth(
                    availableWidth: 112,
                    actualWidth: 112,
                    parentWidth: 648,
                    viewportWidth: 646,
                    fallbackWidth: 112);

        Assert.Equal(648, width);
    }

    [Fact]
    public void PanelWidth_FallsBackWhenLayoutHasNoFiniteWidth()
    {
        double width =
            ViewportVirtualizationCalculator
                .ResolvePanelWidth(
                    availableWidth: double.PositiveInfinity,
                    actualWidth: 0,
                    parentWidth: double.NaN,
                    viewportWidth: -1,
                    fallbackWidth: 112);

        Assert.Equal(112, width);
    }

    [Fact]
    public void ListCell_UsesTheFullAvailableWidth()
    {
        double width =
            ViewportVirtualizationCalculator
                .GetArrangedCellWidth(
                    panelWidth: 650,
                    itemsPerRow: 1,
                    requestedCellWidth: 110,
                    wrap: false);

        Assert.Equal(650, width);
    }

    [Fact]
    public void WrapLayout_ComputesRowsAndExtent()
    {
        ViewportVirtualizationLayout layout =
            ViewportVirtualizationCalculator
                .Calculate(
                    itemCount: 25,
                    panelWidth: 330,
                    itemWidth: 110,
                    itemHeight: 130,
                    visibleTop: 0,
                    visibleBottom: 260,
                    wrap: true,
                    overscanRows: 0);

        Assert.Equal(3, layout.ItemsPerRow);
        Assert.Equal(9, layout.RowCount);
        Assert.Equal(1170, layout.ExtentHeight);
        Assert.Equal(0, layout.FirstRealizedIndex);
        Assert.Equal(5, layout.LastRealizedIndex);
    }

    [Fact]
    public void ScrolledViewport_RealizesOnlyVisibleRowsAndBuffer()
    {
        ViewportVirtualizationLayout layout =
            ViewportVirtualizationCalculator
                .Calculate(
                    itemCount: 1000,
                    panelWidth: 330,
                    itemWidth: 110,
                    itemHeight: 130,
                    visibleTop: 1300,
                    visibleBottom: 1560,
                    wrap: true,
                    overscanRows: 1);

        Assert.Equal(27, layout.FirstRealizedIndex);
        Assert.Equal(38, layout.LastRealizedIndex);
        Assert.True(
            layout.LastRealizedIndex
            - layout.FirstRealizedIndex
            + 1 < 1000);
    }

    [Fact]
    public void ListLayout_AlwaysUsesOneItemPerRow()
    {
        ViewportVirtualizationLayout layout =
            ViewportVirtualizationCalculator
                .Calculate(
                    itemCount: 200,
                    panelWidth: 500,
                    itemWidth: 100,
                    itemHeight: 38,
                    visibleTop: 380,
                    visibleBottom: 570,
                    wrap: false,
                    overscanRows: 1);

        Assert.Equal(1, layout.ItemsPerRow);
        Assert.Equal(200, layout.RowCount);
        Assert.Equal(9, layout.FirstRealizedIndex);
        Assert.Equal(15, layout.LastRealizedIndex);
    }

    [Fact]
    public void OffscreenPanel_RealizesNoItemsButKeepsExtent()
    {
        ViewportVirtualizationLayout layout =
            ViewportVirtualizationCalculator
                .Calculate(
                    itemCount: 100,
                    panelWidth: 220,
                    itemWidth: 110,
                    itemHeight: 130,
                    visibleTop: -600,
                    visibleBottom: -300,
                    wrap: true);

        Assert.False(layout.HasRealizedItems);
        Assert.Equal(6500, layout.ExtentHeight);
    }

    [Fact]
    public void EmptyCollection_HasNoExtentOrContainers()
    {
        ViewportVirtualizationLayout layout =
            ViewportVirtualizationCalculator
                .Calculate(
                    itemCount: 0,
                    panelWidth: 0,
                    itemWidth: 0,
                    itemHeight: 0,
                    visibleTop: 0,
                    visibleBottom: 100,
                    wrap: true);

        Assert.Equal(0, layout.RowCount);
        Assert.Equal(0, layout.ExtentHeight);
        Assert.False(layout.HasRealizedItems);
    }

    [Fact]
    public void LastRow_IsClampedToFinalItem()
    {
        ViewportVirtualizationLayout layout =
            ViewportVirtualizationCalculator
                .Calculate(
                    itemCount: 10,
                    panelWidth: 330,
                    itemWidth: 110,
                    itemHeight: 100,
                    visibleTop: 250,
                    visibleBottom: 400,
                    wrap: true,
                    overscanRows: 1);

        Assert.Equal(3, layout.FirstRealizedIndex);
        Assert.Equal(9, layout.LastRealizedIndex);
    }
}
