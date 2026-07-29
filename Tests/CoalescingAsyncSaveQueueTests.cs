using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class CoalescingAsyncSaveQueueTests
{
    [Fact]
    public async Task BurstForSameItem_IsSavedOnce()
    {
        var item = new SaveItem("A");
        var saved = new List<SaveItem>();
        var queue =
            new CoalescingAsyncSaveQueue<SaveItem>(
                candidate =>
                {
                    saved.Add(candidate);
                    return Task.CompletedTask;
                },
                TimeSpan.FromMilliseconds(20));

        Assert.True(queue.Enqueue(item));
        Assert.True(queue.Enqueue(item));
        Assert.True(queue.Enqueue(item));

        await queue.CompleteAsync();

        Assert.Single(saved);
        Assert.Same(item, saved[0]);
    }

    [Fact]
    public async Task DistinctItems_PreserveFirstSeenOrder()
    {
        var first = new SaveItem("A");
        var second = new SaveItem("B");
        var third = new SaveItem("C");
        var saved = new List<SaveItem>();
        var queue =
            new CoalescingAsyncSaveQueue<SaveItem>(
                candidate =>
                {
                    saved.Add(candidate);
                    return Task.CompletedTask;
                },
                TimeSpan.FromMilliseconds(20));

        queue.Enqueue(first);
        queue.Enqueue(second);
        queue.Enqueue(first);
        queue.Enqueue(third);

        await queue.CompleteAsync();

        Assert.Equal(
            new[] { first, second, third },
            saved);
    }

    [Fact]
    public async Task ChangeDuringActiveSave_ProducesOneTrailingSave()
    {
        var item = new SaveItem("A");
        var firstSaveStarted =
            NewCompletionSource();
        var releaseFirstSave =
            NewCompletionSource();
        int saveCount = 0;
        var queue =
            new CoalescingAsyncSaveQueue<SaveItem>(
                async _ =>
                {
                    saveCount++;
                    if (saveCount == 1)
                    {
                        firstSaveStarted.SetResult();
                        await releaseFirstSave.Task;
                    }
                },
                TimeSpan.Zero);

        queue.Enqueue(item);
        await firstSaveStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        queue.Enqueue(item);
        queue.Enqueue(item);
        releaseFirstSave.SetResult();

        await queue.CompleteAsync();

        Assert.Equal(2, saveCount);
    }

    [Fact]
    public async Task FailedItem_DoesNotStopLaterItems()
    {
        var first = new SaveItem("A");
        var second = new SaveItem("B");
        var saved = new List<SaveItem>();
        var failures =
            new List<(SaveItem Item, Exception Error)>();
        var queue =
            new CoalescingAsyncSaveQueue<SaveItem>(
                candidate =>
                {
                    if (ReferenceEquals(
                            candidate,
                            first))
                    {
                        throw new InvalidOperationException(
                            "write failed");
                    }

                    saved.Add(candidate);
                    return Task.CompletedTask;
                },
                TimeSpan.FromMilliseconds(10));
        queue.ItemSaveFailed +=
            (item, error) =>
                failures.Add((item, error));

        queue.Enqueue(first);
        queue.Enqueue(second);

        await queue.CompleteAsync();

        Assert.Single(failures);
        Assert.Same(first, failures[0].Item);
        Assert.Equal(
            "write failed",
            failures[0].Error.Message);
        Assert.Equal(
            new[] { second },
            saved);
    }

    [Fact]
    public async Task Discard_RemovesPendingItem()
    {
        var item = new SaveItem("A");
        int saveCount = 0;
        var queue =
            new CoalescingAsyncSaveQueue<SaveItem>(
                _ =>
                {
                    saveCount++;
                    return Task.CompletedTask;
                },
                TimeSpan.FromMilliseconds(30));

        queue.Enqueue(item);
        Assert.True(queue.Discard(item));

        await queue.CompleteAsync();

        Assert.Equal(0, saveCount);
    }

    [Fact]
    public async Task FlushDrainsCurrentWork_AndCompleteRejectsNewWork()
    {
        var first = new SaveItem("A");
        var second = new SaveItem("B");
        int saveCount = 0;
        var queue =
            new CoalescingAsyncSaveQueue<SaveItem>(
                _ =>
                {
                    saveCount++;
                    return Task.CompletedTask;
                },
                TimeSpan.FromMilliseconds(10));

        queue.Enqueue(first);
        await queue.FlushAsync();
        Assert.Equal(1, saveCount);

        await queue.CompleteAsync();

        Assert.False(queue.Enqueue(second));
        Assert.Equal(1, saveCount);
    }

    [Fact]
    public async Task ObserverFailure_DoesNotStopQueueOrOtherObservers()
    {
        var first = new SaveItem("A");
        var second = new SaveItem("B");
        int saveCount = 0;
        int observedCount = 0;
        var queue =
            new CoalescingAsyncSaveQueue<SaveItem>(
                _ =>
                {
                    saveCount++;
                    return Task.CompletedTask;
                },
                TimeSpan.FromMilliseconds(10));
        queue.ItemSaved +=
            _ => throw new InvalidOperationException(
                "observer is gone");
        queue.ItemSaved +=
            _ => observedCount++;

        queue.Enqueue(first);
        queue.Enqueue(second);

        await queue.CompleteAsync();

        Assert.Equal(2, saveCount);
        Assert.Equal(2, observedCount);
    }

    [Fact]
    public void TaskPersistence_UsesQueueGateAndExitDrain()
    {
        string root = FindRepositoryRoot();
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "TasksViewModel.cs"));
        string service = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "TaskService.cs"));

        Assert.DoesNotContain(
            "private async void OnTodoItemPropertyChanged",
            viewModel);
        Assert.Contains(
            "_taskSaveQueue.Enqueue(item);",
            viewModel);
        Assert.Contains(
            "_taskSaveQueue.Discard(item);",
            viewModel);
        Assert.Contains(
            "_taskSaveQueue.CompleteAsync()",
            viewModel);
        Assert.Contains(
            "Interlocked.Increment(",
            viewModel);
        Assert.Contains(
            "loadGeneration",
            viewModel);
        Assert.True(
            viewModel.IndexOf(
                "_taskSaveQueue.CompleteAsync()",
                StringComparison.Ordinal)
            < viewModel.IndexOf(
                "_context.Dispose();",
                StringComparison.Ordinal));

        Assert.Contains(
            "SemaphoreSlim _operationGate",
            service);
        Assert.Equal(
            7,
            service.Split(
                    "ExecuteAsync(",
                    StringSplitOptions.None)
                .Length
            - 1);
        Assert.Contains(
            "WaitForIdleAsync()",
            service);
    }

    private static TaskCompletionSource
        NewCompletionSource() =>
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current =
            new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(
                    Path.Combine(
                        current.FullName,
                        "FocusPanel.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate FocusPanel.csproj.");
    }

    private sealed record SaveItem(string Name);
}
