using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellPreferenceRepositoryTests
{
    [Fact]
    public void Load_NormalizesUnsupportedTheme()
    {
        using var repository =
            new ShellPreferenceRepository(
                () =>
                    new ShellPreferenceSnapshot(
                        true,
                        true,
                        "Neon",
                        false),
                (_, _) => { });

        ShellPreferenceSnapshot snapshot =
            repository.Load();

        Assert.True(snapshot.FirstRunAccepted);
        Assert.True(snapshot.ReplacementEnabled);
        Assert.Equal(
            "System",
            snapshot.ThemeMode);
        Assert.False(
            snapshot.DisableHotZoneInFullscreen);
    }

    [Fact]
    public async Task QueueSave_ReturnsWhileStorageIsBlocked()
    {
        var entered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
        using var release =
            new ManualResetEventSlim(false);
        using var repository =
            new ShellPreferenceRepository(
                () =>
                    ShellPreferenceSnapshot.Default,
                (_, _) =>
                {
                    entered.TrySetResult(true);
                    release.Wait();
                });

        Assert.True(
            repository.QueueSave(
                ShellPreferenceRepository
                    .ThemeModeKey,
                "Dark"));
        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(2));

        Task completion =
            repository.CompleteAsync();
        Assert.False(completion.IsCompleted);

        release.Set();
        await completion.WaitAsync(
            TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task RepeatedPendingKey_CoalescesToLatestValue()
    {
        var writes =
            new ConcurrentQueue<string>();
        var entered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
        using var release =
            new ManualResetEventSlim(false);
        using var repository =
            new ShellPreferenceRepository(
                () =>
                    ShellPreferenceSnapshot.Default,
                (_, value) =>
                {
                    writes.Enqueue(value);
                    if (value == "Dark")
                    {
                        entered.TrySetResult(true);
                        release.Wait();
                    }
                });

        repository.QueueSave(
            ShellPreferenceRepository
                .ThemeModeKey,
            "Dark");
        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        repository.QueueSave(
            ShellPreferenceRepository
                .ThemeModeKey,
            "Light");
        repository.QueueSave(
            ShellPreferenceRepository
                .ThemeModeKey,
            "System");

        release.Set();
        await repository.CompleteAsync()
            .WaitAsync(
                TimeSpan.FromSeconds(2));

        Assert.Equal(
            new[] { "Dark", "System" },
            writes.ToArray());
    }

    [Fact]
    public async Task FailedSave_ReportsErrorAndContinuesQueue()
    {
        var saved =
            new ConcurrentQueue<string>();
        var failures =
            new ConcurrentQueue<string>();
        using var repository =
            new ShellPreferenceRepository(
                () =>
                    ShellPreferenceSnapshot.Default,
                (key, _) =>
                {
                    if (key == "bad")
                    {
                        throw new InvalidOperationException(
                            "busy");
                    }

                    saved.Enqueue(key);
                });
        repository.SaveFailed +=
            (key, _) =>
                failures.Enqueue(key);

        repository.QueueSave(
            "bad",
            "1");
        repository.QueueSave(
            "good",
            "2");
        await repository.CompleteAsync()
            .WaitAsync(
                TimeSpan.FromSeconds(2));

        Assert.Equal(
            new[] { "bad" },
            failures.ToArray());
        Assert.Equal(
            new[] { "good" },
            saved.ToArray());
    }

    [Fact]
    public async Task Complete_DrainsAcceptedWritesAndRejectsNewWork()
    {
        var saved =
            new List<string>();
        var entered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
        using var release =
            new ManualResetEventSlim(false);
        using var repository =
            new ShellPreferenceRepository(
                () =>
                    ShellPreferenceSnapshot.Default,
                (key, _) =>
                {
                    lock (saved)
                        saved.Add(key);
                    entered.TrySetResult(true);
                    release.Wait();
                });

        Assert.True(
            repository.QueueSave(
                "accepted",
                "1"));
        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        Task completion =
            repository.CompleteAsync();

        Assert.False(
            repository.QueueSave(
                "rejected",
                "2"));
        release.Set();
        await completion.WaitAsync(
            TimeSpan.FromSeconds(2));

        lock (saved)
        {
            Assert.Equal(
                new[] { "accepted" },
                saved);
        }
    }
}
