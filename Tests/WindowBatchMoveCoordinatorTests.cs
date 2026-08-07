using System;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowBatchMoveCoordinatorTests
{
    [Fact]
    public void MixedWindows_MoveOnlyEligibleUniqueHandles()
    {
        WindowReference[] windows =
        {
            Window(1),
            Window(2),
            Window(3),
            Window(2),
            Window(0)
        };

        WindowBatchMoveResult result =
            WindowBatchMoveCoordinator.Execute(
                windows,
                handle => handle != new IntPtr(2),
                handle => handle == new IntPtr(1));

        Assert.Equal(2, result.EligibleCount);
        Assert.Equal(1, result.MovedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.True(result.HasWork);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void AllEligibleWindowsMoved_IsSuccessful()
    {
        WindowBatchMoveResult result =
            WindowBatchMoveCoordinator.Execute(
                new[]
                {
                    Window(1),
                    Window(2)
                },
                _ => true,
                _ => true);

        Assert.Equal(2, result.EligibleCount);
        Assert.Equal(2, result.MovedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.True(result.HasWork);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void WindowsAlreadyOnTarget_AreNoWork()
    {
        WindowBatchMoveResult result =
            WindowBatchMoveCoordinator.Execute(
                new[]
                {
                    Window(1),
                    Window(2)
                },
                _ => false,
                _ => throw new InvalidOperationException());

        Assert.Equal(0, result.EligibleCount);
        Assert.Equal(0, result.MovedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.False(result.HasWork);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ProbeOrMoveException_IsIsolatedPerWindow()
    {
        WindowBatchMoveResult result =
            WindowBatchMoveCoordinator.Execute(
                new[]
                {
                    Window(1),
                    Window(2),
                    Window(3)
                },
                handle => handle == new IntPtr(2)
                    ? throw new InvalidOperationException()
                    : true,
                handle => handle == new IntPtr(3)
                    ? throw new InvalidOperationException()
                    : true);

        Assert.Equal(2, result.EligibleCount);
        Assert.Equal(1, result.MovedCount);
        Assert.Equal(2, result.FailedCount);
        Assert.False(result.Succeeded);
    }

    private static WindowReference Window(
        int handle) =>
        new(
            new IntPtr(handle),
            $"窗口 {handle}");
}
