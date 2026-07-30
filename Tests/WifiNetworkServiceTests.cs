using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WifiNetworkServiceTests
{
    [Fact]
    public void NativeStructures_MatchWindowsWlanLayout()
    {
        Assert.Equal(
            36,
            Marshal.SizeOf<Dot11Ssid>());
        Assert.Equal(
            532,
            Marshal.SizeOf<WlanInterfaceInfo>());
        Assert.Equal(
            628,
            Marshal.SizeOf<
                WlanAvailableNetwork>());
        Assert.Equal(
            40,
            Marshal.SizeOf<
                WlanConnectionParameters>());
    }

    [Fact]
    public async Task GetNetworks_SortsConnectedAndDeduplicates()
    {
        var native = new FakeWifiNetworkNativeApi
        {
            Networks =
            {
                Network(
                    "same",
                    "Coffee",
                    signal: 25),
                Network(
                    "same",
                    "Coffee",
                    signal: 86,
                    hasProfile: true),
                Network(
                    "home",
                    "Home",
                    signal: 41,
                    connected: true,
                    hasProfile: true),
                Network(
                    "office",
                    "Office",
                    signal: 95)
            }
        };
        using var service = CreateService(native);

        WifiNetworkListResult result =
            await service.GetNetworksAsync(
                true,
                CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(
            new[] { "Home", "Office", "Coffee" },
            result.Networks.Select(item =>
                item.DisplayName));
        Assert.Equal(
            86u,
            result.Networks
                .Single(item =>
                    item.DisplayName == "Coffee")
                .SignalQuality);
        Assert.Equal(1, native.ScanRequests);
    }

    [Fact]
    public async Task GetNetworks_LimitsDenseListToTen()
    {
        var native = new FakeWifiNetworkNativeApi();
        for (int index = 0;
             index < 30;
             index++)
        {
            native.Networks.Add(
                Network(
                    $"network-{index}",
                    $"Network {index}",
                    signal:
                        unchecked(
                            (uint)index)));
        }
        using var service = CreateService(native);

        WifiNetworkListResult result =
            await service.GetNetworksAsync(
                false,
                CancellationToken.None);

        Assert.Equal(10, result.Networks.Count);
        Assert.Equal(
            29u,
            result.Networks[0].SignalQuality);
        Assert.Equal(
            20u,
            result.Networks[9].SignalQuality);
    }

    [Theory]
    [InlineData(
        WifiNetworkListStatus.AccessDenied)]
    [InlineData(
        WifiNetworkListStatus.RadioOff)]
    [InlineData(
        WifiNetworkListStatus.ServiceUnavailable)]
    [InlineData(
        WifiNetworkListStatus.NoAdapter)]
    public async Task GetNetworks_PreservesFailure(
        WifiNetworkListStatus status)
    {
        var native = new FakeWifiNetworkNativeApi
        {
            ListStatus = status
        };
        using var service = CreateService(native);

        WifiNetworkListResult result =
            await service.GetNetworksAsync(
                false,
                CancellationToken.None);

        Assert.Equal(status, result.Status);
        Assert.Empty(result.Networks);
    }

    [Fact]
    public async Task Connect_UnconfiguredNetworkNeverWrites()
    {
        var native = new FakeWifiNetworkNativeApi();
        using var service = CreateService(native);
        WifiNetworkSnapshot network =
            Network(
                "guest",
                "Guest",
                hasProfile: false);

        WifiNetworkConnectResult result =
            await service.ConnectAsync(
                network,
                CancellationToken.None);

        Assert.Equal(
            WifiNetworkConnectStatus
                .NeedsCredentials,
            result.Status);
        Assert.Empty(native.ConnectRequests);
    }

    [Theory]
    [InlineData(
        (int)WifiNativeConnectRequestStatus
            .AccessDenied,
        WifiNetworkConnectStatus.AccessDenied)]
    [InlineData(
        (int)WifiNativeConnectRequestStatus.RadioOff,
        WifiNetworkConnectStatus.RadioOff)]
    [InlineData(
        (int)WifiNativeConnectRequestStatus
            .ServiceUnavailable,
        WifiNetworkConnectStatus
            .ServiceUnavailable)]
    [InlineData(
        (int)WifiNativeConnectRequestStatus.NotFound,
        WifiNetworkConnectStatus.NotFound)]
    public async Task Connect_MapsRequestFailure(
        int nativeStatusValue,
        WifiNetworkConnectStatus expected)
    {
        var native = new FakeWifiNetworkNativeApi
        {
            ConnectStatus =
                (WifiNativeConnectRequestStatus)
                nativeStatusValue
        };
        using var service = CreateService(native);

        WifiNetworkConnectResult result =
            await service.ConnectAsync(
                Network(
                    "home",
                    "Home",
                    hasProfile: true),
                CancellationToken.None);

        Assert.Equal(expected, result.Status);
        Assert.Single(native.ConnectRequests);
        Assert.Equal(0, native.ListRequests);
    }

    [Fact]
    public async Task Connect_OnlySucceedsAfterConnectedObservation()
    {
        var target =
            Network(
                "home",
                "Home",
                hasProfile: true);
        var native = new FakeWifiNetworkNativeApi();
        native.ListResults.Enqueue(
            Success(target));
        native.ListResults.Enqueue(
            Success(
                target with
                {
                    IsConnected = true
                }));
        using var service = CreateService(native);

        WifiNetworkConnectResult result =
            await service.ConnectAsync(
                target,
                CancellationToken.None);

        Assert.Equal(
            WifiNetworkConnectStatus.Succeeded,
            result.Status);
        Assert.Equal(2, native.ListRequests);
    }

    [Fact]
    public async Task Connect_AcceptedWithoutTransitionIsNotConfirmed()
    {
        WifiNetworkSnapshot target =
            Network(
                "home",
                "Home",
                hasProfile: true);
        var native = new FakeWifiNetworkNativeApi
        {
            Networks = { target }
        };
        using var service = CreateService(native);

        WifiNetworkConnectResult result =
            await service.ConnectAsync(
                target,
                CancellationToken.None);

        Assert.Equal(
            WifiNetworkConnectStatus.NotConfirmed,
            result.Status);
        Assert.Equal(3, native.ListRequests);
    }

    [Fact]
    public async Task Connect_ListAccessDeniedStopsConfirmation()
    {
        var native = new FakeWifiNetworkNativeApi();
        native.ListResults.Enqueue(
            new WifiNetworkListResult(
                WifiNetworkListStatus.AccessDenied,
                Array.Empty<
                    WifiNetworkSnapshot>()));
        using var service = CreateService(native);

        WifiNetworkConnectResult result =
            await service.ConnectAsync(
                Network(
                    "home",
                    "Home",
                    hasProfile: true),
                CancellationToken.None);

        Assert.Equal(
            WifiNetworkConnectStatus.AccessDenied,
            result.Status);
        Assert.Equal(1, native.ListRequests);
    }

    private static WifiNetworkService CreateService(
        IWifiNetworkNativeApi native) =>
        new(
            native,
            confirmationAttempts: 3,
            confirmationDelay: TimeSpan.Zero);

    private static WifiNetworkListResult Success(
        params WifiNetworkSnapshot[] networks) =>
        new(
            WifiNetworkListStatus.Succeeded,
            networks);

    private static WifiNetworkSnapshot Network(
        string key,
        string name,
        uint signal = 50,
        bool connected = false,
        bool hasProfile = false,
        bool connectable = true) =>
        new(
            key,
            "00000000-0000-0000-0000-000000000001",
            hasProfile ? name : string.Empty,
            name,
            signal,
            connected,
            true,
            hasProfile,
            connectable);

    private sealed class FakeWifiNetworkNativeApi :
        IWifiNetworkNativeApi
    {
        internal List<WifiNetworkSnapshot>
            Networks { get; } = new();

        internal Queue<WifiNetworkListResult>
            ListResults { get; } = new();

        internal List<(string InterfaceId,
            string ProfileName)>
            ConnectRequests { get; } = new();

        internal WifiNetworkListStatus
            ListStatus { get; init; } =
                WifiNetworkListStatus.Succeeded;

        internal WifiNativeConnectRequestStatus
            ConnectStatus { get; init; } =
                WifiNativeConnectRequestStatus
                    .Accepted;

        internal int ScanRequests { get; private set; }

        internal int ListRequests { get; private set; }

        public Task<WifiNetworkListResult>
            GetNetworksAsync(
                bool requestScan,
                CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            if (requestScan)
                ScanRequests++;
            ListRequests++;
            WifiNetworkListResult result =
                ListResults.Count > 0
                    ? ListResults.Dequeue()
                    : new WifiNetworkListResult(
                        ListStatus,
                        Networks.ToArray());
            return Task.FromResult(result);
        }

        public Task<
            WifiNativeConnectRequestStatus>
            RequestConnectAsync(
                string interfaceId,
                string profileName,
                CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            ConnectRequests.Add(
                (interfaceId, profileName));
            return Task.FromResult(
                ConnectStatus);
        }
    }
}
