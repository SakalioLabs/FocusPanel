using System.Collections.Generic;

namespace FocusPanel.Services;

internal readonly record struct PanelStatusSearchEntry(
    StatusCenterDetail Detail,
    string DisplayName,
    string Glyph,
    string Aliases);

internal static class PanelStatusSearchCatalog
{
    internal static IReadOnlyList<PanelStatusSearchEntry>
        All { get; } =
        new[]
        {
            new PanelStatusSearchEntry(
                StatusCenterDetail.Network,
                "网络与无线",
                "\uE701",
                "快捷设置 网络 WiFi 无线 蓝牙 蓝牙开关 quick settings network bluetooth Win A"),
            new PanelStatusSearchEntry(
                StatusCenterDetail.ApplicationAudio,
                "应用音量",
                "\uE767",
                "音量 音量混合器 应用声音 app volume mixer audio"),
            new PanelStatusSearchEntry(
                StatusCenterDetail.MediaAndBattery,
                "媒体与电池",
                "\uE8D6",
                "电池 媒体 播放 暂停 上一首 下一首 充电 battery media power"),
            new PanelStatusSearchEntry(
                StatusCenterDetail.InputMethod,
                "输入法",
                "\uE765",
                "切换输入法 语言 键盘 拼音 五笔 input language keyboard Win Space"),
            new PanelStatusSearchEntry(
                StatusCenterDetail.PanelNotifications,
                "Panel 通知",
                "\uE7E7",
                "消息 通知 更新 专注 完成 恢复 notification history")
        };
}
