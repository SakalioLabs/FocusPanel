using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class InFlightTaskTrackerTests
{
    [Fact]
    public async Task CompleteAsync_WaitsForStartedTask()
    {
        var tracker = new InFlightTaskTracker();
        var release =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
        Task<int>? work =
            tracker.TryStart(
                async () =>
                {
                    await release.Task;
                    return 7;
                });

        Task drain = tracker.CompleteAsync();

        Assert.NotNull(work);
        Assert.False(drain.IsCompleted);
        release.SetResult(true);
        await drain;
        Assert.Equal(7, await work!);
    }

    [Fact]
    public async Task CompleteAsync_RejectsNewTasks()
    {
        var tracker = new InFlightTaskTracker();

        await tracker.CompleteAsync();
        Task<int>? rejected =
            tracker.TryStart(
                () => Task.FromResult(1));

        Assert.Null(rejected);
    }

    [Fact]
    public async Task CompletedTask_IsRemovedBeforeDrain()
    {
        var tracker = new InFlightTaskTracker();
        Task<int>? work =
            tracker.TryStart(
                () => Task.FromResult(3));
        Assert.NotNull(work);
        Assert.Equal(3, await work!);

        Task drain = tracker.CompleteAsync();

        Assert.True(drain.IsCompletedSuccessfully);
    }
}
