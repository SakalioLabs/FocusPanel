using System.Drawing;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class EdgeHotZoneDetectorTests
{
    private static readonly Rectangle PrimaryScreen = new(0, 0, 1920, 1080);

    [Fact]
    public void PointerOutsideEdge_DoesNotTrigger()
    {
        var detector = new EdgeHotZoneDetector();

        Assert.False(detector.Update(new Point(1800, 500), PrimaryScreen, 0));
        Assert.False(detector.Update(new Point(1800, 500), PrimaryScreen, 500));
    }

    [Fact]
    public void PointerDwellingAtEdge_TriggersAfterOneHundredMilliseconds()
    {
        var detector = new EdgeHotZoneDetector();
        var edgePoint = new Point(1919, 500);

        Assert.False(detector.Update(edgePoint, PrimaryScreen, 10));
        Assert.False(detector.Update(edgePoint, PrimaryScreen, 109));
        Assert.True(detector.Update(edgePoint, PrimaryScreen, 110));
    }

    [Fact]
    public void FastPassAcrossEdge_RestartsDwellMeasurement()
    {
        var detector = new EdgeHotZoneDetector();
        var edgePoint = new Point(1919, 500);

        Assert.False(detector.Update(edgePoint, PrimaryScreen, 0));
        Assert.False(detector.Update(new Point(1800, 500), PrimaryScreen, 60));
        Assert.False(detector.Update(edgePoint, PrimaryScreen, 90));
        Assert.False(detector.Update(edgePoint, PrimaryScreen, 189));
        Assert.True(detector.Update(edgePoint, PrimaryScreen, 190));
    }

    [Fact]
    public void TriggerRemainsLatchedUntilPointerLeavesResetZone()
    {
        var detector = new EdgeHotZoneDetector();
        var edgePoint = new Point(1919, 500);

        Assert.False(detector.Update(edgePoint, PrimaryScreen, 0));
        Assert.True(detector.Update(edgePoint, PrimaryScreen, 100));
        Assert.False(detector.Update(edgePoint, PrimaryScreen, 500));
        Assert.False(detector.Update(new Point(1900, 500), PrimaryScreen, 600));
        Assert.False(detector.Update(edgePoint, PrimaryScreen, 700));

        Assert.False(detector.Update(new Point(1887, 500), PrimaryScreen, 800));
        Assert.False(detector.Update(edgePoint, PrimaryScreen, 900));
        Assert.True(detector.Update(edgePoint, PrimaryScreen, 1000));
    }

    [Fact]
    public void DetectorSupportsPrimaryScreensWithNegativeCoordinates()
    {
        var detector = new EdgeHotZoneDetector();
        var negativeScreen = new Rectangle(-2560, -200, 2560, 1440);
        var edgePoint = new Point(-1, 0);

        Assert.False(detector.Update(edgePoint, negativeScreen, 25));
        Assert.True(detector.Update(edgePoint, negativeScreen, 125));
    }

    [Fact]
    public void PointerOutsideVerticalBounds_DoesNotTrigger()
    {
        var detector = new EdgeHotZoneDetector();

        Assert.False(detector.Update(new Point(1919, -1), PrimaryScreen, 0));
        Assert.False(detector.Update(new Point(1919, -1), PrimaryScreen, 200));
    }
}
