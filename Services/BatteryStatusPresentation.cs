using System;

namespace FocusPanel.Services;

internal readonly record struct BatteryStatusPresentation(
    string Glyph,
    string ValueText,
    string Summary);

internal static class BatteryStatusPresentationComposer
{
    internal const string FullBatteryGlyph = "\uE83F";
    internal const string ChargingLevel9Glyph = "\uE83E";
    private const int BatteryLevel0CodePoint = 0xE850;
    private const int ChargingLevel0CodePoint = 0xE85A;

    internal static BatteryStatusPresentation Compose(
        bool hasBattery,
        int batteryPercent,
        bool isCharging)
    {
        if (!hasBattery)
        {
            return new BatteryStatusPresentation(
                string.Empty,
                string.Empty,
                string.Empty);
        }

        int percent = Math.Clamp(batteryPercent, 0, 100);
        int level = Math.Min(10, percent / 10);
        string glyph = GetGlyph(level, isCharging);
        string valueText = isCharging
            ? $"{percent}% · 充电中"
            : $"{percent}%";
        return new BatteryStatusPresentation(
            glyph,
            valueText,
            $"电池 {valueText}");
    }

    private static string GetGlyph(
        int level,
        bool isCharging)
    {
        if (level >= 10)
            return FullBatteryGlyph;
        if (isCharging && level == 9)
            return ChargingLevel9Glyph;

        int codePoint = isCharging
            ? ChargingLevel0CodePoint + level
            : BatteryLevel0CodePoint + level;
        return char.ConvertFromUtf32(codePoint);
    }
}
