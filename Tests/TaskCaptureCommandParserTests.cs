using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskCaptureCommandParserTests
{
    [Fact]
    public void QuickCapturePrefix_IsAValidPrefixButNotAnEmptyTask()
    {
        Assert.Equal(
            "任务 ",
            TaskCaptureCommandParser
                .QuickCapturePrefix);
        Assert.False(
            TaskCaptureCommandParser
                .TryParse(
                    TaskCaptureCommandParser
                        .QuickCapturePrefix,
                    out _));
    }

    [Theory]
    [InlineData("任务 买牛奶", "买牛奶")]
    [InlineData("任务：整理周报", "整理周报")]
    [InlineData("待办: 回复邮件", "回复邮件")]
    [InlineData("todo book dentist", "book dentist")]
    [InlineData("TASK: prepare release", "prepare release")]
    public void ExplicitCapture_ParsesTitle(
        string query,
        string expectedTitle)
    {
        Assert.True(
            TaskCaptureCommandParser
                .TryParse(
                    query,
                    out TaskCaptureCommand
                        command));
        Assert.Equal(
            expectedTitle,
            command.Title);
        Assert.StartsWith(
            "task:capture:",
            command.StableKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("任务")]
    [InlineData("任务管理器")]
    [InlineData("task manager")]
    [InlineData("taskmgr")]
    [InlineData("task:")]
    [InlineData("todo")]
    [InlineData("待办：")]
    [InlineData("任务 一行\n二行")]
    public void AmbiguousOrUnsafeInput_IsRejected(
        string query)
    {
        Assert.False(
            TaskCaptureCommandParser
                .TryParse(
                    query,
                    out _));
    }

    [Fact]
    public void OverlongTitle_IsRejected()
    {
        string query =
            "任务 "
            + new string(
                '项',
                TaskCaptureCommandParser
                    .MaximumTitleLength
                + 1);

        Assert.False(
            TaskCaptureCommandParser
                .TryParse(
                    query,
                    out _));
    }
}
