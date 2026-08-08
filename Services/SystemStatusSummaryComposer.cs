using System;

namespace FocusPanel.Services;

internal static class SystemStatusSummaryComposer
{
    internal static string ComposeNetwork(
        bool isNetworkAvailable,
        string? networkDisplayName)
    {
        if (!isNetworkAvailable)
            return "网络未连接";

        return string.IsNullOrWhiteSpace(networkDisplayName)
            ? "网络已连接"
            : $"网络 {networkDisplayName.Trim()}";
    }

    internal static string Compose(
        string networkSummary,
        string audioSummary,
        string? batterySummary,
        int unreadPanelNotificationCount = 0)
    {
        string network = string.IsNullOrWhiteSpace(
            networkSummary)
            ? "网络状态未知"
            : networkSummary.Trim();
        string audio = string.IsNullOrWhiteSpace(
            audioSummary)
            ? "音频状态未知"
            : audioSummary.Trim();
        string summary = string.IsNullOrWhiteSpace(batterySummary)
            ? $"{network} · {audio}"
            : $"{network} · {audio} · {batterySummary.Trim()}";
        return unreadPanelNotificationCount > 0
            ? $"{summary} · Panel 通知 "
              + $"{unreadPanelNotificationCount} 条未读"
            : summary;
    }
}
