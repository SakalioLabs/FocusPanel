using System;
using System.IO;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class UnhandledExceptionRecoveryPolicyTests
{
    [Fact]
    public void NestedMissingTableErrorUsesDatabaseSafetyNotice()
    {
        var exception = new InvalidOperationException(
            "outer",
            new Exception(
                "SQLite Error 1: 'NO SUCH TABLE: Todos'."));

        FatalExceptionNotice notice =
            UnhandledExceptionRecoveryPolicy.CreateNotice(
                exception,
                @"C:\logs\crash.log");

        Assert.Equal(
            FatalExceptionCategory.DatabaseSchemaMismatch,
            UnhandledExceptionRecoveryPolicy.Classify(
                exception));
        Assert.True(notice.IsWarning);
        Assert.Contains("不会被删除或覆盖", notice.Message);
        Assert.Contains(
            @"C:\logs\crash.log",
            notice.Message);
    }

    [Fact]
    public void OrdinaryFailureStopsInsteadOfPretendingToRecover()
    {
        var exception =
            new InvalidOperationException("render failed");

        FatalExceptionNotice notice =
            UnhandledExceptionRecoveryPolicy.CreateNotice(
                exception,
                @"C:\logs\crash.log");

        Assert.Equal(
            FatalExceptionCategory.Unexpected,
            UnhandledExceptionRecoveryPolicy.Classify(
                exception));
        Assert.False(notice.IsWarning);
        Assert.Contains("安全退出", notice.Message);
        Assert.Contains("render failed", notice.Message);
    }

    [Theory]
    [InlineData(
        false,
        false,
        nameof(DatabaseStartupRecoveryAction.StopWithoutChanges))]
    [InlineData(
        false,
        true,
        nameof(DatabaseStartupRecoveryAction.StopWithoutChanges))]
    [InlineData(
        true,
        true,
        nameof(DatabaseStartupRecoveryAction.ValidateRestoredDatabase))]
    [InlineData(
        true,
        false,
        nameof(DatabaseStartupRecoveryAction.CreateFreshDatabase))]
    public void FreshDatabaseRequiresSuccessfulArchive(
        bool archiveSucceeded,
        bool backupRestored,
        string expected)
    {
        Assert.Equal(
            Enum.Parse<DatabaseStartupRecoveryAction>(
                expected),
            DatabaseStartupRecoveryPolicy.Decide(
                archiveSucceeded,
                backupRestored));
    }

    [Fact]
    public void CrashLogCreatesUserWritableDirectory()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel-crash-log-tests",
            Guid.NewGuid().ToString("N"));
        string logPath = Path.Combine(
            root,
            "nested",
            "crash.log");
        try
        {
            var service =
                new CrashLogService(logPath);

            Assert.True(
                service.TryAppend(
                    new InvalidOperationException(
                        "test crash")));
            Assert.Contains(
                "test crash",
                File.ReadAllText(logPath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DefaultCrashLogLivesOutsideInstallDirectory()
    {
        string expectedRoot = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "FocusPanel",
            "Logs");

        var service =
            new CrashLogService();

        Assert.StartsWith(
            expectedRoot,
            service.LogPath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CrashLogFailureNeverEscapesRecoveryPath()
    {
        string invalidPath = Path.Combine(
            "\0",
            "crash.log");
        var service =
            new CrashLogService(invalidPath);

        Assert.False(
            service.TryAppend(
                new Exception("test")));
    }
}
