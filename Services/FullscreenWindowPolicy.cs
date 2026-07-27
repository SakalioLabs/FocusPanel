using System;
using System.Drawing;

namespace FocusPanel.Services;

internal static class FullscreenWindowPolicy
{
    public static bool IsFullscreenApplication(
        string windowClass,
        uint processId,
        uint currentProcessId,
        Rectangle windowBounds,
        Rectangle screenBounds,
        bool isMaximized,
        bool hasStandardFrame)
    {
        if (processId == currentProcessId || IsShellSurface(windowClass))
            return false;

        bool coversScreen =
            Math.Abs(windowBounds.Left - screenBounds.Left) <= 1
            && Math.Abs(windowBounds.Top - screenBounds.Top) <= 1
            && Math.Abs(windowBounds.Right - screenBounds.Right) <= 1
            && Math.Abs(windowBounds.Bottom - screenBounds.Bottom) <= 1;
        if (!coversScreen)
            return false;

        // Hiding the native taskbar lets ordinary maximized windows cover the
        // complete monitor. They must keep the edge hot zone available.
        return !(isMaximized && hasStandardFrame);
    }

    private static bool IsShellSurface(string windowClass)
    {
        return windowClass is
            "Progman"
            or "WorkerW"
            or "Shell_TrayWnd"
            or "Shell_SecondaryTrayWnd";
    }
}
