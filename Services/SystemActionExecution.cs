using System;

namespace FocusPanel.Services;

internal static class SystemActionExecution
{
    internal static bool Try(Func<bool> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            return action();
        }
        catch
        {
            return false;
        }
    }

    internal static bool TryStart(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            action();
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static bool TryWithFallback(
        Func<bool> primary,
        Func<bool> fallback)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(fallback);
        return Try(primary) || Try(fallback);
    }
}
