using System;
using System.Collections.Generic;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal static class TaskbarWindowCyclePolicy
{
    internal static WindowReference? SelectTarget(
        IReadOnlyList<WindowReference> windows,
        int wheelDelta,
        IntPtr preferredCurrentHandle = default)
    {
        if (windows.Count < 2
            || wheelDelta == 0)
        {
            return null;
        }

        int currentIndex = -1;
        if (preferredCurrentHandle
            != IntPtr.Zero)
        {
            for (int index = 0;
                 index < windows.Count;
                 index++)
            {
                if (windows[index].Handle
                    == preferredCurrentHandle)
                {
                    currentIndex = index;
                    break;
                }
            }
        }

        if (currentIndex < 0)
        {
            for (int index = 0;
                 index < windows.Count;
                 index++)
            {
                if (windows[index].IsActive)
                {
                    currentIndex = index;
                    break;
                }
            }
        }

        if (currentIndex < 0)
        {
            return wheelDelta > 0
                ? windows[^1]
                : windows[0];
        }

        int step =
            wheelDelta > 0 ? -1 : 1;
        int targetIndex =
            (currentIndex
             + step
             + windows.Count)
            % windows.Count;
        return windows[targetIndex];
    }
}
