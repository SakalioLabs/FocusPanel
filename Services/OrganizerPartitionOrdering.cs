using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal static class OrganizerPartitionOrdering
{
    internal static bool Reorder(
        IReadOnlyList<DesktopPartition> partitions,
        string sourceName,
        string targetName,
        bool insertAfter)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        DesktopPartition? source = Find(
            partitions,
            sourceName);
        DesktopPartition? target = Find(
            partitions,
            targetName);
        if (source == null
            || target == null
            || ReferenceEquals(source, target))
        {
            return false;
        }

        int oldColumn = source.ColumnIndex;
        int targetColumn = target.ColumnIndex;
        source.ColumnIndex = targetColumn;
        List<DesktopPartition> targetItems =
            partitions
                .Where(item =>
                    item.ColumnIndex == targetColumn
                    && !ReferenceEquals(item, source))
                .OrderBy(item => item.OrderIndex)
                .ThenBy(item => item.Id)
                .ToList();
        int targetIndex = targetItems.IndexOf(target);
        if (targetIndex < 0)
            targetItems.Add(source);
        else
            targetItems.Insert(
                insertAfter
                    ? targetIndex + 1
                    : targetIndex,
                source);
        Reindex(targetItems);

        if (oldColumn != targetColumn)
        {
            Reindex(
                partitions
                    .Where(item =>
                        item.ColumnIndex == oldColumn
                        && !ReferenceEquals(
                            item,
                            source))
                    .OrderBy(item => item.OrderIndex)
                    .ThenBy(item => item.Id));
        }

        return true;
    }

    internal static bool MoveToColumn(
        IReadOnlyList<DesktopPartition> partitions,
        string sourceName,
        int targetColumn)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        if (targetColumn is < 0 or > 1)
            throw new ArgumentOutOfRangeException(
                nameof(targetColumn));

        DesktopPartition? source = Find(
            partitions,
            sourceName);
        if (source == null
            || source.ColumnIndex == targetColumn)
        {
            return false;
        }

        int oldColumn = source.ColumnIndex;
        source.ColumnIndex = targetColumn;
        List<DesktopPartition> targetItems =
            partitions
                .Where(item =>
                    item.ColumnIndex == targetColumn
                    && !ReferenceEquals(item, source))
                .OrderBy(item => item.OrderIndex)
                .ThenBy(item => item.Id)
                .ToList();
        targetItems.Add(source);
        Reindex(targetItems);
        Reindex(
            partitions
                .Where(item =>
                    item.ColumnIndex == oldColumn)
                .OrderBy(item => item.OrderIndex)
                .ThenBy(item => item.Id));
        return true;
    }

    private static DesktopPartition? Find(
        IEnumerable<DesktopPartition> partitions,
        string name) =>
        partitions.FirstOrDefault(item =>
            string.Equals(
                item.Name,
                name,
                StringComparison.OrdinalIgnoreCase));

    private static void Reindex(
        IEnumerable<DesktopPartition> partitions)
    {
        int index = 0;
        foreach (DesktopPartition partition in partitions)
            partition.OrderIndex = index++;
    }
}
