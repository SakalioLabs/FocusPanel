using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal static class TaskbarAppCollectionSynchronizer
{
    internal static void Synchronize(
        ObservableCollection<TaskbarAppItem> destination,
        IReadOnlyList<TaskbarAppItem> desired)
    {
        for (int targetIndex = 0; targetIndex < desired.Count; targetIndex++)
        {
            TaskbarAppItem candidate = desired[targetIndex];
            int existingIndex = FindIdentity(destination, candidate.IdentityKey, targetIndex);
            if (existingIndex < 0)
            {
                destination.Insert(targetIndex, candidate);
                continue;
            }

            if (existingIndex != targetIndex)
                destination.Move(existingIndex, targetIndex);

            TaskbarAppItem current =
                destination[targetIndex];
            if (!AreEquivalent(current, candidate))
                current.ApplySnapshot(candidate);
        }

        while (destination.Count > desired.Count)
            destination.RemoveAt(destination.Count - 1);
    }

    private static int FindIdentity(
        IReadOnlyList<TaskbarAppItem> items,
        string identity,
        int startIndex)
    {
        for (int index = startIndex; index < items.Count; index++)
        {
            if (string.Equals(
                    items[index].IdentityKey,
                    identity,
                    StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool AreEquivalent(TaskbarAppItem current, TaskbarAppItem candidate)
        => string.Equals(current.IdentityKey, candidate.IdentityKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(current.DisplayName, candidate.DisplayName, StringComparison.Ordinal)
            && ReferenceEquals(current.Icon, candidate.Icon)
            && LaunchesEqual(current.LaunchItem, candidate.LaunchItem)
            && LaunchListsEqual(current.PinnedLaunches, candidate.PinnedLaunches)
            && RunningTasksEqual(current.RunningTask, candidate.RunningTask);

    private static bool LaunchListsEqual(
        IReadOnlyList<AppLaunchItem> current,
        IReadOnlyList<AppLaunchItem> candidate)
        => current.Count == candidate.Count
            && current.Zip(candidate, LaunchesEqual).All(equal => equal);

    private static bool LaunchesEqual(AppLaunchItem? current, AppLaunchItem? candidate)
        => ReferenceEquals(current, candidate)
            || current != null
            && candidate != null
            && current.LaunchKind == candidate.LaunchKind
            && string.Equals(current.IdentityKey, candidate.IdentityKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(current.DisplayName, candidate.DisplayName, StringComparison.Ordinal)
            && string.Equals(current.LaunchTarget, candidate.LaunchTarget, StringComparison.OrdinalIgnoreCase)
            && string.Equals(current.Arguments, candidate.Arguments, StringComparison.Ordinal);

    private static bool RunningTasksEqual(WindowTaskItem? current, WindowTaskItem? candidate)
        => ReferenceEquals(current, candidate)
            || current != null
            && candidate != null
            && current.IsActive == candidate.IsActive
            && string.Equals(current.IdentityKey, candidate.IdentityKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(current.DisplayName, candidate.DisplayName, StringComparison.Ordinal)
            && string.Equals(
                current.ApplicationUserModelId,
                candidate.ApplicationUserModelId,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                current.ExecutablePath,
                candidate.ExecutablePath,
                StringComparison.OrdinalIgnoreCase)
            && WindowsEqual(current.Windows, candidate.Windows);

    private static bool WindowsEqual(
        IReadOnlyList<WindowReference> current,
        IReadOnlyList<WindowReference> candidate)
        => current.Count == candidate.Count
            && current.Zip(candidate, (left, right) =>
                    left.Handle == right.Handle
                    && left.IsActive == right.IsActive
                    && string.Equals(left.Title, right.Title, StringComparison.Ordinal))
                .All(equal => equal);
}
