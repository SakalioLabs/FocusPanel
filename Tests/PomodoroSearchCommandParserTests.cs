using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    PomodoroSearchCommandParserTests
{
    [Theory]
    [InlineData("专注 25", 25)]
    [InlineData("开始专注45分钟", 45)]
    [InlineData("番茄 60", 60)]
    [InlineData("番茄钟 90 分", 90)]
    [InlineData("focus 30 min", 30)]
    [InlineData("POMODORO 120 minutes", 120)]
    [InlineData("专注 ２５ 分钟", 25)]
    public void ExplicitCommand_ParsesDuration(
        string query,
        int expectedMinutes)
    {
        Assert.True(
            PomodoroSearchCommandParser
                .TryParse(
                    query,
                    out PomodoroSearchCommand
                        command));
        Assert.Equal(
            expectedMinutes,
            command.DurationMinutes);
        Assert.Equal(
            $"focus:start:{expectedMinutes}",
            command.StableKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("专注")]
    [InlineData("开始专注")]
    [InlineData("专注 0")]
    [InlineData("专注 181")]
    [InlineData("专注 25.5")]
    [InlineData("专注 25 然后关机")]
    [InlineData("focus.exe")]
    [InlineData("pomodoro notes")]
    public void AmbiguousOrUnsafeInput_IsRejected(
        string query)
    {
        Assert.False(
            PomodoroSearchCommandParser
                .TryParse(
                    query,
                    out _));
    }
}
