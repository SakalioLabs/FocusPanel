using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TrayOverflowTargetSelectorTests
{
    [Fact]
    public void NamedOverflowButton_WinsOverOtherTrayItems()
    {
        var nodes = new[]
        {
            new TrayAutomationNode(
                "任务栏输入指示 中文模式",
                "SystemTrayIcon",
                "SystemTray.NormalButton",
                true),
            new TrayAutomationNode(
                "同步工具",
                "NotifyItemIcon",
                "SystemTray.NormalButton",
                true),
            new TrayAutomationNode(
                "显示隐藏的图标",
                "SystemTrayIcon",
                "SystemTray.NormalButton",
                true)
        };

        Assert.Equal(
            2,
            TrayOverflowTargetSelector
                .FindBestCandidate(nodes));
    }

    [Fact]
    public void AccessibleShape_SupportsUnknownLanguage()
    {
        var nodes = new[]
        {
            new TrayAutomationNode(
                "本地化文本",
                "SystemTrayIcon",
                "SystemTray.NormalButton",
                true),
            new TrayAutomationNode(
                "Network",
                "SystemTrayIcon",
                "SystemTray.AccentButton",
                true)
        };

        Assert.Equal(
            0,
            TrayOverflowTargetSelector
                .FindBestCandidate(nodes));
    }

    [Fact]
    public void RejectsSystemIndicatorsAndUnavailableNodes()
    {
        var nodes = new[]
        {
            new TrayAutomationNode(
                "Input indicator English",
                "SystemTrayIcon",
                "SystemTray.NormalButton",
                true),
            new TrayAutomationNode(
                "Show hidden icons",
                "SystemTrayIcon",
                "SystemTray.NormalButton",
                false),
            new TrayAutomationNode(
                "Volume",
                "SystemTrayIcon",
                "SystemTray.NormalButton",
                true)
        };

        Assert.Equal(
            -1,
            TrayOverflowTargetSelector
                .FindBestCandidate(nodes));
    }

    [Fact]
    public void UnknownLanguage_DoesNotGuessBetweenMultipleButtons()
    {
        var nodes = new[]
        {
            new TrayAutomationNode(
                "本地化文本一",
                "SystemTrayIcon",
                "SystemTray.NormalButton",
                true),
            new TrayAutomationNode(
                "本地化文本二",
                "SystemTrayIcon",
                "SystemTray.NormalButton",
                true)
        };

        Assert.Equal(
            -1,
            TrayOverflowTargetSelector
                .FindBestCandidate(nodes));
    }
}
