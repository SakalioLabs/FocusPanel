using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AudioSearchCommandParserTests
{
    [Theory]
    [InlineData("音量 35", 35)]
    [InlineData("音量35％", 35)]
    [InlineData("volume 80%", 80)]
    [InlineData("VOL 0", 0)]
    [InlineData("音量 100", 100)]
    public void AbsoluteVolume_ParsesStrictPercent(
        string query,
        int expected)
    {
        Assert.True(
            AudioSearchCommandParser.TryParse(
                query,
                out AudioSearchCommand command));
        Assert.Equal(
            AudioSearchCommandKind.SetVolume,
            command.Kind);
        Assert.Equal(expected, command.Percent);
        Assert.Equal(
            expected / 100f,
            command.Resolve(0.42f).Volume);
    }

    [Theory]
    [InlineData("音量 +10", 10)]
    [InlineData("音量－15％", -15)]
    [InlineData("音量提高 20", 20)]
    [InlineData("音量减少5", -5)]
    [InlineData("volume up 8", 8)]
    [InlineData("vol down 12%", -12)]
    public void RelativeVolume_ParsesDirection(
        string query,
        int expected)
    {
        Assert.True(
            AudioSearchCommandParser.TryParse(
                query,
                out AudioSearchCommand command));
        Assert.Equal(
            AudioSearchCommandKind.AdjustVolume,
            command.Kind);
        Assert.Equal(expected, command.Percent);
        Assert.True(
            command.RequiresCurrentVolume);
    }

    [Theory]
    [InlineData("静音", true)]
    [InlineData("MUTE", true)]
    [InlineData("取消静音", false)]
    [InlineData("解除静音", false)]
    [InlineData("unmute", false)]
    public void Mute_UsesExplicitTargetState(
        string query,
        bool expected)
    {
        Assert.True(
            AudioSearchCommandParser.TryParse(
                query,
                out AudioSearchCommand command));
        Assert.Equal(
            AudioSearchCommandKind.SetMuted,
            command.Kind);
        Assert.False(
            command.RequiresCurrentVolume);
        Assert.Equal(
            expected,
            command.Resolve(0.5f).Muted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("音量")]
    [InlineData("音量 101")]
    [InlineData("音量 -0")]
    [InlineData("音量 +101")]
    [InlineData("音量 3.5")]
    [InlineData("音量 30 后关机")]
    [InlineData("muted")]
    [InlineData("volume.exe")]
    public void UnsafeOrAmbiguousInput_DoesNotBecomeCommand(
        string query)
    {
        Assert.False(
            AudioSearchCommandParser.TryParse(
                query,
                out _));
    }

    [Fact]
    public void RelativeVolume_ClampsAndPositiveVolumeUnmutes()
    {
        var increase =
            new AudioSearchCommand(
                AudioSearchCommandKind
                    .AdjustVolume,
                20,
                false)
                .Resolve(0.9f);
        var decrease =
            new AudioSearchCommand(
                AudioSearchCommandKind
                    .AdjustVolume,
                -20,
                false)
                .Resolve(0.1f);

        Assert.Equal(1f, increase.Volume);
        Assert.False(increase.Muted);
        Assert.Equal(0f, decrease.Volume);
        Assert.Null(decrease.Muted);
    }
}
