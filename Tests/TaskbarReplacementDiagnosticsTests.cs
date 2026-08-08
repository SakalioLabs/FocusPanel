using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    TaskbarReplacementDiagnosticsTests
{
    [Fact]
    public void DisabledReplacement_IsExplainedWithoutClaimingHealth()
    {
        TaskbarReplacementDiagnostics result =
            TaskbarReplacementDiagnosticsComposer
                .Compose(
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false);

        Assert.False(result.IsEnabled);
        Assert.False(result.IsHealthy);
        Assert.Contains(
            "未启用",
            result.Summary);
    }

    [Theory]
    [InlineData(false, true, true, true,
        "Explorer 任务栏宿主已经变化")]
    [InlineData(true, false, true, true,
        "Windows 又启用了原生边缘呼出")]
    [InlineData(true, true, false, true,
        "主屏工作区仍被原任务栏占用")]
    [InlineData(true, true, true, false,
        "原任务栏呈现抑制层已经失效")]
    public void EnabledReplacement_ReportsFirstBrokenBoundary(
        bool hostCurrent,
        bool revealDisabled,
        bool workAreaReleased,
        bool hasPresentationSuppression,
        string expected)
    {
        TaskbarReplacementDiagnostics result =
            TaskbarReplacementDiagnosticsComposer
                .Compose(
                    true,
                    hostCurrent,
                    revealDisabled,
                    workAreaReleased,
                    hasPresentationSuppression,
                    false,
                    false);

        Assert.False(result.IsHealthy);
        Assert.Contains(expected, result.Summary);
        Assert.Contains(
            "不会循环隐藏",
            result.Summary);
    }

    [Theory]
    [InlineData(true, true, true,
        "窗口区域 + DWM 双层保护")]
    [InlineData(false, true, false,
        "窗口区域隐藏保护")]
    [InlineData(false, false, true,
        "DWM 隐藏保护")]
    [InlineData(true, false, false,
        "任务栏窗口隐藏")]
    public void HealthyReplacement_ExplainsActiveSuppression(
        bool windowHidden,
        bool surfaceSuppressed,
        bool dwmCloaked,
        string expected)
    {
        TaskbarReplacementDiagnostics result =
            TaskbarReplacementDiagnosticsComposer
                .Compose(
                    true,
                    true,
                    true,
                    true,
                    windowHidden,
                    surfaceSuppressed,
                    dwmCloaked);

        Assert.True(result.IsHealthy);
        Assert.Contains(expected, result.Summary);
    }
}
