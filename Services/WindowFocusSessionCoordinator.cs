using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal sealed record WindowFocusStartResult(
    int MinimizedCount,
    int FailedCount,
    int PreviousRestoreFailedCount,
    bool Started)
{
    internal bool BlockedByPreviousSession =>
        PreviousRestoreFailedCount > 0;
}

internal sealed record WindowFocusRestoreResult(
    int RestoredCount,
    int FailedCount,
    int RemovedCount,
    int RemainingCount);

internal sealed class WindowFocusSessionCoordinator
{
    private readonly Dictionary<IntPtr,
        WindowFocusSessionEntry> _entries = new();
    private string _targetLabel = string.Empty;

    internal bool HasActiveSession =>
        _entries.Count > 0;

    internal int HiddenWindowCount =>
        _entries.Count;

    internal string Summary =>
        HasActiveSession
            ? $"窗口专注中 · {_targetLabel} · "
              + $"已收起 {_entries.Count} 个窗口"
            : string.Empty;

    internal WindowFocusStartResult Start(
        IEnumerable<WindowReference>?
            currentWindows,
        IReadOnlyCollection<IntPtr> keepHandles,
        string targetLabel,
        Func<IntPtr, bool> minimize,
        Func<IntPtr, bool> restore,
        Func<IntPtr, bool> maximize)
    {
        ArgumentNullException.ThrowIfNull(
            keepHandles);
        ArgumentNullException.ThrowIfNull(
            minimize);
        ArgumentNullException.ThrowIfNull(
            restore);
        ArgumentNullException.ThrowIfNull(
            maximize);
        WindowReference[] windows =
            NormalizeWindows(currentWindows);
        IReadOnlyDictionary<IntPtr,
            TrackedWindowState> previousStates =
            _entries.Values.ToDictionary(
                entry => entry.Handle,
                entry => entry.OriginalState);
        WindowFocusRestoreResult previous =
            RestoreCore(
                windows,
                restore,
                maximize);
        if (previous.RemainingCount > 0)
        {
            return new WindowFocusStartResult(
                0,
                0,
                previous.FailedCount,
                false);
        }

        if (previous.RestoredCount > 0)
        {
            windows = windows
                .Select(window =>
                    previousStates.TryGetValue(
                        window.Handle,
                        out TrackedWindowState state)
                    && window.State
                        == TrackedWindowState.Minimized
                        ? window with
                        {
                            State = state
                        }
                        : window)
                .ToArray();
        }

        var keep = new HashSet<IntPtr>(
            keepHandles.Where(handle =>
                handle != IntPtr.Zero));
        int minimized = 0;
        int failed = 0;
        int order = 0;
        foreach (WindowReference window
                 in windows)
        {
            if (keep.Contains(window.Handle)
                || window.State
                    == TrackedWindowState
                        .Minimized)
            {
                continue;
            }

            if (!SystemActionExecution.Try(
                    () => minimize(
                        window.Handle)))
            {
                failed++;
                continue;
            }

            _entries[window.Handle] =
                new WindowFocusSessionEntry(
                    window.Handle,
                    window.State,
                    window.IsActive,
                    order++);
            minimized++;
        }

        _targetLabel = minimized > 0
            ? NormalizeTargetLabel(targetLabel)
            : string.Empty;
        return new WindowFocusStartResult(
            minimized,
            failed,
            0,
            minimized > 0);
    }

    internal WindowFocusRestoreResult Restore(
        IEnumerable<WindowReference>?
            currentWindows,
        Func<IntPtr, bool> restore,
        Func<IntPtr, bool> maximize)
    {
        ArgumentNullException.ThrowIfNull(
            restore);
        ArgumentNullException.ThrowIfNull(
            maximize);
        return RestoreCore(
            NormalizeWindows(currentWindows),
            restore,
            maximize);
    }

    internal void Reconcile(
        IEnumerable<WindowReference>?
            currentWindows)
    {
        IReadOnlyDictionary<IntPtr,
            WindowReference> current =
            NormalizeWindows(currentWindows)
                .ToDictionary(window =>
                    window.Handle);
        foreach (IntPtr handle
                 in _entries.Keys.ToArray())
        {
            if (!current.TryGetValue(
                    handle,
                    out WindowReference? window)
                || window.State
                    != TrackedWindowState.Minimized)
            {
                _entries.Remove(handle);
            }
        }

        ClearLabelWhenEmpty();
    }

    private WindowFocusRestoreResult RestoreCore(
        IReadOnlyCollection<WindowReference>
            currentWindows,
        Func<IntPtr, bool> restore,
        Func<IntPtr, bool> maximize)
    {
        if (_entries.Count == 0)
        {
            return new WindowFocusRestoreResult(
                0,
                0,
                0,
                0);
        }

        IReadOnlyDictionary<IntPtr,
            WindowReference> current =
            currentWindows.ToDictionary(window =>
                window.Handle);
        int restored = 0;
        int failed = 0;
        int removed = 0;
        WindowFocusSessionEntry[] entries =
            _entries.Values
                .OrderBy(entry =>
                    entry.WasActive)
                .ThenBy(entry =>
                    entry.OrderIndex)
                .ToArray();
        foreach (WindowFocusSessionEntry entry
                 in entries)
        {
            if (!current.TryGetValue(
                    entry.Handle,
                    out WindowReference? window)
                || window.State
                    != TrackedWindowState.Minimized)
            {
                _entries.Remove(entry.Handle);
                removed++;
                continue;
            }

            bool succeeded =
                SystemActionExecution.Try(
                    () => entry.OriginalState
                        == TrackedWindowState
                            .Maximized
                            ? maximize(entry.Handle)
                            : restore(entry.Handle));
            if (succeeded)
            {
                _entries.Remove(entry.Handle);
                restored++;
            }
            else
            {
                failed++;
            }
        }

        ClearLabelWhenEmpty();
        return new WindowFocusRestoreResult(
            restored,
            failed,
            removed,
            _entries.Count);
    }

    private static WindowReference[]
        NormalizeWindows(
        IEnumerable<WindowReference>?
            windows) =>
        windows?
            .Where(window =>
                window != null
                && window.Handle != IntPtr.Zero)
            .GroupBy(window =>
                window.Handle)
            .Select(group => group.First())
            .ToArray()
        ?? Array.Empty<WindowReference>();

    private static string NormalizeTargetLabel(
        string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "当前窗口"
            : value.Trim();

    private void ClearLabelWhenEmpty()
    {
        if (_entries.Count == 0)
            _targetLabel = string.Empty;
    }

    private sealed record WindowFocusSessionEntry(
        IntPtr Handle,
        TrackedWindowState OriginalState,
        bool WasActive,
        int OrderIndex);
}
