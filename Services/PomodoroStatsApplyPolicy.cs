namespace FocusPanel.Services;

public static class PomodoroStatsApplyPolicy
{
    public static bool ShouldApply(
        PomodoroStatsSnapshot snapshot,
        long capturedRevision,
        long currentRevision) =>
        snapshot.IsValid
        && capturedRevision == currentRevision;
}
