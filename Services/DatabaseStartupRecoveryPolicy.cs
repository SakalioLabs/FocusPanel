namespace FocusPanel.Services;

internal enum DatabaseStartupRecoveryAction
{
    StopWithoutChanges,
    ValidateRestoredDatabase,
    CreateFreshDatabase
}

internal static class DatabaseStartupRecoveryPolicy
{
    internal static DatabaseStartupRecoveryAction Decide(
        bool archiveSucceeded,
        bool backupRestored)
    {
        if (!archiveSucceeded)
        {
            return DatabaseStartupRecoveryAction
                .StopWithoutChanges;
        }

        return backupRestored
            ? DatabaseStartupRecoveryAction
                .ValidateRestoredDatabase
            : DatabaseStartupRecoveryAction
                .CreateFreshDatabase;
    }
}
