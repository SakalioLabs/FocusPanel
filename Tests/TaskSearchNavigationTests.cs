using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FocusPanel.Models;
using FocusPanel.Services;
using FocusPanel.ViewModels;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskSearchNavigationTests
{
    [Fact]
    public async Task Navigate_LoadsParentAndSelectsCurrentTask()
    {
        TodoItem project =
            Project();
        TodoItem visibleTask =
            TaskItem();
        TaskService service =
            Service(
                project,
                visibleTask);
        TasksViewModel viewModel =
            ViewModel(service);

        bool opened =
            await viewModel
                .NavigateToSearchTaskAsync(
                    visibleTask.Id);

        Assert.True(opened);
        Assert.Equal(
            project.Id,
            viewModel.CurrentParentItem?.Id);
        Assert.Equal(
            visibleTask.Id,
            viewModel.SelectedTask?.Id);
        Assert.Same(
            visibleTask,
            viewModel.SelectedTask);
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task Navigate_MissingTaskDoesNotChangeScope()
    {
        TasksViewModel viewModel =
            ViewModel(
                Service(
                    Project(),
                    TaskItem(),
                    loadMissing: true));

        bool opened =
            await viewModel
                .NavigateToSearchTaskAsync(99);

        Assert.False(opened);
        Assert.Null(
            viewModel.CurrentParentItem);
        Assert.Null(
            viewModel.SelectedTask);
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task ExternalCompletionUpdatesVisibleCopyWithoutSecondWrite()
    {
        int writes = 0;
        TodoItem project =
            Project();
        TodoItem visibleTask =
            TaskItem();
        TaskService service =
            Service(
                project,
                visibleTask,
                update: _ =>
                    writes++);
        TasksViewModel viewModel =
            ViewModel(service);
        Assert.True(
            await viewModel
                .NavigateToSearchTaskAsync(
                    visibleTask.Id));

        viewModel
            .ApplyExternallyCompletedTask(
                visibleTask.Id);

        Assert.True(
            visibleTask.IsCompleted);
        await Task.Delay(260);
        Assert.Equal(0, writes);
        await viewModel.DisposeAsync();
    }

    private static TasksViewModel ViewModel(
        TaskService service)
    {
        string root =
            Path.Combine(
                Path.GetTempPath(),
                "FocusPanel-task-search-tests",
                Guid.NewGuid()
                    .ToString("N"));
        return new TasksViewModel(
            new CancelFolderPicker(),
            new CancelFilePicker(),
            taskService: service,
            settingsService:
                new SettingsService(
                    Path.Combine(
                        root,
                        "settings.json"),
                    Path.Combine(
                        root,
                        "legacy.json")));
    }

    private static TaskService Service(
        TodoItem project,
        TodoItem visibleTask,
        bool loadMissing = false,
        Action<TodoItem>? update = null) =>
        new(
            new TaskPersistenceHandlers(
                () =>
                    new List<TodoItem>
                    {
                        project
                    },
                parentId =>
                    parentId == project.Id
                        ? new List<TodoItem>
                        {
                            visibleTask
                        }
                        : new List<TodoItem>(),
                id =>
                {
                    if (loadMissing)
                        return null;
                    if (id == project.Id)
                        return project;
                    if (id == visibleTask.Id)
                    {
                        return new TodoItem
                        {
                            Id =
                                visibleTask.Id,
                            ParentId =
                                project.Id,
                            Title =
                                visibleTask.Title,
                            Status =
                                visibleTask.Status
                        };
                    }
                    return null;
                },
                _ => { },
                update
                ?? (_ => { }),
                _ => { },
                fallback => fallback,
                _ => { },
                () =>
                    new List<TaskSearchItem>()));

    private static TodoItem Project() =>
        new()
        {
            Id = 1,
            Title = "Inbox",
            ParentId = null
        };

    private static TodoItem TaskItem() =>
        new()
        {
            Id = 9,
            Title = "搜索直达任务",
            ParentId = 1,
            Status = "To Do"
        };

    private sealed class CancelFolderPicker
        : IFolderPickerService
    {
        public FolderPickerResult PickFolder(
            FolderPickerRequest request) =>
            FolderPickerResult.Canceled();
    }

    private sealed class CancelFilePicker
        : IFilePickerService
    {
        public FilePickerResult PickFile(
            FilePickerRequest request) =>
            FilePickerResult.Canceled();
    }
}
