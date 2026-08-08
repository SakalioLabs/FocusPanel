using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal sealed class TaskbarAppComposer
{
    private readonly Dictionary<string, long> _runningOrder =
        new(StringComparer.OrdinalIgnoreCase);
    private long _nextRunningOrder;

    internal IReadOnlyList<TaskbarAppItem> Compose(
        IReadOnlyList<AppLaunchItem> pinned,
        IReadOnlyList<WindowTaskItem> running)
    {
        var runningByIdentity = running
            .GroupBy(item => item.IdentityKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, MergeRunning, StringComparer.OrdinalIgnoreCase);
        foreach (string identity in runningByIdentity.Keys)
        {
            if (!_runningOrder.ContainsKey(identity))
                _runningOrder[identity] = _nextRunningOrder++;
        }
        foreach (string stale in _runningOrder.Keys.Except(runningByIdentity.Keys, StringComparer.OrdinalIgnoreCase).ToList())
            _runningOrder.Remove(stale);

        var result = new List<TaskbarAppItem>();
        var pinnedIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (IGrouping<string, AppLaunchItem> group in pinned.GroupBy(
                     item => item.IdentityKey,
                     StringComparer.OrdinalIgnoreCase))
        {
            AppLaunchItem launch = group.First();
            runningByIdentity.TryGetValue(group.Key, out WindowTaskItem? runtime);
            result.Add(Create(group.Key, launch, group.ToList(), runtime));
            pinnedIdentities.Add(group.Key);
        }

        result.AddRange(runningByIdentity
            .Where(pair => !pinnedIdentities.Contains(pair.Key))
            // Keep switchable windows in the visible taskbar section. Pure
            // background owners remain available from the background drawer,
            // but must not push normal running applications below the fold.
            .OrderBy(pair =>
                pair.Value.Windows.Count == 0
                    ? 1
                    : 0)
            .ThenBy(pair => _runningOrder[pair.Key])
            .Select(pair => Create(pair.Key, null, Array.Empty<AppLaunchItem>(), pair.Value)));
        return result;
    }

    private static TaskbarAppItem Create(
        string identity,
        AppLaunchItem? launch,
        IReadOnlyList<AppLaunchItem> pinned,
        WindowTaskItem? runtime) => new()
    {
        IdentityKey = identity,
        DisplayName = launch?.DisplayName ?? runtime?.DisplayName ?? "应用",
        Icon = launch?.Icon ?? runtime?.Icon,
        LaunchItem = launch,
        PinnedLaunches = pinned,
        RunningTask = runtime
    };

    private static WindowTaskItem MergeRunning(IEnumerable<WindowTaskItem> items)
    {
        List<WindowTaskItem> values = items.ToList();
        WindowTaskItem first = values[0];
        return new WindowTaskItem
        {
            AppKey = first.AppKey,
            IdentityKey = first.IdentityKey,
            ApplicationUserModelId = values.Select(item => item.ApplicationUserModelId).FirstOrDefault(value => value != null),
            DisplayName = first.DisplayName,
            ExecutablePath = values.Select(item => item.ExecutablePath).FirstOrDefault(value => value != null),
            Icon = values.Select(item => item.Icon).FirstOrDefault(value => value != null),
            Windows = values.SelectMany(item => item.Windows).ToList(),
            IsActive = values.Any(item => item.IsActive)
        };
    }
}
