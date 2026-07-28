using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace FocusPanel.Services;

public sealed class DatabaseBackupService
{
    private const int MaxBackups = 5;
    private const string BackupPattern =
        "focuspanel_backup_*.db";

    private readonly string _dbPath;
    private readonly string _appDataFolder;
    private readonly string _appDataBackupFolder;
    private readonly string _localBackupFolder;

    public DatabaseBackupService()
    {
        _appDataFolder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData),
            "FocusPanel");
        Directory.CreateDirectory(_appDataFolder);
        _dbPath = Path.Combine(
            _appDataFolder,
            "focuspanel.db");
        _appDataBackupFolder = Path.Combine(
            _appDataFolder,
            "Backups");
        _localBackupFolder = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Backups");
    }

    internal DatabaseBackupService(
        string dbPath,
        string appDataBackupFolder,
        string localBackupFolder)
    {
        _dbPath = dbPath;
        _appDataFolder =
            Path.GetDirectoryName(dbPath)
            ?? throw new ArgumentException(
                "数据库路径必须包含目录。",
                nameof(dbPath));
        _appDataBackupFolder = appDataBackupFolder;
        _localBackupFolder = localBackupFolder;
    }

    public void PerformStartupBackup()
    {
        if (!File.Exists(_dbPath))
            return;

        if (!IsValidSqliteDatabase(_dbPath))
        {
            Debug.WriteLine(
                "Backup skipped: the current database did not pass SQLite quick_check.");
            return;
        }

        string backupFileName =
            $"focuspanel_backup_{DateTime.Now:yyyyMMdd_HHmmss_fff}.db";
        BackupToFolder(
            _appDataBackupFolder,
            backupFileName);
        BackupToFolder(
            _localBackupFolder,
            backupFileName);
    }

    public bool ArchiveCorruptedDatabase()
    {
        try
        {
            if (!File.Exists(_dbPath))
                return false;

            string corruptedPath = Path.Combine(
                _appDataFolder,
                $"focuspanel_corrupted_{DateTime.Now:yyyyMMdd_HHmmss_fff}.db");
            File.Move(_dbPath, corruptedPath);
            MoveSidecarIfPresent(
                _dbPath + "-wal",
                corruptedPath + "-wal");
            MoveSidecarIfPresent(
                _dbPath + "-shm",
                corruptedPath + "-shm");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Database archive failed: {ex.Message}");
            return false;
        }
    }

    public bool RestoreLatestBackup()
        => TryRestoreLatestBackup(out _);

    public bool TryRestoreLatestBackup(
        out string message)
    {
        IReadOnlyList<FileInfo> candidates =
            GetBackupCandidates();
        if (candidates.Count == 0)
        {
            message = "没有找到可用的数据库备份文件。";
            return false;
        }

        Directory.CreateDirectory(_appDataFolder);
        var failures = new List<string>();
        foreach (FileInfo candidate in candidates)
        {
            if (!IsValidSqliteDatabase(candidate.FullName))
            {
                failures.Add(
                    $"{candidate.Name} 未通过 SQLite 完整性检查");
                continue;
            }

            string stagingPath = Path.Combine(
                _appDataFolder,
                $".focuspanel_restore_{Guid.NewGuid():N}.tmp");
            try
            {
                File.Copy(
                    candidate.FullName,
                    stagingPath,
                    overwrite: true);
                if (!IsValidSqliteDatabase(stagingPath))
                {
                    failures.Add(
                        $"{candidate.Name} 复制后未通过完整性检查");
                    continue;
                }

                File.Move(
                    stagingPath,
                    _dbPath,
                    overwrite: true);
                DeleteSidecarIfPresent(_dbPath + "-wal");
                DeleteSidecarIfPresent(_dbPath + "-shm");
                message =
                    $"已从备份 {candidate.Name} 恢复数据库。";
                return true;
            }
            catch (Exception ex)
            {
                failures.Add(
                    $"{candidate.Name}：{ex.Message}");
            }
            finally
            {
                DeleteSidecarIfPresent(stagingPath);
            }
        }

        message =
            $"找到 {candidates.Count} 个备份，但没有一个能够安全恢复。"
            + (failures.Count == 0
                ? string.Empty
                : $" {string.Join("；", failures.Take(3))}");
        return false;
    }

    public List<string> GetAvailableBackups()
        => GetBackupCandidates()
            .Select(file => file.FullName)
            .ToList();

    internal IReadOnlyList<FileInfo> GetBackupCandidates()
    {
        var candidates = new List<FileInfo>();
        AddCandidates(
            candidates,
            _appDataBackupFolder);
        AddCandidates(
            candidates,
            _localBackupFolder);
        return candidates
            .GroupBy(
                file => file.FullName,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(
                file => file.LastWriteTimeUtc)
            .ThenByDescending(
                file => file.Name,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static bool IsValidSqliteDatabase(
        string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length == 0)
                return false;

            var connectionString =
                new SqliteConnectionStringBuilder
                {
                    DataSource = path,
                    Mode = SqliteOpenMode.ReadOnly,
                    Cache = SqliteCacheMode.Private,
                    Pooling = false
                }.ToString();
            using var connection =
                new SqliteConnection(connectionString);
            connection.Open();
            using SqliteCommand command =
                connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            object? result = command.ExecuteScalar();
            return string.Equals(
                result?.ToString(),
                "ok",
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void BackupToFolder(
        string folderPath,
        string backupFileName)
    {
        try
        {
            Directory.CreateDirectory(folderPath);
            string backupPath = Path.Combine(
                folderPath,
                backupFileName);
            CreateConsistentBackup(backupPath);
            if (!IsValidSqliteDatabase(backupPath))
            {
                DeleteSidecarIfPresent(backupPath);
                throw new InvalidDataException(
                    "生成的备份未通过 SQLite 完整性检查。");
            }
            CleanupOldBackups(folderPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Backup to {folderPath} failed: {ex.Message}");
        }
    }

    private void CreateConsistentBackup(
        string backupPath)
    {
        DeleteSidecarIfPresent(backupPath);
        var sourceBuilder =
            new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private,
                Pooling = false
            };
        var destinationBuilder =
            new SqliteConnectionStringBuilder
            {
                DataSource = backupPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                Pooling = false
            };
        using var source =
            new SqliteConnection(
                sourceBuilder.ToString());
        using var destination =
            new SqliteConnection(
                destinationBuilder.ToString());
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
        using (SqliteCommand command =
               destination.CreateCommand())
        {
            command.CommandText =
                "PRAGMA wal_checkpoint(TRUNCATE);";
            command.ExecuteNonQuery();
            command.CommandText =
                "PRAGMA journal_mode=DELETE;";
            command.ExecuteScalar();
        }
        destination.Close();
        source.Close();
        DeleteSidecarIfPresent(backupPath + "-wal");
        DeleteSidecarIfPresent(backupPath + "-shm");
    }

    private static void CleanupOldBackups(
        string folderPath)
    {
        try
        {
            var directory =
                new DirectoryInfo(folderPath);
            List<FileInfo> files = directory
                .GetFiles(BackupPattern)
                .OrderByDescending(
                    file => IsValidSqliteDatabase(
                        file.FullName))
                .ThenByDescending(
                    file => file.LastWriteTimeUtc)
                .ToList();

            foreach (FileInfo file in files.Skip(MaxBackups))
                file.Delete();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Backup cleanup failed: {ex.Message}");
        }
    }

    private static void AddCandidates(
        ICollection<FileInfo> destination,
        string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return;

        foreach (FileInfo file in
                 new DirectoryInfo(folderPath)
                     .GetFiles(BackupPattern))
        {
            destination.Add(file);
        }
    }

    private static void MoveSidecarIfPresent(
        string source,
        string destination)
    {
        if (File.Exists(source))
            File.Move(source, destination);
    }

    private static void DeleteSidecarIfPresent(
        string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Could not remove database sidecar {path}: {ex.Message}");
        }
    }
}
