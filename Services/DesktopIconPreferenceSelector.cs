using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal static class DesktopIconPreferenceSelector
{
    internal static DesktopFilePreference? Select(
        IReadOnlyList<DesktopFilePreference> preferences,
        string fullPath,
        string fileName,
        string? fileIdentity)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        string normalizedPath = NormalizePath(fullPath);

        DesktopFilePreference? pathMatch = preferences
            .FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(item.ManagedPath)
                && string.Equals(
                    NormalizePath(item.ManagedPath),
                    normalizedPath,
                    StringComparison.OrdinalIgnoreCase));
        if (pathMatch != null)
            return pathMatch;

        if (!string.IsNullOrWhiteSpace(fileIdentity))
        {
            DesktopFilePreference? identityMatch =
                preferences.FirstOrDefault(item =>
                    !string.IsNullOrWhiteSpace(
                        item.FileIdentity)
                    && string.Equals(
                        item.FileIdentity,
                        fileIdentity,
                        StringComparison.OrdinalIgnoreCase));
            if (identityMatch != null)
                return identityMatch;
        }

        DesktopFilePreference[] nameMatches = preferences
            .Where(item => string.Equals(
                item.FilePath,
                fileName,
                StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        return nameMatches.Length == 1
            ? nameMatches[0]
            : null;
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
        try
        {
            return Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(
                    path.Trim().Trim('"')))
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.Trim();
        }
    }
}
