using System.Drawing;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellWindowPlacementTests
{
    [Fact]
    public void MixedDpiOffsetPrimary_UsesPhysicalScreenOrigin()
    {
        var primary = new Rectangle(
            2560,
            -180,
            3840,
            2160);

        PhysicalWindowBounds bounds =
            ShellWindowPlacement.CalculatePanel(
                primary,
                144,
                720,
                12);

        Assert.Equal(5302, bounds.Left);
        Assert.Equal(-162, bounds.Top);
        Assert.Equal(1080, bounds.Width);
        Assert.Equal(2124, bounds.Height);
        Assert.Equal(
            primary.Right - 18,
            bounds.Left + bounds.Width);
    }

    [Fact]
    public void NegativePrimary_AnchorsCompactDockInsideRightEdge()
    {
        var primary = new Rectangle(
            -2560,
            -200,
            2560,
            1440);

        PhysicalWindowBounds bounds =
            ShellWindowPlacement.CalculatePanel(
                primary,
                120,
                76,
                12);

        Assert.Equal(-110, bounds.Left);
        Assert.Equal(-185, bounds.Top);
        Assert.Equal(95, bounds.Width);
        Assert.Equal(1410, bounds.Height);
        Assert.Equal(-15, bounds.Left + bounds.Width);
    }

    [Fact]
    public void EdgeIndicator_IsExactlyThreePhysicalPixels()
    {
        var primary = new Rectangle(
            1920,
            120,
            2560,
            1440);

        PhysicalWindowBounds bounds =
            ShellWindowPlacement.CalculateIndicator(
                primary,
                3);

        Assert.Equal(4477, bounds.Left);
        Assert.Equal(120, bounds.Top);
        Assert.Equal(3, bounds.Width);
        Assert.Equal(1440, bounds.Height);
    }
}
