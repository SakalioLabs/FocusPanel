namespace FocusPanel.Services;

internal static class OrganizerLayoutApplyPolicy
{
    internal static bool CanApplyOptions(
        OrganizerLayoutSnapshot snapshot,
        long capturedRevision,
        long currentRevision) =>
        snapshot.IsValid
        && capturedRevision == currentRevision;
}
