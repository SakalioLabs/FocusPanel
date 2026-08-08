using System;
using System.Collections.Generic;

namespace FocusPanel.Services;

internal static class MessageOnlyWindowEnumerator
{
    internal const int DefaultLimit = 4096;

    internal static IReadOnlyList<IntPtr> Enumerate(
        Func<IntPtr, IntPtr> findNext,
        int limit = DefaultLimit)
    {
        ArgumentNullException.ThrowIfNull(findNext);
        if (limit <= 0)
            return Array.Empty<IntPtr>();

        var windows = new List<IntPtr>();
        var seen = new HashSet<IntPtr>();
        IntPtr previous = IntPtr.Zero;
        for (int index = 0;
             index < limit;
             index++)
        {
            IntPtr next = findNext(previous);
            if (next == IntPtr.Zero
                || !seen.Add(next))
            {
                break;
            }

            windows.Add(next);
            previous = next;
        }

        return windows;
    }
}
