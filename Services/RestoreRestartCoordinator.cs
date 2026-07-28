using System;
using System.Diagnostics;

namespace FocusPanel.Services;

internal interface IRestoreRestartBoundary
{
    bool WaitForParentExit(
        int parentProcessId,
        TimeSpan timeout);
    bool StartRestoreProcess(
        string executablePath);
}

internal sealed class WindowsRestoreRestartBoundary
    : IRestoreRestartBoundary
{
    public bool WaitForParentExit(
        int parentProcessId,
        TimeSpan timeout)
    {
        try
        {
            using Process parent =
                Process.GetProcessById(parentProcessId);
            return parent.HasExited
                || parent.WaitForExit(
                    checked((int)timeout.TotalMilliseconds));
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    public bool StartRestoreProcess(
        string executablePath)
        => Process.Start(
            new ProcessStartInfo(
                executablePath,
                "--restore")
            {
                UseShellExecute = true
            }) != null;
}

internal static class RestoreRestartCoordinator
{
    private static readonly TimeSpan ParentExitTimeout =
        TimeSpan.FromSeconds(30);

    internal static int Run(
        int parentProcessId)
        => Run(
            parentProcessId,
            Environment.ProcessPath,
            new WindowsRestoreRestartBoundary());

    internal static int Run(
        int parentProcessId,
        string? executablePath,
        IRestoreRestartBoundary boundary)
    {
        if (parentProcessId <= 0
            || string.IsNullOrWhiteSpace(executablePath))
        {
            return 2;
        }

        try
        {
            if (!boundary.WaitForParentExit(
                    parentProcessId,
                    ParentExitTimeout))
            {
                return 3;
            }

            return boundary.StartRestoreProcess(
                executablePath)
                ? 0
                : 4;
        }
        catch
        {
            return 4;
        }
    }
}
