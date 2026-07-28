using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class NetworkStatusSnapshotTests
{
    [Fact]
    public void OfflineObservation_IgnoresStaleInterfaceData()
    {
        Assert.Equal(
            NetworkStatusSnapshot.Unavailable,
            NetworkStatusSnapshot.FromObservation(
                false,
                "WLAN",
                NetworkConnectionKind.WiFi,
                "Wi‑Fi",
                "192.168.1.8"));
    }

    [Fact]
    public void ConnectedInterface_UsesOneConsistentObservation()
    {
        NetworkStatusSnapshot snapshot =
            NetworkStatusSnapshot.FromObservation(
                true,
                "  WLAN  ",
                NetworkConnectionKind.WiFi,
                " Wi‑Fi ",
                " 192.168.1.8 ");

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(
            NetworkConnectionKind.WiFi,
            snapshot.ConnectionKind);
        Assert.Equal("WLAN", snapshot.DisplayName);
        Assert.Equal(
            "Wi‑Fi · 192.168.1.8",
            snapshot.Detail);
    }

    [Theory]
    [InlineData("以太网", "", "以太网")]
    [InlineData("", "10.0.0.2", "10.0.0.2")]
    [InlineData("", "", "网络已连接")]
    public void Detail_UsesOnlyAvailableParts(
        string kind,
        string address,
        string expected)
    {
        NetworkStatusSnapshot snapshot =
            NetworkStatusSnapshot.FromObservation(
                true,
                "连接",
                NetworkConnectionKind.Other,
                kind,
                address);

        Assert.Equal(expected, snapshot.Detail);
    }

    [Theory]
    [InlineData("", "Wi‑Fi", "Wi‑Fi")]
    [InlineData(" ", " ", "网络已连接")]
    public void MissingInterfaceName_HasStableFallback(
        string name,
        string kind,
        string expected)
    {
        NetworkStatusSnapshot snapshot =
            NetworkStatusSnapshot.FromObservation(
                true,
                name,
                NetworkConnectionKind.WiFi,
                kind,
                null);

        Assert.Equal(expected, snapshot.DisplayName);
    }
}
