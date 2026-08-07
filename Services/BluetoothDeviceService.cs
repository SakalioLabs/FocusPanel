using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;

namespace FocusPanel.Services;

public enum BluetoothDeviceListStatus
{
    Succeeded,
    AccessDenied,
    Unavailable,
    Failed
}

public sealed record BluetoothDeviceSnapshot(
    string Id,
    string IdentityKey,
    string DisplayName,
    bool IsPaired,
    bool CanPair,
    bool IsConnected,
    bool IsPresent,
    int? SignalStrength,
    string ModelName)
{
    public string StatusText =>
        IsConnected
            ? "已连接"
            : IsPaired && IsPresent
                ? "已配对 · 在附近"
                : IsPaired
                    ? "已配对 · 未连接"
                    : CanPair
                        ? "可配对"
                        : "暂不可配对";

    public string ActionText =>
        IsPaired ? "移除" : "配对";

    public bool CanInvokeAction =>
        IsPaired || CanPair;

    public string DetailText =>
        string.IsNullOrWhiteSpace(ModelName)
            ? StatusText
            : $"{StatusText} · {ModelName}";
}

public sealed record BluetoothDeviceListResult(
    BluetoothDeviceListStatus Status,
    IReadOnlyList<BluetoothDeviceSnapshot> Devices)
{
    public bool Succeeded =>
        Status == BluetoothDeviceListStatus.Succeeded;
}

public enum BluetoothDeviceOperationStatus
{
    Succeeded,
    AlreadyInDesiredState,
    NotFound,
    Canceled,
    AccessDenied,
    NotReady,
    AuthenticationFailed,
    Rejected,
    NotConfirmed,
    Failed
}

public readonly record struct
    BluetoothDeviceOperationResult(
        BluetoothDeviceOperationStatus Status,
        string DisplayName,
        bool RequestedPaired)
{
    public bool Succeeded =>
        Status is
            BluetoothDeviceOperationStatus.Succeeded
            or BluetoothDeviceOperationStatus
                .AlreadyInDesiredState;
}

public interface IBluetoothDeviceService : IDisposable
{
    Task<BluetoothDeviceListResult> GetDevicesAsync(
        CancellationToken cancellationToken);

    Task<BluetoothDeviceOperationResult> PairAsync(
        BluetoothDeviceSnapshot device,
        CancellationToken cancellationToken);

    Task<BluetoothDeviceOperationResult> UnpairAsync(
        BluetoothDeviceSnapshot device,
        CancellationToken cancellationToken);
}

internal enum BluetoothNativeOperationStatus
{
    Succeeded,
    AlreadyPaired,
    AlreadyUnpaired,
    NotFound,
    Canceled,
    AccessDenied,
    NotReady,
    AuthenticationFailed,
    Rejected,
    Failed
}

internal sealed record BluetoothNativeObservation(
    string Id,
    string IdentityKey,
    string DisplayName,
    bool IsPaired,
    bool CanPair,
    bool IsConnected,
    bool IsPresent,
    int? SignalStrength,
    string ModelName);

internal interface IBluetoothDeviceNativeApi
{
    Task<IReadOnlyList<BluetoothNativeObservation>>
        GetDevicesAsync(
            CancellationToken cancellationToken);

    Task<BluetoothNativeOperationStatus> PairAsync(
        string id,
        CancellationToken cancellationToken);

    Task<BluetoothNativeOperationStatus> UnpairAsync(
        string id,
        CancellationToken cancellationToken);
}

public sealed class BluetoothDeviceService :
    IBluetoothDeviceService
{
    private const int MaximumDeviceCount = 12;
    private readonly IBluetoothDeviceNativeApi _native;
    private readonly SemaphoreSlim _operationGate =
        new(1, 1);
    private readonly int _confirmationAttempts;
    private readonly TimeSpan _confirmationDelay;
    private bool _isDisposed;

    public BluetoothDeviceService()
        : this(
            new WinRtBluetoothDeviceNativeApi(),
            6,
            TimeSpan.FromMilliseconds(250))
    {
    }

    internal BluetoothDeviceService(
        IBluetoothDeviceNativeApi native,
        int confirmationAttempts = 6,
        TimeSpan? confirmationDelay = null)
    {
        _native = native
            ?? throw new ArgumentNullException(
                nameof(native));
        _confirmationAttempts =
            Math.Max(1, confirmationAttempts);
        _confirmationDelay =
            confirmationDelay
            ?? TimeSpan.FromMilliseconds(250);
    }

    public async Task<BluetoothDeviceListResult>
        GetDevicesAsync(
            CancellationToken cancellationToken)
    {
        if (_isDisposed)
            return Empty(BluetoothDeviceListStatus.Failed);

        try
        {
            IReadOnlyList<BluetoothNativeObservation>
                observations =
                    await _native.GetDevicesAsync(
                        cancellationToken);
            BluetoothDeviceSnapshot[] devices =
                observations
                    .Where(item =>
                        !string.IsNullOrWhiteSpace(
                            item.Id)
                        && !string.IsNullOrWhiteSpace(
                            item.DisplayName))
                    .GroupBy(
                        item => item.IdentityKey,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                        group
                            .OrderByDescending(item =>
                                item.IsConnected)
                            .ThenByDescending(item =>
                                item.IsPaired)
                            .ThenByDescending(item =>
                                item.IsPresent)
                            .ThenByDescending(item =>
                                item.SignalStrength
                                ?? int.MinValue)
                            .First())
                    .OrderByDescending(item =>
                        item.IsConnected)
                    .ThenByDescending(item =>
                        item.IsPaired)
                    .ThenByDescending(item =>
                        item.IsPresent)
                    .ThenByDescending(item =>
                        item.SignalStrength
                        ?? int.MinValue)
                    .ThenBy(
                        item => item.DisplayName,
                        StringComparer
                            .CurrentCultureIgnoreCase)
                    .Take(MaximumDeviceCount)
                    .Select(ToSnapshot)
                    .ToArray();
            return new BluetoothDeviceListResult(
                BluetoothDeviceListStatus.Succeeded,
                devices);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return Empty(
                BluetoothDeviceListStatus.AccessDenied);
        }
        catch (PlatformNotSupportedException)
        {
            return Empty(
                BluetoothDeviceListStatus.Unavailable);
        }
        catch
        {
            return Empty(BluetoothDeviceListStatus.Failed);
        }
    }

    public Task<BluetoothDeviceOperationResult>
        PairAsync(
            BluetoothDeviceSnapshot device,
            CancellationToken cancellationToken) =>
        ChangePairingAsync(
            device,
            true,
            cancellationToken);

    public Task<BluetoothDeviceOperationResult>
        UnpairAsync(
            BluetoothDeviceSnapshot device,
            CancellationToken cancellationToken) =>
        ChangePairingAsync(
            device,
            false,
            cancellationToken);

    private async Task<BluetoothDeviceOperationResult>
        ChangePairingAsync(
            BluetoothDeviceSnapshot device,
            bool requestedPaired,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (_isDisposed)
            return Result(
                BluetoothDeviceOperationStatus.Failed,
                device,
                requestedPaired);
        if (device.IsPaired == requestedPaired)
        {
            return Result(
                BluetoothDeviceOperationStatus
                    .AlreadyInDesiredState,
                device,
                requestedPaired);
        }

        await _operationGate.WaitAsync(
            cancellationToken);
        try
        {
            BluetoothNativeOperationStatus native =
                requestedPaired
                    ? await _native.PairAsync(
                        device.Id,
                        cancellationToken)
                    : await _native.UnpairAsync(
                        device.Id,
                        cancellationToken);
            BluetoothDeviceOperationStatus mapped =
                Map(native, requestedPaired);
            if (mapped
                != BluetoothDeviceOperationStatus
                    .Succeeded)
            {
                return Result(
                    mapped,
                    device,
                    requestedPaired);
            }

            for (int attempt = 0;
                 attempt < _confirmationAttempts;
                 attempt++)
            {
                IReadOnlyList<
                    BluetoothNativeObservation>
                    observations =
                        await _native.GetDevicesAsync(
                            cancellationToken);
                BluetoothNativeObservation? current =
                    observations.FirstOrDefault(item =>
                        string.Equals(
                            item.IdentityKey,
                            device.IdentityKey,
                            StringComparison
                                .OrdinalIgnoreCase)
                        || string.Equals(
                            item.Id,
                            device.Id,
                            StringComparison.Ordinal));
                if ((!requestedPaired
                        && (current == null
                            || !current.IsPaired))
                    || (requestedPaired
                        && current?.IsPaired == true))
                {
                    return Result(
                        BluetoothDeviceOperationStatus
                            .Succeeded,
                        device,
                        requestedPaired);
                }

                if (attempt
                    < _confirmationAttempts - 1)
                {
                    await Task.Delay(
                        _confirmationDelay,
                        cancellationToken);
                }
            }

            return Result(
                BluetoothDeviceOperationStatus
                    .NotConfirmed,
                device,
                requestedPaired);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return Result(
                BluetoothDeviceOperationStatus
                    .AccessDenied,
                device,
                requestedPaired);
        }
        catch
        {
            return Result(
                BluetoothDeviceOperationStatus.Failed,
                device,
                requestedPaired);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private static BluetoothDeviceOperationStatus Map(
        BluetoothNativeOperationStatus status,
        bool requestedPaired) =>
        status switch
        {
            BluetoothNativeOperationStatus.Succeeded =>
                BluetoothDeviceOperationStatus.Succeeded,
            BluetoothNativeOperationStatus.AlreadyPaired
                when requestedPaired =>
                BluetoothDeviceOperationStatus
                    .AlreadyInDesiredState,
            BluetoothNativeOperationStatus.AlreadyUnpaired
                when !requestedPaired =>
                BluetoothDeviceOperationStatus
                    .AlreadyInDesiredState,
            BluetoothNativeOperationStatus.NotFound =>
                BluetoothDeviceOperationStatus.NotFound,
            BluetoothNativeOperationStatus.Canceled =>
                BluetoothDeviceOperationStatus.Canceled,
            BluetoothNativeOperationStatus.AccessDenied =>
                BluetoothDeviceOperationStatus.AccessDenied,
            BluetoothNativeOperationStatus.NotReady =>
                BluetoothDeviceOperationStatus.NotReady,
            BluetoothNativeOperationStatus
                .AuthenticationFailed =>
                BluetoothDeviceOperationStatus
                    .AuthenticationFailed,
            BluetoothNativeOperationStatus.Rejected =>
                BluetoothDeviceOperationStatus.Rejected,
            _ => BluetoothDeviceOperationStatus.Failed
        };

    private static BluetoothDeviceSnapshot ToSnapshot(
        BluetoothNativeObservation item) =>
        new(
            item.Id,
            item.IdentityKey,
            item.DisplayName,
            item.IsPaired,
            item.CanPair,
            item.IsConnected,
            item.IsPresent,
            item.SignalStrength,
            item.ModelName);

    private static BluetoothDeviceOperationResult Result(
        BluetoothDeviceOperationStatus status,
        BluetoothDeviceSnapshot device,
        bool requestedPaired) =>
        new(
            status,
            device.DisplayName,
            requestedPaired);

    private static BluetoothDeviceListResult Empty(
        BluetoothDeviceListStatus status) =>
        new(
            status,
            Array.Empty<BluetoothDeviceSnapshot>());

    public void Dispose()
    {
        _isDisposed = true;
    }
}

internal sealed class WinRtBluetoothDeviceNativeApi :
    IBluetoothDeviceNativeApi
{
    private const string ClassicProtocolId =
        "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}";
    private const string LowEnergyProtocolId =
        "{bb7bb05e-5972-42b5-94fc-76eaa7084d49}";
    private const string IsConnectedProperty =
        "System.Devices.Aep.IsConnected";
    private const string IsPresentProperty =
        "System.Devices.Aep.IsPresent";
    private const string ContainerIdProperty =
        "System.Devices.Aep.ContainerId";
    private const string DeviceAddressProperty =
        "System.Devices.Aep.DeviceAddress";
    private const string SignalStrengthProperty =
        "System.Devices.Aep.SignalStrength";
    private const string ModelNameProperty =
        "System.Devices.Aep.ModelName";
    private static readonly string Selector =
        $"System.Devices.Aep.ProtocolId:=\"{ClassicProtocolId}\" OR "
        + $"System.Devices.Aep.ProtocolId:=\"{LowEnergyProtocolId}\"";
    private static readonly string[] RequestedProperties =
    {
        IsConnectedProperty,
        IsPresentProperty,
        ContainerIdProperty,
        DeviceAddressProperty,
        SignalStrengthProperty,
        ModelNameProperty
    };

    public async Task<IReadOnlyList<
        BluetoothNativeObservation>> GetDevicesAsync(
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();
        DeviceInformationCollection devices =
            await DeviceInformation.FindAllAsync(
                Selector,
                RequestedProperties,
                DeviceInformationKind.AssociationEndpoint);
        cancellationToken
            .ThrowIfCancellationRequested();
        return devices
            .Select(ToObservation)
            .ToArray();
    }

    public async Task<BluetoothNativeOperationStatus>
        PairAsync(
            string id,
            CancellationToken cancellationToken)
    {
        DeviceInformation? device =
            await FindAsync(id, cancellationToken);
        if (device == null)
            return BluetoothNativeOperationStatus.NotFound;
        if (device.Pairing.IsPaired)
            return BluetoothNativeOperationStatus.AlreadyPaired;
        if (!device.Pairing.CanPair)
            return BluetoothNativeOperationStatus.NotReady;

        DevicePairingResult result =
            await device.Pairing.PairAsync();
        cancellationToken
            .ThrowIfCancellationRequested();
        return MapPairing(result.Status, true);
    }

    public async Task<BluetoothNativeOperationStatus>
        UnpairAsync(
            string id,
            CancellationToken cancellationToken)
    {
        DeviceInformation? device =
            await FindAsync(id, cancellationToken);
        if (device == null)
            return BluetoothNativeOperationStatus.NotFound;
        if (!device.Pairing.IsPaired)
            return BluetoothNativeOperationStatus.AlreadyUnpaired;

        DeviceUnpairingResult result =
            await device.Pairing.UnpairAsync();
        cancellationToken
            .ThrowIfCancellationRequested();
        return MapUnpairing(result.Status);
    }

    private static async Task<DeviceInformation?> FindAsync(
        string id,
        CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();
        try
        {
            DeviceInformation device =
                await DeviceInformation.CreateFromIdAsync(
                    id,
                    RequestedProperties,
                    DeviceInformationKind.AssociationEndpoint);
            cancellationToken
                .ThrowIfCancellationRequested();
            return device;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static BluetoothNativeObservation ToObservation(
        DeviceInformation device)
    {
        string identity =
            ReadString(device, ContainerIdProperty)
            ?? ReadString(device, DeviceAddressProperty)
            ?? device.Id;
        return new BluetoothNativeObservation(
            device.Id,
            identity,
            device.Name?.Trim() ?? string.Empty,
            device.Pairing.IsPaired,
            device.Pairing.CanPair,
            ReadBool(device, IsConnectedProperty),
            ReadBool(device, IsPresentProperty),
            ReadInt32(device, SignalStrengthProperty),
            ReadString(device, ModelNameProperty)
            ?? string.Empty);
    }

    private static bool ReadBool(
        DeviceInformation device,
        string propertyName) =>
        device.Properties.TryGetValue(
            propertyName,
            out object? value)
        && value is bool result
        && result;

    private static int? ReadInt32(
        DeviceInformation device,
        string propertyName)
    {
        if (!device.Properties.TryGetValue(
                propertyName,
                out object? value)
            || value == null)
        {
            return null;
        }

        return value switch
        {
            int result => result,
            short result => result,
            _ => null
        };
    }

    private static string? ReadString(
        DeviceInformation device,
        string propertyName)
    {
        if (!device.Properties.TryGetValue(
                propertyName,
                out object? value)
            || value == null)
        {
            return null;
        }

        string text = value switch
        {
            Guid guid => guid.ToString("D"),
            string source => source,
            _ => value.ToString() ?? string.Empty
        };
        return string.IsNullOrWhiteSpace(text)
            ? null
            : text.Trim();
    }

    private static BluetoothNativeOperationStatus MapPairing(
        DevicePairingResultStatus status,
        bool requestedPaired) =>
        status switch
        {
            DevicePairingResultStatus.Paired =>
                BluetoothNativeOperationStatus.Succeeded,
            DevicePairingResultStatus.AlreadyPaired =>
                requestedPaired
                    ? BluetoothNativeOperationStatus
                        .AlreadyPaired
                    : BluetoothNativeOperationStatus.Failed,
            DevicePairingResultStatus.PairingCanceled =>
                BluetoothNativeOperationStatus.Canceled,
            DevicePairingResultStatus.AccessDenied =>
                BluetoothNativeOperationStatus.AccessDenied,
            DevicePairingResultStatus.NotReadyToPair
                or DevicePairingResultStatus
                    .OperationAlreadyInProgress =>
                BluetoothNativeOperationStatus.NotReady,
            DevicePairingResultStatus.AuthenticationFailure
                or DevicePairingResultStatus
                    .AuthenticationTimeout
                or DevicePairingResultStatus
                    .AuthenticationNotAllowed =>
                BluetoothNativeOperationStatus
                    .AuthenticationFailed,
            DevicePairingResultStatus.ConnectionRejected
                or DevicePairingResultStatus
                    .RejectedByHandler =>
                BluetoothNativeOperationStatus.Rejected,
            _ => BluetoothNativeOperationStatus.Failed
        };

    private static BluetoothNativeOperationStatus
        MapUnpairing(
            DeviceUnpairingResultStatus status) =>
        status switch
        {
            DeviceUnpairingResultStatus.Unpaired =>
                BluetoothNativeOperationStatus.Succeeded,
            DeviceUnpairingResultStatus.AlreadyUnpaired =>
                BluetoothNativeOperationStatus.AlreadyUnpaired,
            DeviceUnpairingResultStatus.AccessDenied =>
                BluetoothNativeOperationStatus.AccessDenied,
            _ => BluetoothNativeOperationStatus.Failed
        };
}
