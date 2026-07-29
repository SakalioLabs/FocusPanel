using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskbarHoverPreviewPolicyTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void RunningAppUnderPointer_OpensActionablePreview(
        int windowCount)
    {
        Assert.True(
            TaskbarHoverPreviewPolicy.ShouldOpen(
                windowCount,
                isPointerOver: true,
                isMouseButtonPressed: false,
                hasOpenMenu: false));
    }

    [Theory]
    [InlineData(0, true, false, false)]
    [InlineData(1, false, false, false)]
    [InlineData(1, true, true, false)]
    [InlineData(1, true, false, true)]
    public void UnsafeOrIrrelevantHover_DoesNotOpenPreview(
        int windowCount,
        bool isPointerOver,
        bool isMouseButtonPressed,
        bool hasOpenMenu)
    {
        Assert.False(
            TaskbarHoverPreviewPolicy.ShouldOpen(
                windowCount,
                isPointerOver,
                isMouseButtonPressed,
                hasOpenMenu));
    }
}
