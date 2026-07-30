using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusPanel.Services;

internal static class TaskSearchPolicy
{
    internal const int DefaultLimit = 6;

    internal static IReadOnlyList<TaskSearchItem>
        Search(
            IEnumerable<TaskSearchItem>? items,
            string? query,
            int limit = DefaultLimit)
    {
        if (limit <= 0
            || string.IsNullOrWhiteSpace(
                query))
        {
            return Array.Empty<TaskSearchItem>();
        }

        return (items
                ?? Array.Empty<TaskSearchItem>())
            .Select(
                (item, index) =>
                    new RankedTask(
                        item,
                        AppSearchPolicy
                            .GetTextRank(
                                item.Title,
                                item.ParentTitle,
                                query),
                        index))
            .Where(item =>
                item.Rank.HasValue)
            .OrderBy(item =>
                item.Rank!.Value)
            .ThenByDescending(item =>
                item.Task.CreatedAt)
            .ThenBy(item =>
                item.Task.Id)
            .ThenBy(item =>
                item.OriginalIndex)
            .Take(limit)
            .Select(item =>
                item.Task)
            .ToList();
    }

    private sealed record RankedTask(
        TaskSearchItem Task,
        int? Rank,
        int OriginalIndex);
}
