using System.Collections.Generic;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DisplayBrightnessServiceTests
{
    [Fact]
    public void GetStatus_NoActiveDeviceReturnsUnavailable()
    {
        var native = new FakeBrightnessNativeApi();
        using var service =
            new DisplayBrightnessService(native);

        BrightnessStatusSnapshot snapshot =
            service.GetStatus();

        Assert.False(snapshot.IsAvailable);
        Assert.Contains(
            "未公开",
            snapshot.Detail);
    }

    [Fact]
    public void GetStatus_MultipleDevicesUsesStableAverage()
    {
        var native =
            new FakeBrightnessNativeApi
            {
                Devices =
                {
                    new("DISPLAY-B", 80),
                    new("DISPLAY-A", 40)
                }
            };
        using var service =
            new DisplayBrightnessService(native);

        BrightnessStatusSnapshot snapshot =
            service.GetStatus();

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(60, snapshot.Percent);
        Assert.Contains("2", snapshot.Detail);
    }

    [Fact]
    public void TrySetBrightness_ClampsAndWritesEveryActiveDevice()
    {
        var native =
            new FakeBrightnessNativeApi
            {
                Devices =
                {
                    new("DISPLAY-A", 20),
                    new("DISPLAY-B", 30)
                }
            };
        using var service =
            new DisplayBrightnessService(native);

        Assert.True(
            service.TrySetBrightness(130));
        Assert.Equal(
            new[]
            {
                ("DISPLAY-A", (byte)100),
                ("DISPLAY-B", (byte)100)
            },
            native.Writes);
    }

    [Fact]
    public void TrySetBrightness_PartialFailureIsReported()
    {
        var native =
            new FakeBrightnessNativeApi
            {
                Devices =
                {
                    new("DISPLAY-A", 20),
                    new("DISPLAY-B", 30)
                },
                FailingInstance = "DISPLAY-B"
            };
        using var service =
            new DisplayBrightnessService(native);

        Assert.False(
            service.TrySetBrightness(50));
        Assert.Equal(2, native.Writes.Count);
    }

    private sealed class FakeBrightnessNativeApi :
        IBrightnessNativeApi
    {
        internal List<BrightnessDeviceObservation>
            Devices
        {
            get;
        } = new();

        internal List<(string, byte)> Writes
        {
            get;
        } = new();

        internal string? FailingInstance
        {
            get;
            init;
        }

        public IReadOnlyList<
            BrightnessDeviceObservation>
            GetActiveDevices() =>
            Devices;

        public bool TrySetBrightness(
            string instanceName,
            byte percent)
        {
            Writes.Add(
                (instanceName, percent));
            return instanceName
                   != FailingInstance;
        }
    }
}
