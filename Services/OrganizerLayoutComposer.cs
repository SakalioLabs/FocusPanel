using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Models;
using FocusPanel.ViewModels;

namespace FocusPanel.Services;

internal static class OrganizerLayoutComposer
{
    internal static IReadOnlyList<PartitionViewModel>
        Compose(
            OrganizerLayoutSnapshot snapshot,
            bool personalizedView,
            IReadOnlyList<DesktopFile> allFiles)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(allFiles);
        var viewModels =
            new List<PartitionViewModel>();
        if (!personalizedView)
        {
            foreach (DesktopFile file in allFiles)
                file.CustomPartition = null;

            int column = 0;
            foreach (IGrouping<string, DesktopFile> group
                     in allFiles
                         .GroupBy(file => file.DateGroup)
                         .OrderBy(group =>
                             GetDateGroupSortOrder(
                                 group.Key)))
            {
                var partition =
                    new PartitionViewModel(group.Key)
                    {
                        IsCustom = false,
                        ColumnIndex = column++ % 2
                    };
                foreach (DesktopFile file in group
                             .OrderByDescending(
                                 item => item.CreatedAt))
                {
                    partition.Files.Add(file);
                }
                viewModels.Add(partition);
            }
            return viewModels;
        }

        var partitionMap =
            new Dictionary<
                string,
                PartitionViewModel>(
                StringComparer.OrdinalIgnoreCase);
        foreach (OrganizerPartitionSnapshot stored
                 in snapshot.Partitions)
        {
            var partition =
                new PartitionViewModel(stored.Name)
                {
                    IsCustom = true,
                    ColumnIndex = stored.ColumnIndex
                };
            partitionMap[stored.Name] = partition;
            viewModels.Add(partition);
        }
        Dictionary<
            string,
            OrganizerFilePreferenceSnapshot>
            preferences = snapshot.Preferences
                .GroupBy(
                    item => item.FilePath,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last(),
                    StringComparer.OrdinalIgnoreCase);
        foreach (DesktopFile file in allFiles)
        {
            if (preferences.TryGetValue(
                    file.Name,
                    out OrganizerFilePreferenceSnapshot?
                        preference)
                && partitionMap.TryGetValue(
                    preference.PartitionName,
                    out PartitionViewModel? partition))
            {
                partition.Files.Add(file);
                file.CustomPartition =
                    preference.PartitionName;
            }
            else
            {
                file.CustomPartition = null;
            }
        }

        return viewModels;
    }

    private static int GetDateGroupSortOrder(
        string groupName) =>
        groupName switch
        {
            "今天" => 0,
            "昨天" => 1,
            "本周" => 2,
            "本月" => 3,
            "更早" => 4,
            _ => 5
        };
}
