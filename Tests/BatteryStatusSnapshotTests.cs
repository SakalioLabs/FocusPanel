using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class BatteryStatusSnapshotTests
{
    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.004f, 0)]
    [InlineData(0.005f, 1)]
    [InlineData(0.455f, 46)]
    [InlineData(1f, 100)]
    [InlineData(-2f, 0)]
    [InlineData(2f, 100)]
    public void Fraction_IsClampedAndRounded(
        float fraction,
        int expectedPercent)
    {
        BatteryStatusSnapshot snapshot =
            BatteryStatusSnapshot.FromFraction(
                true,
                fraction,
                true);

        Assert.True(snapshot.HasBattery);
        Assert.Equal(expectedPercent, snapshot.Percent);
        Assert.True(snapshot.IsCharging);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void InvalidFraction_IsUnavailable(float fraction)
    {
        Assert.Equal(
            BatteryStatusSnapshot.Unavailable,
            BatteryStatusSnapshot.FromFraction(
                true,
                fraction,
                true));
    }

    [Fact]
    public void DeviceWithoutBattery_IsUnavailable()
    {
        Assert.Equal(
            BatteryStatusSnapshot.Unavailable,
            BatteryStatusSnapshot.FromFraction(
                false,
                0.5f,
                true));
    }
}
