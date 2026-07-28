using System;
using System.Collections.Generic;

namespace FocusPanel.Services;

public sealed class FocusToastQueue
{
    private readonly List<FocusToastNotification> _pending = new();

    public FocusToastNotification? Current { get; private set; }

    public int PendingCount => _pending.Count;

    public bool Enqueue(FocusToastNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (Current == null)
        {
            Current = notification;
            return true;
        }

        if (HasSameKey(Current, notification))
            return false;

        int existingIndex = _pending.FindIndex(
            item => HasSameKey(item, notification));
        if (existingIndex >= 0)
            _pending[existingIndex] = notification;
        else
            _pending.Add(notification);

        return false;
    }

    public FocusToastNotification? CompleteCurrent()
    {
        if (_pending.Count == 0)
        {
            Current = null;
            return null;
        }

        Current = _pending[0];
        _pending.RemoveAt(0);
        return Current;
    }

    public void Clear()
    {
        Current = null;
        _pending.Clear();
    }

    private static bool HasSameKey(
        FocusToastNotification left,
        FocusToastNotification right) =>
        string.Equals(
            left.Key,
            right.Key,
            StringComparison.OrdinalIgnoreCase);
}
