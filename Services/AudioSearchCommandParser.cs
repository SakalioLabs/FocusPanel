using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FocusPanel.Services;

internal enum AudioSearchCommandKind
{
    SetVolume,
    AdjustVolume,
    SetMuted
}

internal readonly record struct AudioSearchCommand(
    AudioSearchCommandKind Kind,
    int Percent,
    bool Muted)
{
    internal bool RequiresCurrentVolume =>
        Kind
        == AudioSearchCommandKind.AdjustVolume;

    internal AudioSearchMutation Resolve(
        float currentVolume)
    {
        float? volume = Kind switch
        {
            AudioSearchCommandKind.SetVolume =>
                Percent / 100f,
            AudioSearchCommandKind.AdjustVolume =>
                Math.Clamp(
                    currentVolume
                    + Percent / 100f,
                    0f,
                    1f),
            _ => null
        };
        bool? muted =
            Kind
            == AudioSearchCommandKind.SetMuted
                ? Muted
                : volume > 0f
                    ? false
                    : null;
        return new AudioSearchMutation(
            volume,
            muted);
    }

    internal string StableKey =>
        Kind switch
        {
            AudioSearchCommandKind.SetVolume =>
                $"audio:set:{Percent}",
            AudioSearchCommandKind.AdjustVolume =>
                $"audio:adjust:{Percent:+0;-0}",
            AudioSearchCommandKind.SetMuted =>
                Muted
                    ? "audio:mute"
                    : "audio:unmute",
            _ => "audio:unknown"
        };

    internal string DisplayName =>
        Kind switch
        {
            AudioSearchCommandKind.SetVolume =>
                $"将音量设为 {Percent}%",
            AudioSearchCommandKind.AdjustVolume =>
                Percent > 0
                    ? $"音量提高 {Percent}%"
                    : $"音量降低 {-Percent}%",
            AudioSearchCommandKind.SetMuted =>
                Muted
                    ? "静音"
                    : "取消静音",
            _ => "音频命令"
        };

    internal string Glyph =>
        Kind
            == AudioSearchCommandKind.SetMuted
            && Muted
                ? AudioStatusPresentationComposer
                    .MuteGlyph
                : AudioStatusPresentationComposer
                    .VolumeGlyph;
}

internal readonly record struct AudioSearchMutation(
    float? Volume,
    bool? Muted);

internal static class AudioSearchCommandParser
{
    private static readonly Regex SetVolumePattern =
        new(
            @"^(?:音量|volume|vol)\s*(\d{1,3})\s*%?$",
            RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant);

    private static readonly Regex SignedVolumePattern =
        new(
            @"^(?:音量|volume|vol)\s*([+-])\s*(\d{1,3})\s*%?$",
            RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant);

    private static readonly Regex ChineseAdjustPattern =
        new(
            @"^音量\s*(增加|提高|加|降低|减少|减)\s*(\d{1,3})\s*%?$",
            RegexOptions.CultureInvariant);

    private static readonly Regex EnglishAdjustPattern =
        new(
            @"^(?:volume|vol)\s*(up|down)\s*(\d{1,3})\s*%?$",
            RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant);

    internal static bool TryParse(
        string? query,
        out AudioSearchCommand command)
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
        string keyword =
            normalized.ToLowerInvariant();
        if (keyword is "静音" or "mute")
        {
            command = new AudioSearchCommand(
                AudioSearchCommandKind.SetMuted,
                0,
                true);
            return true;
        }

        if (keyword is "取消静音"
            or "解除静音"
            or "unmute")
        {
            command = new AudioSearchCommand(
                AudioSearchCommandKind.SetMuted,
                0,
                false);
            return true;
        }

        Match match =
            SignedVolumePattern.Match(
                normalized);
        if (match.Success
            && TryReadPercent(
                match.Groups[2].Value,
                allowZero: false,
                out int signedPercent))
        {
            if (match.Groups[1].Value
                == "-")
            {
                signedPercent =
                    -signedPercent;
            }

            command = new AudioSearchCommand(
                AudioSearchCommandKind.AdjustVolume,
                signedPercent,
                false);
            return true;
        }

        match =
            ChineseAdjustPattern.Match(
                normalized);
        if (match.Success
            && TryReadPercent(
                match.Groups[2].Value,
                allowZero: false,
                out int chinesePercent))
        {
            if (match.Groups[1].Value
                is "降低" or "减少" or "减")
            {
                chinesePercent =
                    -chinesePercent;
            }

            command = new AudioSearchCommand(
                AudioSearchCommandKind.AdjustVolume,
                chinesePercent,
                false);
            return true;
        }

        match =
            EnglishAdjustPattern.Match(
                normalized);
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
                englishPercent =
                    -englishPercent;
            }

            command = new AudioSearchCommand(
                AudioSearchCommandKind.AdjustVolume,
                englishPercent,
                false);
            return true;
        }

        match =
            SetVolumePattern.Match(
                normalized);
        if (!match.Success
            || !TryReadPercent(
                match.Groups[1].Value,
                allowZero: true,
                out int absolutePercent))
        {
            return false;
        }

        command = new AudioSearchCommand(
            AudioSearchCommandKind.SetVolume,
            absolutePercent,
            false);
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
