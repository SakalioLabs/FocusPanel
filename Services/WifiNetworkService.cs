using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FocusPanel.Services;

public enum WifiNetworkListStatus
{
    Succeeded,
    NoAdapter,
    AccessDenied,
    RadioOff,
    ServiceUnavailable,
    Failed
}

public sealed record WifiNetworkSnapshot(
    string Key,
    string InterfaceId,
    string ProfileName,
    string DisplayName,
    uint SignalQuality,
    bool IsConnected,
    bool IsSecure,
    bool HasProfile,
    bool IsConnectable)
{
    public bool CanConnect =>
        !IsConnected
        && IsConnectable
        && HasProfile
        && !string.IsNullOrWhiteSpace(
            ProfileName);

    public string SignalText =>
        $"{Math.Min(SignalQuality, 100)}%";

    public string SecurityText =>
        IsConnected
            ? "已连接"
            : !IsConnectable
                ? "暂时不可连接"
            : HasProfile
                ? IsSecure
                    ? "已保存 · 安全网络"
                    : "已保存 · 开放网络"
                : IsSecure
                    ? "需要密码"
                    : "未保存";

    public string ActionText =>
        IsConnected
            ? "当前"
            : CanConnect
                ? "连接"
                : IsConnectable
                    ? "打开系统面板"
                    : "不可连接";

    public bool CanInvokeAction =>
        !IsConnected
        && (CanConnect
            || (IsConnectable
                && !HasProfile));
}

public sealed record WifiNetworkListResult(
    WifiNetworkListStatus Status,
    IReadOnlyList<WifiNetworkSnapshot> Networks)
{
    public bool Succeeded =>
        Status == WifiNetworkListStatus.Succeeded;
}

public enum WifiNetworkConnectStatus
{
    Succeeded,
    NeedsCredentials,
    NotFound,
    AccessDenied,
    RadioOff,
    ServiceUnavailable,
    NotConfirmed,
    Failed
}

public readonly record struct WifiNetworkConnectResult(
    WifiNetworkConnectStatus Status,
    string DisplayName)
{
    public bool Succeeded =>
        Status == WifiNetworkConnectStatus.Succeeded;
}

public interface IWifiNetworkService : IDisposable
{
    Task<WifiNetworkListResult> GetNetworksAsync(
        bool requestScan,
        CancellationToken cancellationToken);

    Task<WifiNetworkConnectResult> ConnectAsync(
        WifiNetworkSnapshot network,
        CancellationToken cancellationToken);
}

internal enum WifiNativeConnectRequestStatus
{
    Accepted,
    NotFound,
    AccessDenied,
    RadioOff,
    ServiceUnavailable,
    Failed
}

internal interface IWifiNetworkNativeApi
{
    Task<WifiNetworkListResult> GetNetworksAsync(
        bool requestScan,
        CancellationToken cancellationToken);

    Task<WifiNativeConnectRequestStatus>
        RequestConnectAsync(
            string interfaceId,
            string profileName,
            CancellationToken cancellationToken);
}

public sealed class WifiNetworkService :
    IWifiNetworkService
{
    private readonly IWifiNetworkNativeApi _nativeApi;
    private readonly SemaphoreSlim _connectGate =
        new(1, 1);
    private readonly int _confirmationAttempts;
    private readonly TimeSpan _confirmationDelay;
    private bool _isDisposed;

    public WifiNetworkService()
        : this(
            new NativeWifiNetworkApi(),
            24,
            TimeSpan.FromMilliseconds(300))
    {
    }

    internal WifiNetworkService(
        IWifiNetworkNativeApi nativeApi,
        int confirmationAttempts = 24,
        TimeSpan? confirmationDelay = null)
    {
        _nativeApi =
            nativeApi
            ?? throw new ArgumentNullException(
                nameof(nativeApi));
        _confirmationAttempts =
            Math.Max(1, confirmationAttempts);
        _confirmationDelay =
            confirmationDelay
            ?? TimeSpan.FromMilliseconds(300);
    }

    public async Task<WifiNetworkListResult>
        GetNetworksAsync(
            bool requestScan,
            CancellationToken cancellationToken)
    {
        if (_isDisposed)
        {
            return Empty(
                WifiNetworkListStatus.Failed);
        }

        WifiNetworkListResult result;
        try
        {
            result =
                await _nativeApi
                    .GetNetworksAsync(
                        requestScan,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Empty(
                WifiNetworkListStatus.Failed);
        }

        if (!result.Succeeded)
            return Empty(result.Status);

        WifiNetworkSnapshot[] networks =
            result.Networks
                .Where(network =>
                    !string.IsNullOrWhiteSpace(
                        network.DisplayName))
                .GroupBy(
                    network =>
                        network.Key,
                    StringComparer.Ordinal)
                .Select(group =>
                    group
                        .OrderByDescending(network =>
                            network.IsConnected)
                        .ThenByDescending(network =>
                            network.HasProfile)
                        .ThenByDescending(network =>
                            network.SignalQuality)
                        .First())
                .OrderByDescending(network =>
                    network.IsConnected)
                .ThenByDescending(network =>
                    network.SignalQuality)
                .ThenBy(
                    network =>
                        network.DisplayName,
                    StringComparer
                        .CurrentCultureIgnoreCase)
                .Take(10)
                .ToArray();
        return new WifiNetworkListResult(
            WifiNetworkListStatus.Succeeded,
            networks);
    }

    public async Task<WifiNetworkConnectResult>
        ConnectAsync(
            WifiNetworkSnapshot network,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(network);
        if (!network.HasProfile
            || string.IsNullOrWhiteSpace(
                network.ProfileName))
        {
            return new WifiNetworkConnectResult(
                WifiNetworkConnectStatus
                    .NeedsCredentials,
                network.DisplayName);
        }

        if (_isDisposed)
        {
            return new WifiNetworkConnectResult(
                WifiNetworkConnectStatus.Failed,
                network.DisplayName);
        }

        await _connectGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            WifiNativeConnectRequestStatus request =
                await _nativeApi
                    .RequestConnectAsync(
                        network.InterfaceId,
                        network.ProfileName,
                        cancellationToken)
                    .ConfigureAwait(false);
            WifiNetworkConnectStatus? failure =
                MapRequestFailure(request);
            if (failure.HasValue)
            {
                return new WifiNetworkConnectResult(
                    failure.Value,
                    network.DisplayName);
            }

            for (int attempt = 0;
                 attempt < _confirmationAttempts;
                 attempt++)
            {
                WifiNetworkListResult current =
                    await _nativeApi
                        .GetNetworksAsync(
                            false,
                            cancellationToken)
                        .ConfigureAwait(false);
                if (current.Succeeded
                    && current.Networks.Any(item =>
                        item.IsConnected
                        && string.Equals(
                            item.InterfaceId,
                            network.InterfaceId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            item.ProfileName,
                            network.ProfileName,
                            StringComparison.Ordinal)))
                {
                    return new WifiNetworkConnectResult(
                        WifiNetworkConnectStatus
                            .Succeeded,
                        network.DisplayName);
                }

                WifiNetworkConnectStatus?
                    listFailure =
                        MapListFailure(
                            current.Status);
                if (listFailure.HasValue)
                {
                    return new WifiNetworkConnectResult(
                        listFailure.Value,
                        network.DisplayName);
                }

                if (attempt
                    < _confirmationAttempts - 1)
                {
                    await Task.Delay(
                            _confirmationDelay,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            return new WifiNetworkConnectResult(
                WifiNetworkConnectStatus.NotConfirmed,
                network.DisplayName);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new WifiNetworkConnectResult(
                WifiNetworkConnectStatus.Failed,
                network.DisplayName);
        }
        finally
        {
            _connectGate.Release();
        }
    }

    private static WifiNetworkConnectStatus?
        MapRequestFailure(
            WifiNativeConnectRequestStatus status) =>
        status switch
        {
            WifiNativeConnectRequestStatus.Accepted =>
                null,
            WifiNativeConnectRequestStatus.NotFound =>
                WifiNetworkConnectStatus.NotFound,
            WifiNativeConnectRequestStatus
                .AccessDenied =>
                WifiNetworkConnectStatus.AccessDenied,
            WifiNativeConnectRequestStatus.RadioOff =>
                WifiNetworkConnectStatus.RadioOff,
            WifiNativeConnectRequestStatus
                .ServiceUnavailable =>
                WifiNetworkConnectStatus
                    .ServiceUnavailable,
            _ => WifiNetworkConnectStatus.Failed
        };

    private static WifiNetworkConnectStatus?
        MapListFailure(
            WifiNetworkListStatus status) =>
        status switch
        {
            WifiNetworkListStatus.Succeeded =>
                null,
            WifiNetworkListStatus.AccessDenied =>
                WifiNetworkConnectStatus.AccessDenied,
            WifiNetworkListStatus.RadioOff =>
                WifiNetworkConnectStatus.RadioOff,
            WifiNetworkListStatus
                .ServiceUnavailable =>
                WifiNetworkConnectStatus
                    .ServiceUnavailable,
            WifiNetworkListStatus.NoAdapter =>
                WifiNetworkConnectStatus.NotFound,
            _ => null
        };

    private static WifiNetworkListResult Empty(
        WifiNetworkListStatus status) =>
        new(status, Array.Empty<WifiNetworkSnapshot>());

    public void Dispose()
    {
        _isDisposed = true;
    }
}

internal sealed class NativeWifiNetworkApi :
    IWifiNetworkNativeApi
{
    public Task<WifiNetworkListResult>
        GetNetworksAsync(
            bool requestScan,
            CancellationToken cancellationToken) =>
        Task.Run(
            () =>
                ReadNetworks(
                    requestScan,
                    cancellationToken),
            cancellationToken);

    public Task<WifiNativeConnectRequestStatus>
        RequestConnectAsync(
            string interfaceId,
            string profileName,
            CancellationToken cancellationToken) =>
        Task.Run(
            () =>
                RequestConnect(
                    interfaceId,
                    profileName,
                    cancellationToken),
            cancellationToken);

    private static WifiNetworkListResult
        ReadNetworks(
            bool requestScan,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();
        uint openStatus =
            NativeMethods.WlanOpenHandle(
                2,
                IntPtr.Zero,
                out _,
                out IntPtr clientHandle);
        if (openStatus != NativeMethods.ErrorSuccess)
            return Empty(MapListStatus(openStatus));

        try
        {
            InterfaceObservation[] interfaces =
                EnumerateInterfaces(
                    clientHandle,
                    out uint enumStatus);
            if (enumStatus
                != NativeMethods.ErrorSuccess)
            {
                return Empty(
                    MapListStatus(enumStatus));
            }
            if (interfaces.Length == 0)
            {
                return Empty(
                    WifiNetworkListStatus.NoAdapter);
            }

            if (requestScan)
            {
                uint scanStatus =
                    ScanAndWait(
                        clientHandle,
                        interfaces,
                        cancellationToken);
                if (scanStatus
                    is NativeMethods.ErrorAccessDenied
                    or NativeMethods
                        .ErrorServiceNotActive)
                {
                    return Empty(
                        MapListStatus(scanStatus));
                }
            }

            var networks =
                new List<WifiNetworkSnapshot>();
            bool sawRadioOff = false;
            bool sawSuccess = false;
            foreach (InterfaceObservation adapter
                     in interfaces)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();
                uint status =
                    ReadAvailableNetworks(
                        clientHandle,
                        adapter,
                        networks);
                if (status
                    == NativeMethods.ErrorSuccess)
                {
                    sawSuccess = true;
                }
                else if (status
                         == NativeMethods
                             .ErrorRadioPowerInvalid)
                {
                    sawRadioOff = true;
                }
                else if (status
                         is NativeMethods.ErrorAccessDenied
                         or NativeMethods
                             .ErrorServiceNotActive)
                {
                    return Empty(
                        MapListStatus(status));
                }
            }

            if (sawSuccess)
            {
                return new WifiNetworkListResult(
                    WifiNetworkListStatus.Succeeded,
                    networks);
            }

            return Empty(
                sawRadioOff
                    ? WifiNetworkListStatus.RadioOff
                    : WifiNetworkListStatus.Failed);
        }
        finally
        {
            NativeMethods.WlanCloseHandle(
                clientHandle,
                IntPtr.Zero);
        }
    }

    private static WifiNativeConnectRequestStatus
        RequestConnect(
            string interfaceId,
            string profileName,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();
        if (!Guid.TryParse(
                interfaceId,
                out Guid interfaceGuid)
            || string.IsNullOrWhiteSpace(
                profileName))
        {
            return WifiNativeConnectRequestStatus
                .NotFound;
        }

        uint openStatus =
            NativeMethods.WlanOpenHandle(
                2,
                IntPtr.Zero,
                out _,
                out IntPtr clientHandle);
        if (openStatus != NativeMethods.ErrorSuccess)
            return MapConnectStatus(openStatus);

        try
        {
            var parameters =
                new WlanConnectionParameters
                {
                    ConnectionMode =
                        WlanConnectionMode.Profile,
                    Profile = profileName,
                    Dot11Ssid = IntPtr.Zero,
                    DesiredBssidList = IntPtr.Zero,
                    Dot11BssType =
                        Dot11BssType.Any,
                    Flags = 0
                };
            uint status =
                NativeMethods.WlanConnect(
                    clientHandle,
                    ref interfaceGuid,
                    ref parameters,
                    IntPtr.Zero);
            return status
                   == NativeMethods.ErrorSuccess
                ? WifiNativeConnectRequestStatus.Accepted
                : MapConnectStatus(status);
        }
        finally
        {
            NativeMethods.WlanCloseHandle(
                clientHandle,
                IntPtr.Zero);
        }
    }

    private static uint ScanAndWait(
        IntPtr clientHandle,
        IReadOnlyList<InterfaceObservation>
            interfaces,
        CancellationToken cancellationToken)
    {
        var pending =
            new HashSet<Guid>(
                interfaces.Select(item =>
                    item.Id));
        using var completed =
            new ManualResetEventSlim(
                pending.Count == 0);
        object sync = new();
        WlanNotificationCallback callback =
            (ref WlanNotificationData data,
                IntPtr _) =>
            {
                if (data.NotificationSource
                    != NativeMethods
                        .NotificationSourceAcm
                    || data.NotificationCode
                        is not (
                            NativeMethods
                                .AcmScanComplete
                            or NativeMethods
                                .AcmScanFailed))
                {
                    return;
                }

                lock (sync)
                {
                    pending.Remove(
                        data.InterfaceGuid);
                    if (pending.Count == 0)
                    {
                        try
                        {
                            completed.Set();
                        }
                        catch (
                            ObjectDisposedException)
                        {
                            // A queued native callback can race
                            // with notification teardown.
                        }
                    }
                }
            };

        uint registerStatus =
            NativeMethods.WlanRegisterNotification(
                clientHandle,
                NativeMethods.NotificationSourceAcm,
                false,
                callback,
                IntPtr.Zero,
                IntPtr.Zero,
                out _);
        if (registerStatus
            != NativeMethods.ErrorSuccess)
        {
            return registerStatus;
        }

        uint firstFailure =
            NativeMethods.ErrorSuccess;
        try
        {
            foreach (InterfaceObservation adapter
                     in interfaces)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();
                Guid id = adapter.Id;
                uint status =
                    NativeMethods.WlanScan(
                        clientHandle,
                        ref id,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero);
                if (status
                    != NativeMethods.ErrorSuccess)
                {
                    lock (sync)
                    {
                        pending.Remove(id);
                        if (pending.Count == 0)
                        {
                            try
                            {
                                completed.Set();
                            }
                            catch (
                                ObjectDisposedException)
                            {
                                // Notification teardown won.
                            }
                        }
                    }

                    if (firstFailure
                        == NativeMethods.ErrorSuccess)
                    {
                        firstFailure = status;
                    }
                }
            }

            completed.Wait(
                TimeSpan.FromSeconds(4),
                cancellationToken);
            return firstFailure;
        }
        finally
        {
            NativeMethods.WlanRegisterNotification(
                clientHandle,
                NativeMethods.NotificationSourceNone,
                false,
                null,
                IntPtr.Zero,
                IntPtr.Zero,
                out _);
            GC.KeepAlive(callback);
        }
    }

    private static InterfaceObservation[]
        EnumerateInterfaces(
            IntPtr clientHandle,
            out uint status)
    {
        status =
            NativeMethods.WlanEnumInterfaces(
                clientHandle,
                IntPtr.Zero,
                out IntPtr listPointer);
        if (status != NativeMethods.ErrorSuccess
            || listPointer == IntPtr.Zero)
        {
            return Array.Empty<
                InterfaceObservation>();
        }

        try
        {
            uint count =
                unchecked(
                    (uint)Marshal.ReadInt32(
                        listPointer));
            int offset = sizeof(uint) * 2;
            int itemSize =
                Marshal.SizeOf<WlanInterfaceInfo>();
            var result =
                new List<InterfaceObservation>(
                    checked((int)count));
            for (uint index = 0;
                 index < count;
                 index++)
            {
                IntPtr itemPointer =
                    IntPtr.Add(
                        listPointer,
                        checked(
                            offset
                            + (int)index
                            * itemSize));
                WlanInterfaceInfo item =
                    Marshal.PtrToStructure<
                        WlanInterfaceInfo>(
                        itemPointer);
                result.Add(
                    new InterfaceObservation(
                        item.InterfaceGuid,
                        item.Description?.Trim()
                        ?? string.Empty));
            }

            return result.ToArray();
        }
        finally
        {
            NativeMethods.WlanFreeMemory(
                listPointer);
        }
    }

    private static uint ReadAvailableNetworks(
        IntPtr clientHandle,
        InterfaceObservation adapter,
        ICollection<WifiNetworkSnapshot>
            destination)
    {
        Guid id = adapter.Id;
        uint status =
            NativeMethods
                .WlanGetAvailableNetworkList(
                    clientHandle,
                    ref id,
                    0,
                    IntPtr.Zero,
                    out IntPtr listPointer);
        if (status != NativeMethods.ErrorSuccess
            || listPointer == IntPtr.Zero)
        {
            return status;
        }

        try
        {
            uint count =
                unchecked(
                    (uint)Marshal.ReadInt32(
                        listPointer));
            int offset = sizeof(uint) * 2;
            int itemSize =
                Marshal.SizeOf<
                    WlanAvailableNetwork>();
            for (uint index = 0;
                 index < count;
                 index++)
            {
                IntPtr itemPointer =
                    IntPtr.Add(
                        listPointer,
                        checked(
                            offset
                            + (int)index
                            * itemSize));
                WlanAvailableNetwork item =
                    Marshal.PtrToStructure<
                        WlanAvailableNetwork>(
                        itemPointer);
                string ssid =
                    DecodeSsid(item.Dot11Ssid);
                string profile =
                    item.ProfileName?.Trim()
                    ?? string.Empty;
                string displayName =
                    string.IsNullOrWhiteSpace(
                        ssid)
                        ? string.IsNullOrWhiteSpace(
                            profile)
                            ? "隐藏网络"
                            : profile
                        : ssid;
                bool connected =
                    (item.Flags
                     & NativeMethods
                         .AvailableNetworkConnected)
                    != 0;
                bool hasProfile =
                    (item.Flags
                     & NativeMethods
                         .AvailableNetworkHasProfile)
                    != 0
                    && !string.IsNullOrWhiteSpace(
                        profile);
                string interfaceId =
                    adapter.Id.ToString("D");
                byte[] ssidBytes =
                    GetSsidBytes(
                        item.Dot11Ssid);
                string identity =
                    ssidBytes.Length > 0
                        ? Convert.ToBase64String(
                            ssidBytes)
                        : $"profile:{profile}";
                string key =
                    string.Join(
                        "|",
                        interfaceId,
                        identity);
                destination.Add(
                    new WifiNetworkSnapshot(
                        key,
                        interfaceId,
                        profile,
                        displayName,
                        Math.Min(
                            item.SignalQuality,
                            100),
                        connected,
                        item.SecurityEnabled,
                        hasProfile,
                        item.NetworkConnectable));
            }

            return NativeMethods.ErrorSuccess;
        }
        finally
        {
            NativeMethods.WlanFreeMemory(
                listPointer);
        }
    }

    private static string DecodeSsid(
        Dot11Ssid ssid)
    {
        byte[] bytes = GetSsidBytes(ssid);
        if (bytes.Length == 0)
            return string.Empty;

        try
        {
            return new UTF8Encoding(
                    false,
                    true)
                .GetString(bytes)
                .Trim();
        }
        catch (DecoderFallbackException)
        {
            return "SSID "
                   + Convert.ToHexString(bytes);
        }
    }

    private static byte[] GetSsidBytes(
        Dot11Ssid ssid)
    {
        if (ssid.Ssid == null
            || ssid.SsidLength == 0)
        {
            return Array.Empty<byte>();
        }

        int length =
            Math.Min(
                checked((int)ssid.SsidLength),
                Math.Min(
                    ssid.Ssid.Length,
                    32));
        return ssid.Ssid
            .Take(length)
            .ToArray();
    }

    private static WifiNetworkListStatus
        MapListStatus(uint status) =>
        status switch
        {
            NativeMethods.ErrorAccessDenied =>
                WifiNetworkListStatus.AccessDenied,
            NativeMethods.ErrorRadioPowerInvalid =>
                WifiNetworkListStatus.RadioOff,
            NativeMethods.ErrorServiceNotActive =>
                WifiNetworkListStatus
                    .ServiceUnavailable,
            _ => WifiNetworkListStatus.Failed
        };

    private static
        WifiNativeConnectRequestStatus
        MapConnectStatus(uint status) =>
        status switch
        {
            NativeMethods.ErrorAccessDenied =>
                WifiNativeConnectRequestStatus
                    .AccessDenied,
            NativeMethods.ErrorRadioPowerInvalid =>
                WifiNativeConnectRequestStatus.RadioOff,
            NativeMethods.ErrorServiceNotActive =>
                WifiNativeConnectRequestStatus
                    .ServiceUnavailable,
            NativeMethods.ErrorNotFound =>
                WifiNativeConnectRequestStatus.NotFound,
            _ =>
                WifiNativeConnectRequestStatus.Failed
        };

    private static WifiNetworkListResult Empty(
        WifiNetworkListStatus status) =>
        new(status, Array.Empty<WifiNetworkSnapshot>());

    private sealed record InterfaceObservation(
        Guid Id,
        string Description);
}

[UnmanagedFunctionPointer(
    CallingConvention.Winapi)]
internal delegate void WlanNotificationCallback(
    ref WlanNotificationData data,
    IntPtr context);

[StructLayout(LayoutKind.Sequential)]
internal struct WlanNotificationData
{
    internal uint NotificationSource;
    internal uint NotificationCode;
    internal Guid InterfaceGuid;
    internal uint DataSize;
    internal IntPtr DataPointer;
}

internal enum WlanConnectionMode
{
    Profile = 0
}

internal enum Dot11BssType
{
    Infrastructure = 1,
    Independent = 2,
    Any = 3
}

[StructLayout(
    LayoutKind.Sequential,
    CharSet = CharSet.Unicode)]
internal struct WlanConnectionParameters
{
    internal WlanConnectionMode ConnectionMode;

    [MarshalAs(UnmanagedType.LPWStr)]
    internal string Profile;

    internal IntPtr Dot11Ssid;
    internal IntPtr DesiredBssidList;
    internal Dot11BssType Dot11BssType;
    internal uint Flags;
}

[StructLayout(
    LayoutKind.Sequential,
    CharSet = CharSet.Unicode)]
internal struct WlanInterfaceInfo
{
    internal Guid InterfaceGuid;

    [MarshalAs(
        UnmanagedType.ByValTStr,
        SizeConst = 256)]
    internal string Description;

    internal int State;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Dot11Ssid
{
    internal uint SsidLength;

    [MarshalAs(
        UnmanagedType.ByValArray,
        SizeConst = 32,
        ArraySubType = UnmanagedType.U1)]
    internal byte[] Ssid;
}

[StructLayout(
    LayoutKind.Sequential,
    CharSet = CharSet.Unicode)]
internal struct WlanAvailableNetwork
{
    [MarshalAs(
        UnmanagedType.ByValTStr,
        SizeConst = 256)]
    internal string ProfileName;

    internal Dot11Ssid Dot11Ssid;
    internal Dot11BssType Dot11BssType;
    internal uint NumberOfBssids;

    [MarshalAs(UnmanagedType.Bool)]
    internal bool NetworkConnectable;

    internal uint NotConnectableReason;
    internal uint NumberOfPhyTypes;

    [MarshalAs(
        UnmanagedType.ByValArray,
        SizeConst = 8)]
    internal uint[] PhyTypes;

    [MarshalAs(UnmanagedType.Bool)]
    internal bool MorePhyTypes;

    internal uint SignalQuality;

    [MarshalAs(UnmanagedType.Bool)]
    internal bool SecurityEnabled;

    internal uint DefaultAuthAlgorithm;
    internal uint DefaultCipherAlgorithm;
    internal uint Flags;
    internal uint Reserved;
}

internal static class NativeMethods
{
    internal const uint ErrorSuccess = 0;
    internal const uint ErrorAccessDenied = 5;
    internal const uint ErrorServiceNotActive = 1062;
    internal const uint ErrorNotFound = 1168;
    internal const uint ErrorRadioPowerInvalid =
        0x80342002;
    internal const uint NotificationSourceNone = 0;
    internal const uint NotificationSourceAcm =
        0x00000008;
    internal const uint AcmScanComplete = 7;
    internal const uint AcmScanFailed = 8;
    internal const uint AvailableNetworkConnected =
        0x00000001;
    internal const uint AvailableNetworkHasProfile =
        0x00000002;

    [DllImport("wlanapi.dll")]
    internal static extern uint WlanOpenHandle(
        uint clientVersion,
        IntPtr reserved,
        out uint negotiatedVersion,
        out IntPtr clientHandle);

    [DllImport("wlanapi.dll")]
    internal static extern uint WlanCloseHandle(
        IntPtr clientHandle,
        IntPtr reserved);

    [DllImport("wlanapi.dll")]
    internal static extern uint WlanEnumInterfaces(
        IntPtr clientHandle,
        IntPtr reserved,
        out IntPtr interfaceList);

    [DllImport("wlanapi.dll")]
    internal static extern uint WlanGetAvailableNetworkList(
        IntPtr clientHandle,
        ref Guid interfaceGuid,
        uint flags,
        IntPtr reserved,
        out IntPtr availableNetworkList);

    [DllImport("wlanapi.dll")]
    internal static extern uint WlanScan(
        IntPtr clientHandle,
        ref Guid interfaceGuid,
        IntPtr dot11Ssid,
        IntPtr ieData,
        IntPtr reserved);

    [DllImport("wlanapi.dll")]
    internal static extern uint WlanRegisterNotification(
        IntPtr clientHandle,
        uint notificationSource,
        [MarshalAs(UnmanagedType.Bool)]
        bool ignoreDuplicate,
        WlanNotificationCallback? callback,
        IntPtr callbackContext,
        IntPtr reserved,
        out uint previousNotificationSource);

    [DllImport(
        "wlanapi.dll",
        CharSet = CharSet.Unicode)]
    internal static extern uint WlanConnect(
        IntPtr clientHandle,
        ref Guid interfaceGuid,
        ref WlanConnectionParameters
            connectionParameters,
        IntPtr reserved);

    [DllImport("wlanapi.dll")]
    internal static extern void WlanFreeMemory(
        IntPtr memory);
}
