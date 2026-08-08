using System.Collections.Generic;
using System.Drawing;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellWindowPlacementTests
{
    [Fact]
    public void SideBySideDisplays_SelectsOutermostRightEdge()
    {
        var displays = new List<ShellDisplaySnapshot>
        {
            new(new Rectangle(0, 0, 1920, 1080), true),
            new(new Rectangle(1920, 0, 2560, 1440), false)
        };

        ShellDisplaySnapshot? target =
            ShellDisplayTarget.Select(displays);

        Assert.NotNull(target);
        Assert.Equal(displays[1], target.Value);
    }

    [Fact]
    public void LeftEdgeAutoTarget_SelectsOutermostLeftDisplay()
    {
        var displays = new List<ShellDisplaySnapshot>
        {
            new(new Rectangle(0, 0, 1920, 1080), true),
            new(new Rectangle(-2560, -180, 2560, 1440), false),
            new(new Rectangle(1920, 0, 1600, 900), false)
        };

        ShellDisplaySnapshot? target =
            ShellDisplayTarget.Select(
                displays,
                ShellDisplayTarget
                    .OutermostRightValue,
                ShellPanelEdgePolicy.LeftValue);

        Assert.NotNull(target);
        Assert.Equal(displays[1], target.Value);
    }

    [Fact]
    public void LeftSecondaryDisplay_KeepsPrimaryOuterRightEdge()
    {
        var displays = new List<ShellDisplaySnapshot>
        {
            new(new Rectangle(0, 0, 1920, 1080), true),
            new(new Rectangle(-2560, -180, 2560, 1440), false)
        };

        ShellDisplaySnapshot? target =
            ShellDisplayTarget.Select(displays);

        Assert.NotNull(target);
        Assert.Equal(displays[0], target.Value);
    }

    [Fact]
    public void EqualRightEdges_PrefersPrimaryDisplay()
    {
        var displays = new List<ShellDisplaySnapshot>
        {
            new(new Rectangle(0, 0, 1920, 1080), true),
            new(new Rectangle(640, 1080, 1280, 1024), false)
        };

        ShellDisplaySnapshot? target =
            ShellDisplayTarget.Select(displays);

        Assert.NotNull(target);
        Assert.Equal(displays[0], target.Value);
    }

    [Fact]
    public void PrimaryMode_SelectsPrimaryInsteadOfRightmostDisplay()
    {
        var displays = new List<ShellDisplaySnapshot>
        {
            new(new Rectangle(0, 0, 1920, 1080), true),
            new(new Rectangle(1920, 0, 2560, 1440), false)
        };

        ShellDisplaySnapshot? target =
            ShellDisplayTarget.Select(
                displays,
                ShellDisplayTargetMode.Primary);

        Assert.NotNull(target);
        Assert.Equal(displays[0], target.Value);
    }

    [Fact]
    public void PrimaryMode_WithoutPrimaryFallsBackToOutermost()
    {
        var displays = new List<ShellDisplaySnapshot>
        {
            new(new Rectangle(-1920, 0, 1920, 1080), false),
            new(new Rectangle(0, 0, 2560, 1440), false)
        };

        ShellDisplaySnapshot? target =
            ShellDisplayTarget.Select(
                displays,
                ShellDisplayTargetMode.Primary);

        Assert.NotNull(target);
        Assert.Equal(displays[1], target.Value);
    }

    [Theory]
    [InlineData(
        "Primary",
        "Primary")]
    [InlineData(
        "OutermostRight",
        "OutermostRight")]
    [InlineData(
        "unknown",
        "OutermostRight")]
    [InlineData(
        null,
        "OutermostRight")]
    public void DisplayTargetMode_ParsesWithSafeFallback(
        string? value,
        string expected)
    {
        Assert.Equal(
            expected,
            ShellDisplayTarget
                .Parse(value)
                .ToString());
    }

    [Fact]
    public void DeviceTarget_SelectsExactDisplayRegardlessOfLayout()
    {
        var displays = new List<ShellDisplaySnapshot>
        {
            new(
                new Rectangle(0, 0, 1920, 1080),
                true,
                @"\\.\DISPLAY1"),
            new(
                new Rectangle(-2560, -300, 2560, 1440),
                false,
                @"\\.\DISPLAY2"),
            new(
                new Rectangle(1920, 200, 1600, 900),
                false,
                @"\\.\DISPLAY3")
        };

        ShellDisplaySnapshot? target =
            ShellDisplayTarget.Select(
                displays,
                @"Device:\\.\DISPLAY2");

        Assert.NotNull(target);
        Assert.Equal(displays[1], target.Value);
    }

    [Fact]
    public void DisconnectedDeviceTarget_FallsBackToPrimary()
    {
        var displays = new List<ShellDisplaySnapshot>
        {
            new(
                new Rectangle(-1920, 0, 1920, 1080),
                false,
                @"\\.\DISPLAY2"),
            new(
                new Rectangle(0, 0, 2560, 1440),
                true,
                @"\\.\DISPLAY1")
        };

        ShellDisplaySnapshot? target =
            ShellDisplayTarget.Select(
                displays,
                @"Device:\\.\DISPLAY9");

        Assert.NotNull(target);
        Assert.Equal(displays[1], target.Value);
    }

    [Fact]
    public void DisplayOptions_KeepDisconnectedSelectionVisible()
    {
        var displays = new List<ShellDisplaySnapshot>
        {
            new(
                new Rectangle(0, 0, 1920, 1080),
                true,
                @"\\.\DISPLAY1")
        };

        IReadOnlyList<ShellDisplayTargetOption> options =
            ShellDisplayTarget.CreateOptions(
                displays,
                @"Device:\\.\DISPLAY2");

        Assert.Contains(
            options,
            option =>
                option.Value
                    == @"Device:\\.\DISPLAY1"
                && option.DisplayName
                    .Contains("1920×1080"));
        Assert.Contains(
            options,
            option =>
                option.Value
                    == @"Device:\\.\DISPLAY2"
                && option.DisplayName
                    .Contains("已断开"));
    }

    [Fact]
    public void SelectedDisplay_UsesItsOwnWorkingArea()
    {
        var displays = new List<ShellDisplaySnapshot>
        {
            new(
                new Rectangle(0, 0, 1920, 1080),
                true,
                @"\\.\DISPLAY1",
                new Rectangle(0, 0, 1920, 1040)),
            new(
                new Rectangle(1920, -200, 2560, 1440),
                false,
                @"\\.\DISPLAY2",
                new Rectangle(1920, -160, 2560, 1400))
        };

        Assert.Equal(
            displays[1].WorkingArea,
            ShellDisplayTarget.GetWorkingArea(
                displays,
                @"Device:\\.\DISPLAY2"));
    }

    [Fact]
    public void MissingWorkingArea_FallsBackToDisplayBounds()
    {
        var display = new ShellDisplaySnapshot(
            new Rectangle(
                -1920,
                0,
                1920,
                1080),
            true,
            @"\\.\DISPLAY1");

        Assert.Equal(
            display.Bounds,
            ShellDisplayTarget.GetWorkingArea(
                new[] { display },
                ShellDisplayTarget.PrimaryValue));
    }

    [Theory]
    [InlineData(
        @"Device:\\.\DISPLAY2",
        @"Device:\\.\DISPLAY2")]
    [InlineData(
        "device:  DISPLAY-X  ",
        "Device:DISPLAY-X")]
    public void DeviceTarget_NormalizesWithoutLosingIdentity(
        string value,
        string expected)
    {
        Assert.Equal(
            expected,
            ShellDisplayTarget.NormalizeValue(value));
    }

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
    public void NegativeDisplay_AnchorsExpandedPanelInsideLeftEdge()
    {
        var display = new Rectangle(
            -2560,
            -200,
            2560,
            1440);

        PhysicalWindowBounds bounds =
            ShellWindowPlacement
                .CalculateAnchoredPanel(
                    display,
                    120,
                    720,
                    12,
                    820,
                    ShellPanelVerticalAnchorPolicy
                        .CenterValue,
                    ShellPanelEdgePolicy
                        .LeftValue);

        Assert.Equal(-2545, bounds.Left);
        Assert.Equal(900, bounds.Width);
        Assert.True(
            bounds.Left + bounds.Width
            <= display.Right);
    }

    [Theory]
    [InlineData("Top", 12)]
    [InlineData("Center", 130)]
    [InlineData("Bottom", 248)]
    [InlineData("invalid", 130)]
    public void AnchoredPanel_UsesRequestedVerticalPosition(
        string anchor,
        int expectedTop)
    {
        var display = new Rectangle(
            0,
            0,
            1920,
            1080);

        PhysicalWindowBounds bounds =
            ShellWindowPlacement
                .CalculateAnchoredPanel(
                    display,
                    96,
                    76,
                    12,
                    820,
                    anchor);

        Assert.Equal(expectedTop, bounds.Top);
        Assert.Equal(820, bounds.Height);
        Assert.Equal(1832, bounds.Left);
    }

    [Fact]
    public void AnchoredPanel_ShrinksToShortDisplayWithoutLeavingIt()
    {
        var display = new Rectangle(
            -1280,
            -100,
            1280,
            720);

        PhysicalWindowBounds bounds =
            ShellWindowPlacement
                .CalculateAnchoredPanel(
                    display,
                    96,
                    720,
                    12,
                    820,
                    ShellPanelVerticalAnchorPolicy
                        .BottomValue);

        Assert.Equal(-88, bounds.Top);
        Assert.Equal(696, bounds.Height);
        Assert.Equal(
            608,
            bounds.Top + bounds.Height);
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

    [Fact]
    public void LeftEdgeIndicator_IsExactlyThreePhysicalPixels()
    {
        var display = new Rectangle(
            -1920,
            120,
            1920,
            1080);

        PhysicalWindowBounds bounds =
            ShellWindowPlacement.CalculateIndicator(
                display,
                3,
                ShellPanelEdgePolicy.LeftValue);

        Assert.Equal(-1920, bounds.Left);
        Assert.Equal(3, bounds.Width);
    }

    [Theory]
    [InlineData("Top", 138)]
    [InlineData("Center", 225)]
    [InlineData("Bottom", 312)]
    public void AnchoredEdgeIndicator_MatchesPanelVerticalRegion(
        string anchor,
        int expectedTop)
    {
        var display = new Rectangle(
            1920,
            120,
            2560,
            1440);

        PhysicalWindowBounds bounds =
            ShellWindowPlacement
                .CalculateAnchoredIndicator(
                    display,
                    144,
                    3,
                    12,
                    820,
                    anchor);

        Assert.Equal(expectedTop, bounds.Top);
        Assert.Equal(1230, bounds.Height);
        Assert.Equal(4477, bounds.Left);
        Assert.Equal(3, bounds.Width);
    }

    [Fact]
    public void SideBySideMixedDpi_ExpandedPanelStaysOnTargetDisplay()
    {
        var target = new Rectangle(
            1920,
            -240,
            2560,
            1440);

        PhysicalWindowBounds compact =
            ShellWindowPlacement.CalculatePanel(
                target,
                144,
                76,
                12);
        PhysicalWindowBounds expanded =
            ShellWindowPlacement.CalculatePanel(
                target,
                144,
                720,
                12);

        Assert.Equal(
            target.Right - 18,
            compact.Left + compact.Width);
        Assert.Equal(
            target.Right - 18,
            expanded.Left + expanded.Width);
        Assert.True(expanded.Left >= target.Left);
        Assert.Equal(compact.Top, expanded.Top);
        Assert.Equal(compact.Height, expanded.Height);
    }
}
