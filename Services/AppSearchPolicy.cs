using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal static class AppSearchPolicy
{
    internal static IReadOnlyList<AppLaunchItem>
        Search(
            IEnumerable<AppLaunchItem> apps,
            string? query,
            int limit)
    {
        if (limit <= 0)
            return Array.Empty<AppLaunchItem>();

        SearchText normalizedQuery =
            SearchText.Create(query);
        return apps
            .Select(
                (app, index) =>
                    new RankedApp(
                        app,
                        GetRank(
                            app,
                            normalizedQuery),
                        index))
            .Where(result =>
                result.Rank.HasValue)
            .OrderBy(result =>
                result.Rank!.Value)
            .ThenByDescending(result =>
                result.App.IsPinned)
            .ThenBy(result =>
                result.App.DisplayName,
                StringComparer
                    .CurrentCultureIgnoreCase)
            .ThenBy(result =>
                result.App.IdentityKey,
                StringComparer
                    .OrdinalIgnoreCase)
            .ThenBy(result =>
                result.OriginalIndex)
            .Take(limit)
            .Select(result =>
                result.App)
            .ToList();
    }

    private static int? GetRank(
        AppLaunchItem app,
        SearchText query)
    {
        if (query.IsEmpty)
            return 0;

        SearchText display =
            SearchText.Create(
                app.DisplayName);
        SearchText executable =
            SearchText.Create(
                GetExecutableName(
                    app.LaunchTarget));

        if (display.Normalized
            == query.Normalized)
        {
            return 0;
        }
        if (executable.Normalized
            == query.Normalized)
        {
            return 1;
        }
        if (display.Normalized.StartsWith(
                query.Normalized,
                StringComparison.Ordinal))
        {
            return 2;
        }
        if (executable.Normalized.StartsWith(
                query.Normalized,
                StringComparison.Ordinal))
        {
            return 3;
        }
        if (query.Collapsed.Length >= 2
            && (display.Acronym.StartsWith(
                    query.Collapsed,
                    StringComparison.Ordinal)
                || executable.Acronym.StartsWith(
                    query.Collapsed,
                    StringComparison.Ordinal)))
        {
            return 4;
        }
        if (query.Words.Count > 0
            && query.Words.All(
                token =>
                    display.Words.Any(
                        word =>
                            word.StartsWith(
                                token,
                                StringComparison.Ordinal))
                    || executable.Words.Any(
                        word =>
                            word.StartsWith(
                                token,
                                StringComparison.Ordinal))))
        {
            return 5;
        }
        if (query.Collapsed.Length > 0
            && (display.Collapsed.Contains(
                    query.Collapsed,
                    StringComparison.Ordinal)
                || executable.Collapsed.Contains(
                    query.Collapsed,
                    StringComparison.Ordinal)))
        {
            return 6;
        }
        if (query.Words.Count > 0
            && query.Words.All(
                token =>
                    display.Normalized.Contains(
                        token,
                        StringComparison.Ordinal)
                    || executable.Normalized.Contains(
                        token,
                        StringComparison.Ordinal)))
        {
            return 7;
        }

        return null;
    }

    private static string GetExecutableName(
        string launchTarget)
    {
        try
        {
            return Path
                .GetFileNameWithoutExtension(
                    launchTarget)
                ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed record RankedApp(
        AppLaunchItem App,
        int? Rank,
        int OriginalIndex);

    private sealed record SearchText(
        string Normalized,
        string Collapsed,
        IReadOnlyList<string> Words,
        string Acronym)
    {
        internal bool IsEmpty =>
            Normalized.Length == 0;

        internal static SearchText Create(
            string? value)
        {
            string normalized =
                Normalize(value);
            string[] words =
                normalized.Split(
                    ' ',
                    StringSplitOptions
                        .RemoveEmptyEntries);
            return new SearchText(
                normalized,
                string.Concat(words),
                words,
                string.Concat(
                    words.Select(
                        word => word[0])));
        }

        private static string Normalize(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return string.Empty;
            }

            string decomposed =
                AddCamelCaseBoundaries(
                        value.Trim())
                    .Normalize(
                        NormalizationForm.FormD);
            var builder =
                new StringBuilder(
                    decomposed.Length);
            bool pendingSpace = false;
            foreach (char character
                     in decomposed)
            {
                UnicodeCategory category =
                    CharUnicodeInfo
                        .GetUnicodeCategory(
                            character);
                if (category
                    == UnicodeCategory
                        .NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(
                        character))
                {
                    if (pendingSpace
                        && builder.Length > 0)
                    {
                        builder.Append(' ');
                    }
                    builder.Append(
                        char.ToLowerInvariant(
                            character));
                    pendingSpace = false;
                }
                else
                {
                    pendingSpace = true;
                }
            }

            return builder.ToString();
        }

        private static string
            AddCamelCaseBoundaries(
                string value)
        {
            var builder =
                new StringBuilder(
                    value.Length + 8);
            for (int index = 0;
                 index < value.Length;
                 index++)
            {
                char current = value[index];
                if (index > 0
                    && char.IsUpper(current)
                    && (char.IsLower(
                            value[index - 1])
                        || char.IsDigit(
                            value[index - 1])))
                {
                    builder.Append(' ');
                }
                builder.Append(current);
            }
            return builder.ToString();
        }
    }
}
