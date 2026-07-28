using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class SystemStatusSummaryComposerTests
{
    [Theory]
    [InlineData(false, "WLAN", "网络未连接")]
    [InlineData(true, "WLAN", "网络 WLAN")]
    [InlineData(true, "  以太网  ", "网络 以太网")]
    [InlineData(true, "", "网络已连接")]
    [InlineData(true, "   ", "网络已连接")]
    public void NetworkSummary_HandlesConnectionAndMissingName(
        bool isAvailable,
        string name,
        string expected)
    {
        Assert.Equal(
            expected,
            SystemStatusSummaryComposer.ComposeNetwork(
                isAvailable,
                name));
    }

    [Fact]
    public void DesktopSummary_OmitsMissingBattery()
    {
        Assert.Equal(
            "网络 以太网 · 音量 60%",
            SystemStatusSummaryComposer.Compose(
                "网络 以太网",
                "音量 60%",
                string.Empty));
    }

    [Fact]
    public void LaptopSummary_IncludesBattery()
    {
        Assert.Equal(
            "网络 WLAN · 已静音 · 电池 82% · 充电中",
            SystemStatusSummaryComposer.Compose(
                "网络 WLAN",
                "已静音",
                "电池 82% · 充电中"));
    }

    [Fact]
    public void MissingInputs_HaveExplicitFallbacks()
    {
        Assert.Equal(
            "网络状态未知 · 音频状态未知",
            SystemStatusSummaryComposer.Compose(
                string.Empty,
                " ",
                null));
    }
}
