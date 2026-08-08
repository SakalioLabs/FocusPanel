using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusPanel.Services;

internal sealed class WindowAttentionState
{
    private readonly object _gate = new();
    private readonly HashSet<IntPtr> _requested = new();

    internal bool Observe(
        uint eventType,
        IntPtr window,
        IntPtr foregroundWindow)
    {
        if (window == IntPtr.Zero)
            return false;

        lock (_gate)
        {
            if (eventType
                == WindowTrackingEventPolicy
                    .EventSystemForeground)
            {
                return _requested.Remove(window);
            }

            if (eventType
                    != WindowTrackingEventPolicy
                        .EventSystemAlert
                || window == foregroundWindow)
            {
                return false;
            }

            return _requested.Add(window);
        }
    }

    internal bool IsRequested(IntPtr window)
    {
        lock (_gate)
            return _requested.Contains(window);
    }

    internal bool Clear(IntPtr window)
    {
        if (window == IntPtr.Zero)
            return false;

        lock (_gate)
            return _requested.Remove(window);
    }

    internal void Retain(
        IEnumerable<IntPtr> liveWindows)
    {
        ArgumentNullException.ThrowIfNull(
            liveWindows);
        var live = new HashSet<IntPtr>(
            liveWindows.Where(
                window => window != IntPtr.Zero));
        lock (_gate)
        {
            _requested.RemoveWhere(
                window => !live.Contains(window));
        }
    }

    internal void ClearAll()
    {
        lock (_gate)
            _requested.Clear();
    }
}
