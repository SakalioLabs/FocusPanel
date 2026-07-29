using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class SystemStatusRefreshPolicyTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(8, 8, true)]
    [InlineData(7, 8, false)]
    [InlineData(8, 9, false)]
    public void AudioSnapshot_AppliesOnlyAtCapturedRevision(
        long capturedRevision,
        long currentRevision,
        bool expected)
    {
        Assert.Equal(
            expected,
            SystemStatusRefreshPolicy.ShouldApplyAudio(
                capturedRevision,
                currentRevision));
    }
}
