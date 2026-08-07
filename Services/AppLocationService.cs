using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal enum AppLocationKind
{
    Executable,
    Shortcut
}

internal readonly record struct AppLocationTarget(
    string Path,
    AppLocationKind Kind)
{
    internal string MenuLabel =>
        Kind == AppLocationKind.Shortcut
            ? "打开快捷方式位置"
            : "打开程序位置";
}

internal enum AppLocationOpenStatus
{
    Opened,
    Missing,
    Failed
}

internal readonly record struct AppLocationOpenResult(
    AppLocationOpenStatus Status,
    string? Error = null);

internal interface IAppLocationService
{
    Task<AppLocationOpenResult> OpenAsync(
        AppLocationTarget target);
}

internal static class AppLocationPolicy
{
    internal static bool TryResolve(
        AppLaunchItem? launch,
        string? runningExecutablePath,
        out AppLocationTarget target)
    {
        if (TryCreate(
                runningExecutablePath,
                AppLocationKind.Executable,
                out target))
        {
            return true;
        }

        if (launch == null
            || launch.LaunchKind == AppLaunchKind.ShellApp
            && !Path.IsPathFullyQualified(
                launch.LaunchTarget))
        {
            target = default;
            return false;
        }

        AppLocationKind kind =
            launch.LaunchKind == AppLaunchKind.Shortcut
                ? AppLocationKind.Shortcut
                : AppLocationKind.Executable;
        return TryCreate(
            launch.LaunchTarget,
            kind,
            out target);
    }

    private static bool TryCreate(
        string? path,
        AppLocationKind kind,
        out AppLocationTarget target)
    {
        string normalized = path?.Trim()
            ?? string.Empty;
        if (normalized.Length == 0
            || !Path.IsPathFullyQualified(
                normalized))
        {
            target = default;
            return false;
        }

        target = new AppLocationTarget(
            normalized,
            kind);
        return true;
    }
}

internal sealed class AppLocationService :
    IAppLocationService
{
    private readonly Func<string, bool>
        _fileExists;
    private readonly Func<ProcessStartInfo, bool>
        _start;

    internal AppLocationService(
        Func<string, bool>? fileExists = null,
        Func<ProcessStartInfo, bool>? start = null)
    {
        _fileExists = fileExists ?? File.Exists;
        _start = start ?? Start;
    }

    public Task<AppLocationOpenResult> OpenAsync(
        AppLocationTarget target) =>
        Task.Run(
            () => Open(target));

    private AppLocationOpenResult Open(
        AppLocationTarget target)
    {
        try
        {
            if (!_fileExists(target.Path))
            {
                return new AppLocationOpenResult(
                    AppLocationOpenStatus.Missing);
            }

            ProcessStartInfo request =
                BuildExplorerRequest(
                    target.Path);
            return _start(request)
                ? new AppLocationOpenResult(
                    AppLocationOpenStatus.Opened)
                : new AppLocationOpenResult(
                    AppLocationOpenStatus.Failed);
        }
        catch (Exception exception)
        {
            return new AppLocationOpenResult(
                AppLocationOpenStatus.Failed,
                exception.Message);
        }
    }

    internal static ProcessStartInfo
        BuildExplorerRequest(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "应用位置不能为空。",
                nameof(path));
        }
        return new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments =
                $"/select,\"{path}\"",
            UseShellExecute = true
        };
    }

    private static bool Start(
        ProcessStartInfo request)
    {
        Process.Start(request);
        return true;
    }
}
