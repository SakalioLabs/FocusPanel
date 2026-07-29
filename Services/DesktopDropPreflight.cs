using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FocusPanel.Services;

internal sealed record DesktopDropCandidate(
    string FullPath,
    DesktopDropLocation Location);

internal sealed record DesktopDropPreflightResult(
    IReadOnlyList<DesktopDropCandidate> Candidates,
    int OutsideDesktop,
    int MissingOrInvalid,
    int SkippedDuplicates);

internal sealed class DesktopDropPreflight
{
    private readonly Func<string, string>
        _normalizePath;
    private readonly Func<string, bool>
        _exists;

    internal DesktopDropPreflight()
        : this(
            NormalizePath,
            path =>
                File.Exists(path)
                || Directory.Exists(path))
    {
    }

    internal DesktopDropPreflight(
        Func<string, string> normalizePath,
        Func<string, bool> exists)
    {
        _normalizePath =
            normalizePath
            ?? throw new ArgumentNullException(
                nameof(normalizePath));
        _exists =
            exists
            ?? throw new ArgumentNullException(
                nameof(exists));
    }

    internal Task<DesktopDropPreflightResult>
        ResolveAsync(
            IReadOnlyList<string> paths,
            string userDesktopPath,
            string commonDesktopPath)
    {
        ArgumentNullException.ThrowIfNull(paths);
        string[] snapshot = new string[paths.Count];
        for (int index = 0;
             index < paths.Count;
             index++)
        {
            snapshot[index] =
                paths[index]
                ?? string.Empty;
        }

        return Task.Run(
            () =>
                Resolve(
                    snapshot,
                    userDesktopPath,
                    commonDesktopPath));
    }

    private DesktopDropPreflightResult Resolve(
        IReadOnlyList<string> paths,
        string userDesktopPath,
        string commonDesktopPath)
    {
        var candidates =
            new List<DesktopDropCandidate>();
        var seen =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
        int outsideDesktop = 0;
        int missingOrInvalid = 0;
        int skippedDuplicates = 0;

        foreach (string path in paths)
        {
            string fullPath;
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    missingOrInvalid++;
                    continue;
                }

                fullPath = _normalizePath(path);
            }
            catch
            {
                missingOrInvalid++;
                continue;
            }

            if (!seen.Add(fullPath))
            {
                skippedDuplicates++;
                continue;
            }
            bool exists;
            try
            {
                exists =
                    _exists(fullPath);
            }
            catch
            {
                missingOrInvalid++;
                continue;
            }
            if (!exists)
            {
                missingOrInvalid++;
                continue;
            }

            DesktopDropLocation location =
                DesktopDropPolicy.Classify(
                    fullPath,
                    userDesktopPath,
                    commonDesktopPath);
            if (location
                == DesktopDropLocation
                    .OutsideDesktop)
            {
                outsideDesktop++;
                continue;
            }

            candidates.Add(
                new DesktopDropCandidate(
                    fullPath,
                    location));
        }

        return new DesktopDropPreflightResult(
            candidates,
            outsideDesktop,
            missingOrInvalid,
            skippedDuplicates);
    }

    private static string NormalizePath(
        string path) =>
        Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(path));
}
