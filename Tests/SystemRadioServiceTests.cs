using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class SystemRadioServiceTests
{
    [Fact]
    public async Task GetStatus_NoRadiosReturnsBothUnavailable()
    {
        var native = new FakeSystemRadioNativeApi();
        using var service = CreateService(native);

        IReadOnlyList<SystemRadioSnapshot> status =
            await service.GetStatusAsync(
                CancellationToken.None);

        Assert.Equal(2, status.Count);
        Assert.All(
            status,
            snapshot =>
                Assert.Equal(
                    SystemRadioState.Unavailable,
                    snapshot.State));
    }

    [Fact]
    public async Task GetStatus_AggregatesMultipleRadiosByKind()
    {
        var native = new FakeSystemRadioNativeApi
        {
            Radios =
            {
                Radio("wifi-a", SystemRadioKind.WiFi,
                    SystemRadioState.Off),
                Radio("wifi-b", SystemRadioKind.WiFi,
                    SystemRadioState.On),
                Radio("bt", SystemRadioKind.Bluetooth,
                    SystemRadioState.Disabled)
            }
        };
        using var service = CreateService(native);

        IReadOnlyList<SystemRadioSnapshot> status =
            await service.GetStatusAsync(
                CancellationToken.None);

        Assert.Equal(
            SystemRadioState.On,
            status.Single(item =>
                item.Kind == SystemRadioKind.WiFi).State);
        Assert.Equal(
            SystemRadioState.Disabled,
            status.Single(item =>
                item.Kind
                == SystemRadioKind.Bluetooth).State);
    }

    [Theory]
    [InlineData(
        (int)SystemRadioNativeAccessStatus.DeniedByUser,
        SystemRadioSetStatus.DeniedByUser)]
    [InlineData(
        (int)SystemRadioNativeAccessStatus.DeniedBySystem,
        SystemRadioSetStatus.DeniedBySystem)]
    public async Task SetEnabled_AccessDenialIsExplicit(
        int accessValue,
        SystemRadioSetStatus expected)
    {
        var native = new FakeSystemRadioNativeApi
        {
            AccessStatus =
                (SystemRadioNativeAccessStatus)
                    accessValue
        };
        using var service = CreateService(native);

        SystemRadioSetResult result =
            await service.SetEnabledAsync(
                SystemRadioKind.WiFi,
                true,
                CancellationToken.None);

        Assert.Equal(expected, result.Status);
        Assert.Empty(native.SetCalls);
    }

    [Fact]
    public async Task SetEnabled_HardwareDisabledIsNotWritten()
    {
        var native = new FakeSystemRadioNativeApi
        {
            Radios =
            {
                Radio("wifi", SystemRadioKind.WiFi,
                    SystemRadioState.Disabled)
            }
        };
        using var service = CreateService(native);

        SystemRadioSetResult result =
            await service.SetEnabledAsync(
                SystemRadioKind.WiFi,
                true,
                CancellationToken.None);

        Assert.Equal(
            SystemRadioSetStatus.HardwareDisabled,
            result.Status);
        Assert.Empty(native.SetCalls);
    }

    [Fact]
    public async Task SetEnabled_OnlySucceedsAfterObservedStateChanges()
    {
        var native = new FakeSystemRadioNativeApi
        {
            ApplyWrites = true,
            Radios =
            {
                Radio("wifi", SystemRadioKind.WiFi,
                    SystemRadioState.Off)
            }
        };
        using var service = CreateService(native);

        SystemRadioSetResult result =
            await service.SetEnabledAsync(
                SystemRadioKind.WiFi,
                true,
                CancellationToken.None);

        Assert.Equal(
            SystemRadioSetStatus.Succeeded,
            result.Status);
        Assert.Equal(
            new[] { ("wifi", true) },
            native.SetCalls);
    }

    [Fact]
    public async Task SetEnabled_AcceptedWithoutTransitionIsNotConfirmed()
    {
        var native = new FakeSystemRadioNativeApi
        {
            Radios =
            {
                Radio("bt", SystemRadioKind.Bluetooth,
                    SystemRadioState.Off)
            }
        };
        using var service = CreateService(native);

        SystemRadioSetResult result =
            await service.SetEnabledAsync(
                SystemRadioKind.Bluetooth,
                true,
                CancellationToken.None);

        Assert.Equal(
            SystemRadioSetStatus.NotConfirmed,
            result.Status);
    }

    [Fact]
    public async Task SetEnabled_CachesPermissionForSession()
    {
        var native = new FakeSystemRadioNativeApi
        {
            ApplyWrites = true,
            Radios =
            {
                Radio("wifi", SystemRadioKind.WiFi,
                    SystemRadioState.Off)
            }
        };
        using var service = CreateService(native);

        await service.SetEnabledAsync(
            SystemRadioKind.WiFi,
            true,
            CancellationToken.None);
        await service.SetEnabledAsync(
            SystemRadioKind.WiFi,
            false,
            CancellationToken.None);

        Assert.Equal(1, native.AccessRequests);
    }

    private static SystemRadioService CreateService(
        ISystemRadioNativeApi native) =>
        new(
            native,
            confirmationAttempts: 2,
            confirmationDelay: TimeSpan.Zero);

    private static SystemRadioNativeObservation Radio(
        string id,
        SystemRadioKind kind,
        SystemRadioState state) =>
        new(id, id, kind, state);

    private sealed class FakeSystemRadioNativeApi :
        ISystemRadioNativeApi
    {
        internal List<SystemRadioNativeObservation>
            Radios { get; } = new();

        internal List<(string Id, bool Enabled)>
            SetCalls { get; } = new();

        internal SystemRadioNativeAccessStatus
            AccessStatus { get; init; } =
                SystemRadioNativeAccessStatus.Allowed;

        internal bool ApplyWrites { get; init; }

        internal int AccessRequests { get; private set; }

        public Task<IReadOnlyList<
            SystemRadioNativeObservation>>
            GetRadiosAsync(
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<
                IReadOnlyList<
                    SystemRadioNativeObservation>>(
                Radios.ToArray());
        }

        public Task<SystemRadioNativeAccessStatus>
            RequestAccessAsync(
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AccessRequests++;
            return Task.FromResult(AccessStatus);
        }

        public Task<SystemRadioNativeAccessStatus>
            SetStateAsync(
                string id,
                bool enabled,
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetCalls.Add((id, enabled));
            if (ApplyWrites)
            {
                int index =
                    Radios.FindIndex(item =>
                        item.Id == id);
                SystemRadioNativeObservation current =
                    Radios[index];
                Radios[index] =
                    current with
                    {
                        State = enabled
                            ? SystemRadioState.On
                            : SystemRadioState.Off
                    };
            }

            return Task.FromResult(
                SystemRadioNativeAccessStatus.Allowed);
        }
    }
}
