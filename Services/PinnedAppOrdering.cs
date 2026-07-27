using System;
using System.Collections.Generic;

namespace FocusPanel.Services;

internal static class PinnedAppOrdering
{
    internal static void Move<T>(IList<T> items, T item, int newIndex)
    {
        int oldIndex = items.IndexOf(item);
        if (oldIndex < 0)
            return;

        items.RemoveAt(oldIndex);
        items.Insert(Math.Clamp(newIndex, 0, items.Count), item);
    }
}
