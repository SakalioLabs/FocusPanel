using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Data;
using FocusPanel.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.Services;

public class TaskService
{
    private readonly AppDbContext _context;
    private readonly SemaphoreSlim _operationGate =
        new(1, 1);

    public TaskService(AppDbContext context)
    {
        _context = context;
    }

    // --- Unified CRUD ---

    public async Task<List<TodoItem>> GetRootItemsAsync()
    {
        return await ExecuteAsync(
                () =>
                    _context.Todos
                        .Where(t => t.ParentId == null)
                        .OrderBy(t => t.Id)
                        .ToListAsync())
            .ConfigureAwait(false);
    }

    public async Task<List<TodoItem>> GetChildItemsAsync(int parentId)
    {
        return await ExecuteAsync(
                () =>
                    _context.Todos
                        .Where(t => t.ParentId == parentId)
                        .OrderByDescending(t => t.CreatedAt)
                        .ToListAsync())
            .ConfigureAwait(false);
    }

    public async Task<TodoItem?> GetItemByIdAsync(int id)
    {
        return await ExecuteAsync(
                () =>
                    _context.Todos
                        .Include(t => t.Children)
                        .FirstOrDefaultAsync(t => t.Id == id))
            .ConfigureAwait(false);
    }

    public async Task AddItemAsync(TodoItem item)
    {
        await ExecuteAsync(
                async () =>
                {
                    _context.Todos.Add(item);
                    await _context.SaveChangesAsync()
                        .ConfigureAwait(false);
                })
            .ConfigureAwait(false);
    }

    public async Task UpdateItemAsync(TodoItem item)
    {
        await ExecuteAsync(
                async () =>
                {
                    _context.Todos.Update(item);
                    await _context.SaveChangesAsync()
                        .ConfigureAwait(false);
                })
            .ConfigureAwait(false);
    }

    public async Task DeleteItemAsync(TodoItem item)
    {
        // Protect Inbox (Root item with Id 1)
        if (item.ParentId == null && item.Id == 1)
            return;

        await ExecuteAsync(
                async () =>
                {
                    _context.Todos.Remove(item);
                    await _context.SaveChangesAsync()
                        .ConfigureAwait(false);
                })
            .ConfigureAwait(false);
    }

    public async Task WaitForIdleAsync()
    {
        await _operationGate.WaitAsync()
            .ConfigureAwait(false);
        _operationGate.Release();
    }

    private async Task ExecuteAsync(
        Func<Task> operation)
    {
        await _operationGate.WaitAsync()
            .ConfigureAwait(false);
        try
        {
            await operation()
                .ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<TResult> ExecuteAsync<TResult>(
        Func<Task<TResult>> operation)
    {
        await _operationGate.WaitAsync()
            .ConfigureAwait(false);
        try
        {
            return await operation()
                .ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }
}
