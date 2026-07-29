using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskServiceBackgroundTests
{
    [Fact]
    public async Task RootLoad_ReturnsWhilePersistenceIsBlocked()
    {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        var service = new TaskService(
            CreateHandlers(
                loadRoots: () =>
                {
                    started.Set();
                    release.Wait(
                        TimeSpan.FromSeconds(2));
                    return new List<TodoItem>
                    {
                        new()
                        {
                            Id = 1,
                            Title = "Inbox"
                        }
                    };
                }));

        Stopwatch duration =
            Stopwatch.StartNew();
        Task<List<TodoItem>> pending =
            service.GetRootItemsAsync();
        duration.Stop();

        Assert.True(
            started.Wait(
                TimeSpan.FromSeconds(2)));
        Assert.True(
            duration.Elapsed
                < TimeSpan.FromMilliseconds(500),
            $"Task request blocked for {duration.Elapsed}.");
        release.Set();

        List<TodoItem> result =
            await pending.WaitAsync(
                TimeSpan.FromSeconds(2));
        Assert.Single(result);
    }

    [Fact]
    public async Task PersistenceOperations_AreStrictlySerialized()
    {
        using var loadStarted =
            new ManualResetEventSlim();
        using var releaseLoad =
            new ManualResetEventSlim();
        using var updateStarted =
            new ManualResetEventSlim();
        var service = new TaskService(
            CreateHandlers(
                loadRoots: () =>
                {
                    loadStarted.Set();
                    releaseLoad.Wait(
                        TimeSpan.FromSeconds(2));
                    return new List<TodoItem>();
                },
                update: _ =>
                    updateStarted.Set()));

        Task load = service.GetRootItemsAsync();
        Assert.True(
            loadStarted.Wait(
                TimeSpan.FromSeconds(2)));
        Task update = service.UpdateItemAsync(
            new TodoItem
            {
                Id = 4,
                Title = "queued"
            });

        Assert.False(
            updateStarted.Wait(
                TimeSpan.FromMilliseconds(120)));
        releaseLoad.Set();
        await Task.WhenAll(load, update)
            .WaitAsync(
                TimeSpan.FromSeconds(2));
        Assert.True(updateStarted.IsSet);
    }

    [Fact]
    public async Task GlobalCustomFields_UseTheSameBackgroundGate()
    {
        string? saved = null;
        var service = new TaskService(
            CreateHandlers(
                loadGlobal: fallback =>
                    fallback + "-db",
                saveGlobal: json =>
                    saved = json));

        string loaded =
            await service
                .LoadGlobalCustomFieldsAsync(
                    "legacy");
        await service
            .SaveGlobalCustomFieldsAsync(
                "[{\"Name\":\"Priority\"}]");

        Assert.Equal(
            "legacy-db",
            loaded);
        Assert.Equal(
            "[{\"Name\":\"Priority\"}]",
            saved);
    }

    [Fact]
    public async Task ProtectedInbox_DoesNotReachDeleteHandler()
    {
        int deleteCount = 0;
        var service = new TaskService(
            CreateHandlers(
                delete: _ =>
                    deleteCount++));

        await service.DeleteItemAsync(
            new TodoItem
            {
                Id = 1,
                ParentId = null,
                Title = "Inbox"
            });

        Assert.Equal(0, deleteCount);
    }

    [Fact]
    public async Task HandlerFailure_PropagatesWithoutPoisoningGate()
    {
        int calls = 0;
        var service = new TaskService(
            CreateHandlers(
                loadRoots: () =>
                {
                    if (Interlocked.Increment(
                            ref calls) == 1)
                    {
                        throw new InvalidOperationException(
                            "database busy");
                    }

                    return new List<TodoItem>();
                }));

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            service.GetRootItemsAsync);
        List<TodoItem> recovered =
            await service.GetRootItemsAsync();

        Assert.Empty(recovered);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void TaskSettingsWrites_DoNotRunDirectlyOnUiCommandPath()
    {
        string root = FindRepositoryRoot();
        string viewModel = System.IO.File.ReadAllText(
            System.IO.Path.Combine(
                root,
                "ViewModels",
                "TasksViewModel.cs"));

        Assert.Contains(
            "private async Task SelectImageSavePath()",
            viewModel);
        Assert.True(
            CountOccurrences(
                viewModel,
                "await Task.Run(") >= 2);
        Assert.DoesNotContain(
            "private void SelectImageSavePath()",
            viewModel);
    }

    private static TaskPersistenceHandlers
        CreateHandlers(
            Func<List<TodoItem>>? loadRoots = null,
            Action<TodoItem>? update = null,
            Action<TodoItem>? delete = null,
            Func<string, string>? loadGlobal = null,
            Action<string>? saveGlobal = null) =>
        new(
            loadRoots
            ?? (() => new List<TodoItem>()),
            _ => new List<TodoItem>(),
            _ => null,
            _ => { },
            update ?? (_ => { }),
            delete ?? (_ => { }),
            loadGlobal ?? (fallback => fallback),
            saveGlobal ?? (_ => { }));

    private static string FindRepositoryRoot() =>
        System.IO.Path.GetFullPath(
            System.IO.Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                ".."));

    private static int CountOccurrences(
        string value,
        string token) =>
        value.Split(
                token,
                StringSplitOptions.None)
            .Length
        - 1;
}
