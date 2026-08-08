using System;
using System.Collections.Generic;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowFocusSessionCoordinatorTests
{
    [Fact]
    public void Start_MinimizesOnlyOtherNormalOrMaximizedWindows()
    {
        var coordinator = new WindowFocusSessionCoordinator();
        var minimized = new List<IntPtr>();
        WindowReference[] windows =
        {
            Window(1, "保留", isActive: true),
            Window(2, "普通"),
            Window(3, "最大化", state: TrackedWindowState.Maximized),
            Window(4, "已最小化", state: TrackedWindowState.Minimized),
            Window(2, "重复")
        };

        WindowFocusStartResult result = coordinator.Start(
            windows,
            new[] { new IntPtr(1) },
            "保留",
            handle =>
            {
                minimized.Add(handle);
                return true;
            },
            _ => true,
            _ => true);

        Assert.True(result.Started);
        Assert.Equal(2, result.MinimizedCount);
        Assert.Equal(new[] { new IntPtr(2), new IntPtr(3) }, minimized);
        Assert.Equal(2, coordinator.HiddenWindowCount);
        Assert.Contains("保留", coordinator.Summary);
    }

    [Fact]
    public void Restore_PreservesStateAndRestoresFormerActiveLast()
    {
        var coordinator = new WindowFocusSessionCoordinator();
        WindowReference[] original =
        {
            Window(1, "目标"),
            Window(2, "普通", isActive: true),
            Window(3, "最大化", state: TrackedWindowState.Maximized)
        };
        coordinator.Start(
            original,
            new[] { new IntPtr(1) },
            "目标",
            _ => true,
            _ => true,
            _ => true);
        var operations = new List<string>();

        WindowFocusRestoreResult result = coordinator.Restore(
            new[]
            {
                Window(1, "目标"),
                Window(2, "普通", state: TrackedWindowState.Minimized),
                Window(3, "最大化", state: TrackedWindowState.Minimized)
            },
            handle =>
            {
                operations.Add($"restore:{handle}");
                return true;
            },
            handle =>
            {
                operations.Add($"maximize:{handle}");
                return true;
            });

        Assert.Equal(2, result.RestoredCount);
        Assert.Equal(
            new[] { "maximize:3", "restore:2" },
            operations);
        Assert.False(coordinator.HasActiveSession);
    }

    [Fact]
    public void Restore_FailureRemainsRetryable()
    {
        var coordinator = StartedCoordinator();

        WindowFocusRestoreResult first = coordinator.Restore(
            MinimizedSessionWindows(),
            _ => false,
            _ => false);
        Assert.Equal(1, first.RemainingCount);
        Assert.True(coordinator.HasActiveSession);

        WindowFocusRestoreResult second = coordinator.Restore(
            MinimizedSessionWindows(),
            _ => true,
            _ => true);

        Assert.Equal(1, second.RestoredCount);
        Assert.Equal(0, second.RemainingCount);
        Assert.False(coordinator.HasActiveSession);
    }

    [Fact]
    public void Reconcile_RemovesClosedOrManuallyRestoredWindows()
    {
        var coordinator = StartedCoordinator();

        coordinator.Reconcile(
            new[] { Window(2, "其他", state: TrackedWindowState.Normal) });

        Assert.False(coordinator.HasActiveSession);
        Assert.Equal(string.Empty, coordinator.Summary);
    }

    [Fact]
    public void Start_IsolatesMinimizeFailureAndTracksOnlySucceededWindows()
    {
        var coordinator = new WindowFocusSessionCoordinator();

        WindowFocusStartResult result = coordinator.Start(
            new[]
            {
                Window(1, "目标"),
                Window(2, "可收起"),
                Window(3, "拒绝收起")
            },
            new[] { new IntPtr(1) },
            "目标",
            handle => handle == new IntPtr(2),
            _ => true,
            _ => true);

        Assert.Equal(1, result.MinimizedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(1, coordinator.HiddenWindowCount);
    }

    [Fact]
    public void Start_BlocksWhenPreviousSessionCannotBeRestored()
    {
        var coordinator = StartedCoordinator();

        WindowFocusStartResult result = coordinator.Start(
            MinimizedSessionWindows(),
            new[] { new IntPtr(2) },
            "新目标",
            _ => true,
            _ => false,
            _ => false);

        Assert.True(result.BlockedByPreviousSession);
        Assert.False(result.Started);
        Assert.Equal(1, coordinator.HiddenWindowCount);
    }

    [Fact]
    public void Start_NewSessionCanMinimizeWindowRestoredFromPreviousSession()
    {
        var coordinator = StartedCoordinator();
        var minimized = new List<IntPtr>();

        WindowFocusStartResult result = coordinator.Start(
            new[]
            {
                Window(1, "旧目标"),
                Window(2, "上一会话收起", state: TrackedWindowState.Minimized),
                Window(3, "新目标")
            },
            new[] { new IntPtr(3) },
            "新目标",
            handle =>
            {
                minimized.Add(handle);
                return true;
            },
            _ => true,
            _ => true);

        Assert.True(result.Started);
        Assert.Equal(
            new[] { new IntPtr(1), new IntPtr(2) },
            minimized);
        Assert.Equal(2, coordinator.HiddenWindowCount);
    }

    private static WindowFocusSessionCoordinator StartedCoordinator()
    {
        var coordinator = new WindowFocusSessionCoordinator();
        coordinator.Start(
            new[]
            {
                Window(1, "目标", isActive: true),
                Window(2, "其他")
            },
            new[] { new IntPtr(1) },
            "目标",
            _ => true,
            _ => true,
            _ => true);
        return coordinator;
    }

    private static WindowReference[] MinimizedSessionWindows() =>
        new[]
        {
            Window(1, "目标"),
            Window(2, "其他", state: TrackedWindowState.Minimized)
        };

    private static WindowReference Window(
        int handle,
        string title,
        bool isActive = false,
        TrackedWindowState state = TrackedWindowState.Normal) =>
        new(new IntPtr(handle), title, isActive, state);
}
