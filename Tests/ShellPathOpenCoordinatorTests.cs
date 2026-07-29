using System;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellPathOpenCoordinatorTests
{
    [Fact]
    public async Task Open_RunsOffCallingThread()
    {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        int callingThread =
            Environment.CurrentManagedThreadId;
        int openThread = callingThread;
        var coordinator =
            new ShellPathOpenCoordinator(
                _ =>
                {
                    openThread =
                        Environment
                            .CurrentManagedThreadId;
                    started.Set();
                    release.Wait(
                        TimeSpan.FromSeconds(5));
                    return true;
                });

        Task<ShellPathOpenCompletion> operation =
            coordinator.OpenAsync(
                @"C:\Desktop\报告.docx");

        Assert.True(
            started.Wait(TimeSpan.FromSeconds(2)));
        Assert.False(operation.IsCompleted);
        Assert.NotEqual(callingThread, openThread);

        release.Set();
        Assert.True((await operation).Succeeded);
    }

    [Fact]
    public async Task Open_TrimsDetachedPath()
    {
        string? observed = null;
        var coordinator =
            new ShellPathOpenCoordinator(
                path =>
                {
                    observed = path;
                    return true;
                });

        ShellPathOpenCompletion completion =
            await coordinator.OpenAsync(
                "  C:\\Desktop\\文件.txt  ");

        Assert.True(completion.Succeeded);
        Assert.Equal(
            @"C:\Desktop\文件.txt",
            observed);
    }

    [Fact]
    public async Task EmptyPathFailsWithoutCallingShell()
    {
        int calls = 0;
        var coordinator =
            new ShellPathOpenCoordinator(
                _ =>
                {
                    calls++;
                    return true;
                });

        ShellPathOpenCompletion completion =
            await coordinator.OpenAsync("  ");

        Assert.False(completion.Succeeded);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task UnexpectedExceptionBecomesFailure()
    {
        var coordinator =
            new ShellPathOpenCoordinator(
                _ =>
                    throw new InvalidOperationException(
                        "Shell unavailable"));

        ShellPathOpenCompletion completion =
            await coordinator.OpenAsync(
                @"C:\Desktop\失效.lnk");

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
            new ShellPathOpenCoordinator(
                path =>
                {
                    if (path.EndsWith(
                            "first.txt",
                            StringComparison.Ordinal))
                    {
                        firstStarted.Set();
                        releaseFirst.Wait(
                            TimeSpan.FromSeconds(5));
                        return false;
                    }
                    return true;
                });

        Task<ShellPathOpenCompletion> first =
            coordinator.OpenAsync("first.txt");
        Assert.True(
            firstStarted.Wait(
                TimeSpan.FromSeconds(2)));
        ShellPathOpenCompletion second =
            await coordinator.OpenAsync(
                "second.txt");

        Assert.True(
            coordinator.IsCurrent(
                second.Revision));
        releaseFirst.Set();
        ShellPathOpenCompletion stale =
            await first;
        Assert.False(
            coordinator.IsCurrent(
                stale.Revision));
    }
}
