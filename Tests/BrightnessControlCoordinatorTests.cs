using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class BrightnessControlCoordinatorTests
{
    [Fact]
    public async Task PendingChanges_CoalesceToLatestValue()
    {
        var writes =
            new ConcurrentQueue<int>();
        var entered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
        using var release =
            new ManualResetEventSlim(false);
        using var coordinator =
            new BrightnessControlCoordinator(
                value =>
                {
                    writes.Enqueue(value);
                    if (value == 20)
                    {
                        entered.TrySetResult(true);
                        release.Wait();
                    }
                    return true;
                });

        Assert.True(coordinator.Queue(1, 20));
        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        coordinator.Queue(2, 50);
        coordinator.Queue(3, 80);
        release.Set();
        await coordinator.CompleteAsync()
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(
            new[] { 20, 80 },
            writes.ToArray());
    }

    [Fact]
    public async Task FailureDoesNotStopTrailingWrite()
    {
        var outcomes =
            new ConcurrentQueue<
                BrightnessControlOutcome>();
        using var coordinator =
            new BrightnessControlCoordinator(
                value => value == 70);
        coordinator.Completed += outcomes.Enqueue;

        coordinator.Queue(1, 30);
        await Task.Delay(20);
        coordinator.Queue(2, 70);
        await coordinator.CompleteAsync()
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Contains(
            outcomes,
            outcome =>
                outcome.Percent == 30
                && !outcome.Succeeded);
        Assert.Contains(
            outcomes,
            outcome =>
                outcome.Percent == 70
                && outcome.Succeeded);
    }

    [Fact]
    public async Task CompleteRejectsNewWritesAndDrainsAcceptedValue()
    {
        int writes = 0;
        using var coordinator =
            new BrightnessControlCoordinator(
                _ =>
                {
                    Interlocked.Increment(
                        ref writes);
                    return true;
                });

        Assert.True(coordinator.Queue(1, 45));
        Task completion =
            coordinator.CompleteAsync();
        Assert.False(coordinator.Queue(2, 70));
        await completion.WaitAsync(
            TimeSpan.FromSeconds(2));
        Assert.Equal(1, writes);
    }
}
