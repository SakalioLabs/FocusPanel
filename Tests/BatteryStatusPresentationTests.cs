using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class BatteryStatusPresentationTests
{
    [Theory]
    [InlineData(0, "\uE850")]
    [InlineData(1, "\uE850")]
    [InlineData(10, "\uE851")]
    [InlineData(19, "\uE851")]
    [InlineData(20, "\uE852")]
    [InlineData(50, "\uE855")]
    [InlineData(99, "\uE859")]
    [InlineData(100, BatteryStatusPresentationComposer.FullBatteryGlyph)]
    [InlineData(180, BatteryStatusPresentationComposer.FullBatteryGlyph)]
    [InlineData(-10, "\uE850")]
    public void BatteryLevel_UsesClampedTenPercentGlyph(
        int percent,
        string expectedGlyph)
    {
        BatteryStatusPresentation presentation =
            BatteryStatusPresentationComposer.Compose(
                true,
                percent,
                false);

        Assert.Equal(expectedGlyph, presentation.Glyph);
    }

    [Theory]
    [InlineData(0, "\uE85A")]
    [InlineData(40, "\uE85E")]
    [InlineData(
        90,
        BatteryStatusPresentationComposer.ChargingLevel9Glyph)]
    [InlineData(
        100,
        BatteryStatusPresentationComposer.FullBatteryGlyph)]
    public void ChargingLevel_UsesChargingGlyphAndText(
        int percent,
        string expectedGlyph)
    {
        BatteryStatusPresentation presentation =
            BatteryStatusPresentationComposer.Compose(
                true,
                percent,
                true);

        Assert.Equal(expectedGlyph, presentation.Glyph);
        Assert.Equal(
            $"{percent}% · 充电中",
            presentation.ValueText);
        Assert.Equal(
            $"电池 {percent}% · 充电中",
            presentation.Summary);
    }

    [Fact]
    public void DeviceWithoutBattery_HasNoBatteryPresentation()
    {
        BatteryStatusPresentation presentation =
            BatteryStatusPresentationComposer.Compose(
                false,
                75,
                true);

        Assert.Equal(string.Empty, presentation.Glyph);
        Assert.Equal(string.Empty, presentation.ValueText);
        Assert.Equal(string.Empty, presentation.Summary);
    }
}
