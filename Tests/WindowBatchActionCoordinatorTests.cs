using System;
using System.Collections.Generic;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    WindowBatchActionCoordinatorTests
{
    [Fact]
    public void Execute_OnlyRunsEligibleUniqueWindows()
    {
        var acted = new List<IntPtr>();
        WindowBatchActionResult result =
            WindowBatchActionCoordinator.Execute(
                new[]
                {
                    Window(
                        1,
                        TrackedWindowState.Normal),
                    Window(
                        2,
                        TrackedWindowState.Minimized),
                    Window(
                        1,
                        TrackedWindowState.Normal),
                    Window(
                        0,
                        TrackedWindowState.Normal)
                },
                window =>
                    window.State
                    != TrackedWindowState.Minimized,
                handle =>
                {
                    acted.Add(handle);
                    return true;
                });

        Assert.Equal(
            new[] { new IntPtr(1) },
            acted);
        Assert.Equal(1, result.EligibleCount);
        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.True(result.HasWork);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Execute_NoEligibleWindows_IsNoWork()
    {
        WindowBatchActionResult result =
            WindowBatchActionCoordinator.Execute(
                new[]
                {
                    Window(
                        1,
                        TrackedWindowState.Normal)
                },
                window =>
                    window.State
                    == TrackedWindowState.Minimized,
                _ => throw new
                    InvalidOperationException());

        Assert.False(result.HasWork);
        Assert.False(result.Succeeded);
        Assert.Equal(0, result.EligibleCount);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public void Execute_ActionFailures_AreIsolated()
    {
        WindowBatchActionResult result =
            WindowBatchActionCoordinator.Execute(
                new[]
                {
                    Window(
                        1,
                        TrackedWindowState.Normal),
                    Window(
                        2,
                        TrackedWindowState.Normal),
                    Window(
                        3,
                        TrackedWindowState.Normal)
                },
                _ => true,
                handle => handle switch
                {
                    var value when value
                        == new IntPtr(2) => false,
                    var value when value
                        == new IntPtr(3) => throw new
                            InvalidOperationException(),
                    _ => true
                });

        Assert.Equal(3, result.EligibleCount);
        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(2, result.FailedCount);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Execute_EligibilityFailure_DoesNotStopLaterWindows()
    {
        WindowBatchActionResult result =
            WindowBatchActionCoordinator.Execute(
                new[]
                {
                    Window(
                        1,
                        TrackedWindowState.Normal),
                    Window(
                        2,
                        TrackedWindowState.Normal)
                },
                window => window.Handle
                    == new IntPtr(1)
                        ? throw new
                            InvalidOperationException()
                        : true,
                _ => true);

        Assert.Equal(1, result.EligibleCount);
        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(1, result.FailedCount);
        Assert.True(result.HasWork);
        Assert.False(result.Succeeded);
    }

    private static WindowReference Window(
        int handle,
        TrackedWindowState state) =>
        new(
            new IntPtr(handle),
            $"窗口 {handle}",
            State: state);
}
