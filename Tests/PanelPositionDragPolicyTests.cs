using System.Drawing;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PanelPositionDragPolicyTests
{
    private static readonly ShellDisplaySnapshot[]
        Displays =
        {
            new(
                new Rectangle(
                    -1600,
                    120,
                    1600,
                    900),
                false,
                @"\\.\DISPLAY2"),
            new(
                new Rectangle(
                    120,
                    -200,
                    1920,
                    1080),
                true,
                @"\\.\DISPLAY1")
        };

    [Theory]
    [InlineData(-1500, 200,
        @"Device:\\.\DISPLAY2", "Left", "Top")]
    [InlineData(-200, 600,
        @"Device:\\.\DISPLAY2", "Right", "Center")]
    [InlineData(200, -100,
        @"Device:\\.\DISPLAY1", "Left", "Top")]
    [InlineData(2000, 700,
        @"Device:\\.\DISPLAY1", "Right", "Bottom")]
    public void FromCursor_ResolvesDisplayEdgeAndAnchor(
        int x,
        int y,
        string expectedDisplay,
        string expectedEdge,
        string expectedAnchor)
    {
        PanelPositionDragTarget target =
            Assert.IsType<PanelPositionDragTarget>(
                PanelPositionDragPolicy
                    .FromCursor(
                        new Point(x, y),
                        Displays));

        Assert.Equal(
            expectedDisplay,
            target.DisplayTarget);
        Assert.Equal(
            expectedEdge,
            target.PanelEdge);
        Assert.Equal(
            expectedAnchor,
            target.VerticalAnchor);
    }

    [Fact]
    public void FromCursor_MidpointBelongsToRightEdge()
    {
        PanelPositionDragTarget target =
            Assert.IsType<PanelPositionDragTarget>(
                PanelPositionDragPolicy
                    .FromCursor(
                        new Point(
                            1080,
                            300),
                        Displays));

        Assert.Equal(
            ShellPanelEdgePolicy.RightValue,
            target.PanelEdge);
    }

    [Fact]
    public void FromCursor_GapUsesNearestDisplay()
    {
        PanelPositionDragTarget target =
            Assert.IsType<PanelPositionDragTarget>(
                PanelPositionDragPolicy
                    .FromCursor(
                        new Point(
                            80,
                            400),
                        Displays));

        Assert.Equal(
            @"Device:\\.\DISPLAY1",
            target.DisplayTarget);
        Assert.Equal(
            ShellPanelEdgePolicy.LeftValue,
            target.PanelEdge);
    }

    [Fact]
    public void FromCursor_EqualGapPrefersPrimaryDisplay()
    {
        ShellDisplaySnapshot[] displays =
        {
            new(
                new Rectangle(
                    0,
                    0,
                    100,
                    100),
                false,
                "LEFT"),
            new(
                new Rectangle(
                    119,
                    0,
                    100,
                    100),
                true,
                "RIGHT")
        };

        PanelPositionDragTarget target =
            Assert.IsType<PanelPositionDragTarget>(
                PanelPositionDragPolicy
                    .FromCursor(
                        new Point(
                            109,
                            50),
                        displays));

        Assert.Equal(
            "Device:RIGHT",
            target.DisplayTarget);
    }

    [Fact]
    public void FromCursor_RejectsMissingOrInvalidDisplays()
    {
        Assert.Null(
            PanelPositionDragPolicy
                .FromCursor(
                    Point.Empty,
                    System.Array.Empty<
                        ShellDisplaySnapshot>()));
        Assert.Null(
            PanelPositionDragPolicy
                .FromCursor(
                    Point.Empty,
                    new[]
                    {
                        new ShellDisplaySnapshot(
                            Rectangle.Empty,
                            true,
                            "")
                    }));
    }
}
