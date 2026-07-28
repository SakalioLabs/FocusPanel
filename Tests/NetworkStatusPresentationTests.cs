using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class NetworkStatusPresentationTests
{
    [Fact]
    public void ConnectedNetwork_UsesWifiGlyphAndName()
    {
        NetworkStatusPresentation presentation =
            NetworkStatusPresentationComposer.Compose(
                true,
                NetworkConnectionKind.WiFi,
                "WLAN");

        Assert.Equal(
            NetworkStatusPresentationComposer.WiFiGlyph,
            presentation.Glyph);
        Assert.Equal("网络 WLAN", presentation.Summary);
    }

    [Fact]
    public void DisconnectedNetwork_UsesErrorGlyph()
    {
        NetworkStatusPresentation presentation =
            NetworkStatusPresentationComposer.Compose(
                false,
                NetworkConnectionKind.WiFi,
                "stale");

        Assert.Equal(
            NetworkStatusPresentationComposer.DisconnectedGlyph,
            presentation.Glyph);
        Assert.Equal(
            "网络未连接",
            presentation.Summary);
    }

    [Theory]
    [InlineData(
        NetworkConnectionKind.Ethernet,
        NetworkStatusPresentationComposer.EthernetGlyph)]
    [InlineData(
        NetworkConnectionKind.Other,
        NetworkStatusPresentationComposer.GenericNetworkGlyph)]
    [InlineData(
        NetworkConnectionKind.Unknown,
        NetworkStatusPresentationComposer.GenericNetworkGlyph)]
    public void ConnectedNonWifi_UsesMatchingGlyph(
        NetworkConnectionKind connectionKind,
        string expectedGlyph)
    {
        NetworkStatusPresentation presentation =
            NetworkStatusPresentationComposer.Compose(
                true,
                connectionKind,
                "连接");

        Assert.Equal(expectedGlyph, presentation.Glyph);
    }
}
