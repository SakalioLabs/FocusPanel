using FocusPanel.Models;

namespace FocusPanel.Services;

internal readonly record struct UpdateAvailabilityState(
    bool IsAvailable,
    string Version);

internal static class UpdateAvailabilityPolicy
{
    internal static UpdateAvailabilityState FromUpdate(
        AppUpdateInfo? update)
    {
        if (update == null
            || string.IsNullOrWhiteSpace(update.Version))
        {
            return new UpdateAvailabilityState(
                IsAvailable: false,
                Version: string.Empty);
        }

        return new UpdateAvailabilityState(
            IsAvailable: true,
            Version: update.Version.Trim());
    }
}
