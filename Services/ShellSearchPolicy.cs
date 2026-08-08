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
            int limit = DefaultLimit,
            IEnumerable<TaskSearchItem>?
                taskItems = null,
            ShellSearchScope scope =
                ShellSearchScope.All,
            string? windowIdentityFilter = null,
            IReadOnlySet<string>?
                recentApplicationIdentities = null,
            IEnumerable<
                ApplicationAudioSessionSnapshot>?
                applicationAudioSessions = null,
            IReadOnlyList<IntPtr>?
                recentWindowHandles = null)
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
            if (scope
                == ShellSearchScope.Windows)
            {
                return ComposeWindowOverview(
                    runningApplications,
                    limit,
                    windowIdentityFilter,
                    recentWindowHandles);
            }

            if (scope
                == ShellSearchScope.System)
            {
                return ComposeSystemOverview(
                    limit);
            }

            return apps
                .Take(limit)
                .Select(
                    app => ShellSearchResult
                        .FromApplication(
                            app,
                            recentApplicationIdentities?
                                .Contains(
                                    app.IdentityKey)
                            == true))
                .ToList();
        }

        var ranked =
            new List<RankedResult>();
        int originalIndex = 0;
        bool includeApplications =
            scope is ShellSearchScope.All
                or ShellSearchScope.Applications;
        bool includeWindows =
            scope is ShellSearchScope.All
                or ShellSearchScope.Windows;
        bool includeSystem =
            scope is ShellSearchScope.All
                or ShellSearchScope.System;
        bool includeTasks =
            scope == ShellSearchScope.All;

        if (includeSystem
            && PanelRunCommandParser.TryParse(
                query,
                out PanelRunCommand runCommand))
        {
            ranked.Add(
                new RankedResult(
                    ShellSearchResult
                        .FromRunCommand(
                            runCommand),
                    Rank: -5,
                    Category: -1,
                    IsActive: false,
                    originalIndex++));
        }

        if (includeTasks
            && TaskCaptureCommandParser
            .TryParse(
                query,
                out TaskCaptureCommand
                    taskCapture))
        {
            ranked.Add(
                new RankedResult(
                    ShellSearchResult
                        .FromTaskCapture(
                            taskCapture),
                    Rank: -4,
                    Category: -1,
                    IsActive: false,
                    originalIndex++));
        }

        if (includeSystem
            && PomodoroSearchCommandParser
            .TryParse(
                query,
                out PomodoroSearchCommand
                    focusCommand))
        {
            ranked.Add(
                new RankedResult(
                    ShellSearchResult
                        .FromFocusCommand(
                            focusCommand),
                    Rank: -3,
                    Category: -1,
                    IsActive: false,
                    originalIndex++));
        }

        if (includeSystem
            && AudioSearchCommandParser
            .TryParse(
                query,
                out AudioSearchCommand
                    audioCommand))
        {
            ranked.Add(
                new RankedResult(
                    ShellSearchResult
                        .FromAudioCommand(
                            audioCommand),
                    Rank: -2,
                    Category: -1,
                    IsActive: false,
                    originalIndex++));
        }

        if (includeSystem)
        {
            foreach (
                ApplicationAudioSearchCommand
                    applicationAudioCommand
                in ApplicationAudioSearchCommandParser
                    .Parse(
                        query,
                        applicationAudioSessions))
            {
                ranked.Add(
                    new RankedResult(
                        ShellSearchResult
                            .FromApplicationAudioCommand(
                                applicationAudioCommand),
                        Rank: -2,
                        Category: -1,
                        IsActive: false,
                        originalIndex++));
            }
        }

        if (includeSystem
            && BrightnessSearchCommandParser
            .TryParse(
                query,
                out BrightnessSearchCommand
                    brightnessCommand))
        {
            ranked.Add(
                new RankedResult(
                    ShellSearchResult
                        .FromBrightnessCommand(
                            brightnessCommand),
                    Rank: -2,
                    Category: -1,
                    IsActive: false,
                    originalIndex++));
        }

        if (includeSystem
            && SafeExpressionEvaluator
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
                 in includeApplications
                     ? apps
                     : Array.Empty<AppLaunchItem>())
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

        foreach (TaskSearchItem task
                 in includeTasks
                     ? TaskSearchPolicy.Search(
                         taskItems,
                         query)
                     : Array.Empty<TaskSearchItem>())
        {
            int? rank =
                AppSearchPolicy
                    .GetTextRank(
                        task.Title,
                        task.ParentTitle,
                        query);
            if (!rank.HasValue)
                continue;

            ranked.Add(
                new RankedResult(
                    ShellSearchResult
                        .FromTask(task),
                    rank.Value,
                    Category: 1,
                    IsActive: false,
                    originalIndex++));
        }

        foreach (WindowTaskItem running
                 in FilterWindowApplications(
                     includeWindows
                         ? runningApplications
                         : null,
                     windowIdentityFilter))
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
                        Category: 2,
                        window.IsActive,
                        originalIndex++));
            }
        }

        foreach (SystemManagementSearchEntry
                 command
                 in includeSystem
                     ? SystemManagementSearchCatalog
                         .All
                     : Array.Empty<SystemManagementSearchEntry>())
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
                    Category: 3,
                    IsActive: false,
                    originalIndex++));
        }

        foreach (PanelStatusSearchEntry entry
                 in includeSystem
                     ? PanelStatusSearchCatalog.All
                     : Array.Empty<PanelStatusSearchEntry>())
        {
            int? rank = AppSearchPolicy.GetTextRank(
                entry.DisplayName,
                entry.Aliases,
                query);
            if (!rank.HasValue)
                continue;

            ranked.Add(
                new RankedResult(
                    ShellSearchResult.FromPanelStatus(entry),
                    rank.Value,
                    Category: 2,
                    IsActive: false,
                    originalIndex++));
        }

        foreach (WindowsShellSearchEntry
                 command
                 in includeSystem
                     ? WindowsShellSearchCatalog
                         .All
                     : Array.Empty<WindowsShellSearchEntry>())
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
                    Category: 4,
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

    private static IReadOnlyList<ShellSearchResult>
        ComposeWindowOverview(
            IEnumerable<WindowTaskItem>?
                runningApplications,
            int limit,
            string? windowIdentityFilter,
            IReadOnlyList<IntPtr>?
                recentWindowHandles)
    {
        IReadOnlyDictionary<IntPtr, int>
            recentRanks = BuildRecentWindowRanks(
                recentWindowHandles);
        return
        FilterWindowApplications(
            runningApplications,
            windowIdentityFilter)
        .SelectMany(
            application =>
                application.Windows.Select(
                    window => new
                    {
                        Application = application,
                        Window = window
                    }))
        .GroupBy(
            item => item.Window.Handle)
        .Select(group => group.First())
        .OrderByDescending(
            item => item.Window.IsActive)
        .ThenBy(item =>
            recentRanks.TryGetValue(
                item.Window.Handle,
                out int rank)
                ? rank
                : int.MaxValue)
        .ThenBy(
            item => item.Application.DisplayName,
            StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(
            item => item.Window.Title,
            StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(
            item => item.Window.Handle.ToInt64())
        .Take(limit)
        .Select(
            item => ShellSearchResult.FromWindow(
                item.Application,
                item.Window))
        .ToList();
    }

    private static IReadOnlyDictionary<IntPtr, int>
        BuildRecentWindowRanks(
            IReadOnlyList<IntPtr>? handles)
    {
        var ranks =
            new Dictionary<IntPtr, int>();
        if (handles == null)
            return ranks;

        for (int index = 0;
             index < handles.Count;
             index++)
        {
            IntPtr handle = handles[index];
            if (handle != IntPtr.Zero
                && !ranks.ContainsKey(handle))
            {
                ranks.Add(handle, index);
            }
        }

        return ranks;
    }

    private static IEnumerable<WindowTaskItem>
        FilterWindowApplications(
            IEnumerable<WindowTaskItem>?
                runningApplications,
            string? identityFilter)
    {
        IEnumerable<WindowTaskItem> source =
            runningApplications
            ?? Array.Empty<WindowTaskItem>();
        if (string.IsNullOrWhiteSpace(
                identityFilter))
        {
            return source;
        }

        return source.Where(application =>
            string.Equals(
                application.IdentityKey,
                identityFilter,
                StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<ShellSearchResult>
        ComposeSystemOverview(int limit) =>
        PanelStatusSearchCatalog.All
            .Select(
                ShellSearchResult.FromPanelStatus)
            .Concat(
                SystemManagementSearchCatalog.All
            .Select(
                ShellSearchResult.FromSystemCommand))
            .Concat(
                WindowsShellSearchCatalog.All.Select(
                    ShellSearchResult.FromShellCommand))
            .Take(limit)
            .ToList();

    private sealed record RankedResult(
        ShellSearchResult Result,
        int Rank,
        int Category,
        bool IsActive,
        int OriginalIndex);
}
