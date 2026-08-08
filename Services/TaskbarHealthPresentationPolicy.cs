namespace FocusPanel.Services;

internal sealed record TaskbarHealthPresentation(
    bool HasWarning,
    string Status);

internal static class
    TaskbarHealthPresentationPolicy
{
    internal static TaskbarHealthPresentation
        Compose(
            bool isReplacementEnabled,
            bool hasReplacementWarning,
            bool hasDiagnostics,
            bool diagnosticsHealthy)
    {
        if (hasReplacementWarning)
        {
            return new TaskbarHealthPresentation(
                true,
                "Windows 任务栏已安全恢复");
        }

        if (hasDiagnostics)
        {
            return new TaskbarHealthPresentation(
                !diagnosticsHealthy,
                diagnosticsHealthy
                    ? "任务栏接管检查正常"
                    : "任务栏接管需要注意");
        }

        return new TaskbarHealthPresentation(
            false,
            isReplacementEnabled
                ? "任务栏接管等待检查"
                : "任务栏替代未启用");
    }
}
