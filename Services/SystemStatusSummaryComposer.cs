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
        string? batterySummary)
    {
        string network = string.IsNullOrWhiteSpace(
            networkSummary)
            ? "网络状态未知"
            : networkSummary.Trim();
        string audio = string.IsNullOrWhiteSpace(
            audioSummary)
            ? "音频状态未知"
            : audioSummary.Trim();
        return string.IsNullOrWhiteSpace(batterySummary)
            ? $"{network} · {audio}"
            : $"{network} · {audio} · {batterySummary.Trim()}";
    }
}
