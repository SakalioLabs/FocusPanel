using System;
using System.IO;

namespace FocusPanel.Services;

internal sealed class CrashLogService
{
    internal CrashLogService()
        : this(
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "FocusPanel",
                "Logs",
                "crash.log"))
    {
    }

    internal CrashLogService(string logPath)
    {
        LogPath = logPath;
    }

    internal string LogPath { get; }

    internal bool TryAppend(Exception exception)
    {
        try
        {
            string? directory =
                Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.AppendAllText(
                LogPath,
                $"[{DateTimeOffset.Now:O}] {exception}{Environment.NewLine}{Environment.NewLine}");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
