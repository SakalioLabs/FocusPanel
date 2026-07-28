using System;

namespace FocusPanel.Services;

public static class AppSearchSelectionPolicy
{
    public static int Move(int itemCount, int currentIndex, int direction)
    {
        if (itemCount <= 0)
            return -1;

        if (currentIndex < 0 || currentIndex >= itemCount)
            return direction < 0 ? itemCount - 1 : 0;

        return Math.Clamp(currentIndex + Math.Sign(direction), 0, itemCount - 1);
    }

    public static int ResolveLaunchIndex(int itemCount, int selectedIndex)
        => itemCount <= 0
            ? -1
            : Math.Clamp(selectedIndex, 0, itemCount - 1);
}
