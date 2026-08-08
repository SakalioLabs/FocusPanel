using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class StatusCenterEntryActionPolicyTests
{
    [Fact]
    public void Resolve_WithoutShiftTogglesStatusCenter()
    {
        Assert.Equal(
            StatusCenterEntryAction.ToggleStatusCenter,
            StatusCenterEntryActionPolicy.Resolve(
                shiftPressed: false));
    }

    [Fact]
    public void Resolve_WithShiftTogglesPanelNotifications()
    {
        Assert.Equal(
            StatusCenterEntryAction.TogglePanelNotifications,
            StatusCenterEntryActionPolicy.Resolve(
                shiftPressed: true));
    }
}
