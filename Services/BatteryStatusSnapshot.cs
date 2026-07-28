using System;

namespace FocusPanel.Services;

public readonly record struct BatteryStatusSnapshot(
    bool HasBattery,
    int Percent,
    bool IsCharging)
{
    public static BatteryStatusSnapshot Unavailable { get; } =
        new(false, 0, false);

    internal static BatteryStatusSnapshot FromFraction(
        bool hasBattery,
        float fraction,
        bool isCharging)
    {
        if (!hasBattery
            || float.IsNaN(fraction)
            || float.IsInfinity(fraction))
        {
            return Unavailable;
        }

        int percent = (int)Math.Round(
            Math.Clamp(fraction, 0f, 1f) * 100,
            MidpointRounding.AwayFromZero);
        return new BatteryStatusSnapshot(
            true,
            percent,
            isCharging);
    }
}
