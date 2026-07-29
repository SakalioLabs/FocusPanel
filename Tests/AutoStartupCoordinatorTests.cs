using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AutoStartupCoordinatorTests
{
    [Fact]
    public async Task Read_RunsOffCallingThreadWithoutBlocking()
    {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        int callingThread =
            Environment.CurrentManagedThreadId;
        int readThread =
            callingThread;
        var coordinator =
            new AutoStartupCoordinator(
                read: () =>
                {
                    readThread =
                        Environment
                            .CurrentManagedThreadId;
                    started.Set();
                    release.Wait(
                        TimeSpan.FromSeconds(5));
                    return true;
                },
                write: _ =>
                    new AutoStartupMutation(
                        true,
                        string.Empty));

        Task<AutoStartupCompletion> operation =
            coordinator.ReadAsync();

        Assert.True(
            started.Wait(
                TimeSpan.FromSeconds(2)));
        Assert.False(operation.IsCompleted);
        Assert.NotEqual(
            callingThread,
            readThread);

        release.Set();
        AutoStartupCompletion result =
            await operation;
        Assert.True(result.Succeeded);
        Assert.True(result.Enabled);
    }

    [Fact]
    public async Task RapidWrites_AreSerializedInRequestOrder()
    {
        using var firstStarted =
            new ManualResetEventSlim();
        using var releaseFirst =
            new ManualResetEventSlim();
        using var secondStarted =
            new ManualResetEventSlim();
        var calls = new List<bool>();
        bool actual = false;
        var coordinator =
            new AutoStartupCoordinator(
                read: () => actual,
                write: enabled =>
                {
                    lock (calls)
                        calls.Add(enabled);
                    if (enabled)
                    {
                        firstStarted.Set();
                        releaseFirst.Wait(
                            TimeSpan.FromSeconds(5));
                    }
                    else
                    {
                        secondStarted.Set();
                    }
                    actual = enabled;
                    return new AutoStartupMutation(
                        true,
                        string.Empty);
                });

        Task<AutoStartupCompletion> enable =
            coordinator.SetAsync(true);
        Assert.True(
            firstStarted.Wait(
                TimeSpan.FromSeconds(2)));
        Task<AutoStartupCompletion> disable =
            coordinator.SetAsync(false);

        Assert.False(
            secondStarted.Wait(
                TimeSpan.FromMilliseconds(120)));
        releaseFirst.Set();
        await Task.WhenAll(
            enable,
            disable);

        Assert.True(secondStarted.IsSet);
        Assert.Equal(
            new[]
            {
                true,
                false
            },
            calls);
        Assert.False(actual);
    }

    [Fact]
    public async Task FailedWrite_ReturnsRereadRegistryState()
    {
        var coordinator =
            new AutoStartupCoordinator(
                read: () => true,
                write: _ =>
                    new AutoStartupMutation(
                        false,
                        "访问被拒绝"));

        AutoStartupCompletion result =
            await coordinator.SetAsync(false);

        Assert.False(result.Succeeded);
        Assert.True(result.Enabled);
        Assert.Equal(
            "访问被拒绝",
            result.Error);
    }

    [Fact]
    public async Task DelegateExceptionsBecomeRecoverableResults()
    {
        var coordinator =
            new AutoStartupCoordinator(
                read: () =>
                    throw new InvalidOperationException(
                        "registry unavailable"),
                write: _ =>
                    throw new UnauthorizedAccessException(
                        "policy denied"));

        AutoStartupCompletion read =
            await coordinator.ReadAsync();
        AutoStartupCompletion write =
            await coordinator.SetAsync(true);

        Assert.False(read.Succeeded);
        Assert.False(read.Enabled);
        Assert.Contains(
            "registry unavailable",
            read.Error);
        Assert.False(write.Succeeded);
        Assert.False(write.Enabled);
        Assert.Contains(
            "policy denied",
            write.Error);
    }

    [Fact]
    public async Task OnlyLatestRequestOwnsUiFeedback()
    {
        using var firstStarted =
            new ManualResetEventSlim();
        using var releaseFirst =
            new ManualResetEventSlim();
        var coordinator =
            new AutoStartupCoordinator(
                read: () =>
                {
                    firstStarted.Set();
                    releaseFirst.Wait(
                        TimeSpan.FromSeconds(5));
                    return false;
                },
                write: enabled =>
                    new AutoStartupMutation(
                        true,
                        string.Empty));

        Task<AutoStartupCompletion> read =
            coordinator.ReadAsync();
        Assert.True(
            firstStarted.Wait(
                TimeSpan.FromSeconds(2)));
        Task<AutoStartupCompletion> write =
            coordinator.SetAsync(true);
        releaseFirst.Set();
        AutoStartupCompletion[] results =
            await Task.WhenAll(
                read,
                write);

        Assert.False(
            coordinator.IsCurrent(
                results[0].Revision));
        Assert.True(
            coordinator.IsCurrent(
                results[1].Revision));
    }

    [Fact]
    public async Task Complete_WaitsForAcceptedWriteAndRejectsLaterWork()
    {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        int reads = 0;
        var coordinator =
            new AutoStartupCoordinator(
                read: () =>
                {
                    reads++;
                    return false;
                },
                write: _ =>
                {
                    started.Set();
                    release.Wait(
                        TimeSpan.FromSeconds(5));
                    return new AutoStartupMutation(
                        true,
                        string.Empty);
                });

        Task<AutoStartupCompletion> write =
            coordinator.SetAsync(true);
        Assert.True(
            started.Wait(
                TimeSpan.FromSeconds(2)));
        Task drain =
            coordinator.CompleteAsync();
        Assert.False(drain.IsCompleted);
        release.Set();
        await drain;
        Assert.True(
            (await write).Succeeded);

        AutoStartupCompletion rejected =
            await coordinator.ReadAsync();
        Assert.False(rejected.Succeeded);
        Assert.Equal(0, reads);
        Assert.Contains(
            "正在退出",
            rejected.Error);
    }
}
