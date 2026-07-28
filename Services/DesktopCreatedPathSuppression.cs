using System;
using System.Collections.Generic;

namespace FocusPanel.Services;

internal sealed class DesktopCreatedPathSuppression
{
    private static readonly TimeSpan DefaultLifetime =
        TimeSpan.FromSeconds(15);

    private readonly object _gate = new();
    private readonly Dictionary<string, DateTimeOffset>
        _expirations =
            new(StringComparer.OrdinalIgnoreCase);

    internal void Suppress(
        string? path,
        DateTimeOffset now,
        TimeSpan? lifetime = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        lock (_gate)
        {
            RemoveExpired(now);
            _expirations[path] =
                now + (lifetime ?? DefaultLifetime);
        }
    }

    internal bool TryConsume(
        string? path,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        lock (_gate)
        {
            RemoveExpired(now);
            return _expirations.Remove(path);
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        var expired = new List<string>();
        foreach ((string path, DateTimeOffset expiration)
                 in _expirations)
        {
            if (expiration <= now)
                expired.Add(path);
        }

        foreach (string path in expired)
            _expirations.Remove(path);
    }
}

