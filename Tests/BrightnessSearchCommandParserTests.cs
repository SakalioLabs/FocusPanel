using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class BrightnessSearchCommandParserTests
{
    [Theory]
    [InlineData("亮度 35", "Set", 35)]
    [InlineData("brightness 80%", "Set", 80)]
    [InlineData("亮度 +10", "Adjust", 10)]
    [InlineData("亮度降低 15", "Adjust", -15)]
    [InlineData("bright up 5", "Adjust", 5)]
    [InlineData("brightness down 20%", "Adjust", -20)]
    public void TryParse_AcceptsExactBrightnessCommands(
        string query,
        string kind,
        int percent)
    {
        Assert.True(
            BrightnessSearchCommandParser
                .TryParse(
                    query,
                    out BrightnessSearchCommand
                        command));
        Assert.Equal(
            kind,
            command.Kind.ToString());
        Assert.Equal(percent, command.Percent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("亮度")]
    [InlineData("亮度 101")]
    [InlineData("亮度 +0")]
    [InlineData("把亮度改成 50")]
    [InlineData("brightness maybe 50")]
    public void TryParse_RejectsIncompleteOrAmbiguousText(
        string query)
    {
        Assert.False(
            BrightnessSearchCommandParser
                .TryParse(
                    query,
                    out _));
    }

    [Fact]
    public void Resolve_ClampsRelativeResult()
    {
        var raise =
            new BrightnessSearchCommand(
                BrightnessSearchCommandKind.Adjust,
                30);
        var lower =
            new BrightnessSearchCommand(
                BrightnessSearchCommandKind.Adjust,
                -50);

        Assert.Equal(100, raise.Resolve(85));
        Assert.Equal(0, lower.Resolve(20));
    }
}
