using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class BluetoothDeviceServiceTests
{
    [Fact]
    public async Task GetDevices_DeduplicatesClassicAndLowEnergyEndpoints()
    {
        var native = new FakeBluetoothNativeApi
        {
            Devices =
            {
                Device("classic", "same", "耳机", present: true),
                Device("ble", "same", "耳机", paired: true, connected: true),
                Device("mouse", "mouse", "鼠标", paired: true)
            }
        };
        using var service = CreateService(native);

        BluetoothDeviceListResult result =
            await service.GetDevicesAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "耳机", "鼠标" },
            result.Devices.Select(item => item.DisplayName));
        Assert.Equal("ble", result.Devices[0].Id);
    }

    [Fact]
    public async Task GetDevices_UsesStablePriorityAndLimitsDenseLists()
    {
        var native = new FakeBluetoothNativeApi();
        for (int index = 0; index < 20; index++)
        {
            native.Devices.Add(Device(
                $"id-{index}",
                $"identity-{index}",
                $"设备 {index:D2}",
                present: true,
                signal: index));
        }
        using var service = CreateService(native);

        BluetoothDeviceListResult result =
            await service.GetDevicesAsync(CancellationToken.None);

        Assert.Equal(12, result.Devices.Count);
        Assert.Equal("设备 19", result.Devices[0].DisplayName);
        Assert.Equal("设备 08", result.Devices[^1].DisplayName);
    }

    [Fact]
    public async Task Pair_ConfirmsPairedState()
    {
        var native = new FakeBluetoothNativeApi();
        BluetoothNativeObservation target =
            Device("id", "identity", "键盘", canPair: true);
        native.Devices.Add(target);
        native.PairStatus = BluetoothNativeOperationStatus.Succeeded;
        native.AfterPair = target with { IsPaired = true };
        using var service = CreateService(native);

        BluetoothDeviceOperationResult result =
            await service.PairAsync(
                Snapshot(target),
                CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, native.PairRequests);
    }

    [Fact]
    public async Task Unpair_ConfirmsWhenEndpointDisappears()
    {
        var native = new FakeBluetoothNativeApi();
        BluetoothNativeObservation target =
            Device("id", "identity", "手柄", paired: true);
        native.Devices.Add(target);
        native.UnpairStatus = BluetoothNativeOperationStatus.Succeeded;
        native.RemoveAfterUnpair = true;
        using var service = CreateService(native);

        BluetoothDeviceOperationResult result =
            await service.UnpairAsync(
                Snapshot(target),
                CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, native.UnpairRequests);
    }

    [Theory]
    [InlineData((int)BluetoothNativeOperationStatus.Canceled,
        BluetoothDeviceOperationStatus.Canceled)]
    [InlineData((int)BluetoothNativeOperationStatus.AccessDenied,
        BluetoothDeviceOperationStatus.AccessDenied)]
    [InlineData((int)BluetoothNativeOperationStatus.AuthenticationFailed,
        BluetoothDeviceOperationStatus.AuthenticationFailed)]
    [InlineData((int)BluetoothNativeOperationStatus.Rejected,
        BluetoothDeviceOperationStatus.Rejected)]
    public async Task Pair_PreservesFailureReason(
        int nativeStatusValue,
        BluetoothDeviceOperationStatus expected)
    {
        BluetoothNativeOperationStatus nativeStatus =
            (BluetoothNativeOperationStatus)nativeStatusValue;
        var native = new FakeBluetoothNativeApi
        {
            PairStatus = nativeStatus
        };
        BluetoothNativeObservation target =
            Device("id", "identity", "设备", canPair: true);
        native.Devices.Add(target);
        using var service = CreateService(native);

        BluetoothDeviceOperationResult result =
            await service.PairAsync(
                Snapshot(target),
                CancellationToken.None);

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task Pair_AlreadyPairedNeverCallsNativeBoundary()
    {
        var native = new FakeBluetoothNativeApi();
        using var service = CreateService(native);
        BluetoothNativeObservation target =
            Device("id", "identity", "音箱", paired: true);

        BluetoothDeviceOperationResult result =
            await service.PairAsync(
                Snapshot(target),
                CancellationToken.None);

        Assert.Equal(
            BluetoothDeviceOperationStatus.AlreadyInDesiredState,
            result.Status);
        Assert.Equal(0, native.PairRequests);
    }

    private static BluetoothDeviceService CreateService(
        IBluetoothDeviceNativeApi native) =>
        new(native, 2, System.TimeSpan.Zero);

    private static BluetoothNativeObservation Device(
        string id,
        string identity,
        string name,
        bool paired = false,
        bool canPair = false,
        bool connected = false,
        bool present = false,
        int? signal = null) =>
        new(id, identity, name, paired, canPair, connected,
            present, signal, string.Empty);

    private static BluetoothDeviceSnapshot Snapshot(
        BluetoothNativeObservation item) =>
        new(item.Id, item.IdentityKey, item.DisplayName,
            item.IsPaired, item.CanPair, item.IsConnected,
            item.IsPresent, item.SignalStrength, item.ModelName);

    private sealed class FakeBluetoothNativeApi :
        IBluetoothDeviceNativeApi
    {
        internal List<BluetoothNativeObservation> Devices { get; set; } = new();
        internal BluetoothNativeOperationStatus PairStatus { get; set; } =
            BluetoothNativeOperationStatus.Succeeded;
        internal BluetoothNativeOperationStatus UnpairStatus { get; set; } =
            BluetoothNativeOperationStatus.Succeeded;
        internal BluetoothNativeObservation? AfterPair { get; set; }
        internal bool RemoveAfterUnpair { get; set; }
        internal int PairRequests { get; private set; }
        internal int UnpairRequests { get; private set; }

        public Task<IReadOnlyList<BluetoothNativeObservation>> GetDevicesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BluetoothNativeObservation>>(
                Devices.ToArray());

        public Task<BluetoothNativeOperationStatus> PairAsync(
            string id,
            CancellationToken cancellationToken)
        {
            PairRequests++;
            if (PairStatus == BluetoothNativeOperationStatus.Succeeded
                && AfterPair != null)
            {
                Devices = new List<BluetoothNativeObservation> { AfterPair };
            }
            return Task.FromResult(PairStatus);
        }

        public Task<BluetoothNativeOperationStatus> UnpairAsync(
            string id,
            CancellationToken cancellationToken)
        {
            UnpairRequests++;
            if (UnpairStatus == BluetoothNativeOperationStatus.Succeeded)
            {
                if (RemoveAfterUnpair)
                    Devices.Clear();
                else
                    Devices = Devices.Select(item =>
                        item.Id == id ? item with { IsPaired = false } : item).ToList();
            }
            return Task.FromResult(UnpairStatus);
        }
    }
}
