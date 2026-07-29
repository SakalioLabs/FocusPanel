namespace FocusPanel.Services;

public static class PomodoroSaveResultPolicy
{
    public static bool ShouldUpdateCompletionMessage(
        long savedSessionRevision,
        long currentUiRevision,
        bool isRunning) =>
        !isRunning
        && savedSessionRevision
            == currentUiRevision;
}
