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
