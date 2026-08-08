using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    TaskbarHealthPresentationPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false,
        false, "任务栏替代未启用")]
    [InlineData(true, false, false, false,
        false, "任务栏接管等待检查")]
    [InlineData(true, false, true, true,
        false, "任务栏接管检查正常")]
    [InlineData(true, false, true, false,
        true, "任务栏接管需要注意")]
    [InlineData(false, true, false, false,
        true, "Windows 任务栏已安全恢复")]
    [InlineData(true, true, true, true,
        true, "Windows 任务栏已安全恢复")]
    public void Compose_PrioritizesSafetyAndLatestHealth(
        bool enabled,
        bool replacementWarning,
        bool hasDiagnostics,
        bool diagnosticsHealthy,
        bool expectedWarning,
        string expectedStatus)
    {
        TaskbarHealthPresentation result =
            TaskbarHealthPresentationPolicy
                .Compose(
                    enabled,
                    replacementWarning,
                    hasDiagnostics,
                    diagnosticsHealthy);

        Assert.Equal(
            expectedWarning,
            result.HasWarning);
        Assert.Equal(
            expectedStatus,
            result.Status);
    }
}
