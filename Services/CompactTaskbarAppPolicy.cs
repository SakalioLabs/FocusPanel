using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal static class CompactTaskbarAppPolicy
{
    internal static bool ShouldShow(
        TaskbarAppItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.IsPinned
               || item.IsRunning;
    }

    internal static IReadOnlyList<TaskbarAppItem>
        Select(
            IEnumerable<TaskbarAppItem> items) =>
        items
            .Where(ShouldShow)
            .ToList();
}
