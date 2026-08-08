using System.Drawing;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PanelVerticalAnchorDragPolicyTests
{
    [Theory]
    [InlineData(-200, "Top")]
    [InlineData(99, "Top")]
    [InlineData(100, "Center")]
    [InlineData(399, "Center")]
    [InlineData(400, "Bottom")]
    [InlineData(900, "Bottom")]
    public void FromCursor_UsesPhysicalScreenThirds(
        int cursorY,
        string expected)
    {
        var screen = new Rectangle(
            0,
            -200,
            1920,
            900);

        Assert.Equal(
            expected,
            PanelVerticalAnchorDragPolicy
                .FromCursor(
                    cursorY,
                    screen));
    }

    [Theory]
    [InlineData("Top", "Center")]
    [InlineData("Center", "Bottom")]
    [InlineData("Bottom", "Top")]
    [InlineData("invalid", "Bottom")]
    public void GetNext_CyclesNormalizedAnchors(
        string current,
        string expected)
    {
        Assert.Equal(
            expected,
            PanelVerticalAnchorDragPolicy
                .GetNext(current));
    }
}
