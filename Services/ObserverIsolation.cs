using System;

namespace FocusPanel.Services;

internal static class ObserverIsolation
{
    internal static void Notify(
        Action? observers,
        Action<Exception>? onError = null)
    {
        if (observers == null)
            return;

        foreach (Delegate observer
                 in observers.GetInvocationList())
        {
            try
            {
                ((Action)observer)();
            }
            catch (Exception ex)
            {
                try
                {
                    onError?.Invoke(ex);
                }
                catch
                {
                    // Diagnostics cannot break remaining observers.
                }
            }
        }
    }
}
