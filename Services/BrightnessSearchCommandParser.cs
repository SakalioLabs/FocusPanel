using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FocusPanel.Services;

internal enum BrightnessSearchCommandKind
{
    Set,
    Adjust
}

internal readonly record struct
    BrightnessSearchCommand(
        BrightnessSearchCommandKind Kind,
        int Percent)
{
    internal bool RequiresCurrentBrightness =>
        Kind == BrightnessSearchCommandKind.Adjust;

    internal int Resolve(int currentPercent) =>
        Kind == BrightnessSearchCommandKind.Set
            ? Percent
            : Math.Clamp(
                currentPercent + Percent,
                0,
                100);

    internal string StableKey =>
        Kind == BrightnessSearchCommandKind.Set
            ? $"brightness:set:{Percent}"
            : $"brightness:adjust:{Percent:+0;-0}";

    internal string DisplayName =>
        Kind == BrightnessSearchCommandKind.Set
            ? $"将亮度设为 {Percent}%"
            : Percent > 0
                ? $"亮度提高 {Percent}%"
                : $"亮度降低 {-Percent}%";
}

internal static class BrightnessSearchCommandParser
{
    private static readonly Regex SetPattern =
        new(
            @"^(?:亮度|brightness|bright)\s*(\d{1,3})\s*%?$",
            RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant);

    private static readonly Regex SignedPattern =
        new(
            @"^(?:亮度|brightness|bright)\s*([+-])\s*(\d{1,3})\s*%?$",
            RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant);

    private static readonly Regex ChineseAdjustPattern =
        new(
            @"^亮度\s*(增加|提高|加|降低|减少|减)\s*(\d{1,3})\s*%?$",
            RegexOptions.CultureInvariant);

    private static readonly Regex EnglishAdjustPattern =
        new(
            @"^(?:brightness|bright)\s*(up|down)\s*(\d{1,3})\s*%?$",
            RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant);

    internal static bool TryParse(
        string? query,
        out BrightnessSearchCommand command)
    {
        command = default;
        if (string.IsNullOrWhiteSpace(query))
            return false;

        string normalized =
            query.Trim()
                .Replace('％', '%')
                .Replace('＋', '+')
                .Replace('－', '-')
                .Replace('−', '-');
        Match match = SignedPattern.Match(normalized);
        if (match.Success
            && TryReadPercent(
                match.Groups[2].Value,
                allowZero: false,
                out int signedPercent))
        {
            command =
                new BrightnessSearchCommand(
                    BrightnessSearchCommandKind.Adjust,
                    match.Groups[1].Value == "-"
                        ? -signedPercent
                        : signedPercent);
            return true;
        }

        match = ChineseAdjustPattern.Match(normalized);
        if (match.Success
            && TryReadPercent(
                match.Groups[2].Value,
                allowZero: false,
                out int chinesePercent))
        {
            if (match.Groups[1].Value
                is "降低" or "减少" or "减")
            {
                chinesePercent = -chinesePercent;
            }

            command =
                new BrightnessSearchCommand(
                    BrightnessSearchCommandKind.Adjust,
                    chinesePercent);
            return true;
        }

        match = EnglishAdjustPattern.Match(normalized);
        if (match.Success
            && TryReadPercent(
                match.Groups[2].Value,
                allowZero: false,
                out int englishPercent))
        {
            if (string.Equals(
                    match.Groups[1].Value,
                    "down",
                    StringComparison.OrdinalIgnoreCase))
            {
                englishPercent = -englishPercent;
            }

            command =
                new BrightnessSearchCommand(
                    BrightnessSearchCommandKind.Adjust,
                    englishPercent);
            return true;
        }

        match = SetPattern.Match(normalized);
        if (!match.Success
            || !TryReadPercent(
                match.Groups[1].Value,
                allowZero: true,
                out int percent))
        {
            return false;
        }

        command =
            new BrightnessSearchCommand(
                BrightnessSearchCommandKind.Set,
                percent);
        return true;
    }

    private static bool TryReadPercent(
        string value,
        bool allowZero,
        out int percent) =>
        int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out percent)
        && percent <= 100
        && (allowZero || percent > 0);
}
