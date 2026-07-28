using System;
using System.Diagnostics;

namespace FocusPanel.Services;

internal static class AppLaunchExecution
{
    internal static bool TryStart(
        ProcessStartInfo startInfo,
        Action<ProcessStartInfo>? start = null)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        try
        {
            (start ?? StartProcess)(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void StartProcess(
        ProcessStartInfo startInfo) =>
        Process.Start(startInfo);
}
