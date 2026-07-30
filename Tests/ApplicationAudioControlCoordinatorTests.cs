using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    ApplicationAudioControlCoordinatorTests
{
    [Fact]
    public async Task SameSessionVolumeWrites_CoalesceToLatest()
    {
        var writes =
            new ConcurrentQueue<(string, float)>();
        var entered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
        using var release =
            new ManualResetEventSlim(false);
        using var coordinator =
            new ApplicationAudioControlCoordinator(
                (id, value) =>
                {
                    writes.Enqueue((id, value));
                    if (value == 0.2f)
                    {
                        entered.TrySetResult(true);
                        release.Wait();
                    }
                    return true;
                },
                (_, _) => true);

        coordinator.QueueVolume(
            "music",
            1,
            0.2f);
        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        coordinator.QueueVolume(
            "music",
            2,
            0.5f);
        coordinator.QueueVolume(
            "music",
            3,
            0.8f);
        release.Set();
        await coordinator.CompleteAsync()
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(
            new[]
            {
                ("music", 0.2f),
                ("music", 0.8f)
            },
            writes.ToArray());
    }

    [Fact]
    public async Task DifferentSessions_AreSerializedAndRetained()
    {
        var writes =
            new ConcurrentQueue<string>();
        int active = 0;
        int maximum = 0;
        using var coordinator =
            new ApplicationAudioControlCoordinator(
                (id, _) =>
                {
                    int current =
                        Interlocked.Increment(
                            ref active);
                    maximum = Math.Max(
                        maximum,
                        current);
                    Thread.Sleep(5);
                    writes.Enqueue(id);
                    Interlocked.Decrement(
                        ref active);
                    return true;
                },
                (_, _) => true);

        coordinator.QueueVolume("a", 1, 0.3f);
        coordinator.QueueVolume("b", 2, 0.6f);
        await coordinator.CompleteAsync()
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, maximum);
        Assert.Contains("a", writes);
        Assert.Contains("b", writes);
    }

    [Fact]
    public async Task VolumeAndMuteForPendingSession_Merge()
    {
        var entered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
        using var release =
            new ManualResetEventSlim(false);
        var outcomes =
            new ConcurrentQueue<
                ApplicationAudioControlOutcome>();
        using var coordinator =
            new ApplicationAudioControlCoordinator(
                (_, value) =>
                {
                    if (value == 0.1f)
                    {
                        entered.TrySetResult(true);
                        release.Wait();
                    }
                    return true;
                },
                (_, _) => true);
        coordinator.Completed += outcomes.Enqueue;

        coordinator.QueueVolume("blocker", 1, 0.1f);
        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        coordinator.QueueVolume("music", 2, 0.7f);
        coordinator.QueueMuted("music", 3, true);
        release.Set();
        await coordinator.CompleteAsync()
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Contains(
            outcomes,
            outcome =>
                outcome.Mutation.SessionId
                    == "music"
                && outcome.Mutation.Volume
                    == 0.7f
                && outcome.Mutation.IsMuted
                    == true
                && outcome.Mutation.Revision
                    == 3);
    }
}
