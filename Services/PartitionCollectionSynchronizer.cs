using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FocusPanel.Models;
using FocusPanel.ViewModels;

namespace FocusPanel.Services;

internal static class PartitionCollectionSynchronizer
{
    internal static void Synchronize(
        ObservableCollection<PartitionViewModel> all,
        ObservableCollection<PartitionViewModel> columnOne,
        ObservableCollection<PartitionViewModel> columnTwo,
        IReadOnlyList<PartitionViewModel> desired)
    {
        var available = all
            .GroupBy(
                GetKey,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new Queue<PartitionViewModel>(
                    group),
                StringComparer.OrdinalIgnoreCase);
        var resolved =
            new List<PartitionViewModel>(
                desired.Count);

        foreach (PartitionViewModel candidate in desired)
        {
            string key = GetKey(candidate);
            PartitionViewModel current =
                available.TryGetValue(
                    key,
                    out Queue<PartitionViewModel>? matches)
                && matches.Count > 0
                    ? matches.Dequeue()
                    : candidate;

            if (!ReferenceEquals(current, candidate))
            {
                current.Name = candidate.Name;
                current.IsCustom = candidate.IsCustom;
                current.ColumnIndex =
                    candidate.ColumnIndex;
                SynchronizeFiles(
                    current.Files,
                    candidate.Files);
            }
            resolved.Add(current);
        }

        SynchronizeReferences(all, resolved);
        SynchronizeReferences(
            columnOne,
            resolved
                .Where(item => item.ColumnIndex == 0)
                .ToList());
        SynchronizeReferences(
            columnTwo,
            resolved
                .Where(item => item.ColumnIndex != 0)
                .ToList());
    }

    private static void SynchronizeFiles(
        ObservableCollection<DesktopFile> destination,
        IReadOnlyList<DesktopFile> desired)
    {
        for (int targetIndex = 0;
             targetIndex < desired.Count;
             targetIndex++)
        {
            DesktopFile candidate = desired[targetIndex];
            int existingIndex = FindFile(
                destination,
                candidate,
                targetIndex);
            if (existingIndex < 0)
            {
                destination.Insert(
                    targetIndex,
                    candidate);
                continue;
            }

            if (existingIndex != targetIndex)
            {
                destination.Move(
                    existingIndex,
                    targetIndex);
            }

            DesktopFile current =
                destination[targetIndex];
            if (!ReferenceEquals(current, candidate))
            {
                candidate.IsSelected |=
                    current.IsSelected;
                destination[targetIndex] =
                    candidate;
            }
        }

        while (destination.Count > desired.Count)
        {
            destination.RemoveAt(
                destination.Count - 1);
        }
    }

    private static int FindFile(
        IReadOnlyList<DesktopFile> items,
        DesktopFile candidate,
        int startIndex)
    {
        for (int index = startIndex;
             index < items.Count;
             index++)
        {
            DesktopFile current = items[index];
            if (ReferenceEquals(current, candidate)
                || string.Equals(
                    current.FullPath,
                    candidate.FullPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return -1;
    }

    private static void SynchronizeReferences<T>(
        ObservableCollection<T> destination,
        IReadOnlyList<T> desired)
        where T : class
    {
        for (int targetIndex = 0;
             targetIndex < desired.Count;
             targetIndex++)
        {
            T candidate = desired[targetIndex];
            int existingIndex = -1;
            for (int index = targetIndex;
                 index < destination.Count;
                 index++)
            {
                if (ReferenceEquals(
                        destination[index],
                        candidate))
                {
                    existingIndex = index;
                    break;
                }
            }

            if (existingIndex < 0)
            {
                destination.Insert(
                    targetIndex,
                    candidate);
            }
            else if (existingIndex != targetIndex)
            {
                destination.Move(
                    existingIndex,
                    targetIndex);
            }
        }

        while (destination.Count > desired.Count)
        {
            destination.RemoveAt(
                destination.Count - 1);
        }
    }

    private static string GetKey(
        PartitionViewModel partition) =>
        $"{partition.IsCustom}|{partition.Name}";
}
