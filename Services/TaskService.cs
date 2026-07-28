using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FocusPanel.Data;
using FocusPanel.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.Services;

public class TaskService
{
    private readonly AppDbContext _context;

    public TaskService(AppDbContext context)
    {
        _context = context;
    }

    // --- Unified CRUD ---

    public async Task<List<TodoItem>> GetRootItemsAsync()
    {
        return await _context.Todos
            .Where(t => t.ParentId == null)
            .OrderBy(t => t.Id) // Keep Inbox (Id=1) first usually
            .ToListAsync();
    }

    public async Task<List<TodoItem>> GetChildItemsAsync(int parentId)
    {
        return await _context.Todos
            .Where(t => t.ParentId == parentId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<TodoItem?> GetItemByIdAsync(int id)
    {
        return await _context.Todos
            .Include(t => t.Children)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task AddItemAsync(TodoItem item)
    {
        _context.Todos.Add(item);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateItemAsync(TodoItem item)
    {
        _context.Todos.Update(item);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteItemAsync(TodoItem item)
    {
        // Protect Inbox (Root item with Id 1)
        if (item.ParentId == null && item.Id == 1) return;
        
        _context.Todos.Remove(item);
        await _context.SaveChangesAsync();
    }
}
