using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class UpdateAvailabilityPolicyTests
{
    [Fact]
    public void MissingUpdateClearsBadgeState()
    {
        UpdateAvailabilityState state =
            UpdateAvailabilityPolicy.FromUpdate(null);

        Assert.False(state.IsAvailable);
        Assert.Equal(string.Empty, state.Version);
    }

    [Fact]
    public void BlankVersionDoesNotCreateMisleadingBadge()
    {
        UpdateAvailabilityState state =
            UpdateAvailabilityPolicy.FromUpdate(
                new AppUpdateInfo("  ", null, 0));

        Assert.False(state.IsAvailable);
        Assert.Equal(string.Empty, state.Version);
    }

    [Fact]
    public void AvailableVersionIsNormalizedForDisplay()
    {
        UpdateAvailabilityState state =
            UpdateAvailabilityPolicy.FromUpdate(
                new AppUpdateInfo(" 0.9.32 ", "说明", 1024));

        Assert.True(state.IsAvailable);
        Assert.Equal("0.9.32", state.Version);
    }
}
