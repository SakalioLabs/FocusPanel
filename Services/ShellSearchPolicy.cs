using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal static class ShellSearchPolicy
{
    internal const int DefaultLimit = 24;

    internal static IReadOnlyList<
        ShellSearchResult> Compose(
            IEnumerable<AppLaunchItem>?
                applications,
            IEnumerable<WindowTaskItem>?
                runningApplications,
            string? query,
            int limit = DefaultLimit)
    {
        if (limit <= 0)
        {
            return Array.Empty<
                ShellSearchResult>();
        }

        AppLaunchItem[] apps =
            applications?
                .Where(
                    app =>
                        app != null)
                .ToArray()
            ?? Array.Empty<
                AppLaunchItem>();
        if (string.IsNullOrWhiteSpace(
                query))
        {
            return apps
                .Take(limit)
                .Select(
                    ShellSearchResult
                        .FromApplication)
                .ToList();
        }

        var ranked =
            new List<RankedResult>();
        int originalIndex = 0;
        if (SafeExpressionEvaluator
            .TryEvaluate(
                query,
                out string calculation))
        {
            ranked.Add(
                new RankedResult(
                    ShellSearchResult
                        .FromCalculation(
                            query,
                            calculation),
                    Rank: -1,
                    Category: -1,
                    IsActive: false,
                    originalIndex++));
        }

        foreach (AppLaunchItem app
                 in apps)
        {
            int? rank =
                AppSearchPolicy.GetRank(
                    app,
                    query);
            if (rank.HasValue)
            {
                ranked.Add(
                    new RankedResult(
                        ShellSearchResult
                            .FromApplication(
                                app),
                        rank.Value,
                        Category: 0,
                        IsActive: false,
                        originalIndex++));
            }
        }

        foreach (WindowTaskItem running
                 in runningApplications
                     ?? Array.Empty<
                         WindowTaskItem>())
        {
            foreach (WindowReference window
                     in running.Windows)
            {
                int? rank =
                    AppSearchPolicy
                        .GetTextRank(
                            window.Title,
                            running.DisplayName,
                            query);
                if (!rank.HasValue)
                    continue;

                ranked.Add(
                    new RankedResult(
                        ShellSearchResult
                            .FromWindow(
                                running,
                                window),
                        rank.Value,
                        Category: 1,
                        window.IsActive,
                        originalIndex++));
            }
        }

        foreach (SystemManagementSearchEntry
                 command
                 in SystemManagementSearchCatalog
                     .All)
        {
            int? rank =
                AppSearchPolicy
                    .GetTextRank(
                        command.DisplayName,
                        command.Aliases,
                        query);
            if (!rank.HasValue)
                continue;

            ranked.Add(
                new RankedResult(
                    ShellSearchResult
                        .FromSystemCommand(
                            command),
                    rank.Value,
                    Category: 2,
                    IsActive: false,
                    originalIndex++));
        }

        foreach (WindowsShellSearchEntry
                 command
                 in WindowsShellSearchCatalog
                     .All)
        {
            int? rank =
                AppSearchPolicy
                    .GetTextRank(
                        command.DisplayName,
                        command.Aliases,
                        query);
            if (!rank.HasValue)
                continue;

            ranked.Add(
                new RankedResult(
                    ShellSearchResult
                        .FromShellCommand(
                            command),
                    rank.Value,
                    Category: 2,
                    IsActive: false,
                    originalIndex++));
        }

        return ranked
            .OrderBy(
                item =>
                    item.Rank)
            .ThenBy(
                item =>
                    item.Category)
            .ThenByDescending(
                item =>
                    item.IsActive)
            .ThenBy(
                item =>
                    item.Result
                        .DisplayName,
                StringComparer
                    .CurrentCultureIgnoreCase)
            .ThenBy(
                item =>
                    item.Result
                        .StableKey,
                StringComparer
                    .OrdinalIgnoreCase)
            .ThenBy(
                item =>
                    item.OriginalIndex)
            .Take(limit)
            .Select(
                item =>
                    item.Result)
            .ToList();
    }

    private sealed record RankedResult(
        ShellSearchResult Result,
        int Rank,
        int Category,
        bool IsActive,
        int OriginalIndex);
}
