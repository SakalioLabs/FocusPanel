using System;
using System.Diagnostics;
using System.IO;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal static class AppLaunchRequestBuilder
{
    internal static bool TryBuild(
        AppLaunchItem app,
        out ProcessStartInfo? startInfo)
    {
        ArgumentNullException.ThrowIfNull(app);
        string target = app.LaunchTarget.Trim();
        if (target.Length == 0)
        {
            startInfo = null;
            return false;
        }

        if (app.LaunchKind == AppLaunchKind.ShellApp
            && IsApplicationUserModelId(target))
        {
            startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true
            };
            startInfo.ArgumentList.Add(
                $@"shell:AppsFolder\{target}");
            return true;
        }

        startInfo = new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        };
        if (!string.IsNullOrWhiteSpace(app.Arguments))
            startInfo.Arguments = app.Arguments;
        return true;
    }

    private static bool IsApplicationUserModelId(
        string target) =>
        !target.StartsWith(
            "shell:",
            StringComparison.OrdinalIgnoreCase)
        && !Path.IsPathFullyQualified(target);
}
