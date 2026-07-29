using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PomodoroSaveResultPolicyTests
{
    [Theory]
    [InlineData(3, 3, false, true)]
    [InlineData(2, 3, false, false)]
    [InlineData(3, 3, true, false)]
    public void CompletionMessage_OnlyUpdatesMatchingIdleSession(
        long savedRevision,
        long currentRevision,
        bool isRunning,
        bool expected)
    {
        Assert.Equal(
            expected,
            PomodoroSaveResultPolicy
                .ShouldUpdateCompletionMessage(
                    savedRevision,
                    currentRevision,
                    isRunning));
    }
}
