using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal readonly record struct
    TaskQuickCaptureResult(
        bool Succeeded,
        TodoItem? Item,
        string? Error)
{
    internal static TaskQuickCaptureResult
        Unavailable { get; } =
        new(
            false,
            null,
            "任务收集服务正在关闭");
}

internal sealed class
    TaskQuickCaptureCoordinator
{
    internal const int InboxId = 1;

    private readonly TaskService _taskService;
    private readonly InFlightTaskTracker
        _operations = new();

    internal TaskQuickCaptureCoordinator(
        TaskService taskService)
    {
        _taskService =
            taskService
            ?? throw new ArgumentNullException(
                nameof(taskService));
    }

    internal Task<TaskQuickCaptureResult>
        CaptureAsync(string title)
    {
        Task<TaskQuickCaptureResult>? operation =
            _operations.TryStart(
                () => CaptureCoreAsync(
                    title));
        return operation
            ?? Task.FromResult(
                TaskQuickCaptureResult
                    .Unavailable);
    }

    internal Task CompleteAsync() =>
        _operations.CompleteAsync();

    private async Task<TaskQuickCaptureResult>
        CaptureCoreAsync(string title)
    {
        string normalized =
            title?.Trim()
            ?? string.Empty;
        if (normalized.Length == 0
            || normalized.Length
                > TaskCaptureCommandParser
                    .MaximumTitleLength)
        {
            return new TaskQuickCaptureResult(
                false,
                null,
                "任务标题为空或过长");
        }

        var item = new TodoItem
        {
            Title = normalized,
            ParentId = InboxId,
            IsCompleted = false,
            Status = "To Do",
            CreatedAt = DateTime.Now,
            ViewMode = ProjectViewMode.List
        };
        try
        {
            await _taskService
                .AddItemAsync(item);
            return new TaskQuickCaptureResult(
                true,
                item,
                null);
        }
        catch (Exception ex)
        {
            return new TaskQuickCaptureResult(
                false,
                null,
                ex.Message);
        }
    }
}

internal static class
    TaskInboxSynchronizationPolicy
{
    internal static bool ShouldInsert(
        bool isDisposed,
        int? currentParentId,
        IEnumerable<TodoItem>? currentItems,
        TodoItem item) =>
        !isDisposed
        && item != null
        && item.ParentId
            == TaskQuickCaptureCoordinator
                .InboxId
        && currentParentId
            == TaskQuickCaptureCoordinator
                .InboxId
        && !(currentItems
            ?? Array.Empty<TodoItem>())
            .Any(
                existing =>
                    existing.Id == item.Id);
}
