namespace FocusPanel.Services;

internal enum VirtualDesktopWheelAction
{
    Ignore,
    Throttled,
    Previous,
    Next
}

internal static class VirtualDesktopWheelPolicy
{
    internal const int
        DefaultThrottleMilliseconds = 160;

    internal static VirtualDesktopWheelAction
        GetAction(
            int delta,
            long lastActionTick,
            long currentTick,
            int throttleMilliseconds =
                DefaultThrottleMilliseconds)
    {
        if (delta == 0)
            return VirtualDesktopWheelAction.Ignore;

        if (lastActionTick >= 0
            && currentTick >= lastActionTick
            && currentTick - lastActionTick
                < throttleMilliseconds)
        {
            return VirtualDesktopWheelAction
                .Throttled;
        }

        return delta > 0
            ? VirtualDesktopWheelAction.Previous
            : VirtualDesktopWheelAction.Next;
    }
}
