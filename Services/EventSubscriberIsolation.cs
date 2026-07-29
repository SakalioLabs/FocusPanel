using System;

namespace FocusPanel.Services;

internal static class EventSubscriberIsolation
{
    internal static int Publish(
        EventHandler? subscribers,
        object sender,
        Action<Exception>? onFailure = null)
    {
        if (subscribers == null)
            return 0;

        int failureCount = 0;
        foreach (EventHandler subscriber in
                 subscribers.GetInvocationList())
        {
            try
            {
                subscriber(
                    sender,
                    EventArgs.Empty);
            }
            catch (Exception ex)
            {
                failureCount++;
                try
                {
                    onFailure?.Invoke(ex);
                }
                catch
                {
                    // Diagnostics must not break the remaining subscribers.
                }
            }
        }

        return failureCount;
    }
}
