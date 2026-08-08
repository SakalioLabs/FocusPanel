namespace FocusPanel.Services;

internal static class
    WindowOverviewHotkeySelectionPolicy
{
    internal static int Select(
        int itemCount,
        int currentIndex,
        bool isRepeatedInvocation)
    {
        if (itemCount <= 0)
            return -1;

        if (!isRepeatedInvocation)
            return itemCount > 1 ? 1 : 0;

        if (currentIndex < 0
            || currentIndex >= itemCount)
        {
            return itemCount > 1 ? 1 : 0;
        }

        return (currentIndex + 1) % itemCount;
    }
}
