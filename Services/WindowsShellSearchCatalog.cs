using System.Collections.Generic;

namespace FocusPanel.Services;

internal readonly record struct WindowsShellSearchEntry(
    WindowsShellAction Action,
    string DisplayName,
    string Glyph,
    string Aliases);

internal static class WindowsShellSearchCatalog
{
    internal static IReadOnlyList<
        WindowsShellSearchEntry> All { get; } =
        new[]
        {
            new WindowsShellSearchEntry(
                WindowsShellAction.RunDialog,
                "运行",
                "\uE7B8",
                "运行命令 打开程序 run dialog Win R"),
            new WindowsShellSearchEntry(
                WindowsShellAction.QuickSettings,
                "快捷设置",
                "\uE713",
                "音量 网络 WiFi 蓝牙 quick settings Win A"),
            new WindowsShellSearchEntry(
                WindowsShellAction.Notifications,
                "通知中心",
                "\uE7F4",
                "通知 消息 notification center Win N"),
            new WindowsShellSearchEntry(
                WindowsShellAction.InputSwitcher,
                "切换输入法",
                "\uE765",
                "语言 键盘 输入法 input language keyboard Win Space"),
            new WindowsShellSearchEntry(
                WindowsShellAction.TaskView,
                "任务视图",
                "\uE7C4",
                "窗口总览 虚拟桌面 task view Win Tab"),
            new WindowsShellSearchEntry(
                WindowsShellAction.Widgets,
                "小组件",
                "\uECA5",
                "资讯 天气 widgets Win W"),
            new WindowsShellSearchEntry(
                WindowsShellAction.ShowDesktop,
                "显示桌面",
                "\uE18A",
                "最小化窗口 查看桌面 show desktop Win D")
        };
}
