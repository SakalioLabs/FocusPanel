using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal sealed record DesktopItemRefresh(
    string RequestedPath,
    DesktopFile? Item,
    bool ShouldRemove);

internal static class DesktopFileCollectionSynchronizer
{
    internal static void Apply(
        ObservableCollection<DesktopFile> all,
        ObservableCollection<DesktopFile> visible,
        IReadOnlyList<DesktopItemRefresh> changes)
    {
        foreach (DesktopItemRefresh change in changes)
        {
            ApplyOne(
                all,
                visible,
                change);
        }

        if (changes.Count == 0)
            return;

        Sort(all);
        Sort(visible);
    }

    private static void ApplyOne(
        ObservableCollection<DesktopFile> all,
        ObservableCollection<DesktopFile> visible,
        DesktopItemRefresh change)
    {
        DesktopFile? existing = all
            .FirstOrDefault(item =>
                string.Equals(
                    item.FullPath,
                    change.RequestedPath,
                    StringComparison.OrdinalIgnoreCase));
        if (existing == null
            && change.Item != null)
        {
            existing = all.FirstOrDefault(item =>
                string.Equals(
                    item.Name,
                    change.Item.Name,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (change.ShouldRemove)
        {
            if (existing != null)
            {
                all.Remove(existing);
                visible.Remove(existing);
            }
            return;
        }

        if (change.Item == null)
            return;

        if (existing == null)
        {
            existing = change.Item;
            all.Add(existing);
        }
        else
        {
            CopyState(
                existing,
                change.Item);
        }

        DesktopFile? visibleItem = visible
            .FirstOrDefault(item =>
                ReferenceEquals(item, existing)
                || string.Equals(
                    item.FullPath,
                    existing.FullPath,
                    StringComparison.OrdinalIgnoreCase));
        if (existing.IsHidden)
        {
            if (visibleItem != null)
                visible.Remove(visibleItem);
        }
        else if (visibleItem == null)
        {
            visible.Add(existing);
        }
    }

    internal static void CopyState(
        DesktopFile destination,
        DesktopFile source)
    {
        destination.Name = source.Name;
        destination.FullPath = source.FullPath;
        destination.Extension = source.Extension;
        destination.Size = source.Size;
        destination.CreatedAt = source.CreatedAt;
        destination.FileType = source.FileType;
        destination.IsHidden = source.IsHidden;
        destination.NeedsRecovery = source.NeedsRecovery;
        destination.DesktopX = source.DesktopX;
        destination.DesktopY = source.DesktopY;
        destination.CustomIconPath =
            source.CustomIconPath;
        destination.CustomIconIndex =
            source.CustomIconIndex;
        if (source.Icon != null)
            destination.Icon = source.Icon;
    }

    internal static void Sort(
        ObservableCollection<DesktopFile> files)
    {
        IReadOnlyList<DesktopFile> desired = files
            .OrderByDescending(
                file => file.FileType == "Folder")
            .ThenBy(
                file => file.Name,
                StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        for (int targetIndex = 0;
             targetIndex < desired.Count;
             targetIndex++)
        {
            DesktopFile item = desired[targetIndex];
            int currentIndex = files.IndexOf(item);
            if (currentIndex != targetIndex)
            {
                files.Move(
                    currentIndex,
                    targetIndex);
            }
        }
    }
}
