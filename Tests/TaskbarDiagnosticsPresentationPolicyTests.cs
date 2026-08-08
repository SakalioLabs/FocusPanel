using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    TaskbarDiagnosticsPresentationPolicyTests
{
    [Theory]
    [InlineData(false, false,
        "检查任务栏接管", true)]
    [InlineData(false, true,
        "重新检查任务栏接管", true)]
    [InlineData(true, false,
        "正在检查…", false)]
    [InlineData(true, true,
        "正在检查…", false)]
    public void Compose_ShowsOneUnambiguousAction(
        bool isBusy,
        bool hasDiagnostics,
        string expectedText,
        bool expectedEnabled)
    {
        TaskbarDiagnosticsActionPresentation result =
            TaskbarDiagnosticsPresentationPolicy
                .Compose(
                    isBusy,
                    hasDiagnostics);

        Assert.Equal(
            expectedText,
            result.Text);
        Assert.Equal(
            expectedEnabled,
            result.IsEnabled);
    }

    [Theory]
    [InlineData(false, 4, 4, true, true, true, true)]
    [InlineData(true, 4, 4, true, true, true, false)]
    [InlineData(false, 3, 4, true, true, true, false)]
    [InlineData(false, 4, 4, true, false, true, false)]
    [InlineData(false, 4, 4, true, true, false, false)]
    [InlineData(false, 4, 4, false, false, false, true)]
    public void ResultPolicy_RejectsStaleOrChangedState(
        bool isExit,
        long requestRevision,
        long currentRevision,
        bool enabledAtStart,
        bool currentlyEnabled,
        bool resultEnabled,
        bool expected)
    {
        Assert.Equal(
            expected,
            TaskbarDiagnosticsResultPolicy.CanApply(
                isExit,
                requestRevision,
                currentRevision,
                enabledAtStart,
                currentlyEnabled,
                resultEnabled));
    }
}
