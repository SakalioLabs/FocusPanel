using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusPanel.Services;

internal sealed record DesktopChangeBatch(
    bool RequiresFullRefresh,
    IReadOnlyList<string> Paths,
    IReadOnlyList<string> CreatedPaths)
{
    internal bool IsEmpty =>
        !RequiresFullRefresh
        && Paths.Count == 0
        && CreatedPaths.Count == 0;
}

internal sealed class DesktopChangeAccumulator
{
    private readonly object _gate = new();
    private readonly HashSet<string> _paths =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _createdPaths =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _requiresFullRefresh;

    internal void AddPath(
        string? path,
        bool isCreated = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        lock (_gate)
        {
            if (!_requiresFullRefresh)
            {
                _paths.Add(path);
                if (isCreated)
                    _createdPaths.Add(path);
            }
        }
    }

    internal void RenamePath(
        string? oldPath,
        string? newPath)
    {
        if (string.IsNullOrWhiteSpace(oldPath)
            || string.IsNullOrWhiteSpace(newPath))
        {
            return;
        }

        lock (_gate)
        {
            if (_requiresFullRefresh)
                return;

            _paths.Add(oldPath);
            _paths.Add(newPath);
            if (_createdPaths.Remove(oldPath))
                _createdPaths.Add(newPath);
        }
    }

    internal void RequireFullRefresh()
    {
        lock (_gate)
        {
            _requiresFullRefresh = true;
            _paths.Clear();
            _createdPaths.Clear();
        }
    }

    internal DesktopChangeBatch Take()
    {
        lock (_gate)
        {
            var batch = new DesktopChangeBatch(
                _requiresFullRefresh,
                _paths
                    .OrderBy(
                        path => path,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                _createdPaths
                    .OrderBy(
                        path => path,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray());
            _requiresFullRefresh = false;
            _paths.Clear();
            _createdPaths.Clear();
            return batch;
        }
    }
}
