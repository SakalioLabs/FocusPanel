namespace FocusPanel.Services;

internal static class TaskbarRepairPolicy
{
    internal static bool IsRepairable(
        TaskbarReplacementStopReason reason) =>
        reason is
            TaskbarReplacementStopReason
                .WindowsTaskbarReappeared
            or TaskbarReplacementStopReason
                .ExplorerHostChanged;
}
