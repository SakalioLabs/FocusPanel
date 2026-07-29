using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Data;
using FocusPanel.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.Services;

internal sealed record TaskPersistenceHandlers(
    Func<List<TodoItem>> LoadRootItems,
    Func<int, List<TodoItem>> LoadChildItems,
    Func<int, TodoItem?> LoadItemById,
    Action<TodoItem> AddItem,
    Action<TodoItem> UpdateItem,
    Action<TodoItem> DeleteItem,
    Func<string, string> LoadGlobalCustomFields,
    Action<string> SaveGlobalCustomFields);

public sealed class TaskService
{
    private const string GlobalCustomFieldsKey =
        "GlobalCustomFieldsJson";
    private readonly SemaphoreSlim _operationGate =
        new(1, 1);
    private readonly TaskPersistenceHandlers _handlers;

    public TaskService()
        : this(
            new TaskPersistenceHandlers(
                LoadRootItemsCore,
                LoadChildItemsCore,
                LoadItemByIdCore,
                AddItemCore,
                UpdateItemCore,
                DeleteItemCore,
                LoadGlobalCustomFieldsCore,
                SaveGlobalCustomFieldsCore))
    {
    }

    internal TaskService(
        TaskPersistenceHandlers handlers)
    {
        _handlers = handlers
            ?? throw new ArgumentNullException(
                nameof(handlers));
    }

    public Task<List<TodoItem>> GetRootItemsAsync() =>
        ExecuteAsync(
            _handlers.LoadRootItems);

    public Task<List<TodoItem>> GetChildItemsAsync(
        int parentId) =>
        ExecuteAsync(
            () => _handlers.LoadChildItems(
                parentId));

    public Task<TodoItem?> GetItemByIdAsync(
        int id) =>
        ExecuteAsync(
            () => _handlers.LoadItemById(id));

    public Task AddItemAsync(
        TodoItem item) =>
        ExecuteAsync(
            () => _handlers.AddItem(item));

    public Task UpdateItemAsync(
        TodoItem item) =>
        ExecuteAsync(
            () => _handlers.UpdateItem(item));

    public Task DeleteItemAsync(
        TodoItem item)
    {
        if (item.ParentId == null
            && item.Id == 1)
        {
            return Task.CompletedTask;
        }

        return ExecuteAsync(
            () => _handlers.DeleteItem(item));
    }

    public Task<string> LoadGlobalCustomFieldsAsync(
        string fallbackJson) =>
        ExecuteAsync(
            () => _handlers.LoadGlobalCustomFields(
                fallbackJson));

    public Task SaveGlobalCustomFieldsAsync(
        string json) =>
        ExecuteAsync(
            () => _handlers.SaveGlobalCustomFields(
                json));

    public async Task WaitForIdleAsync()
    {
        await _operationGate.WaitAsync()
            .ConfigureAwait(false);
        _operationGate.Release();
    }

    private async Task ExecuteAsync(
        Action operation)
    {
        await _operationGate.WaitAsync()
            .ConfigureAwait(false);
        try
        {
            await Task.Run(operation)
                .ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<TResult> ExecuteAsync<TResult>(
        Func<TResult> operation)
    {
        await _operationGate.WaitAsync()
            .ConfigureAwait(false);
        try
        {
            return await Task.Run(operation)
                .ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private static List<TodoItem>
        LoadRootItemsCore()
    {
        using var context = new AppDbContext();
        return context.Todos
            .AsNoTracking()
            .Where(item =>
                item.ParentId == null)
            .OrderBy(item =>
                item.Id)
            .ToList();
    }

    private static List<TodoItem>
        LoadChildItemsCore(
            int parentId)
    {
        using var context = new AppDbContext();
        return context.Todos
            .AsNoTracking()
            .Where(item =>
                item.ParentId == parentId)
            .OrderByDescending(item =>
                item.CreatedAt)
            .ToList();
    }

    private static TodoItem?
        LoadItemByIdCore(
            int id)
    {
        using var context = new AppDbContext();
        return context.Todos
            .AsNoTrackingWithIdentityResolution()
            .Include(item =>
                item.Children)
            .FirstOrDefault(item =>
                item.Id == id);
    }

    private static void AddItemCore(
        TodoItem item)
    {
        using var context = new AppDbContext();
        context.Todos.Add(item);
        context.SaveChanges();
    }

    private static void UpdateItemCore(
        TodoItem item)
    {
        using var context = new AppDbContext();
        TodoItem persisted =
            TaskPersistenceMapper
                .CloneState(item);
        context.Attach(persisted);
        context.Entry(persisted).State =
            EntityState.Modified;
        context.SaveChanges();
    }

    private static void DeleteItemCore(
        TodoItem item)
    {
        using var context = new AppDbContext();
        context.Todos.Remove(
            new TodoItem
            {
                Id = item.Id
            });
        context.SaveChanges();
    }

    private static string
        LoadGlobalCustomFieldsCore(
            string fallbackJson)
    {
        using var context = new AppDbContext();
        AppConfig? config =
            context.AppConfigs
                .AsNoTracking()
                .FirstOrDefault(item =>
                    item.Key
                    == GlobalCustomFieldsKey);
        if (config != null)
            return config.Value
                ?? string.Empty;

        string fallback =
            fallbackJson
            ?? string.Empty;
        if (fallback.Length == 0)
            return fallback;

        context.AppConfigs.Add(
            new AppConfig
            {
                Key =
                    GlobalCustomFieldsKey,
                Value = fallback
            });
        context.SaveChanges();
        return fallback;
    }

    private static void
        SaveGlobalCustomFieldsCore(
            string json)
    {
        using var context = new AppDbContext();
        AppConfig? config =
            context.AppConfigs.Find(
                GlobalCustomFieldsKey);
        if (config == null)
        {
            context.AppConfigs.Add(
                new AppConfig
                {
                    Key =
                        GlobalCustomFieldsKey,
                    Value = json
                });
        }
        else
        {
            config.Value = json;
        }

        context.SaveChanges();
    }
}
