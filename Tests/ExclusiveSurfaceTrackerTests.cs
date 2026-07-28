using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ExclusiveSurfaceTrackerTests
{
    [Fact]
    public void Activate_ReplacesThePreviousSurface()
    {
        var tracker =
            new ExclusiveSurfaceTracker<object>();
        var first = new object();
        var second = new object();

        Assert.Null(tracker.Activate(first));
        Assert.Same(
            first,
            tracker.Activate(second));
        Assert.Same(second, tracker.Active);
    }

    [Fact]
    public void Activate_LeavesTheSameSurfaceActive()
    {
        var tracker =
            new ExclusiveSurfaceTracker<object>();
        var surface = new object();

        tracker.Activate(surface);

        Assert.Null(tracker.Activate(surface));
        Assert.Same(surface, tracker.Active);
    }

    [Fact]
    public void Deactivate_IgnoresAStaleClosedSurface()
    {
        var tracker =
            new ExclusiveSurfaceTracker<object>();
        var stale = new object();
        var active = new object();
        tracker.Activate(stale);
        tracker.Activate(active);

        Assert.False(tracker.Deactivate(stale));
        Assert.Same(active, tracker.Active);
        Assert.True(tracker.Deactivate(active));
        Assert.Null(tracker.Active);
    }

    [Fact]
    public void Clear_ReturnsAndRemovesTheActiveSurface()
    {
        var tracker =
            new ExclusiveSurfaceTracker<object>();
        var surface = new object();
        tracker.Activate(surface);

        Assert.Same(surface, tracker.Clear());
        Assert.Null(tracker.Active);
        Assert.Null(tracker.Clear());
    }
}
