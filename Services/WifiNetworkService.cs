using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
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

public enum WifiNetworkSecurityKind
{
    Open,
    WpaPersonal,
    Wpa2Personal,
    Wpa3Personal,
    Enterprise,
    Unsupported
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
    bool IsConnectable,
    WifiNetworkSecurityKind SecurityKind =
        WifiNetworkSecurityKind.Wpa2Personal,
    string SsidHex = "",
    uint AuthenticationAlgorithm = 7,
    uint CipherAlgorithm = 4)
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
                : SecurityKind ==
                    WifiNetworkSecurityKind.Enterprise
                    ? "组织网络 · 需要企业凭据"
                : SecurityKind ==
                    WifiNetworkSecurityKind.Unsupported
                    ? "安全模式暂不支持"
                : IsSecure
                    ? "需要密码"
                    : "未保存";

    public string ActionText =>
        IsConnected
            ? "断开"
            : CanConnect
                ? "连接"
                : !HasProfile
                    && SecurityKind ==
                        WifiNetworkSecurityKind.Open
                    ? "连接"
                : !HasProfile
                    && SecurityKind is
                        WifiNetworkSecurityKind.WpaPersonal
                        or WifiNetworkSecurityKind.Wpa2Personal
                        or WifiNetworkSecurityKind.Wpa3Personal
                    ? "输入密码"
                : SecurityKind ==
                    WifiNetworkSecurityKind.Enterprise
                    ? "组织登录"
                : IsConnectable
                    ? "暂不支持"
                    : "不可连接";

    public bool CanInvokeAction =>
        IsConnected
        || (IsConnectable
            && (CanConnect
            || (!HasProfile
                && SecurityKind is
                    WifiNetworkSecurityKind.Open
                    or WifiNetworkSecurityKind.WpaPersonal
                    or WifiNetworkSecurityKind.Wpa2Personal
                    or WifiNetworkSecurityKind.Wpa3Personal)));

    public bool CanForget =>
        HasProfile
        && !string.IsNullOrWhiteSpace(ProfileName);

    public bool NeedsCredentials =>
        !HasProfile
        && SecurityKind is
            WifiNetworkSecurityKind.WpaPersonal
            or WifiNetworkSecurityKind.Wpa2Personal
            or WifiNetworkSecurityKind.Wpa3Personal;
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
    InvalidCredentials,
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

public enum WifiNetworkManageAction
{
    Disconnect,
    Forget
}

public enum WifiNetworkManageStatus
{
    Succeeded,
    AlreadyInDesiredState,
    NotFound,
    AccessDenied,
    RadioOff,
    ServiceUnavailable,
    NotConfirmed,
    Failed
}

public readonly record struct WifiNetworkManageResult(
    WifiNetworkManageStatus Status,
    WifiNetworkManageAction Action,
    string DisplayName)
{
    public bool Succeeded =>
        Status is WifiNetworkManageStatus.Succeeded
            or WifiNetworkManageStatus.AlreadyInDesiredState;
}

public interface IWifiNetworkService : IDisposable
{
    Task<WifiNetworkListResult> GetNetworksAsync(
        bool requestScan,
        CancellationToken cancellationToken);

    Task<WifiNetworkConnectResult> ConnectAsync(
        WifiNetworkSnapshot network,
        CancellationToken cancellationToken);

    Task<WifiNetworkConnectResult>
        ConnectWithCredentialsAsync(
            WifiNetworkSnapshot network,
            SecureString password,
            CancellationToken cancellationToken);

    Task<WifiNetworkManageResult> DisconnectAsync(
        WifiNetworkSnapshot network,
        CancellationToken cancellationToken);

    Task<WifiNetworkManageResult> ForgetAsync(
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

internal enum WifiNativeManageRequestStatus
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

    Task<WifiNativeConnectRequestStatus>
        RequestProfileConnectAsync(
            string interfaceId,
            string profileName,
            char[] profileXml,
            CancellationToken cancellationToken);

    Task RemoveProfileAsync(
        string interfaceId,
        string profileName,
        CancellationToken cancellationToken);

    Task<WifiNativeManageRequestStatus>
        RequestDisconnectAsync(
            string interfaceId,
            CancellationToken cancellationToken);

    Task<WifiNativeManageRequestStatus>
        RequestForgetProfileAsync(
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

    public async Task<WifiNetworkConnectResult>
        ConnectWithCredentialsAsync(
            WifiNetworkSnapshot network,
            SecureString password,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(password);
        if (network.HasProfile)
        {
            return await ConnectAsync(
                    network,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (_isDisposed
            || !network.IsConnectable
            || network.SecurityKind is
                WifiNetworkSecurityKind.Enterprise
                or WifiNetworkSecurityKind.Unsupported)
        {
            return new WifiNetworkConnectResult(
                WifiNetworkConnectStatus.Failed,
                network.DisplayName);
        }

        if (!WifiProfileXmlBuilder
                .IsCredentialValid(
                    network.SecurityKind,
                    password))
        {
            return new WifiNetworkConnectResult(
                WifiNetworkConnectStatus
                    .InvalidCredentials,
                network.DisplayName);
        }

        char[] profileXml =
            WifiProfileXmlBuilder.Build(
                network,
                password);
        try
        {
            return await ConnectProfileAndConfirmAsync(
                    network,
                    profileXml,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            Array.Clear(
                profileXml,
                0,
                profileXml.Length);
        }
    }

    public async Task<WifiNetworkManageResult>
        DisconnectAsync(
            WifiNetworkSnapshot network,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(network);
        if (!network.IsConnected)
        {
            return ManageResult(
                WifiNetworkManageStatus
                    .AlreadyInDesiredState,
                WifiNetworkManageAction.Disconnect,
                network);
        }

        return await ManageNetworkAsync(
                network,
                WifiNetworkManageAction.Disconnect,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WifiNetworkManageResult>
        ForgetAsync(
            WifiNetworkSnapshot network,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(network);
        if (!network.HasProfile
            || string.IsNullOrWhiteSpace(
                network.ProfileName))
        {
            return ManageResult(
                WifiNetworkManageStatus
                    .AlreadyInDesiredState,
                WifiNetworkManageAction.Forget,
                network);
        }

        return await ManageNetworkAsync(
                network,
                WifiNetworkManageAction.Forget,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<WifiNetworkManageResult>
        ManageNetworkAsync(
            WifiNetworkSnapshot network,
            WifiNetworkManageAction action,
            CancellationToken cancellationToken)
    {
        if (_isDisposed)
        {
            return ManageResult(
                WifiNetworkManageStatus.Failed,
                action,
                network);
        }

        await _connectGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            if (network.IsConnected)
            {
                WifiNativeManageRequestStatus request =
                    await _nativeApi
                        .RequestDisconnectAsync(
                            network.InterfaceId,
                            cancellationToken)
                        .ConfigureAwait(false);
                WifiNetworkManageStatus? failure =
                    MapManageRequestFailure(request);
                if (failure.HasValue)
                {
                    return ManageResult(
                        failure.Value,
                        action,
                        network);
                }

                WifiNetworkManageStatus disconnected =
                    await ConfirmDisconnectedAsync(
                            network,
                            cancellationToken)
                        .ConfigureAwait(false);
                if (disconnected
                    != WifiNetworkManageStatus.Succeeded)
                {
                    return ManageResult(
                        disconnected,
                        action,
                        network);
                }
            }

            if (action
                == WifiNetworkManageAction.Disconnect)
            {
                return ManageResult(
                    WifiNetworkManageStatus.Succeeded,
                    action,
                    network);
            }

            WifiNativeManageRequestStatus forgetRequest =
                await _nativeApi
                    .RequestForgetProfileAsync(
                        network.InterfaceId,
                        network.ProfileName,
                        cancellationToken)
                    .ConfigureAwait(false);
            WifiNetworkManageStatus? forgetFailure =
                MapManageRequestFailure(forgetRequest);
            if (forgetFailure.HasValue)
            {
                return ManageResult(
                    forgetFailure.Value,
                    action,
                    network);
            }

            WifiNetworkManageStatus forgotten =
                await ConfirmForgottenAsync(
                        network,
                        cancellationToken)
                    .ConfigureAwait(false);
            return ManageResult(
                forgotten,
                action,
                network);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return ManageResult(
                WifiNetworkManageStatus.Failed,
                action,
                network);
        }
        finally
        {
            _connectGate.Release();
        }
    }

    private Task<WifiNetworkManageStatus>
        ConfirmDisconnectedAsync(
            WifiNetworkSnapshot network,
            CancellationToken cancellationToken) =>
        ConfirmManageStateAsync(
            current =>
                !current.Networks.Any(item =>
                    item.IsConnected
                    && string.Equals(
                        item.InterfaceId,
                        network.InterfaceId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        item.Key,
                        network.Key,
                        StringComparison.Ordinal)),
            cancellationToken);

    private Task<WifiNetworkManageStatus>
        ConfirmForgottenAsync(
            WifiNetworkSnapshot network,
            CancellationToken cancellationToken) =>
        ConfirmManageStateAsync(
            current =>
                !current.Networks.Any(item =>
                    item.HasProfile
                    && string.Equals(
                        item.InterfaceId,
                        network.InterfaceId,
                        StringComparison.Ordinal)
                    && (string.Equals(
                            item.Key,
                            network.Key,
                            StringComparison.Ordinal)
                        || string.Equals(
                            item.ProfileName,
                            network.ProfileName,
                            StringComparison.Ordinal))),
            cancellationToken);

    private async Task<WifiNetworkManageStatus>
        ConfirmManageStateAsync(
            Func<WifiNetworkListResult, bool>
                isConfirmed,
            CancellationToken cancellationToken)
    {
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
                && isConfirmed(current))
            {
                return WifiNetworkManageStatus.Succeeded;
            }

            WifiNetworkManageStatus? failure =
                MapManageListFailure(current.Status);
            if (failure.HasValue)
                return failure.Value;

            if (attempt
                < _confirmationAttempts - 1)
            {
                await Task.Delay(
                        _confirmationDelay,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return WifiNetworkManageStatus.NotConfirmed;
    }

    private async Task<WifiNetworkConnectResult>
        ConnectProfileAndConfirmAsync(
            WifiNetworkSnapshot network,
            char[] profileXml,
            CancellationToken cancellationToken)
    {
        await _connectGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            WifiNativeConnectRequestStatus request =
                await _nativeApi
                    .RequestProfileConnectAsync(
                        network.InterfaceId,
                        network.DisplayName,
                        profileXml,
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

            WifiNetworkConnectResult result =
                await ConfirmConnectionAsync(
                    network,
                    network.DisplayName,
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.Status
                == WifiNetworkConnectStatus.NotConfirmed)
            {
                try
                {
                    await _nativeApi
                        .RemoveProfileAsync(
                            network.InterfaceId,
                            network.DisplayName,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // The connection result remains authoritative. Cleanup is
                    // best-effort so a wrong first password can be retried.
                }
            }

            return result;
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

    private async Task<WifiNetworkConnectResult>
        ConfirmConnectionAsync(
            WifiNetworkSnapshot network,
            string expectedProfileName,
            CancellationToken cancellationToken)
    {
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
                    && (string.Equals(
                            item.Key,
                            network.Key,
                            StringComparison.Ordinal)
                        || string.Equals(
                            item.ProfileName,
                            expectedProfileName,
                            StringComparison.Ordinal))))
            {
                return new WifiNetworkConnectResult(
                    WifiNetworkConnectStatus.Succeeded,
                    network.DisplayName);
            }

            WifiNetworkConnectStatus? listFailure =
                MapListFailure(current.Status);
            if (listFailure.HasValue)
            {
                return new WifiNetworkConnectResult(
                    listFailure.Value,
                    network.DisplayName);
            }

            if (attempt < _confirmationAttempts - 1)
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

    private static WifiNetworkManageStatus?
        MapManageRequestFailure(
            WifiNativeManageRequestStatus status) =>
        status switch
        {
            WifiNativeManageRequestStatus.Accepted =>
                null,
            WifiNativeManageRequestStatus.NotFound =>
                WifiNetworkManageStatus.NotFound,
            WifiNativeManageRequestStatus.AccessDenied =>
                WifiNetworkManageStatus.AccessDenied,
            WifiNativeManageRequestStatus.RadioOff =>
                WifiNetworkManageStatus.RadioOff,
            WifiNativeManageRequestStatus
                .ServiceUnavailable =>
                WifiNetworkManageStatus
                    .ServiceUnavailable,
            _ => WifiNetworkManageStatus.Failed
        };

    private static WifiNetworkManageStatus?
        MapManageListFailure(
            WifiNetworkListStatus status) =>
        status switch
        {
            WifiNetworkListStatus.Succeeded => null,
            WifiNetworkListStatus.AccessDenied =>
                WifiNetworkManageStatus.AccessDenied,
            WifiNetworkListStatus.RadioOff =>
                WifiNetworkManageStatus.RadioOff,
            WifiNetworkListStatus
                .ServiceUnavailable =>
                WifiNetworkManageStatus
                    .ServiceUnavailable,
            WifiNetworkListStatus.NoAdapter =>
                WifiNetworkManageStatus.NotFound,
            _ => null
        };

    private static WifiNetworkManageResult
        ManageResult(
            WifiNetworkManageStatus status,
            WifiNetworkManageAction action,
            WifiNetworkSnapshot network) =>
        new(status, action, network.DisplayName);

    private static WifiNetworkListResult Empty(
        WifiNetworkListStatus status) =>
        new(status, Array.Empty<WifiNetworkSnapshot>());

    public void Dispose()
    {
        _isDisposed = true;
    }
}

internal static class WifiProfileXmlBuilder
{
    internal static bool IsCredentialValid(
        WifiNetworkSecurityKind securityKind,
        SecureString password) =>
        securityKind == WifiNetworkSecurityKind.Open
            ? password.Length == 0
            : securityKind is
                WifiNetworkSecurityKind.WpaPersonal
                or WifiNetworkSecurityKind.Wpa2Personal
                or WifiNetworkSecurityKind.Wpa3Personal
                && password.Length is >= 8 and <= 63;

    internal static char[] Build(
        WifiNetworkSnapshot network,
        SecureString password)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(password);
        if (!IsCredentialValid(
                network.SecurityKind,
                password))
        {
            throw new ArgumentException(
                "Wi-Fi password does not match the selected security mode.",
                nameof(password));
        }

        (string authentication, string encryption) =
            ResolveSecurity(network);
        var xml = new List<char>(1024);
        Append(
            xml,
            "<?xml version=\"1.0\"?><WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\"><name>");
        AppendEscaped(xml, network.DisplayName);
        Append(xml, "</name><SSIDConfig><SSID><hex>");
        Append(
            xml,
            string.IsNullOrWhiteSpace(network.SsidHex)
                ? Convert.ToHexString(
                    Encoding.UTF8.GetBytes(
                        network.DisplayName))
                : network.SsidHex);
        Append(xml, "</hex><name>");
        AppendEscaped(xml, network.DisplayName);
        Append(
            xml,
            "</name></SSID></SSIDConfig><connectionType>ESS</connectionType><connectionMode>auto</connectionMode><MSM><security><authEncryption><authentication>");
        Append(xml, authentication);
        Append(xml, "</authentication><encryption>");
        Append(xml, encryption);
        Append(
            xml,
            "</encryption><useOneX>false</useOneX></authEncryption>");

        if (network.SecurityKind
            != WifiNetworkSecurityKind.Open)
        {
            Append(
                xml,
                "<sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>");
            AppendSecureEscaped(xml, password);
            Append(xml, "</keyMaterial></sharedKey>");
        }

        Append(xml, "</security></MSM></WLANProfile>");
        xml.Add('\0');
        return xml.ToArray();
    }

    private static (string Authentication,
        string Encryption) ResolveSecurity(
            WifiNetworkSnapshot network) =>
        network.SecurityKind switch
        {
            WifiNetworkSecurityKind.Open =>
                ("open", "none"),
            WifiNetworkSecurityKind.WpaPersonal =>
                ("WPAPSK", ResolveCipher(network)),
            WifiNetworkSecurityKind.Wpa2Personal =>
                ("WPA2PSK", ResolveCipher(network)),
            WifiNetworkSecurityKind.Wpa3Personal =>
                ("WPA3SAE", "AES"),
            _ => throw new NotSupportedException(
                "This Wi-Fi authentication mode cannot be configured by FocusPanel.")
        };

    private static string ResolveCipher(
        WifiNetworkSnapshot network) =>
        network.CipherAlgorithm switch
        {
            2 => "TKIP",
            4 => "AES",
            _ => throw new NotSupportedException(
                "This Wi-Fi cipher cannot be configured by FocusPanel.")
        };

    private static void Append(
        ICollection<char> destination,
        string value)
    {
        foreach (char character in value)
            destination.Add(character);
    }

    private static void AppendEscaped(
        ICollection<char> destination,
        string value)
    {
        foreach (char character in value)
            AppendEscapedCharacter(
                destination,
                character);
    }

    private static void AppendSecureEscaped(
        ICollection<char> destination,
        SecureString value)
    {
        IntPtr pointer = IntPtr.Zero;
        try
        {
            pointer = Marshal
                .SecureStringToGlobalAllocUnicode(
                    value);
            for (int index = 0;
                 index < value.Length;
                 index++)
            {
                AppendEscapedCharacter(
                    destination,
                    unchecked((char)Marshal.ReadInt16(
                        pointer,
                        index * sizeof(char))));
            }
        }
        finally
        {
            if (pointer != IntPtr.Zero)
            {
                Marshal.ZeroFreeGlobalAllocUnicode(
                    pointer);
            }
        }
    }

    private static void AppendEscapedCharacter(
        ICollection<char> destination,
        char character)
    {
        string? replacement = character switch
        {
            '&' => "&amp;",
            '<' => "&lt;",
            '>' => "&gt;",
            '\"' => "&quot;",
            '\'' => "&apos;",
            _ => null
        };
        if (replacement == null)
        {
            destination.Add(character);
            return;
        }

        Append(destination, replacement);
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

    public Task<WifiNativeConnectRequestStatus>
        RequestProfileConnectAsync(
            string interfaceId,
            string profileName,
            char[] profileXml,
            CancellationToken cancellationToken) =>
        Task.Run(
            () =>
                RequestProfileConnect(
                    interfaceId,
                    profileName,
                    profileXml,
                    cancellationToken),
            cancellationToken);

    public Task RemoveProfileAsync(
        string interfaceId,
        string profileName,
        CancellationToken cancellationToken) =>
        Task.Run(
            () =>
                RemoveProfile(
                    interfaceId,
                    profileName,
                    cancellationToken),
            cancellationToken);

    public Task<WifiNativeManageRequestStatus>
        RequestDisconnectAsync(
            string interfaceId,
            CancellationToken cancellationToken) =>
        Task.Run(
            () =>
                RequestDisconnect(
                    interfaceId,
                    cancellationToken),
            cancellationToken);

    public Task<WifiNativeManageRequestStatus>
        RequestForgetProfileAsync(
            string interfaceId,
            string profileName,
            CancellationToken cancellationToken) =>
        Task.Run(
            () =>
                RequestForgetProfile(
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

    private static WifiNativeConnectRequestStatus
        RequestProfileConnect(
            string interfaceId,
            string profileName,
            char[] profileXml,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();
        if (!Guid.TryParse(
                interfaceId,
                out Guid interfaceGuid)
            || string.IsNullOrWhiteSpace(profileName)
            || profileXml.Length == 0)
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

        GCHandle xmlHandle = default;
        try
        {
            xmlHandle = GCHandle.Alloc(
                profileXml,
                GCHandleType.Pinned);
            uint setStatus =
                NativeMethods.WlanSetProfile(
                    clientHandle,
                    ref interfaceGuid,
                    0,
                    xmlHandle.AddrOfPinnedObject(),
                    IntPtr.Zero,
                    true,
                    IntPtr.Zero,
                    out _);
            if (setStatus != NativeMethods.ErrorSuccess)
                return MapConnectStatus(setStatus);

            WifiNativeConnectRequestStatus result =
                RequestConnectWithHandle(
                clientHandle,
                interfaceGuid,
                profileName);
            if (result
                != WifiNativeConnectRequestStatus.Accepted)
            {
                NativeMethods.WlanDeleteProfile(
                    clientHandle,
                    ref interfaceGuid,
                    profileName,
                    IntPtr.Zero);
            }

            return result;
        }
        finally
        {
            if (xmlHandle.IsAllocated)
                xmlHandle.Free();
            NativeMethods.WlanCloseHandle(
                clientHandle,
                IntPtr.Zero);
        }
    }

    private static void RemoveProfile(
        string interfaceId,
        string profileName,
        CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();
        if (!Guid.TryParse(
                interfaceId,
                out Guid interfaceGuid)
            || string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        uint openStatus =
            NativeMethods.WlanOpenHandle(
                2,
                IntPtr.Zero,
                out _,
                out IntPtr clientHandle);
        if (openStatus != NativeMethods.ErrorSuccess)
            return;

        try
        {
            NativeMethods.WlanDeleteProfile(
                clientHandle,
                ref interfaceGuid,
                profileName,
                IntPtr.Zero);
        }
        finally
        {
            NativeMethods.WlanCloseHandle(
                clientHandle,
                IntPtr.Zero);
        }
    }

    private static WifiNativeManageRequestStatus
        RequestDisconnect(
            string interfaceId,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();
        if (!Guid.TryParse(
                interfaceId,
                out Guid interfaceGuid))
        {
            return WifiNativeManageRequestStatus.NotFound;
        }

        uint openStatus =
            NativeMethods.WlanOpenHandle(
                2,
                IntPtr.Zero,
                out _,
                out IntPtr clientHandle);
        if (openStatus != NativeMethods.ErrorSuccess)
            return MapManageStatus(openStatus);

        try
        {
            uint status =
                NativeMethods.WlanDisconnect(
                    clientHandle,
                    ref interfaceGuid,
                    IntPtr.Zero);
            return status == NativeMethods.ErrorSuccess
                ? WifiNativeManageRequestStatus.Accepted
                : MapManageStatus(status);
        }
        finally
        {
            NativeMethods.WlanCloseHandle(
                clientHandle,
                IntPtr.Zero);
        }
    }

    private static WifiNativeManageRequestStatus
        RequestForgetProfile(
            string interfaceId,
            string profileName,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();
        if (!Guid.TryParse(
                interfaceId,
                out Guid interfaceGuid)
            || string.IsNullOrWhiteSpace(profileName))
        {
            return WifiNativeManageRequestStatus.NotFound;
        }

        uint openStatus =
            NativeMethods.WlanOpenHandle(
                2,
                IntPtr.Zero,
                out _,
                out IntPtr clientHandle);
        if (openStatus != NativeMethods.ErrorSuccess)
            return MapManageStatus(openStatus);

        try
        {
            uint status =
                NativeMethods.WlanDeleteProfile(
                    clientHandle,
                    ref interfaceGuid,
                    profileName,
                    IntPtr.Zero);
            return status == NativeMethods.ErrorSuccess
                ? WifiNativeManageRequestStatus.Accepted
                : MapManageStatus(status);
        }
        finally
        {
            NativeMethods.WlanCloseHandle(
                clientHandle,
                IntPtr.Zero);
        }
    }

    private static WifiNativeConnectRequestStatus
        RequestConnectWithHandle(
            IntPtr clientHandle,
            Guid interfaceGuid,
            string profileName)
    {
        var parameters =
            new WlanConnectionParameters
            {
                ConnectionMode =
                    WlanConnectionMode.Profile,
                Profile = profileName,
                Dot11Ssid = IntPtr.Zero,
                DesiredBssidList = IntPtr.Zero,
                Dot11BssType = Dot11BssType.Any,
                Flags = 0
            };
        uint status = NativeMethods.WlanConnect(
            clientHandle,
            ref interfaceGuid,
            ref parameters,
            IntPtr.Zero);
        return status == NativeMethods.ErrorSuccess
            ? WifiNativeConnectRequestStatus.Accepted
            : MapConnectStatus(status);
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
                WifiNetworkSecurityKind
                    securityKind =
                        ssidBytes.Length == 0
                        && !hasProfile
                            ? WifiNetworkSecurityKind
                                .Unsupported
                            : ResolveSecurityKind(item);
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
                        item.NetworkConnectable,
                        securityKind,
                        Convert.ToHexString(ssidBytes),
                        item.DefaultAuthAlgorithm,
                        item.DefaultCipherAlgorithm));
            }

            return NativeMethods.ErrorSuccess;
        }
        finally
        {
            NativeMethods.WlanFreeMemory(
                listPointer);
        }
    }

    private static WifiNetworkSecurityKind
        ResolveSecurityKind(
            WlanAvailableNetwork network)
    {
        if (!network.SecurityEnabled)
            return WifiNetworkSecurityKind.Open;

        return network.DefaultAuthAlgorithm switch
        {
            4 when network.DefaultCipherAlgorithm
                is 2 or 4 =>
                WifiNetworkSecurityKind.WpaPersonal,
            7 when network.DefaultCipherAlgorithm
                is 2 or 4 =>
                WifiNetworkSecurityKind.Wpa2Personal,
            9 when network.DefaultCipherAlgorithm
                == 4 =>
                WifiNetworkSecurityKind.Wpa3Personal,
            3 or 6 or 8 or 11 =>
                WifiNetworkSecurityKind.Enterprise,
            _ => WifiNetworkSecurityKind.Unsupported
        };
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

    private static WifiNativeManageRequestStatus
        MapManageStatus(uint status) =>
        status switch
        {
            NativeMethods.ErrorAccessDenied =>
                WifiNativeManageRequestStatus.AccessDenied,
            NativeMethods.ErrorRadioPowerInvalid =>
                WifiNativeManageRequestStatus.RadioOff,
            NativeMethods.ErrorServiceNotActive =>
                WifiNativeManageRequestStatus
                    .ServiceUnavailable,
            NativeMethods.ErrorNotFound =>
                WifiNativeManageRequestStatus.NotFound,
            _ => WifiNativeManageRequestStatus.Failed
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
    internal static extern uint WlanDisconnect(
        IntPtr clientHandle,
        ref Guid interfaceGuid,
        IntPtr reserved);

    [DllImport(
        "wlanapi.dll",
        CharSet = CharSet.Unicode)]
    internal static extern uint WlanSetProfile(
        IntPtr clientHandle,
        ref Guid interfaceGuid,
        uint flags,
        IntPtr profileXml,
        IntPtr allUserProfileSecurity,
        [MarshalAs(UnmanagedType.Bool)]
        bool overwrite,
        IntPtr reserved,
        out uint reasonCode);

    [DllImport(
        "wlanapi.dll",
        CharSet = CharSet.Unicode)]
    internal static extern uint WlanDeleteProfile(
        IntPtr clientHandle,
        ref Guid interfaceGuid,
        string profileName,
        IntPtr reserved);

    [DllImport("wlanapi.dll")]
    internal static extern void WlanFreeMemory(
        IntPtr memory);
}
