using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Radios;

namespace FocusPanel.Services;

public enum SystemRadioKind
{
    WiFi,
    Bluetooth
}

public enum SystemRadioState
{
    Unavailable,
    Off,
    On,
    Disabled,
    Unknown
}

public readonly record struct SystemRadioSnapshot(
    SystemRadioKind Kind,
    SystemRadioState State,
    string DisplayName)
{
    public bool IsPresent =>
        State != SystemRadioState.Unavailable;

    public bool IsEnabled =>
        State == SystemRadioState.On;

    public bool CanToggle =>
        State
        is SystemRadioState.On
        or SystemRadioState.Off;
}

public enum SystemRadioSetStatus
{
    Succeeded,
    NotFound,
    DeniedByUser,
    DeniedBySystem,
    HardwareDisabled,
    NotConfirmed,
    Failed
}

public readonly record struct SystemRadioSetResult(
    SystemRadioSetStatus Status,
    SystemRadioKind Kind,
    bool RequestedEnabled)
{
    public bool Succeeded =>
        Status == SystemRadioSetStatus.Succeeded;
}

public interface ISystemRadioService : IDisposable
{
    Task<IReadOnlyList<SystemRadioSnapshot>>
        GetStatusAsync(
            CancellationToken cancellationToken);

    Task<SystemRadioSetResult> SetEnabledAsync(
        SystemRadioKind kind,
        bool enabled,
        CancellationToken cancellationToken);
}

internal enum SystemRadioNativeAccessStatus
{
    Unspecified,
    Allowed,
    DeniedByUser,
    DeniedBySystem
}

internal readonly record struct
    SystemRadioNativeObservation(
        string Id,
        string Name,
        SystemRadioKind Kind,
        SystemRadioState State);

internal interface ISystemRadioNativeApi
{
    Task<IReadOnlyList<
        SystemRadioNativeObservation>>
        GetRadiosAsync(
            CancellationToken cancellationToken);

    Task<SystemRadioNativeAccessStatus>
        RequestAccessAsync(
            CancellationToken cancellationToken);

    Task<SystemRadioNativeAccessStatus>
        SetStateAsync(
            string id,
            bool enabled,
            CancellationToken cancellationToken);
}

public sealed class SystemRadioService :
    ISystemRadioService
{
    private readonly ISystemRadioNativeApi _nativeApi;
    private readonly int _confirmationAttempts;
    private readonly TimeSpan _confirmationDelay;
    private readonly SemaphoreSlim _controlGate =
        new(1, 1);
    private SystemRadioNativeAccessStatus?
        _accessStatus;
    private bool _isDisposed;

    public SystemRadioService()
        : this(
            new WinRtSystemRadioNativeApi(),
            5,
            TimeSpan.FromMilliseconds(160))
    {
    }

    internal SystemRadioService(
        ISystemRadioNativeApi nativeApi,
        int confirmationAttempts = 5,
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
            ?? TimeSpan.FromMilliseconds(160);
    }

    public async Task<
        IReadOnlyList<SystemRadioSnapshot>>
        GetStatusAsync(
            CancellationToken cancellationToken)
    {
        if (_isDisposed)
            return UnavailableSnapshots();

        try
        {
            IReadOnlyList<
                SystemRadioNativeObservation>
                observations =
                    await _nativeApi
                        .GetRadiosAsync(
                            cancellationToken)
                        .ConfigureAwait(false);
            return new[]
            {
                Compose(
                    SystemRadioKind.WiFi,
                    observations),
                Compose(
                    SystemRadioKind.Bluetooth,
                    observations)
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return UnavailableSnapshots();
        }
    }

    public async Task<SystemRadioSetResult>
        SetEnabledAsync(
            SystemRadioKind kind,
            bool enabled,
            CancellationToken cancellationToken)
    {
        if (_isDisposed)
        {
            return Result(
                SystemRadioSetStatus.Failed,
                kind,
                enabled);
        }

        await _controlGate.WaitAsync(
            cancellationToken);
        try
        {
            SystemRadioNativeAccessStatus access =
                _accessStatus
                ?? await _nativeApi
                    .RequestAccessAsync(
                        cancellationToken);
            _accessStatus = access;
            SystemRadioSetStatus? denied =
                MapDeniedAccess(access);
            if (denied.HasValue)
            {
                return Result(
                    denied.Value,
                    kind,
                    enabled);
            }

            IReadOnlyList<
                SystemRadioNativeObservation>
                radios =
                    await _nativeApi
                        .GetRadiosAsync(
                            cancellationToken);
            SystemRadioNativeObservation[]
                matching =
                    radios
                        .Where(radio =>
                            radio.Kind == kind)
                        .ToArray();
            if (matching.Length == 0)
            {
                return Result(
                    SystemRadioSetStatus.NotFound,
                    kind,
                    enabled);
            }

            SystemRadioNativeObservation[]
                controllable =
                    matching
                        .Where(radio =>
                            radio.State
                            != SystemRadioState.Disabled)
                        .ToArray();
            if (controllable.Length == 0)
            {
                return Result(
                    SystemRadioSetStatus
                        .HardwareDisabled,
                    kind,
                    enabled);
            }

            foreach (SystemRadioNativeObservation
                     radio in controllable)
            {
                SystemRadioNativeAccessStatus
                    setStatus =
                        await _nativeApi
                            .SetStateAsync(
                                radio.Id,
                                enabled,
                                cancellationToken);
                denied =
                    MapDeniedAccess(setStatus);
                if (denied.HasValue)
                {
                    return Result(
                        denied.Value,
                        kind,
                        enabled);
                }
                if (setStatus
                    != SystemRadioNativeAccessStatus
                        .Allowed)
                {
                    return Result(
                        SystemRadioSetStatus.Failed,
                        kind,
                        enabled);
                }
            }

            for (int attempt = 0;
                 attempt < _confirmationAttempts;
                 attempt++)
            {
                IReadOnlyList<
                    SystemRadioNativeObservation>
                    confirmed =
                        await _nativeApi
                            .GetRadiosAsync(
                                cancellationToken);
                SystemRadioNativeObservation[]
                    current =
                        confirmed
                            .Where(radio =>
                                radio.Kind == kind
                                && radio.State
                                    != SystemRadioState
                                        .Disabled)
                            .ToArray();
                if (current.Length > 0
                    && current.All(radio =>
                        enabled
                            ? radio.State
                                == SystemRadioState.On
                            : radio.State
                                == SystemRadioState.Off))
                {
                    return Result(
                        SystemRadioSetStatus.Succeeded,
                        kind,
                        enabled);
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
                SystemRadioSetStatus.NotConfirmed,
                kind,
                enabled);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(
                SystemRadioSetStatus.Failed,
                kind,
                enabled);
        }
        finally
        {
            _controlGate.Release();
        }
    }

    private static SystemRadioSnapshot Compose(
        SystemRadioKind kind,
        IReadOnlyList<
            SystemRadioNativeObservation>
            observations)
    {
        SystemRadioNativeObservation[] radios =
            observations
                .Where(radio =>
                    radio.Kind == kind)
                .ToArray();
        if (radios.Length == 0)
        {
            return new SystemRadioSnapshot(
                kind,
                SystemRadioState.Unavailable,
                DisplayName(kind));
        }

        SystemRadioState state;
        if (radios.Any(radio =>
                radio.State
                == SystemRadioState.On))
        {
            state = SystemRadioState.On;
        }
        else if (radios.All(radio =>
                     radio.State
                     == SystemRadioState.Disabled))
        {
            state = SystemRadioState.Disabled;
        }
        else if (radios.All(radio =>
                     radio.State
                     is SystemRadioState.Off
                     or SystemRadioState.Disabled))
        {
            state = SystemRadioState.Off;
        }
        else
        {
            state = SystemRadioState.Unknown;
        }

        string name =
            radios
                .Select(radio =>
                    radio.Name?.Trim())
                .FirstOrDefault(value =>
                    !string.IsNullOrWhiteSpace(
                        value))
            ?? DisplayName(kind);
        return new SystemRadioSnapshot(
            kind,
            state,
            name);
    }

    private static SystemRadioSetStatus?
        MapDeniedAccess(
            SystemRadioNativeAccessStatus status) =>
        status switch
        {
            SystemRadioNativeAccessStatus
                .DeniedByUser =>
                SystemRadioSetStatus.DeniedByUser,
            SystemRadioNativeAccessStatus
                .DeniedBySystem =>
                SystemRadioSetStatus.DeniedBySystem,
            _ => null
        };

    private static SystemRadioSetResult Result(
        SystemRadioSetStatus status,
        SystemRadioKind kind,
        bool enabled) =>
        new(status, kind, enabled);

    private static string DisplayName(
        SystemRadioKind kind) =>
        kind == SystemRadioKind.WiFi
            ? "Wi‑Fi"
            : "蓝牙";

    private static IReadOnlyList<
        SystemRadioSnapshot>
        UnavailableSnapshots() =>
        new[]
        {
            new SystemRadioSnapshot(
                SystemRadioKind.WiFi,
                SystemRadioState.Unavailable,
                "Wi‑Fi"),
            new SystemRadioSnapshot(
                SystemRadioKind.Bluetooth,
                SystemRadioState.Unavailable,
                "蓝牙")
        };

    public void Dispose()
    {
        _isDisposed = true;
    }
}

internal sealed class WinRtSystemRadioNativeApi :
    ISystemRadioNativeApi
{
    public async Task<IReadOnlyList<
        SystemRadioNativeObservation>>
        GetRadiosAsync(
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();
        IReadOnlyList<Radio> radios =
            await Radio.GetRadiosAsync();
        cancellationToken
            .ThrowIfCancellationRequested();
        return BuildEntries(radios)
            .Select(entry =>
                new SystemRadioNativeObservation(
                    entry.Id,
                    entry.Radio.Name,
                    MapKind(entry.Radio.Kind),
                    MapState(entry.Radio.State)))
            .ToArray();
    }

    public async Task<SystemRadioNativeAccessStatus>
        RequestAccessAsync(
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();
        RadioAccessStatus status =
            await Radio.RequestAccessAsync();
        cancellationToken
            .ThrowIfCancellationRequested();
        return MapAccess(status);
    }

    public async Task<SystemRadioNativeAccessStatus>
        SetStateAsync(
            string id,
            bool enabled,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();
        IReadOnlyList<Radio> radios =
            await Radio.GetRadiosAsync();
        Radio? radio =
            BuildEntries(radios)
                .FirstOrDefault(entry =>
                string.Equals(
                    entry.Id,
                    id,
                    StringComparison.Ordinal))
                ?.Radio;
        if (radio == null)
        {
            return SystemRadioNativeAccessStatus
                .Unspecified;
        }

        RadioAccessStatus status =
            await radio.SetStateAsync(
                enabled
                    ? RadioState.On
                    : RadioState.Off);
        cancellationToken
            .ThrowIfCancellationRequested();
        return MapAccess(status);
    }

    private static IReadOnlyList<RadioEntry>
        BuildEntries(
            IReadOnlyList<Radio> radios)
    {
        var occurrences =
            new Dictionary<string, int>(
                StringComparer.Ordinal);
        var entries = new List<RadioEntry>();
        foreach (Radio radio in radios)
        {
            if (radio.Kind
                is not (
                    RadioKind.WiFi
                    or RadioKind.Bluetooth))
            {
                continue;
            }

            string key =
                $"{(int)radio.Kind}:{radio.Name}";
            occurrences.TryGetValue(
                key,
                out int occurrence);
            occurrences[key] = occurrence + 1;
            entries.Add(
                new RadioEntry(
                    $"{key}:{occurrence}",
                    radio));
        }

        return entries;
    }

    private static SystemRadioKind MapKind(
        RadioKind kind) =>
        kind == RadioKind.WiFi
            ? SystemRadioKind.WiFi
            : SystemRadioKind.Bluetooth;

    private static SystemRadioState MapState(
        RadioState state) =>
        state switch
        {
            RadioState.On =>
                SystemRadioState.On,
            RadioState.Off =>
                SystemRadioState.Off,
            RadioState.Disabled =>
                SystemRadioState.Disabled,
            _ => SystemRadioState.Unknown
        };

    private static
        SystemRadioNativeAccessStatus MapAccess(
            RadioAccessStatus status) =>
        status switch
        {
            RadioAccessStatus.Allowed =>
                SystemRadioNativeAccessStatus.Allowed,
            RadioAccessStatus.DeniedByUser =>
                SystemRadioNativeAccessStatus
                    .DeniedByUser,
            RadioAccessStatus.DeniedBySystem =>
                SystemRadioNativeAccessStatus
                    .DeniedBySystem,
            _ =>
                SystemRadioNativeAccessStatus
                    .Unspecified
        };

    private sealed record RadioEntry(
        string Id,
        Radio Radio);
}
