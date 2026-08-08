using System;
using System.Collections.Generic;
using System.IO;

namespace FocusPanel.Services;

internal static class BackgroundAppVisibilityPolicy
{
    private static readonly HashSet<string> ExcludedExecutables =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "crashpad_handler.exe",
            "werfault.exe",
            "wermgr.exe"
        };

    internal static bool ShouldInclude(
        uint processId,
        int currentProcessId,
        int processSessionId,
        int currentSessionId,
        string? executablePath,
        string? windowsDirectory)
    {
        if (processId == 0
            || processId == currentProcessId
            || processSessionId != currentSessionId
            || string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        string? normalized = AppIdentityResolver
            .NormalizePath(executablePath);
        if (string.IsNullOrWhiteSpace(normalized)
            || !string.Equals(
                Path.GetExtension(normalized),
                ".exe",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string fileName = Path.GetFileName(normalized);
        if (ExcludedExecutables.Contains(fileName))
            return false;

        string? normalizedWindows = AppIdentityResolver
            .NormalizePath(windowsDirectory);
        return string.IsNullOrWhiteSpace(normalizedWindows)
            || !IsUnderDirectory(
                normalized,
                normalizedWindows);
    }

    internal static string GetDisplayName(
        string? processName,
        string? fileDescription)
    {
        string? description = NormalizeLabel(
            fileDescription);
        if (description != null)
            return description;

        string? name = NormalizeLabel(processName);
        return name ?? "后台应用";
    }

    private static bool IsUnderDirectory(
        string path,
        string directory)
    {
        string prefix = directory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return path.StartsWith(
            prefix,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeLabel(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
