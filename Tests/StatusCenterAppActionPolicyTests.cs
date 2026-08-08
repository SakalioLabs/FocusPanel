using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class StatusCenterAppActionPolicyTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void ZeroOrSingleWindow_UsesUnifiedPrimaryAction(
        int windowCount)
    {
        Assert.Equal(
            StatusCenterAppAction.ActivateOrLaunch,
            StatusCenterAppActionPolicy.Resolve(
                windowCount));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    public void MultipleWindows_ToggleInlineWindowList(
        int windowCount)
    {
        Assert.Equal(
            StatusCenterAppAction.ToggleWindowList,
            StatusCenterAppActionPolicy.Resolve(
                windowCount));
    }
}
