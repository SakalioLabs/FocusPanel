using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal sealed record TaskSearchItem(
    int Id,
    string Title,
    int ParentId,
    string ParentTitle,
    string Status,
    DateTime CreatedAt)
{
    internal string StableKey =>
        $"task:item:{Id}";
}

internal sealed record TaskSearchIndexResult(
    long Revision,
    bool Succeeded,
    IReadOnlyList<TaskSearchItem> Items,
    string Error);

internal sealed record TaskSearchCompletionResult(
    bool Succeeded,
    int TaskId,
    string Title,
    string Error);

internal sealed class TaskSearchCoordinator
{
    private readonly TaskService _taskService;
    private readonly InFlightTaskTracker _inFlight =
        new();
    private long _revision;

    internal TaskSearchCoordinator(
        TaskService taskService)
    {
        _taskService =
            taskService
            ?? throw new ArgumentNullException(
                nameof(taskService));
    }

    internal Task<TaskSearchIndexResult>
        RefreshAsync()
    {
        long revision =
            Interlocked.Increment(
                ref _revision);
        Task<TaskSearchIndexResult>? operation =
            _inFlight.TryStart(
                () => RefreshCoreAsync(
                    revision));
        return operation
            ?? Task.FromResult(
                new TaskSearchIndexResult(
                    revision,
                    false,
                    Array.Empty<TaskSearchItem>(),
                    "任务搜索已经关闭。"));
    }

    internal Task<TaskSearchCompletionResult>
        CompleteTaskAsync(
            int taskId)
    {
        Task<TaskSearchCompletionResult>? operation =
            _inFlight.TryStart(
                () => CompleteTaskCoreAsync(
                    taskId));
        return operation
            ?? Task.FromResult(
                new TaskSearchCompletionResult(
                    false,
                    taskId,
                    string.Empty,
                    "任务搜索已经关闭。"));
    }

    internal bool IsCurrent(long revision) =>
        revision
        == Volatile.Read(
            ref _revision);

    internal Task CompleteAsync() =>
        _inFlight.CompleteAsync();

    private async Task<TaskSearchIndexResult>
        RefreshCoreAsync(
            long revision)
    {
        try
        {
            IReadOnlyList<TaskSearchItem> items =
                await _taskService
                    .GetOpenTaskSearchItemsAsync()
                    .ConfigureAwait(false);
            return new TaskSearchIndexResult(
                revision,
                true,
                items,
                string.Empty);
        }
        catch (Exception ex)
        {
            return new TaskSearchIndexResult(
                revision,
                false,
                Array.Empty<TaskSearchItem>(),
                ex.Message);
        }
    }

    private async Task<TaskSearchCompletionResult>
        CompleteTaskCoreAsync(
            int taskId)
    {
        if (taskId <= 0)
        {
            return new TaskSearchCompletionResult(
                false,
                taskId,
                string.Empty,
                "任务标识无效。");
        }

        try
        {
            TodoItem? item =
                await _taskService
                    .GetItemByIdAsync(taskId)
                    .ConfigureAwait(false);
            if (item == null
                || item.ParentId == null)
            {
                return new TaskSearchCompletionResult(
                    false,
                    taskId,
                    string.Empty,
                    "任务可能已被删除或不再是可完成的待办。");
            }

            if (!item.IsCompleted)
            {
                item.IsCompleted = true;
                await _taskService
                    .UpdateItemAsync(item)
                    .ConfigureAwait(false);
            }

            return new TaskSearchCompletionResult(
                true,
                taskId,
                item.Title,
                string.Empty);
        }
        catch (Exception ex)
        {
            return new TaskSearchCompletionResult(
                false,
                taskId,
                string.Empty,
                ex.Message);
        }
    }
}
