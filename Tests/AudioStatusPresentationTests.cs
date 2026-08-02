using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AudioStatusPresentationTests
{
    [Theory]
    [InlineData(0.004f, "音量 0%", AudioStatusPresentationComposer.MuteGlyph)]
    [InlineData(0.005f, "音量 1%", AudioStatusPresentationComposer.VolumeGlyph)]
    [InlineData(0.451f, "音量 45%", AudioStatusPresentationComposer.VolumeGlyph)]
    [InlineData(1.5f, "音量 100%", AudioStatusPresentationComposer.VolumeGlyph)]
    [InlineData(-1f, "音量 0%", AudioStatusPresentationComposer.MuteGlyph)]
    public void AvailableVolume_UsesClampedRoundedPresentation(
        float volume,
        string summary,
        string glyph)
    {
        AudioStatusPresentation presentation =
            AudioStatusPresentationComposer.Compose(
                true,
                volume,
                false);

        Assert.Equal(summary, presentation.Summary);
        Assert.Equal(glyph, presentation.Glyph);
        Assert.Equal("静音", presentation.ToggleLabel);
        Assert.Equal(
            summary[3..],
            presentation.CompactValueText);
    }

    [Fact]
    public void MutedState_UsesMuteGlyphAndUnmuteAction()
    {
        AudioStatusPresentation presentation =
            AudioStatusPresentationComposer.Compose(
                true,
                0.8f,
                true);

        Assert.Equal(
            AudioStatusPresentationComposer.MuteGlyph,
            presentation.Glyph);
        Assert.Equal("已静音", presentation.Summary);
        Assert.Equal("取消静音", presentation.ToggleLabel);
        Assert.Equal("静音", presentation.CompactValueText);
    }

    [Fact]
    public void UnavailableState_UsesErrorGlyphAndDisablesSemanticAction()
    {
        AudioStatusPresentation presentation =
            AudioStatusPresentationComposer.Compose(
                false,
                0.8f,
                true);

        Assert.Equal(
            AudioStatusPresentationComposer.UnavailableGlyph,
            presentation.Glyph);
        Assert.Equal(
            "音频设备不可用",
            presentation.Summary);
        Assert.Equal(
            "音频设备不可用",
            presentation.ToggleLabel);
        Assert.Equal(
            "不可用",
            presentation.CompactValueText);
    }
}
