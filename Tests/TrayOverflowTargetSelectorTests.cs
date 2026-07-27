using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TrayOverflowTargetSelectorTests
{
    [Fact]
    public void FindBestCandidate_PrefersNamedOverflowButton()
    {
        var nodes = new[]
        {
            new TrayAutomationNode("任务栏输入指示 中文模式", "SystemTrayIcon", "SystemTray.NormalButton", true),
            new TrayAutomationNode("飞书", "NotifyItemIcon", "SystemTray.NormalButton", true),
            new TrayAutomationNode("显示隐藏的图标", "SystemTrayIcon", "SystemTray.NormalButton", true)
        };

        Assert.Equal(2, TrayOverflowTargetSelector.FindBestCandidate(nodes));
    }

    [Fact]
    public void FindBestCandidate_UsesAccessibleShapeForUnknownLanguage()
    {
        var nodes = new[]
        {
            new TrayAutomationNode("某种本地化文本", "SystemTrayIcon", "SystemTray.NormalButton", true),
            new TrayAutomationNode("Network", "SystemTrayIcon", "SystemTray.AccentButton", true)
        };

        Assert.Equal(0, TrayOverflowTargetSelector.FindBestCandidate(nodes));
    }

    [Fact]
    public void FindBestCandidate_RejectsInputIndicatorAndNonInvokableNodes()
    {
        var nodes = new[]
        {
            new TrayAutomationNode("Input indicator English", "SystemTrayIcon", "SystemTray.NormalButton", true),
            new TrayAutomationNode("Show hidden icons", "SystemTrayIcon", "SystemTray.NormalButton", false)
        };

        Assert.Equal(-1, TrayOverflowTargetSelector.FindBestCandidate(nodes));
    }
}
