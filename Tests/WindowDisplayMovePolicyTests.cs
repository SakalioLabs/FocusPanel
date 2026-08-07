using System.Drawing;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowDisplayMovePolicyTests
{
    [Fact]
    public void SameDisplay_DoesNotOfferMove()
    {
        var area = new Rectangle(
            0,
            0,
            1920,
            1040);

        Assert.False(
            WindowDisplayMovePolicy.CanMove(
                area,
                area));
        Assert.Equal(
            Rectangle.Empty,
            WindowDisplayMovePolicy.CalculateBounds(
                new Rectangle(80, 80, 1200, 800),
                area,
                area));
    }

    [Fact]
    public void Move_PreservesRelativePositionAndFitsTarget()
    {
        Rectangle result =
            WindowDisplayMovePolicy.CalculateBounds(
                new Rectangle(
                    960,
                    520,
                    960,
                    520),
                new Rectangle(
                    0,
                    0,
                    1920,
                    1040),
                new Rectangle(
                    1920,
                    0,
                    1280,
                    680));

        Assert.Equal(
            new Rectangle(
                2240,
                160,
                960,
                520),
            result);
    }

    [Fact]
    public void OversizedWindow_IsClampedInsideSmallerDisplay()
    {
        Rectangle target =
            new(-1600, -120, 1600, 860);

        Rectangle result =
            WindowDisplayMovePolicy.CalculateBounds(
                new Rectangle(
                    100,
                    80,
                    2400,
                    1200),
                new Rectangle(
                    0,
                    0,
                    3840,
                    2080),
                target);

        Assert.Equal(target, result);
    }

    [Fact]
    public void InvalidGeometry_IsRejected()
    {
        Assert.False(
            WindowDisplayMovePolicy.CanMove(
                Rectangle.Empty,
                new Rectangle(
                    0,
                    0,
                    1920,
                    1080)));
        Assert.Equal(
            Rectangle.Empty,
            WindowDisplayMovePolicy.CalculateBounds(
                Rectangle.Empty,
                new Rectangle(
                    0,
                    0,
                    1920,
                    1080),
                new Rectangle(
                    1920,
                    0,
                    1920,
                    1080)));
    }
}
