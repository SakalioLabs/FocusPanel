using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class CompactTaskbarScrollPolicyTests
{
    [Theory]
    [InlineData(0, 0, false, false, false)]
    [InlineData(0, 156, true, false, true)]
    [InlineData(52, 156, true, true, true)]
    [InlineData(156, 156, true, true, false)]
    [InlineData(155.8, 156, true, true, false)]
    [InlineData(double.NaN, double.PositiveInfinity, false, false, false)]
    public void DescribesOverflowNavigationAtEveryScrollPosition(
        double verticalOffset,
        double scrollableHeight,
        bool showsControls,
        bool canScrollUp,
        bool canScrollDown)
    {
        CompactTaskbarScrollState state =
            CompactTaskbarScrollPolicy.GetState(
                verticalOffset,
                scrollableHeight);

        Assert.Equal(showsControls, state.ShowsOverflowControls);
        Assert.Equal(canScrollUp, state.CanScrollUp);
        Assert.Equal(canScrollDown, state.CanScrollDown);
    }

    [Theory]
    [InlineData(
        100,
        40,
        44,
        220,
        400,
        30,
        30,
        100)]
    [InlineData(
        100,
        10,
        44,
        220,
        400,
        30,
        30,
        80)]
    [InlineData(
        100,
        185,
        44,
        220,
        400,
        30,
        30,
        139)]
    [InlineData(
        5,
        -20,
        44,
        220,
        400,
        30,
        30,
        0)]
    [InlineData(
        390,
        210,
        44,
        220,
        400,
        30,
        30,
        400)]
    public void RevealOffset_UsesOnlyMinimumRequiredMovement(
        double currentOffset,
        double itemTop,
        double itemHeight,
        double viewportHeight,
        double scrollableHeight,
        double leadingInset,
        double trailingInset,
        double expected)
    {
        double target =
            CompactTaskbarScrollPolicy
                .GetRevealOffset(
                    currentOffset,
                    itemTop,
                    itemHeight,
                    viewportHeight,
                    scrollableHeight,
                    leadingInset,
                    trailingInset);

        Assert.Equal(expected, target);
    }

    [Fact]
    public void RevealOffset_NormalizesInvalidMetrics()
    {
        double target =
            CompactTaskbarScrollPolicy
                .GetRevealOffset(
                    double.NaN,
                    double.PositiveInfinity,
                    double.NaN,
                    double.NegativeInfinity,
                    200,
                    double.NaN,
                    double.NaN);

        Assert.Equal(0, target);
    }

    [Theory]
    [InlineData(0, 156, 460, 10, 0, 4)]
    [InlineData(52, 156, 460, 10, 2, 3)]
    [InlineData(156, 156, 460, 10, 4, 0)]
    [InlineData(155.8, 156, 460, 10, 4, 0)]
    [InlineData(double.NaN, 156, 460, 10, 0, 4)]
    [InlineData(52, 156, double.NaN, 10, 0, 0)]
    [InlineData(52, 156, 460, 0, 0, 0)]
    public void DescribesHiddenApplicationCounts(
        double verticalOffset,
        double scrollableHeight,
        double extentHeight,
        int itemCount,
        int expectedAbove,
        int expectedBelow)
    {
        CompactTaskbarScrollState state =
            CompactTaskbarScrollPolicy.GetState(
                verticalOffset,
                scrollableHeight,
                extentHeight,
                itemCount);

        Assert.Equal(
            expectedAbove,
            state.HiddenAboveCount);
        Assert.Equal(
            expectedBelow,
            state.HiddenBelowCount);
    }

    [Theory]
    [InlineData(-1, "0")]
    [InlineData(0, "0")]
    [InlineData(12, "12")]
    [InlineData(99, "99")]
    [InlineData(100, "99+")]
    [InlineData(999, "99+")]
    public void HiddenCountLabel_RemainsCompact(
        int count,
        string expected)
    {
        Assert.Equal(
            expected,
            CompactTaskbarScrollPolicy
                .FormatHiddenCount(count));
    }
}
