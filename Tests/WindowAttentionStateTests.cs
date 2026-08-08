using System;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowAttentionStateTests
{
    [Fact]
    public void BackgroundAlert_IsRememberedAndDeduplicated()
    {
        var state = new WindowAttentionState();
        IntPtr window = new(12);

        Assert.True(state.Observe(
            WindowTrackingEventPolicy.EventSystemAlert,
            window,
            new IntPtr(99)));
        Assert.False(state.Observe(
            WindowTrackingEventPolicy.EventSystemAlert,
            window,
            new IntPtr(99)));
        Assert.True(state.IsRequested(window));
    }

    [Fact]
    public void ForegroundWindowAlert_IsIgnored()
    {
        var state = new WindowAttentionState();
        IntPtr window = new(12);

        Assert.False(state.Observe(
            WindowTrackingEventPolicy.EventSystemAlert,
            window,
            window));
        Assert.False(state.IsRequested(window));
    }

    [Fact]
    public void ForegroundEvent_ClearsAttention()
    {
        var state = new WindowAttentionState();
        IntPtr window = new(12);
        state.Observe(
            WindowTrackingEventPolicy.EventSystemAlert,
            window,
            new IntPtr(99));

        Assert.True(state.Observe(
            WindowTrackingEventPolicy.EventSystemForeground,
            window,
            window));
        Assert.False(state.IsRequested(window));
    }

    [Fact]
    public void UnrelatedEvent_DoesNotChangeAttention()
    {
        var state = new WindowAttentionState();

        Assert.False(state.Observe(
            WindowTrackingEventPolicy.EventObjectNameChange,
            new IntPtr(12),
            new IntPtr(99)));
        Assert.False(state.IsRequested(new IntPtr(12)));
    }

    [Fact]
    public void Retain_RemovesClosedWindowsOnly()
    {
        var state = new WindowAttentionState();
        state.Observe(
            WindowTrackingEventPolicy.EventSystemAlert,
            new IntPtr(12),
            new IntPtr(99));
        state.Observe(
            WindowTrackingEventPolicy.EventSystemAlert,
            new IntPtr(13),
            new IntPtr(99));

        state.Retain(new[] { new IntPtr(13) });

        Assert.False(state.IsRequested(new IntPtr(12)));
        Assert.True(state.IsRequested(new IntPtr(13)));
    }

    [Fact]
    public void ZeroHandle_IsNeverTracked()
    {
        var state = new WindowAttentionState();

        Assert.False(state.Observe(
            WindowTrackingEventPolicy.EventSystemAlert,
            IntPtr.Zero,
            new IntPtr(99)));
        Assert.False(state.Clear(IntPtr.Zero));
    }

    [Fact]
    public void ClearAll_DropsSessionAttention()
    {
        var state = new WindowAttentionState();
        state.Observe(
            WindowTrackingEventPolicy.EventSystemAlert,
            new IntPtr(12),
            new IntPtr(99));

        state.ClearAll();

        Assert.False(state.IsRequested(new IntPtr(12)));
    }
}
