namespace FocusPanel.Services;

internal sealed record
    TaskbarDiagnosticsActionPresentation(
        string Text,
        bool IsEnabled);

internal static class
    TaskbarDiagnosticsPresentationPolicy
{
    internal static TaskbarDiagnosticsActionPresentation
        Compose(
            bool isBusy,
            bool hasDiagnostics) =>
        new(
            isBusy
                ? "正在检查…"
                : hasDiagnostics
                    ? "重新检查任务栏接管"
                    : "检查任务栏接管",
            !isBusy);
}

internal static class TaskbarDiagnosticsResultPolicy
{
    internal static bool CanApply(
        bool isExit,
        long requestRevision,
        long currentRevision,
        bool enabledAtStart,
        bool currentlyEnabled,
        bool resultEnabled) =>
        !isExit
        && requestRevision == currentRevision
        && enabledAtStart == currentlyEnabled
        && resultEnabled == currentlyEnabled;
}
