using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal sealed record TaskbarContextWindowSlice(
    IReadOnlyList<WindowReference> Windows,
    int TotalCount)
{
    internal int HiddenCount =>
        Math.Max(
            0,
            TotalCount - Windows.Count);

    internal bool HasHiddenWindows =>
        HiddenCount > 0;
}

internal static class
    TaskbarContextWindowPolicy
{
    internal const int MaximumVisibleWindows = 4;

    internal static TaskbarContextWindowSlice
        Select(
            IReadOnlyList<WindowReference>
                windows,
            int maximumVisible =
                MaximumVisibleWindows)
    {
        ArgumentNullException.ThrowIfNull(
            windows);
        if (maximumVisible <= 0)
        {
            throw new
                ArgumentOutOfRangeException(
                    nameof(maximumVisible));
        }

        WindowReference[] unique = windows
            .Where(window =>
                window.Handle != IntPtr.Zero)
            .DistinctBy(window =>
                window.Handle)
            .ToArray();
        if (unique.Length <= maximumVisible)
        {
            return new
                TaskbarContextWindowSlice(
                    unique,
                    unique.Length);
        }

        var selected =
            new List<WindowReference>(
                maximumVisible);
        WindowReference? active =
            unique.FirstOrDefault(window =>
                window.IsActive);
        if (active != null)
            selected.Add(active);

        foreach (WindowReference window
                 in unique)
        {
            if (selected.Count
                    >= maximumVisible)
            {
                break;
            }
            if (active != null
                && window.Handle
                    == active.Handle)
            {
                continue;
            }

            selected.Add(window);
        }

        return new TaskbarContextWindowSlice(
            selected,
            unique.Length);
    }
}
