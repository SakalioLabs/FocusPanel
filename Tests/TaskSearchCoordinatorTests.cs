using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskSearchCoordinatorTests
{
    [Fact]
    public async Task Refresh_ReturnsOpenTaskSnapshot()
    {
        var expected =
            new TaskSearchItem(
                7,
                "整理周报",
                1,
                "Inbox",
                "To Do",
                DateTime.Now);
        var coordinator =
            new TaskSearchCoordinator(
                Service(
                    search: () =>
                        new List<TaskSearchItem>
                        {
                            expected
                        }));

        TaskSearchIndexResult result =
            await coordinator.RefreshAsync();

        Assert.True(result.Succeeded);
        Assert.Same(
            expected,
            Assert.Single(result.Items));
        Assert.True(
            coordinator.IsCurrent(
                result.Revision));
        await coordinator.CompleteAsync();
    }

    [Fact]
    public async Task LaterRefreshInvalidatesOlderRevision()
    {
        var coordinator =
            new TaskSearchCoordinator(
                Service());

        TaskSearchIndexResult first =
            await coordinator.RefreshAsync();
        TaskSearchIndexResult second =
            await coordinator.RefreshAsync();

        Assert.False(
            coordinator.IsCurrent(
                first.Revision));
        Assert.True(
            coordinator.IsCurrent(
                second.Revision));
        await coordinator.CompleteAsync();
    }

    [Fact]
    public async Task RefreshFailureKeepsTypedError()
    {
        var coordinator =
            new TaskSearchCoordinator(
                Service(
                    search: () =>
                        throw new InvalidOperationException(
                            "database busy")));

        TaskSearchIndexResult result =
            await coordinator.RefreshAsync();

        Assert.False(result.Succeeded);
        Assert.Empty(result.Items);
        Assert.Contains(
            "database busy",
            result.Error);
        await coordinator.CompleteAsync();
    }

    [Fact]
    public async Task CompleteTaskPersistsCurrentDatabaseItem()
    {
        TodoItem persisted =
            new()
            {
                Id = 9,
                ParentId = 1,
                Title = "完成我",
                IsCompleted = false,
                Status = "In Progress"
            };
        TodoItem? saved = null;
        var coordinator =
            new TaskSearchCoordinator(
                Service(
                    load: id =>
                        id == 9
                            ? persisted
                            : null,
                    update: item =>
                        saved = item));

        TaskSearchCompletionResult result =
            await coordinator
                .CompleteTaskAsync(9);

        Assert.True(result.Succeeded);
        Assert.Equal("完成我", result.Title);
        Assert.Same(persisted, saved);
        Assert.True(saved?.IsCompleted);
        Assert.Equal(
            "In Progress",
            saved?.Status);
        await coordinator.CompleteAsync();
    }

    [Fact]
    public async Task CompleteTaskDoesNotRewriteAlreadyCompletedItem()
    {
        int writes = 0;
        var coordinator =
            new TaskSearchCoordinator(
                Service(
                    load: _ =>
                        new TodoItem
                        {
                            Id = 2,
                            ParentId = 1,
                            Title = "已经完成",
                            IsCompleted = true
                        },
                    update: _ =>
                        writes++));

        TaskSearchCompletionResult result =
            await coordinator
                .CompleteTaskAsync(2);

        Assert.True(result.Succeeded);
        Assert.Equal(0, writes);
        await coordinator.CompleteAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8)]
    public async Task CompleteTaskRejectsInvalidMissingOrRoot(
        int taskId)
    {
        var coordinator =
            new TaskSearchCoordinator(
                Service(
                    load: id =>
                        id == 8
                            ? new TodoItem
                            {
                                Id = 8,
                                ParentId = null,
                                Title = "项目"
                            }
                            : null));

        TaskSearchCompletionResult result =
            await coordinator
                .CompleteTaskAsync(
                    taskId);

        Assert.False(result.Succeeded);
        await coordinator.CompleteAsync();
    }

    [Fact]
    public async Task CompleteWaitsBehindSharedTaskServiceRead()
    {
        using var readStarted =
            new ManualResetEventSlim();
        using var releaseRead =
            new ManualResetEventSlim();
        using var updateStarted =
            new ManualResetEventSlim();
        var service =
            Service(
                roots: () =>
                {
                    readStarted.Set();
                    releaseRead.Wait(
                        TimeSpan.FromSeconds(2));
                    return new List<TodoItem>();
                },
                load: _ =>
                    new TodoItem
                    {
                        Id = 4,
                        ParentId = 1,
                        Title = "排队完成"
                    },
                update: _ =>
                    updateStarted.Set());
        var coordinator =
            new TaskSearchCoordinator(
                service);

        Task read =
            service.GetRootItemsAsync();
        Assert.True(
            readStarted.Wait(
                TimeSpan.FromSeconds(2)));
        Task<TaskSearchCompletionResult> complete =
            coordinator.CompleteTaskAsync(4);
        Assert.False(
            updateStarted.Wait(
                TimeSpan.FromMilliseconds(120)));

        releaseRead.Set();
        await Task.WhenAll(
                read,
                complete)
            .WaitAsync(
                TimeSpan.FromSeconds(2));
        Assert.True(updateStarted.IsSet);
        await coordinator.CompleteAsync();
    }

    [Fact]
    public async Task CompleteDrainsAcceptedWorkAndRejectsLateRequests()
    {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        var coordinator =
            new TaskSearchCoordinator(
                Service(
                    search: () =>
                    {
                        started.Set();
                        release.Wait(
                            TimeSpan.FromSeconds(2));
                        return new List<TaskSearchItem>();
                    }));
        Task<TaskSearchIndexResult> refresh =
            coordinator.RefreshAsync();
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
            (await refresh).Succeeded);

        TaskSearchIndexResult rejected =
            await coordinator.RefreshAsync();
        Assert.False(rejected.Succeeded);
        Assert.Contains(
            "关闭",
            rejected.Error);
    }

    private static TaskService Service(
        Func<List<TodoItem>>? roots = null,
        Func<int, TodoItem?>? load = null,
        Action<TodoItem>? update = null,
        Func<List<TaskSearchItem>>? search = null) =>
        new(
            new TaskPersistenceHandlers(
                roots
                ?? (() => new List<TodoItem>()),
                _ => new List<TodoItem>(),
                load
                ?? (_ => null),
                _ => { },
                update
                ?? (_ => { }),
                _ => { },
                fallback => fallback,
                _ => { },
                search
                ?? (() =>
                    new List<TaskSearchItem>())));
}
