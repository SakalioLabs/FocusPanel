using System;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AppLaunchCoordinatorTests
{
    [Fact]
    public async Task Launch_RunsOffCallingThread()
    {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        int callingThread =
            Environment.CurrentManagedThreadId;
        int launchThread = callingThread;
        var coordinator =
            new AppLaunchCoordinator(
                _ =>
                {
                    launchThread =
                        Environment
                            .CurrentManagedThreadId;
                    started.Set();
                    release.Wait(
                        TimeSpan.FromSeconds(5));
                    return true;
                });

        Task<AppLaunchCompletion> operation =
            coordinator.LaunchAsync(Demo("慢应用"));

        Assert.True(
            started.Wait(TimeSpan.FromSeconds(2)));
        Assert.False(operation.IsCompleted);
        Assert.NotEqual(callingThread, launchThread);

        release.Set();
        Assert.True((await operation).Succeeded);
    }

    [Fact]
    public async Task Launch_ContainsUnexpectedException()
    {
        var coordinator =
            new AppLaunchCoordinator(
                _ =>
                    throw new InvalidOperationException(
                        "Shell unavailable"));

        AppLaunchCompletion completion =
            await coordinator.LaunchAsync(
                Demo("失效应用"));

        Assert.False(completion.Succeeded);
        Assert.True(
            coordinator.IsCurrent(
                completion.Revision));
    }

    [Fact]
    public async Task LatestRequestOwnsVisibleFeedback()
    {
        using var firstStarted =
            new ManualResetEventSlim();
        using var releaseFirst =
            new ManualResetEventSlim();
        var coordinator =
            new AppLaunchCoordinator(
                app =>
                {
                    if (app.DisplayName == "第一个")
                    {
                        firstStarted.Set();
                        releaseFirst.Wait(
                            TimeSpan.FromSeconds(5));
                        return false;
                    }
                    return true;
                });

        Task<AppLaunchCompletion> first =
            coordinator.LaunchAsync(
                Demo("第一个"));
        Assert.True(
            firstStarted.Wait(
                TimeSpan.FromSeconds(2)));
        AppLaunchCompletion second =
            await coordinator.LaunchAsync(
                Demo("第二个"));

        Assert.True(second.Succeeded);
        Assert.True(
            coordinator.IsCurrent(
                second.Revision));
        releaseFirst.Set();
        AppLaunchCompletion stale = await first;
        Assert.False(stale.Succeeded);
        Assert.False(
            coordinator.IsCurrent(
                stale.Revision));
    }

    [Fact]
    public async Task Launch_UsesDetachedSnapshotWithoutUiIcon()
    {
        AppLaunchItem? observed = null;
        var coordinator =
            new AppLaunchCoordinator(
                app =>
                {
                    observed = app;
                    return true;
                });
        AppLaunchItem source = Demo("编辑器");
        source.IsPinned = true;

        await coordinator.LaunchAsync(source);

        Assert.NotNull(observed);
        Assert.NotSame(source, observed);
        Assert.Equal(
            source.LaunchTarget,
            observed.LaunchTarget);
        Assert.Null(observed.Icon);
        Assert.False(observed.IsPinned);
    }

    private static AppLaunchItem Demo(
        string displayName) =>
        new()
        {
            DisplayName = displayName,
            LaunchKind = AppLaunchKind.Executable,
            LaunchTarget = displayName + ".exe"
        };
}
