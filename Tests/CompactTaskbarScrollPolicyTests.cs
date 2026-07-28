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
}
