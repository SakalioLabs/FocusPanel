using System;
using System.IO;
using System.Linq;
using FocusPanel.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DatabaseBackupServiceTests
{
    [Fact]
    public void RestoreChoosesNewestBackupAcrossBothLocations()
    {
        using var scope = new BackupTestScope();
        string appDataBackup = scope.CreateValidBackup(
            scope.AppDataBackups,
            "focuspanel_backup_old.db",
            "old");
        string localBackup = scope.CreateValidBackup(
            scope.LocalBackups,
            "focuspanel_backup_new.db",
            "new");
        File.SetLastWriteTimeUtc(
            appDataBackup,
            DateTime.UtcNow.AddMinutes(-5));
        File.SetLastWriteTimeUtc(
            localBackup,
            DateTime.UtcNow);

        bool restored = scope.Service.TryRestoreLatestBackup(
            out string message);

        Assert.True(restored);
        Assert.Contains("focuspanel_backup_new.db", message);
        Assert.Equal("new", ReadMarker(scope.DatabasePath));
    }

    [Fact]
    public void CorruptNewestBackupFallsBackToOlderValidBackup()
    {
        using var scope = new BackupTestScope();
        string validBackup = scope.CreateValidBackup(
            scope.AppDataBackups,
            "focuspanel_backup_valid.db",
            "valid");
        string corruptBackup = scope.CreateCorruptBackup(
            scope.LocalBackups,
            "focuspanel_backup_corrupt.db");
        File.SetLastWriteTimeUtc(
            validBackup,
            DateTime.UtcNow.AddMinutes(-5));
        File.SetLastWriteTimeUtc(
            corruptBackup,
            DateTime.UtcNow);

        bool restored = scope.Service.TryRestoreLatestBackup(
            out string message);

        Assert.True(restored);
        Assert.Contains("focuspanel_backup_valid.db", message);
        Assert.Equal("valid", ReadMarker(scope.DatabasePath));
    }

    [Fact]
    public void FailedRestoreLeavesCurrentDatabaseUntouched()
    {
        using var scope = new BackupTestScope();
        CreateValidDatabase(
            scope.DatabasePath,
            "current");
        scope.CreateCorruptBackup(
            scope.AppDataBackups,
            "focuspanel_backup_corrupt.db");

        bool restored = scope.Service.TryRestoreLatestBackup(
            out string message);

        Assert.False(restored);
        Assert.Contains("没有一个能够安全恢复", message);
        Assert.Equal("current", ReadMarker(scope.DatabasePath));
    }

    [Fact]
    public void RestoreRemovesSidecarsFromPreviousDatabase()
    {
        using var scope = new BackupTestScope();
        scope.CreateValidBackup(
            scope.AppDataBackups,
            "focuspanel_backup_valid.db",
            "restored");
        Directory.CreateDirectory(
            Path.GetDirectoryName(scope.DatabasePath)!);
        File.WriteAllText(scope.DatabasePath + "-wal", "stale");
        File.WriteAllText(scope.DatabasePath + "-shm", "stale");

        Assert.True(
            scope.Service.TryRestoreLatestBackup(out _));

        Assert.False(File.Exists(scope.DatabasePath + "-wal"));
        Assert.False(File.Exists(scope.DatabasePath + "-shm"));
        Assert.Equal("restored", ReadMarker(scope.DatabasePath));
    }

    [Fact]
    public void StartupBackupNeverCopiesCorruptDatabase()
    {
        using var scope = new BackupTestScope();
        Directory.CreateDirectory(
            Path.GetDirectoryName(scope.DatabasePath)!);
        File.WriteAllText(scope.DatabasePath, "not sqlite");

        scope.Service.PerformStartupBackup();

        Assert.Empty(
            Directory.Exists(scope.AppDataBackups)
                ? Directory.GetFiles(scope.AppDataBackups)
                : Array.Empty<string>());
        Assert.Empty(
            Directory.Exists(scope.LocalBackups)
                ? Directory.GetFiles(scope.LocalBackups)
                : Array.Empty<string>());
    }

    [Fact]
    public void StartupBackupCreatesValidatedCopiesInBothLocations()
    {
        using var scope = new BackupTestScope();
        CreateValidDatabase(
            scope.DatabasePath,
            "source");

        scope.Service.PerformStartupBackup();

        string appDataCopy = Assert.Single(
            Directory.GetFiles(scope.AppDataBackups));
        string localCopy = Assert.Single(
            Directory.GetFiles(scope.LocalBackups));
        Assert.True(
            DatabaseBackupService.IsValidSqliteDatabase(
                appDataCopy));
        Assert.True(
            DatabaseBackupService.IsValidSqliteDatabase(
                localCopy));
        Assert.Equal("source", ReadMarker(appDataCopy));
        Assert.Equal("source", ReadMarker(localCopy));
    }

    [Fact]
    public void StartupBackupIncludesCommittedWalChanges()
    {
        using var scope = new BackupTestScope();
        Directory.CreateDirectory(
            Path.GetDirectoryName(scope.DatabasePath)!);
        using var source = new SqliteConnection(
            $"Data Source={scope.DatabasePath};Pooling=False");
        source.Open();
        using (SqliteCommand setup = source.CreateCommand())
        {
            setup.CommandText =
                "PRAGMA journal_mode=WAL;"
                + "PRAGMA wal_autocheckpoint=0;"
                + "CREATE TABLE Marker (Value TEXT NOT NULL);"
                + "INSERT INTO Marker (Value) VALUES ('from-wal');";
            setup.ExecuteNonQuery();
        }

        scope.Service.PerformStartupBackup();

        string backup = Assert.Single(
            Directory.GetFiles(
                scope.AppDataBackups,
                "focuspanel_backup_*.db"));
        Assert.DoesNotContain(
            Directory.GetFiles(scope.AppDataBackups),
            path => path.EndsWith(
                "-wal",
                StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(
                    "-shm",
                    StringComparison.OrdinalIgnoreCase));
        Assert.Equal("from-wal", ReadMarker(backup));
        Assert.True(
            DatabaseBackupService.IsValidSqliteDatabase(
                backup));
    }

    private static void CreateValidDatabase(
        string path,
        string marker)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(path)!);
        using var connection =
            new SqliteConnection(
                $"Data Source={path};Pooling=False");
        connection.Open();
        using SqliteCommand command =
            connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE Marker (Value TEXT NOT NULL);"
            + "INSERT INTO Marker (Value) VALUES ($value);";
        command.Parameters.AddWithValue("$value", marker);
        command.ExecuteNonQuery();
    }

    private static string ReadMarker(string path)
    {
        using var connection =
            new SqliteConnection(
                $"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command =
            connection.CreateCommand();
        command.CommandText =
            "SELECT Value FROM Marker LIMIT 1;";
        return (string)command.ExecuteScalar()!;
    }

    private sealed class BackupTestScope : IDisposable
    {
        public BackupTestScope()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                $"FocusPanel-backup-tests-{Guid.NewGuid():N}");
            DatabasePath = Path.Combine(
                Root,
                "data",
                "focuspanel.db");
            AppDataBackups = Path.Combine(
                Root,
                "appdata-backups");
            LocalBackups = Path.Combine(
                Root,
                "local-backups");
            Service = new DatabaseBackupService(
                DatabasePath,
                AppDataBackups,
                LocalBackups);
        }

        public string Root { get; }
        public string DatabasePath { get; }
        public string AppDataBackups { get; }
        public string LocalBackups { get; }
        public DatabaseBackupService Service { get; }

        public string CreateValidBackup(
            string folder,
            string name,
            string marker)
        {
            string path = Path.Combine(folder, name);
            CreateValidDatabase(path, marker);
            return path;
        }

        public string CreateCorruptBackup(
            string folder,
            string name)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, "not sqlite");
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
