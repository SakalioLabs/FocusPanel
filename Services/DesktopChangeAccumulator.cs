using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusPanel.Services;

internal sealed record DesktopChangeBatch(
    bool RequiresFullRefresh,
    IReadOnlyList<string> Paths)
{
    internal bool IsEmpty =>
        !RequiresFullRefresh && Paths.Count == 0;
}

internal sealed class DesktopChangeAccumulator
{
    private readonly object _gate = new();
    private readonly HashSet<string> _paths =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _requiresFullRefresh;

    internal void AddPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        lock (_gate)
        {
            if (!_requiresFullRefresh)
                _paths.Add(path);
        }
    }

    internal void RequireFullRefresh()
    {
        lock (_gate)
        {
            _requiresFullRefresh = true;
            _paths.Clear();
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
                    .ToArray());
            _requiresFullRefresh = false;
            _paths.Clear();
            return batch;
        }
    }
}
