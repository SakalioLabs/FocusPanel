using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal sealed record BackgroundAppObservation(
    uint ProcessId,
    string DisplayName,
    string ExecutablePath,
    string IdentityKey,
    string? ApplicationUserModelId,
    ImageSource? Icon);

internal static class BackgroundAppSnapshotComposer
{
    internal static IReadOnlyList<WindowTaskItem> Append(
        IReadOnlyList<WindowTaskItem> visibleApplications,
        IEnumerable<BackgroundAppObservation>
            backgroundObservations)
    {
        ArgumentNullException.ThrowIfNull(
            visibleApplications);
        ArgumentNullException.ThrowIfNull(
            backgroundObservations);

        var result = visibleApplications.ToList();
        var visibleIdentities = new HashSet<string>(
            result.Select(item => item.IdentityKey),
            StringComparer.OrdinalIgnoreCase);
        var visibleExecutablePaths = new HashSet<string>(
            result
                .Select(item =>
                    AppIdentityResolver.NormalizePath(
                        item.ExecutablePath))
                .Where(path =>
                    !string.IsNullOrWhiteSpace(path))
                .Select(path => path!),
            StringComparer.OrdinalIgnoreCase);

        result.AddRange(
            backgroundObservations
                .GroupBy(
                    item => item.IdentityKey,
                    StringComparer.OrdinalIgnoreCase)
                .Where(group =>
                    !visibleIdentities.Contains(
                        group.Key)
                    && !group.Any(item =>
                        visibleExecutablePaths.Contains(
                            AppIdentityResolver
                                .NormalizePath(
                                    item.ExecutablePath)
                            ?? string.Empty)))
                .Select(CreateBackgroundTask)
                .OrderBy(item =>
                    item.DisplayName,
                    StringComparer
                        .CurrentCultureIgnoreCase));
        return result;
    }

    private static WindowTaskItem CreateBackgroundTask(
        IGrouping<string, BackgroundAppObservation>
            group)
    {
        BackgroundAppObservation first =
            group.First();
        return new WindowTaskItem
        {
            AppKey = group.Key,
            IdentityKey = group.Key,
            ApplicationUserModelId = group
                .Select(item =>
                    item.ApplicationUserModelId)
                .FirstOrDefault(value =>
                    !string.IsNullOrWhiteSpace(
                        value)),
            DisplayName = first.DisplayName,
            ExecutablePath = group
                .Select(item => item.ExecutablePath)
                .FirstOrDefault(value =>
                    !string.IsNullOrWhiteSpace(value)),
            Icon = group
                .Select(item => item.Icon)
                .FirstOrDefault(value => value != null),
            Windows = Array.Empty<WindowReference>(),
            IsActive = false
        };
    }
}
