using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace FocusPanel.Services;

internal interface IAutoStartupRegistry
{
    string? ReadCommand();
    void WriteCommand(string command);
    void DeleteCommand();
}

internal sealed class WindowsAutoStartupRegistry : IAutoStartupRegistry
{
    private const string AppName = "FocusPanel";
    private const string RunKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public string? ReadCommand()
    {
        using RegistryKey? key =
            Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(AppName) as string;
    }

    public void WriteCommand(string command)
    {
        using RegistryKey key =
            Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException(
                "Windows 未返回可写的启动项注册表键。");
        key.SetValue(
            AppName,
            command,
            RegistryValueKind.String);
    }

    public void DeleteCommand()
    {
        using RegistryKey? key =
            Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }
}

public static class AutoStartupService
{
    public static bool TrySetStartup(
        bool enable,
        out string? error)
        => TrySetStartup(
            enable,
            new WindowsAutoStartupRegistry(),
            ResolveExecutablePath(),
            out error);

    internal static bool TrySetStartup(
        bool enable,
        IAutoStartupRegistry registry,
        string? executablePath,
        out string? error)
    {
        try
        {
            if (enable)
            {
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    error = "无法定位 FocusPanel 可执行文件，未写入 Windows 启动项。";
                    return false;
                }

                registry.WriteCommand(
                    BuildStartupCommand(executablePath));
            }
            else
            {
                registry.DeleteCommand();
            }

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = $"无法更新 Windows 启动项：{ex.Message}";
            return false;
        }
    }

    public static bool IsStartupEnabled()
        => IsStartupEnabled(new WindowsAutoStartupRegistry());

    internal static bool IsStartupEnabled(
        IAutoStartupRegistry registry)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(
                registry.ReadCommand());
        }
        catch
        {
            return false;
        }
    }

    internal static string BuildStartupCommand(
        string executablePath)
        => $"\"{executablePath.Trim()}\"";

    private static string? ResolveExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
            return Environment.ProcessPath;

        try
        {
            return Process.GetCurrentProcess()
                .MainModule?
                .FileName;
        }
        catch
        {
            return null;
        }
    }
}
