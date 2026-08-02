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
    public void Default_DoesNotClaimGlobalNumberShortcuts()
    {
        Assert.False(
            ShellPreferenceSnapshot
                .Default
                .EnableTaskbarSlotHotkeys);
        Assert.Equal(
            ShellAutoHideDelayPolicy
                .DefaultMilliseconds,
            ShellPreferenceSnapshot
                .Default
                .AutoHideDelayMilliseconds);
        Assert.Equal(
            EdgeHotZoneSensitivityPolicy
                .DefaultDwellMilliseconds,
            ShellPreferenceSnapshot
                .Default
                .HotZoneDwellMilliseconds);
    }

    [Fact]
    public async Task Load_NormalizesUnsupportedTheme()
    {
        using var repository =
            new ShellPreferenceRepository(
                () =>
                    new ShellPreferenceSnapshot(
                        true,
                        true,
                        "Neon",
                        false,
                        true,
                        "Unsupported",
                        999,
                        999),
                (_, _) => { });

        ShellPreferenceSnapshot snapshot =
            await repository.LoadAsync();

        Assert.True(snapshot.FirstRunAccepted);
        Assert.True(snapshot.ReplacementEnabled);
        Assert.Equal(
            "System",
            snapshot.ThemeMode);
        Assert.False(
            snapshot.DisableHotZoneInFullscreen);
        Assert.True(
            snapshot.EnableTaskbarSlotHotkeys);
        Assert.Equal(
            ShellDisplayTarget
                .OutermostRightValue,
            snapshot.DisplayTargetMode);
        Assert.Equal(
            ShellAutoHideDelayPolicy
                .DefaultMilliseconds,
            snapshot.AutoHideDelayMilliseconds);
        Assert.Equal(
            EdgeHotZoneSensitivityPolicy
                .DefaultDwellMilliseconds,
            snapshot.HotZoneDwellMilliseconds);
    }

    [Fact]
    public async Task Load_PreservesSpecificDisplayDevice()
    {
        using var repository =
            new ShellPreferenceRepository(
                () =>
                    new ShellPreferenceSnapshot(
                        true,
                        true,
                        "System",
                        true,
                        false,
                        @"device:  \\.\DISPLAY2  ",
                        800,
                        180),
                (_, _) => { });

        ShellPreferenceSnapshot snapshot =
            await repository.LoadAsync();

        Assert.Equal(
            @"Device:\\.\DISPLAY2",
            snapshot.DisplayTargetMode);
        Assert.Equal(
            800,
            snapshot.AutoHideDelayMilliseconds);
        Assert.Equal(
            180,
            snapshot.HotZoneDwellMilliseconds);
    }

    [Fact]
    public async Task Load_ReturnsBeforeStorageCompletesAndIsShared()
    {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        int callingThread =
            Environment.CurrentManagedThreadId;
        int loadThread =
            callingThread;
        using var repository =
            new ShellPreferenceRepository(
                () =>
                {
                    loadThread =
                        Environment
                            .CurrentManagedThreadId;
                    started.Set();
                    release.Wait(
                        TimeSpan.FromSeconds(5));
                    return new ShellPreferenceSnapshot(
                        true,
                        false,
                        "Dark",
                        true,
                        false,
                        ShellDisplayTarget
                            .PrimaryValue,
                        1200,
                        40);
                },
                (_, _) => { });

        Task<ShellPreferenceSnapshot> first =
            repository.LoadAsync();
        Task<ShellPreferenceSnapshot> second =
            repository.LoadAsync();

        Assert.Same(first, second);
        Assert.True(
            started.Wait(
                TimeSpan.FromSeconds(2)));
        Assert.False(first.IsCompleted);
        Assert.NotEqual(
            callingThread,
            loadThread);
        release.Set();

        ShellPreferenceSnapshot snapshot =
            await first;
        Assert.True(snapshot.FirstRunAccepted);
        Assert.False(snapshot.ReplacementEnabled);
        Assert.Equal("Dark", snapshot.ThemeMode);
        Assert.Equal(
            ShellDisplayTarget.PrimaryValue,
            snapshot.DisplayTargetMode);
        Assert.Equal(
            1200,
            snapshot.AutoHideDelayMilliseconds);
        Assert.Equal(
            40,
            snapshot.HotZoneDwellMilliseconds);
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
