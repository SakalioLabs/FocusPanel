using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowTrackingEventPolicyTests
{
    [Theory]
    [InlineData(
        WindowTrackingEventPolicy.EventSystemAlert)]
    [InlineData(
        WindowTrackingEventPolicy.EventSystemForeground)]
    [InlineData(
        WindowTrackingEventPolicy.EventSystemMinimizeStart)]
    [InlineData(
        WindowTrackingEventPolicy.EventSystemMinimizeEnd)]
    [InlineData(
        WindowTrackingEventPolicy.EventObjectCreate)]
    [InlineData(
        WindowTrackingEventPolicy.EventObjectDestroy)]
    [InlineData(
        WindowTrackingEventPolicy.EventObjectShow)]
    [InlineData(
        WindowTrackingEventPolicy.EventObjectHide)]
    [InlineData(
        WindowTrackingEventPolicy.EventObjectLocationChange)]
    [InlineData(
        WindowTrackingEventPolicy.EventObjectNameChange)]
    public void TopLevelWindowEvents_QueueRefresh(
        uint eventType)
    {
        Assert.True(
            WindowTrackingEventPolicy.ShouldQueueRefresh(
                eventType,
                WindowTrackingEventPolicy.ObjectIdWindow));
    }

    [Theory]
    [InlineData(-4)]
    [InlineData(-3)]
    [InlineData(1)]
    public void ChildOrClientObjects_AreIgnored(
        int objectId)
    {
        Assert.False(
            WindowTrackingEventPolicy.ShouldQueueRefresh(
                WindowTrackingEventPolicy.EventObjectCreate,
                objectId));
        Assert.False(
            WindowTrackingEventPolicy.ShouldQueueRefresh(
                WindowTrackingEventPolicy.EventObjectNameChange,
                objectId));
    }

    [Theory]
    [InlineData(-4)]
    [InlineData(-3)]
    [InlineData(1)]
    public void SystemAlert_QueuesRegardlessOfAccessibleObject(
        int objectId)
    {
        Assert.True(
            WindowTrackingEventPolicy.ShouldQueueRefresh(
                WindowTrackingEventPolicy.EventSystemAlert,
                objectId));
    }

    [Theory]
    [InlineData(0x0004)]
    [InlineData(0x8004)]
    [InlineData(0x800D)]
    public void UnrelatedEvents_AreIgnored(
        uint eventType)
    {
        Assert.False(
            WindowTrackingEventPolicy.ShouldQueueRefresh(
                eventType,
                WindowTrackingEventPolicy.ObjectIdWindow));
    }
}
