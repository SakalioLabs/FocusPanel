using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    UpdateInstallPreparationCoordinatorTests
{
    [Fact]
    public async Task Prepare_RunsOffCallingThreadWithoutBlocking()
    {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        int callingThread =
            Environment.CurrentManagedThreadId;
        int preparationThread = callingThread;
        var coordinator =
            new
                UpdateInstallPreparationCoordinator(
                    () =>
                    {
                        preparationThread =
                            Environment
                                .CurrentManagedThreadId;
                        started.Set();
                        release.Wait(
                            TimeSpan.FromSeconds(5));
                    });

        var watch = Stopwatch.StartNew();
        Task<
            UpdateInstallPreparationCompletion>
            task =
                coordinator.PrepareAsync();
        watch.Stop();

        try
        {
            Assert.True(
                watch.Elapsed
                < TimeSpan.FromSeconds(1),
                $"PrepareAsync 阻塞了 {watch.ElapsedMilliseconds}ms。");
            Assert.True(
                started.Wait(
                    TimeSpan.FromSeconds(2)));
            Assert.NotEqual(
                callingThread,
                preparationThread);
        }
        finally
        {
            release.Set();
        }

        UpdateInstallPreparationCompletion
            completion = await task;
        Assert.True(completion.Succeeded);
        Assert.Empty(completion.Error);
    }

    [Fact]
    public async Task Prepare_ConvertsBackupExceptionToFailure()
    {
        var coordinator =
            new
                UpdateInstallPreparationCoordinator(
                    () =>
                        throw new
                            InvalidOperationException(
                                "磁盘不可用"));

        UpdateInstallPreparationCompletion
            completion =
                await coordinator
                    .PrepareAsync();

        Assert.False(completion.Succeeded);
        Assert.Equal(
            "磁盘不可用",
            completion.Error);
    }

    [Fact]
    public async Task Complete_RejectsNewPreparation()
    {
        int calls = 0;
        var coordinator =
            new
                UpdateInstallPreparationCoordinator(
                    () =>
                        Interlocked.Increment(
                            ref calls));

        await coordinator.CompleteAsync();
        UpdateInstallPreparationCompletion
            completion =
                await coordinator
                    .PrepareAsync();

        Assert.False(completion.Succeeded);
        Assert.Contains(
            "正在退出",
            completion.Error);
        Assert.Equal(0, calls);
    }
}
