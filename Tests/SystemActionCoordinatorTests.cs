using System;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class SystemActionCoordinatorTests
{
    [Fact]
    public async Task Execute_RunsOffCallingThreadWithoutBlocking()
    {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        int callingThread =
            Environment.CurrentManagedThreadId;
        int actionThread =
            callingThread;
        var coordinator =
            new SystemActionCoordinator();

        Task<SystemActionCompletion> operation =
            coordinator.ExecuteAsync(
                () =>
                {
                    actionThread =
                        Environment
                            .CurrentManagedThreadId;
                    started.Set();
                    release.Wait(
                        TimeSpan.FromSeconds(5));
                    return true;
                });

        Assert.True(
            started.Wait(
                TimeSpan.FromSeconds(2)));
        Assert.False(operation.IsCompleted);
        Assert.NotEqual(
            callingThread,
            actionThread);

        release.Set();
        Assert.True(
            (await operation).Succeeded);
    }

    [Fact]
    public async Task FalseResultIsPreserved()
    {
        var coordinator =
            new SystemActionCoordinator();

        SystemActionCompletion completion =
            await coordinator.ExecuteAsync(
                () => false);

        Assert.False(completion.Succeeded);
    }

    [Fact]
    public async Task UnexpectedExceptionBecomesFailure()
    {
        var coordinator =
            new SystemActionCoordinator();

        SystemActionCompletion completion =
            await coordinator.ExecuteAsync(
                () =>
                    throw new InvalidOperationException(
                        "Shell unavailable"));

        Assert.False(completion.Succeeded);
    }

    [Fact]
    public async Task OnlyLatestRequestOwnsFeedback()
    {
        using var firstStarted =
            new ManualResetEventSlim();
        using var releaseFirst =
            new ManualResetEventSlim();
        var coordinator =
            new SystemActionCoordinator();

        Task<SystemActionCompletion> first =
            coordinator.ExecuteAsync(
                () =>
                {
                    firstStarted.Set();
                    releaseFirst.Wait(
                        TimeSpan.FromSeconds(5));
                    return false;
                });
        Assert.True(
            firstStarted.Wait(
                TimeSpan.FromSeconds(2)));
        SystemActionCompletion second =
            await coordinator.ExecuteAsync(
                () => true);

        Assert.True(
            coordinator.IsCurrent(
                second.Revision));
        releaseFirst.Set();
        SystemActionCompletion stale =
            await first;
        Assert.False(
            coordinator.IsCurrent(
                stale.Revision));
    }

    [Fact]
    public void NullActionIsRejectedBeforeScheduling()
    {
        var coordinator =
            new SystemActionCoordinator();
        Action execute =
            () =>
            {
                _ = coordinator.ExecuteAsync(
                    null!);
            };

        Assert.Throws<
            ArgumentNullException>(
            execute);
    }
}
