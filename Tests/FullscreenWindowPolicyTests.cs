using System.Drawing;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class FullscreenWindowPolicyTests
{
    private static readonly Rectangle ScreenBounds = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    public void WindowsShellSurface_IsNeverTreatedAsFullscreenApplication(string windowClass)
    {
        bool result = FullscreenWindowPolicy.IsFullscreenApplication(
            windowClass,
            processId: 100,
            currentProcessId: 200,
            ScreenBounds,
            ScreenBounds,
            isMaximized: false,
            hasStandardFrame: false);

        Assert.False(result);
    }

    [Fact]
    public void CurrentProcessWindow_IsNeverTreatedAsFullscreenApplication()
    {
        bool result = FullscreenWindowPolicy.IsFullscreenApplication(
            "HwndWrapper[FocusPanel]",
            processId: 100,
            currentProcessId: 100,
            ScreenBounds,
            ScreenBounds,
            isMaximized: false,
            hasStandardFrame: false);

        Assert.False(result);
    }

    [Fact]
    public void RealApplicationCoveringScreen_IsTreatedAsFullscreen()
    {
        bool result = FullscreenWindowPolicy.IsFullscreenApplication(
            "Chrome_WidgetWin_1",
            processId: 100,
            currentProcessId: 200,
            ScreenBounds,
            ScreenBounds,
            isMaximized: false,
            hasStandardFrame: false);

        Assert.True(result);
    }

    [Fact]
    public void MaximizedApplicationLeavingTaskbarVisible_IsNotTreatedAsFullscreen()
    {
        var workAreaWindow = new Rectangle(0, 0, 1920, 1040);

        bool result = FullscreenWindowPolicy.IsFullscreenApplication(
            "Chrome_WidgetWin_1",
            processId: 100,
            currentProcessId: 200,
            workAreaWindow,
            ScreenBounds,
            isMaximized: true,
            hasStandardFrame: true);

        Assert.False(result);
    }

    [Fact]
    public void MaximizedFramedApplicationCoveringScreen_IsNotTreatedAsFullscreen()
    {
        bool result = FullscreenWindowPolicy.IsFullscreenApplication(
            "Chrome_WidgetWin_1",
            processId: 100,
            currentProcessId: 200,
            ScreenBounds,
            ScreenBounds,
            isMaximized: true,
            hasStandardFrame: true);

        Assert.False(result);
    }

    [Fact]
    public void BorderlessApplicationCoveringScreen_IsTreatedAsFullscreen()
    {
        bool result = FullscreenWindowPolicy.IsFullscreenApplication(
            "UnrealWindow",
            processId: 100,
            currentProcessId: 200,
            ScreenBounds,
            ScreenBounds,
            isMaximized: false,
            hasStandardFrame: false);

        Assert.True(result);
    }
}
