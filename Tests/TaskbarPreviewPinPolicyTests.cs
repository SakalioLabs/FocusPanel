using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskbarPreviewPinPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ZeroOrSingleWindow_KeepsNormalTaskbarAction(
        int windowCount)
    {
        Assert.Equal(
            TaskbarPreviewClickAction
                .ActivateOrLaunch,
            TaskbarPreviewPinPolicy.Resolve(
                windowCount,
                isSamePreviewVisible: false,
                isPreviewPinned: false));
    }

    [Fact]
    public void MultiWindowWithoutMatchingPreview_OpensPinnedPreview()
    {
        Assert.Equal(
            TaskbarPreviewClickAction
                .OpenPinnedPreview,
            TaskbarPreviewPinPolicy.Resolve(
                windowCount: 2,
                isSamePreviewVisible: false,
                isPreviewPinned: false));
    }

    [Fact]
    public void ClickingExistingHoverPreview_PinsItWithoutReopening()
    {
        Assert.Equal(
            TaskbarPreviewClickAction
                .PinExistingPreview,
            TaskbarPreviewPinPolicy.Resolve(
                windowCount: 3,
                isSamePreviewVisible: true,
                isPreviewPinned: false));
    }

    [Fact]
    public void ClickingSamePinnedPreview_ClosesOnlyPreview()
    {
        Assert.Equal(
            TaskbarPreviewClickAction
                .ClosePinnedPreview,
            TaskbarPreviewPinPolicy.Resolve(
                windowCount: 4,
                isSamePreviewVisible: true,
                isPreviewPinned: true));
    }
}
