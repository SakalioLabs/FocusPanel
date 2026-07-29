namespace FocusPanel.Services;

internal static class WindowSnapshotApplyPolicy
{
    internal static bool CanApply(
        long snapshotRevision,
        long currentRevision,
        bool isTrackingActive,
        bool isDisposed,
        bool isCancellationRequested) =>
        snapshotRevision == currentRevision
        && isTrackingActive
        && !isDisposed
        && !isCancellationRequested;
}
