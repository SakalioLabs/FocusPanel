using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    TaskQuickCaptureCoordinatorTests
{
    [Fact]
    public async Task Capture_WritesChildIntoInbox()
    {
        TodoItem? saved = null;
        var coordinator =
            new TaskQuickCaptureCoordinator(
                Service(
                    add: item =>
                        saved = item));

        TaskQuickCaptureResult result =
            await coordinator.CaptureAsync(
                "整理周报");

        Assert.True(result.Succeeded);
        Assert.Same(saved, result.Item);
        Assert.Equal("整理周报", saved?.Title);
        Assert.Equal(
            TaskQuickCaptureCoordinator.InboxId,
            saved?.ParentId);
        Assert.Equal("To Do", saved?.Status);
        Assert.False(saved?.IsCompleted);
        await coordinator.CompleteAsync();
    }

    [Fact]
    public async Task PersistenceFailure_ReturnsError()
    {
        var coordinator =
            new TaskQuickCaptureCoordinator(
                Service(
                    add: _ =>
                        throw new InvalidOperationException(
                            "database busy")));

        TaskQuickCaptureResult result =
            await coordinator.CaptureAsync(
                "不会丢失的标题");

        Assert.False(result.Succeeded);
        Assert.Null(result.Item);
        Assert.Contains(
            "database busy",
            result.Error);
        await coordinator.CompleteAsync();
    }

    [Fact]
    public async Task Complete_WaitsAndRejectsLaterCapture()
    {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        var coordinator =
            new TaskQuickCaptureCoordinator(
                Service(
                    add: _ =>
                    {
                        started.Set();
                        release.Wait(
                            TimeSpan
                                .FromSeconds(2));
                    }));
        Task<TaskQuickCaptureResult> capture =
            coordinator.CaptureAsync("在途任务");
        Assert.True(
            started.Wait(
                TimeSpan.FromSeconds(2)));

        Task drain =
            coordinator.CompleteAsync();
        Assert.False(drain.IsCompleted);
        release.Set();
        await drain.WaitAsync(
            TimeSpan.FromSeconds(2));
        Assert.True(
            (await capture).Succeeded);

        TaskQuickCaptureResult rejected =
            await coordinator.CaptureAsync(
                "迟到任务");
        Assert.False(rejected.Succeeded);
        Assert.Contains(
            "关闭",
            rejected.Error);
    }

    [Fact]
    public async Task Capture_SerializesBehindSharedTaskServiceRead()
    {
        using var readStarted =
            new ManualResetEventSlim();
        using var releaseRead =
            new ManualResetEventSlim();
        using var addStarted =
            new ManualResetEventSlim();
        var service =
            new TaskService(
                new TaskPersistenceHandlers(
                    () =>
                    {
                        readStarted.Set();
                        releaseRead.Wait(
                            TimeSpan
                                .FromSeconds(2));
                        return new List<TodoItem>();
                    },
                    _ => new List<TodoItem>(),
                    _ => null,
                    _ => addStarted.Set(),
                    _ => { },
                    _ => { },
                    fallback => fallback,
                    _ => { }));
        var coordinator =
            new TaskQuickCaptureCoordinator(
                service);

        Task read = service.GetRootItemsAsync();
        Assert.True(
            readStarted.Wait(
                TimeSpan.FromSeconds(2)));
        Task<TaskQuickCaptureResult> capture =
            coordinator.CaptureAsync(
                "排队写入");
        Assert.False(
            addStarted.Wait(
                TimeSpan
                    .FromMilliseconds(120)));

        releaseRead.Set();
        await Task.WhenAll(
                read,
                capture)
            .WaitAsync(
                TimeSpan.FromSeconds(2));
        Assert.True(addStarted.IsSet);
        Assert.True(
            (await capture).Succeeded);
        await coordinator.CompleteAsync();
    }

    [Theory]
    [InlineData(false, 1, 1, false, true)]
    [InlineData(false, null, 1, false, false)]
    [InlineData(false, 2, 1, false, false)]
    [InlineData(false, 1, 2, false, false)]
    [InlineData(true, 1, 1, false, false)]
    [InlineData(false, 1, 1, true, false)]
    public void InboxSynchronization_OnlyInsertsVisibleUniqueChild(
        bool disposed,
        int? currentParentId,
        int itemParentId,
        bool duplicate,
        bool expected)
    {
        var item = new TodoItem
        {
            Id = 8,
            ParentId = itemParentId,
            Title = "新任务"
        };
        TodoItem[] current =
            duplicate
                ? new[]
                {
                    new TodoItem
                    {
                        Id = 8,
                        ParentId = 1,
                        Title = "已有任务"
                    }
                }
                : Array.Empty<TodoItem>();

        Assert.Equal(
            expected,
            TaskInboxSynchronizationPolicy
                .ShouldInsert(
                    disposed,
                    currentParentId,
                    current,
                    item));
    }

    private static TaskService Service(
        Action<TodoItem> add) =>
        new(
            new TaskPersistenceHandlers(
                () => new List<TodoItem>(),
                _ => new List<TodoItem>(),
                _ => null,
                add,
                _ => { },
                _ => { },
                fallback => fallback,
                _ => { }));
}
