namespace FocusPanel.Services;

public static class SystemStatusRefreshPolicy
{
    public static bool ShouldApplyAudio(
        long capturedRevision,
        long currentRevision) =>
        capturedRevision == currentRevision;
}
