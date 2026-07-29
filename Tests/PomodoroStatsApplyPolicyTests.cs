using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PomodoroStatsApplyPolicyTests
{
    [Theory]
    [InlineData(true, 3, 3, true)]
    [InlineData(true, 2, 3, false)]
    [InlineData(false, 3, 3, false)]
    public void Snapshot_AppliesOnlyWhenValidAndCurrent(
        bool isValid,
        long capturedRevision,
        long currentRevision,
        bool expected)
    {
        Assert.Equal(
            expected,
            PomodoroStatsApplyPolicy.ShouldApply(
                new PomodoroStatsSnapshot(
                    isValid,
                    4,
                    100),
                capturedRevision,
                currentRevision));
    }
}
