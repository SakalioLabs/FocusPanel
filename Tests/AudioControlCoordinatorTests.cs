using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AudioControlCoordinatorTests
{
    [Fact]
    public async Task QueueVolume_ReturnsWhileDeviceWriteIsBlocked()
    {
        var entered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
        using var release =
            new ManualResetEventSlim(false);
        using var coordinator =
            new AudioControlCoordinator(
                _ =>
                {
                    entered.TrySetResult(true);
                    release.Wait();
                    return true;
                },
                _ => true);

        Assert.True(
            coordinator.QueueVolume(
                1,
                0.6f));
        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        Task completion =
            coordinator.CompleteAsync();
        Assert.False(completion.IsCompleted);

        release.Set();
        await completion.WaitAsync(
            TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task PendingVolumeChanges_CoalesceToLatestValue()
    {
        var writes =
            new ConcurrentQueue<float>();
        var entered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
        using var release =
            new ManualResetEventSlim(false);
        using var coordinator =
            new AudioControlCoordinator(
                value =>
                {
                    writes.Enqueue(value);
                    if (value == 0.2f)
                    {
                        entered.TrySetResult(true);
                        release.Wait();
                    }
                    return true;
                },
                _ => true);

        coordinator.QueueVolume(
            1,
            0.2f);
        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        coordinator.QueueVolume(
            2,
            0.5f);
        coordinator.QueueVolume(
            3,
            0.8f);

        release.Set();
        await coordinator.CompleteAsync()
            .WaitAsync(
                TimeSpan.FromSeconds(2));

        Assert.Equal(
            new[] { 0.2f, 0.8f },
            writes.ToArray());
    }

    [Fact]
    public async Task VolumeAndMuteWrites_AreSerializedInOneWorker()
    {
        var order =
            new ConcurrentQueue<string>();
        int active = 0;
        int maximum = 0;
        using var coordinator =
            new AudioControlCoordinator(
                value =>
                {
                    int current =
                        Interlocked.Increment(
                            ref active);
                    UpdateMaximum(
                        ref maximum,
                        current);
                    order.Enqueue(
                        $"volume:{value:0.0}");
                    Thread.Sleep(5);
                    Interlocked.Decrement(
                        ref active);
                    return true;
                },
                value =>
                {
                    int current =
                        Interlocked.Increment(
                            ref active);
                    UpdateMaximum(
                        ref maximum,
                        current);
                    order.Enqueue(
                        $"mute:{value}");
                    Thread.Sleep(5);
                    Interlocked.Decrement(
                        ref active);
                    return true;
                });

        coordinator.QueueVolume(
            1,
            0.4f);
        coordinator.QueueMuted(
            2,
            true);
        await coordinator.CompleteAsync()
            .WaitAsync(
                TimeSpan.FromSeconds(2));

        Assert.Equal(
            1,
            maximum);
        Assert.Equal(
            new[]
            {
                "volume:0.4",
                "mute:True"
            },
            order.ToArray());
    }

    [Fact]
    public async Task FailedMutation_DoesNotStopTrailingWrite()
    {
        var outcomes =
            new ConcurrentQueue<
                AudioControlOutcome>();
        using var coordinator =
            new AudioControlCoordinator(
                _ => false,
                _ => true);
        coordinator.Completed +=
            outcomes.Enqueue;

        coordinator.QueueVolume(
            1,
            0.3f);
        coordinator.QueueMuted(
            2,
            true);
        await coordinator.CompleteAsync()
            .WaitAsync(
                TimeSpan.FromSeconds(2));

        AudioControlOutcome[] values =
            outcomes.ToArray();
        Assert.NotEmpty(values);
        Assert.Contains(
            values,
            item =>
                item.VolumeSucceeded
                    == false);
        Assert.Contains(
            values,
            item =>
                item.MuteSucceeded
                    == true);
    }

    [Fact]
    public async Task Complete_DrainsAcceptedWorkAndRejectsNewMutations()
    {
        int writes = 0;
        using var coordinator =
            new AudioControlCoordinator(
                _ =>
                {
                    Interlocked.Increment(
                        ref writes);
                    return true;
                },
                _ => true);

        Assert.True(
            coordinator.QueueVolume(
                1,
                0.5f));
        Task completion =
            coordinator.CompleteAsync();
        Assert.False(
            coordinator.QueueMuted(
                2,
                true));
        await completion.WaitAsync(
            TimeSpan.FromSeconds(2));

        Assert.Equal(
            1,
            writes);
    }

    [Fact]
    public void CompletionPolicy_StaleSuccessUpdatesBaselineWithoutOverwritingUi()
    {
        var state =
            new AudioControlConfirmationState(
                0.2f,
                false,
                3,
                0,
                true,
                false);
        var outcome =
            new AudioControlOutcome(
                new AudioControlMutation(
                    2,
                    0.5f,
                    0,
                    null),
                true,
                null);

        AudioControlCompletion completion =
            AudioControlCompletionPolicy.Apply(
                state,
                outcome);

        Assert.Equal(
            0.5f,
            completion.State
                .ConfirmedVolume);
        Assert.True(
            completion.State.VolumePending);
        Assert.Null(
            completion.DisplayVolume);
        Assert.False(
            completion.CurrentSucceeded);
        Assert.False(
            completion.CurrentFailed);
    }

    [Fact]
    public void CompletionPolicy_CurrentFailureRollsBackToLatestConfirmedValue()
    {
        var state =
            new AudioControlConfirmationState(
                0.5f,
                false,
                3,
                0,
                true,
                false);
        var outcome =
            new AudioControlOutcome(
                new AudioControlMutation(
                    3,
                    0.8f,
                    0,
                    null),
                false,
                null);

        AudioControlCompletion completion =
            AudioControlCompletionPolicy.Apply(
                state,
                outcome);

        Assert.False(
            completion.State.VolumePending);
        Assert.Equal(
            0.5f,
            completion.DisplayVolume);
        Assert.False(
            completion.CurrentSucceeded);
        Assert.True(
            completion.CurrentFailed);
    }

    private static void UpdateMaximum(
        ref int maximum,
        int candidate)
    {
        while (true)
        {
            int current =
                Volatile.Read(
                    ref maximum);
            if (candidate <= current)
                return;
            if (Interlocked.CompareExchange(
                    ref maximum,
                    candidate,
                    current)
                == current)
            {
                return;
            }
        }
    }
}
