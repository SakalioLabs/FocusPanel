using System;

namespace FocusPanel.Services;

internal readonly record struct AudioStatusPresentation(
    string Glyph,
    string Summary,
    string ToggleLabel);

internal static class AudioStatusPresentationComposer
{
    internal const string VolumeGlyph = "\uE767";
    internal const string MuteGlyph = "\uE74F";
    internal const string UnavailableGlyph = "\uE783";

    internal static AudioStatusPresentation Compose(
        bool isAvailable,
        float masterVolume,
        bool isMuted)
    {
        if (!isAvailable)
        {
            return new AudioStatusPresentation(
                UnavailableGlyph,
                "音频设备不可用",
                "音频设备不可用");
        }

        int percent = (int)Math.Round(
            Math.Clamp(masterVolume, 0f, 1f) * 100,
            MidpointRounding.AwayFromZero);
        if (isMuted)
        {
            return new AudioStatusPresentation(
                MuteGlyph,
                "已静音",
                "取消静音");
        }

        return new AudioStatusPresentation(
            percent == 0 ? MuteGlyph : VolumeGlyph,
            $"音量 {percent}%",
            "静音");
    }
}
