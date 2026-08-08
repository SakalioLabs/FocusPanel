using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
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
        Assert.Equal(
            516,
            Marshal.SizeOf<WlanProfileInfo>());
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

    [Fact]
    public async Task GetNetworks_KeepsSixSavedOutOfRangeProfilesAfterNearbyLimit()
    {
        var native = new FakeWifiNetworkNativeApi();
        for (int index = 0; index < 14; index++)
        {
            native.Networks.Add(
                Network(
                    $"near-{index}",
                    $"Nearby {index}",
                    signal: unchecked((uint)(90 - index))));
        }
        for (int index = 0; index < 9; index++)
        {
            native.Networks.Add(
                Network(
                    $"saved-{index}",
                    $"Saved {index}",
                    hasProfile: true,
                    connectable: false));
        }
        using var service = CreateService(native);

        WifiNetworkListResult result =
            await service.GetNetworksAsync(
                false,
                CancellationToken.None);

        Assert.Equal(16, result.Networks.Count);
        Assert.Equal(
            10,
            result.Networks.Count(item =>
                !item.IsSavedOutOfRange));
        Assert.Equal(
            6,
            result.Networks.Count(item =>
                item.IsSavedOutOfRange));
        Assert.All(
            result.Networks.Take(10),
            item => Assert.False(
                item.IsSavedOutOfRange));
    }

    [Fact]
    public void SavedOutOfRangeProfile_HasExplicitPresentation()
    {
        WifiNetworkSnapshot network =
            Network(
                "saved",
                "Studio",
                hasProfile: true,
                connectable: false);

        Assert.True(network.IsSavedOutOfRange);
        Assert.Equal("离线", network.SignalText);
        Assert.Equal(
            "已保存 · 当前不在附近",
            network.SecurityText);
        Assert.True(network.CanForget);
        Assert.False(network.CanInvokeAction);
    }

    [Fact]
    public async Task PolicyManagedProfile_CannotBeForgotten()
    {
        var native = new FakeWifiNetworkNativeApi();
        using var service = CreateService(native);
        WifiNetworkSnapshot network =
            Network(
                "work",
                "Corporate",
                hasProfile: true,
                connectable: false) with
            {
                IsPolicyManaged = true
            };

        WifiNetworkManageResult result =
            await service.ForgetAsync(
                network,
                CancellationToken.None);

        Assert.False(network.CanForget);
        Assert.Contains("组织管理", network.SecurityText);
        Assert.Equal(
            WifiNetworkManageStatus.AccessDenied,
            result.Status);
        Assert.Equal(0, native.ForgetRequests);
    }

    [Fact]
    public void SavedProfileComposer_MergesMatchingAvailableNetwork()
    {
        var networks = new List<WifiNetworkSnapshot>
        {
            Network(
                "available",
                "Corporate",
                hasProfile: true)
        };

        WifiSavedProfileComposer.Merge(
            networks,
            new WifiSavedProfileObservation(
                networks[0].InterfaceId,
                "Corporate",
                true));

        Assert.Single(networks);
        Assert.True(networks[0].IsPolicyManaged);
        Assert.False(networks[0].CanForget);
    }

    [Fact]
    public void SavedProfileComposer_AddsOutOfRangeProfile()
    {
        var networks = new List<WifiNetworkSnapshot>();
        string interfaceId =
            "00000000-0000-0000-0000-000000000001";

        WifiSavedProfileComposer.Merge(
            networks,
            new WifiSavedProfileObservation(
                interfaceId,
                "  Home Backup  ",
                false));

        WifiNetworkSnapshot network =
            Assert.Single(networks);
        Assert.Equal("Home Backup", network.DisplayName);
        Assert.Equal(interfaceId, network.InterfaceId);
        Assert.True(network.IsSavedOutOfRange);
        Assert.True(network.CanForget);
    }

    [Fact]
    public void SavedProfileComposer_KeepsSameNameOnDifferentAdapters()
    {
        var networks = new List<WifiNetworkSnapshot>();

        WifiSavedProfileComposer.Merge(
            networks,
            new WifiSavedProfileObservation(
                "adapter-a",
                "Shared",
                false));
        WifiSavedProfileComposer.Merge(
            networks,
            new WifiSavedProfileObservation(
                "adapter-b",
                "Shared",
                false));

        Assert.Equal(2, networks.Count);
        Assert.NotEqual(networks[0].Key, networks[1].Key);
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

    [Fact]
    public async Task ConnectWithCredentials_CreatesProfileAndConfirms()
    {
        WifiNetworkSnapshot target =
            Network("home", "Home");
        var native = new FakeWifiNetworkNativeApi();
        native.ListResults.Enqueue(
            Success(
                target with
                {
                    IsConnected = true,
                    HasProfile = true,
                    ProfileName = "Home"
                }));
        using var service = CreateService(native);
        using SecureString password =
            Secure("correct-horse");

        WifiNetworkConnectResult result =
            await service
                .ConnectWithCredentialsAsync(
                    target,
                    password,
                    CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Single(native.ProfileConnectRequests);
        Assert.Equal("Home",
            native.ProfileConnectRequests[0]
                .ProfileName);
        Assert.NotNull(native.LastProfileXmlReference);
        Assert.All(
            native.LastProfileXmlReference!,
            character => Assert.Equal('\0', character));
    }

    [Fact]
    public async Task ConnectWithCredentials_RejectsShortPasswordBeforeNativeCall()
    {
        var native = new FakeWifiNetworkNativeApi();
        using var service = CreateService(native);
        using SecureString password = Secure("short");

        WifiNetworkConnectResult result =
            await service
                .ConnectWithCredentialsAsync(
                    Network("home", "Home"),
                    password,
                    CancellationToken.None);

        Assert.Equal(
            WifiNetworkConnectStatus
                .InvalidCredentials,
            result.Status);
        Assert.Empty(native.ProfileConnectRequests);
    }

    [Fact]
    public async Task ConnectWithCredentials_RemovesFailedFirstProfile()
    {
        var native = new FakeWifiNetworkNativeApi
        {
            Networks =
            {
                Network("home", "Home")
            }
        };
        using var service = CreateService(native);
        using SecureString password =
            Secure("wrong-pass");

        WifiNetworkConnectResult result =
            await service
                .ConnectWithCredentialsAsync(
                    Network("home", "Home"),
                    password,
                    CancellationToken.None);

        Assert.Equal(
            WifiNetworkConnectStatus.NotConfirmed,
            result.Status);
        Assert.Equal(1, native.RemoveProfileRequests);
    }

    [Fact]
    public void ProfileXml_EscapesNamesAndCredential()
    {
        WifiNetworkSnapshot network =
            Network("home", "Home & Lab") with
            {
                SsidHex = "486F6D652026204C6162"
            };
        using SecureString password =
            Secure("eight<&chars");

        char[] xml = WifiProfileXmlBuilder.Build(
            network,
            password);
        try
        {
            string text = new string(xml).TrimEnd('\0');
            Assert.Contains("Home &amp; Lab", text);
            Assert.Contains("eight&lt;&amp;chars", text);
            Assert.DoesNotContain("eight<&chars", text);
            Assert.Contains("<authentication>WPA2PSK</authentication>", text);
        }
        finally
        {
            Array.Clear(xml, 0, xml.Length);
        }
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

    [Fact]
    public async Task Disconnect_OnlySucceedsAfterDisconnectedObservation()
    {
        WifiNetworkSnapshot target =
            Network("home", "Home", connected: true,
                hasProfile: true);
        var native = new FakeWifiNetworkNativeApi();
        native.ListResults.Enqueue(
            Success(target with { IsConnected = false }));
        using var service = CreateService(native);

        WifiNetworkManageResult result =
            await service.DisconnectAsync(
                target,
                CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, native.DisconnectRequests);
        Assert.Equal(new[] { "disconnect" }, native.OperationOrder);
    }

    [Fact]
    public async Task Disconnect_AcceptedWithoutTransitionIsNotConfirmed()
    {
        WifiNetworkSnapshot target =
            Network("home", "Home", connected: true,
                hasProfile: true);
        var native = new FakeWifiNetworkNativeApi
        {
            Networks = { target }
        };
        using var service = CreateService(native);

        WifiNetworkManageResult result =
            await service.DisconnectAsync(
                target,
                CancellationToken.None);

        Assert.Equal(
            WifiNetworkManageStatus.NotConfirmed,
            result.Status);
        Assert.Equal(3, native.ListRequests);
    }

    [Fact]
    public async Task Forget_ConnectedNetworkDisconnectsBeforeDeletingProfile()
    {
        WifiNetworkSnapshot target =
            Network("home", "Home", connected: true,
                hasProfile: true);
        var native = new FakeWifiNetworkNativeApi();
        native.ListResults.Enqueue(
            Success(target with { IsConnected = false }));
        native.ListResults.Enqueue(
            Success(target with
            {
                IsConnected = false,
                HasProfile = false,
                ProfileName = string.Empty
            }));
        using var service = CreateService(native);

        WifiNetworkManageResult result =
            await service.ForgetAsync(
                target,
                CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(
            new[] { "disconnect", "forget" },
            native.OperationOrder);
    }

    [Fact]
    public async Task Forget_UnstoredNetworkNeverCallsNativeBoundary()
    {
        var native = new FakeWifiNetworkNativeApi();
        using var service = CreateService(native);

        WifiNetworkManageResult result =
            await service.ForgetAsync(
                Network("guest", "Guest"),
                CancellationToken.None);

        Assert.Equal(
            WifiNetworkManageStatus.AlreadyInDesiredState,
            result.Status);
        Assert.Equal(0, native.ForgetRequests);
        Assert.Empty(native.OperationOrder);
    }

    [Theory]
    [InlineData(
        (int)WifiNativeManageRequestStatus.AccessDenied,
        WifiNetworkManageStatus.AccessDenied)]
    [InlineData(
        (int)WifiNativeManageRequestStatus.RadioOff,
        WifiNetworkManageStatus.RadioOff)]
    [InlineData(
        (int)WifiNativeManageRequestStatus.ServiceUnavailable,
        WifiNetworkManageStatus.ServiceUnavailable)]
    [InlineData(
        (int)WifiNativeManageRequestStatus.NotFound,
        WifiNetworkManageStatus.NotFound)]
    public async Task Disconnect_MapsRequestFailure(
        int nativeStatusValue,
        WifiNetworkManageStatus expected)
    {
        var native = new FakeWifiNetworkNativeApi
        {
            ManageStatus =
                (WifiNativeManageRequestStatus)
                nativeStatusValue
        };
        using var service = CreateService(native);

        WifiNetworkManageResult result =
            await service.DisconnectAsync(
                Network("home", "Home",
                    connected: true,
                    hasProfile: true),
                CancellationToken.None);

        Assert.Equal(expected, result.Status);
        Assert.Equal(0, native.ListRequests);
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

    private static SecureString Secure(string value)
    {
        var result = new SecureString();
        foreach (char character in value)
            result.AppendChar(character);
        result.MakeReadOnly();
        return result;
    }

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

        internal List<(string InterfaceId,
            string ProfileName,
            int XmlLength)>
            ProfileConnectRequests { get; } = new();

        internal char[]? LastProfileXmlReference
        {
            get;
            private set;
        }

        internal WifiNetworkListStatus
            ListStatus { get; init; } =
                WifiNetworkListStatus.Succeeded;

        internal WifiNativeConnectRequestStatus
            ConnectStatus { get; init; } =
                WifiNativeConnectRequestStatus
                    .Accepted;

        internal int ScanRequests { get; private set; }

        internal int ListRequests { get; private set; }

        internal int RemoveProfileRequests
        {
            get;
            private set;
        }

        internal int DisconnectRequests { get; private set; }

        internal int ForgetRequests { get; private set; }

        internal List<string> OperationOrder { get; } = new();

        internal WifiNativeManageRequestStatus
            ManageStatus { get; init; } =
                WifiNativeManageRequestStatus.Accepted;

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

        public Task<
            WifiNativeConnectRequestStatus>
            RequestProfileConnectAsync(
                string interfaceId,
                string profileName,
                char[] profileXml,
                CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            ProfileConnectRequests.Add(
                (interfaceId,
                    profileName,
                    profileXml.Length));
            LastProfileXmlReference = profileXml;
            return Task.FromResult(ConnectStatus);
        }

        public Task RemoveProfileAsync(
            string interfaceId,
            string profileName,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            RemoveProfileRequests++;
            return Task.CompletedTask;
        }

        public Task<WifiNativeManageRequestStatus>
            RequestDisconnectAsync(
                string interfaceId,
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DisconnectRequests++;
            OperationOrder.Add("disconnect");
            return Task.FromResult(ManageStatus);
        }

        public Task<WifiNativeManageRequestStatus>
            RequestForgetProfileAsync(
                string interfaceId,
                string profileName,
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ForgetRequests++;
            OperationOrder.Add("forget");
            return Task.FromResult(ManageStatus);
        }
    }
}
