using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class OrganizerDragInteractionPolicyTests
{
    [Theory]
    [InlineData(10, 10, 12, 12, 4, 4, false)]
    [InlineData(10, 10, 14, 10, 4, 4, true)]
    [InlineData(10, 10, 10, 14, 4, 4, true)]
    [InlineData(10, 10, 7, 6, 4, 4, true)]
    public void DragStartsOnlyAfterSystemThreshold(
        double startX,
        double startY,
        double currentX,
        double currentY,
        double minimumHorizontalDistance,
        double minimumVerticalDistance,
        bool expected)
    {
        Assert.Equal(
            expected,
            OrganizerDragInteractionPolicy.HasExceededDragThreshold(
                startX,
                startY,
                currentX,
                currentY,
                minimumHorizontalDistance,
                minimumVerticalDistance));
    }

    [Theory]
    [InlineData(-1, 400, 100, 500, 0)]
    [InlineData(200, 400, 100, 500, 0)]
    [InlineData(8, 400, 100, 500, -10.5)]
    [InlineData(392, 400, 100, 500, 10.5)]
    [InlineData(8, 400, 0, 500, 0)]
    [InlineData(392, 400, 500, 500, 0)]
    public void AutoScrollOnlyRunsInsideReachableViewportEdge(
        double pointerY,
        double viewportHeight,
        double verticalOffset,
        double scrollableHeight,
        double expected)
    {
        Assert.Equal(
            expected,
            OrganizerDragInteractionPolicy.GetAutoScrollStep(
                pointerY,
                viewportHeight,
                verticalOffset,
                scrollableHeight),
            precision: 6);
    }

    [Fact]
    public void AutoScrollStepNeverOvershootsRemainingDistance()
    {
        Assert.Equal(
            -3,
            OrganizerDragInteractionPolicy.GetAutoScrollStep(
                0,
                400,
                3,
                500));
        Assert.Equal(
            2,
            OrganizerDragInteractionPolicy.GetAutoScrollStep(
                399,
                400,
                498,
                500));
    }
}
