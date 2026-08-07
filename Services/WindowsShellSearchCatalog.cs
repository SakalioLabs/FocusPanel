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
                WindowsShellAction.Notifications,
                "通知中心",
                "\uE7F4",
                "通知 消息 notification center Win N"),
            new WindowsShellSearchEntry(
                WindowsShellAction.Widgets,
                "小组件",
                "\uECA5",
                "资讯 天气 widgets Win W"),
            new WindowsShellSearchEntry(
                WindowsShellAction.SoundOutput,
                "声音输出",
                "\uE767",
                "声音设备 输出设备 音量混合器 sound output audio device volume mixer Win Ctrl V"),
            new WindowsShellSearchEntry(
                WindowsShellAction.ScreenSnipping,
                "屏幕截图",
                "\uE7C4",
                "截图 截屏 屏幕剪辑 snip screenshot screen capture Win Shift S"),
            new WindowsShellSearchEntry(
                WindowsShellAction.ProjectDisplay,
                "投影到其他屏幕",
                "\uE7F4",
                "投影 多屏 扩展屏 复制屏幕 project display monitor Win P"),
            new WindowsShellSearchEntry(
                WindowsShellAction.CastDevices,
                "连接无线显示器",
                "\uE704",
                "投屏 无线显示器 连接设备 cast connect wireless display Win K"),
            new WindowsShellSearchEntry(
                WindowsShellAction.ShowDesktop,
                "显示桌面",
                "\uE18A",
                "最小化窗口 查看桌面 show desktop Win D"),
            new WindowsShellSearchEntry(
                WindowsShellAction
                    .MediaPreviousTrack,
                "上一首",
                "\uE892",
                "上一曲 前一首 previous track prev track media previous"),
            new WindowsShellSearchEntry(
                WindowsShellAction
                    .MediaPlayPause,
                "播放 / 暂停",
                "\uE768",
                "播放 暂停 音乐 play pause media play media pause"),
            new WindowsShellSearchEntry(
                WindowsShellAction
                    .MediaNextTrack,
                "下一首",
                "\uE893",
                "下一曲 后一首 next track media next")
        };
}
