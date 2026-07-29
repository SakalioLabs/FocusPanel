using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class FileOrganizerViewModelFactoryTests
{
    [Fact]
    public async Task CreateAsync_LeavesCallerThreadAndDoesNotBlock()
    {
        int callerThread = Environment.CurrentManagedThreadId;
        int workerThread = callerThread;
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var factory = new FileOrganizerViewModelFactory(
            _ =>
            {
                workerThread =
                    Environment.CurrentManagedThreadId;
                started.Set();
                release.Wait(TimeSpan.FromSeconds(5));
                throw new InvalidOperationException(
                    "expected test boundary");
            });

        Task task = factory.CreateAsync(
            Dispatcher.CurrentDispatcher);

        Assert.True(
            started.Wait(TimeSpan.FromSeconds(5)));
        Assert.False(task.IsCompleted);
        Assert.NotEqual(callerThread, workerThread);

        release.Set();
        await Assert.ThrowsAsync<
            InvalidOperationException>(
                async () => await task);
    }
}
