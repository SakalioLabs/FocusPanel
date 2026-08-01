using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Data;
using FocusPanel.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.Services;

public interface IDashboardDataService
{
    Task<DashboardSnapshot> LoadAsync(
        CancellationToken cancellationToken);
}

public sealed class DashboardDataService :
    IDashboardDataService
{
    public async Task<DashboardSnapshot> LoadAsync(
        CancellationToken cancellationToken)
    {
        await using var context = new AppDbContext();
        context.EnsureSchema();

        DateTime today = DateTime.Today;
        DateTime tomorrow = today.AddDays(1);

        int openTaskCount = await context.Todos
            .AsNoTracking()
            .CountAsync(
                item =>
                    item.ParentId != null
                    && !item.IsCompleted,
                cancellationToken);

        DashboardTaskSummary[] tasks =
            await context.Todos
                .AsNoTracking()
                .Where(
                    item =>
                        item.ParentId != null
                        && !item.IsCompleted)
                .OrderBy(
                    item =>
                        item.Status == "In Progress"
                        || item.Status == "进行中"
                            ? 0
                            : 1)
                .ThenByDescending(item => item.CreatedAt)
                .Take(6)
                .Select(
                    item => new DashboardTaskSummary(
                        item.Id,
                        item.Title,
                        item.Parent != null
                            ? item.Parent.Title
                            : "收集箱",
                        item.Status))
                .ToArrayAsync(cancellationToken);

        int[] focusDurations =
            await context.PomodoroSessions
                .AsNoTracking()
                .Where(
                    item =>
                        item.Status == "Completed"
                        && item.EndTime >= today
                        && item.EndTime < tomorrow)
                .Select(item => item.DurationMinutes)
                .ToArrayAsync(cancellationToken);

        int collectedItemCount =
            await context.DesktopFilePreferences
                .AsNoTracking()
                .CountAsync(
                    item => item.IsHiddenFromDesktop,
                    cancellationToken);

        return new DashboardSnapshot(
            openTaskCount,
            focusDurations.Length,
            focusDurations.Sum(),
            collectedItemCount,
            tasks,
            DateTime.Now);
    }
}
