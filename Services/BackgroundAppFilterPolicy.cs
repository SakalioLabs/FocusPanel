using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FocusPanel.Models;

namespace FocusPanel.Services;

public enum BackgroundAppFilterScope
{
    All,
    Windows,
    Background
}

internal static class BackgroundAppFilterPolicy
{
    internal static IReadOnlyList<TaskbarAppItem> Apply(
        IEnumerable<TaskbarAppItem> apps,
        string? query,
        BackgroundAppFilterScope scope)
    {
        ArgumentNullException.ThrowIfNull(apps);

        return apps
            .Where(item => MatchesScope(item, scope))
            .Where(item => MatchesQuery(item, query))
            .ToList();
    }

    private static bool MatchesScope(
        TaskbarAppItem item,
        BackgroundAppFilterScope scope) =>
        scope switch
        {
            BackgroundAppFilterScope.Windows =>
                item.WindowCount > 0,
            BackgroundAppFilterScope.Background =>
                item.IsBackgroundOnly,
            _ => true
        };

    private static bool MatchesQuery(
        TaskbarAppItem item,
        string? query)
    {
        string? executableName = null;
        try
        {
            executableName = Path.GetFileNameWithoutExtension(
                item.ExecutablePath);
        }
        catch (ArgumentException)
        {
            // Invalid process paths remain searchable by their display name.
        }

        return AppSearchPolicy.GetTextRank(
                item.DisplayName,
                executableName,
                query)
            .HasValue;
    }
}
