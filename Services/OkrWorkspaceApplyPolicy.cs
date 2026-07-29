namespace FocusPanel.Services;

internal static class OkrWorkspaceApplyPolicy
{
    internal static bool CanApply(
        OkrWorkspaceSnapshot snapshot,
        long capturedRevision,
        long currentRevision) =>
        snapshot.IsValid
        && capturedRevision == currentRevision;
}
