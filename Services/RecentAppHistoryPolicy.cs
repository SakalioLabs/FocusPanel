using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal static class RecentAppHistoryPolicy
{
    internal const int MaximumEntries = 8;

    internal static IReadOnlyList<string> Parse(
        string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            string[]? values =
                JsonSerializer.Deserialize<string[]>(json);
            return Normalize(values);
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    internal static string Serialize(
        IEnumerable<string>? identities) =>
        JsonSerializer.Serialize(
            Normalize(identities));

    internal static IReadOnlyList<string> Record(
        IEnumerable<string>? identities,
        string? launchedIdentity)
    {
        string identity = NormalizeIdentity(
            launchedIdentity);
        if (identity.Length == 0)
            return Normalize(identities);

        var result = new List<string>
        {
            identity
        };
        foreach (string existing in Normalize(identities))
        {
            if (string.Equals(
                    existing,
                    identity,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(existing);
            if (result.Count >= MaximumEntries)
                break;
        }

        return result;
    }

    internal static IReadOnlyList<AppLaunchItem>
        OrderForLauncher(
            IEnumerable<AppLaunchItem>? applications,
            IEnumerable<string>? identities)
    {
        AppLaunchItem[] apps =
            applications?
                .Where(app => app != null)
                .ToArray()
            ?? Array.Empty<AppLaunchItem>();
        Dictionary<string, int> recentRanks =
            Normalize(identities)
                .Select(
                    (identity, index) =>
                        new
                        {
                            identity,
                            index
                        })
                .ToDictionary(
                    item => item.identity,
                    item => item.index,
                    StringComparer.OrdinalIgnoreCase);

        return apps
            .Select(
                (app, index) =>
                    new RankedApplication(
                        app,
                        recentRanks.TryGetValue(
                            NormalizeIdentity(
                                app.IdentityKey),
                            out int recentRank)
                            ? recentRank
                            : int.MaxValue,
                        index))
            .OrderByDescending(item => item.App.IsPinned)
            .ThenBy(item => item.RecentRank)
            .ThenBy(item => item.OriginalIndex)
            .Select(item => item.App)
            .ToArray();
    }

    internal static bool Contains(
        IEnumerable<string>? identities,
        string? identity)
    {
        string normalized = NormalizeIdentity(identity);
        return normalized.Length > 0
               && Normalize(identities).Contains(
                   normalized,
                   StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> Normalize(
        IEnumerable<string>? identities)
    {
        if (identities == null)
            return Array.Empty<string>();

        var result = new List<string>();
        var seen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (string? value in identities)
        {
            string identity = NormalizeIdentity(value);
            if (identity.Length == 0
                || !seen.Add(identity))
            {
                continue;
            }

            result.Add(identity);
            if (result.Count >= MaximumEntries)
                break;
        }

        return result;
    }

    private static string NormalizeIdentity(
        string? identity) =>
        identity?.Trim() ?? string.Empty;

    private sealed record RankedApplication(
        AppLaunchItem App,
        int RecentRank,
        int OriginalIndex);
}
