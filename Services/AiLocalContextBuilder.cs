using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Data;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.Services;

public interface IAiLocalContextBuilder
{
    Task<string> BuildAsync(
        CancellationToken cancellationToken);
}

internal sealed record AiTaskSummary(
    string Title,
    string Status);

internal sealed record AiFocusSummary(
    int SessionCount,
    int TotalMinutes);

internal static class AiContextFormatter
{
    internal static string Format(
        IEnumerable<AiTaskSummary> tasks,
        AiFocusSummary focus)
    {
        var lines = new List<string>
        {
            "以下是用户主动授权提供的 FocusPanel 本地摘要："
        };

        AiTaskSummary[] taskItems =
            tasks.Take(20).ToArray();
        lines.Add($"未完成任务（{taskItems.Length} 项）：");
        lines.AddRange(
            taskItems.Select(
                item =>
                    $"- {SafeText(item.Title)}｜{SafeText(item.Status)}"));

        lines.Add(
            $"近 7 天专注：{focus.SessionCount} 次，共 {focus.TotalMinutes} 分钟。");
        lines.Add(
            "摘要不包含文件内容、文件路径、API Key 或其他凭据。");
        return string.Join(Environment.NewLine, lines);
    }

    private static string SafeText(string value)
    {
        string normalized = value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return normalized.Length <= 160
            ? normalized
            : normalized[..160] + "…";
    }
}

public sealed class AiLocalContextBuilder :
    IAiLocalContextBuilder
{
    public async Task<string> BuildAsync(
        CancellationToken cancellationToken)
    {
        await using var context = new AppDbContext();
        context.EnsureSchema();

        AiTaskSummary[] tasks =
            await context.Todos
                .AsNoTracking()
                .Where(
                    item =>
                        item.ParentId != null
                        && !item.IsCompleted)
                .OrderByDescending(item => item.CreatedAt)
                .Take(20)
                .Select(
                    item => new AiTaskSummary(
                        item.Title,
                        item.Status))
                .ToArrayAsync(cancellationToken);

        DateTime since = DateTime.Now.AddDays(-7);
        var focusRows = await context.PomodoroSessions
            .AsNoTracking()
            .Where(
                item =>
                    item.StartTime >= since
                    && item.Status == "Completed")
            .Select(item => item.DurationMinutes)
            .ToArrayAsync(cancellationToken);

        return AiContextFormatter.Format(
            tasks,
            new AiFocusSummary(
                focusRows.Length,
                focusRows.Sum()));
    }
}
