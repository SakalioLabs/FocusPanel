using System.Drawing;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowLayoutPolicyTests
{
    [Theory]
    [InlineData(
        WindowLayoutTarget.LeftHalf,
        -1919, 17, 959, 1039)]
    [InlineData(
        WindowLayoutTarget.RightHalf,
        -960, 17, 960, 1039)]
    [InlineData(
        WindowLayoutTarget.TopLeftQuarter,
        -1919, 17, 959, 519)]
    [InlineData(
        WindowLayoutTarget.TopRightQuarter,
        -960, 17, 960, 519)]
    [InlineData(
        WindowLayoutTarget.BottomLeftQuarter,
        -1919, 536, 959, 520)]
    [InlineData(
        WindowLayoutTarget.BottomRightQuarter,
        -960, 536, 960, 520)]
    public void CalculateBounds_UsesEntireNegativeCoordinateWorkArea(
        WindowLayoutTarget target,
        int x,
        int y,
        int width,
        int height)
    {
        var workArea = new Rectangle(
            -1919,
            17,
            1919,
            1039);

        Assert.Equal(
            new Rectangle(
                x,
                y,
                width,
                height),
            WindowLayoutPolicy.CalculateBounds(
                workArea,
                target));
    }

    [Fact]
    public void OddDimensions_HaveNoGapOrOverlap()
    {
        var workArea = new Rectangle(
            11,
            23,
            1919,
            1039);
        Rectangle left =
            WindowLayoutPolicy.CalculateBounds(
                workArea,
                WindowLayoutTarget.LeftHalf);
        Rectangle right =
            WindowLayoutPolicy.CalculateBounds(
                workArea,
                WindowLayoutTarget.RightHalf);
        Rectangle topRight =
            WindowLayoutPolicy.CalculateBounds(
                workArea,
                WindowLayoutTarget.TopRightQuarter);
        Rectangle bottomRight =
            WindowLayoutPolicy.CalculateBounds(
                workArea,
                WindowLayoutTarget.BottomRightQuarter);

        Assert.Equal(left.Right, right.Left);
        Assert.Equal(workArea.Right, right.Right);
        Assert.Equal(
            topRight.Bottom,
            bottomRight.Top);
        Assert.Equal(
            workArea.Bottom,
            bottomRight.Bottom);
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    [InlineData(1, 1080)]
    [InlineData(1920, 1)]
    public void InvalidWorkArea_ReturnsEmpty(
        int width,
        int height)
    {
        Assert.Equal(
            Rectangle.Empty,
            WindowLayoutPolicy.CalculateBounds(
                new Rectangle(
                    0,
                    0,
                    width,
                    height),
                WindowLayoutTarget.LeftHalf));
    }
}
