using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal enum ElevatedAppLaunchStatus
{
    Started,
    Cancelled,
    Unsupported,
    Failed
}

internal static class ElevatedAppLaunchRequestBuilder
{
    internal static bool TryBuild(
        AppLaunchItem app,
        out ProcessStartInfo? startInfo)
    {
        ArgumentNullException.ThrowIfNull(app);
        string target = app.LaunchTarget.Trim();
        if (target.Length == 0
            || app.LaunchKind
                is not (
                    AppLaunchKind.Executable
                    or AppLaunchKind.Shortcut))
        {
            startInfo = null;
            return false;
        }

        startInfo = new ProcessStartInfo
        {
            FileName = target,
            Arguments = app.Arguments ?? string.Empty,
            Verb = "runas",
            UseShellExecute = true
        };
        return true;
    }
}

internal sealed class ElevatedAppLaunchService
{
    private const int ErrorCancelled = 1223;
    private readonly Action<ProcessStartInfo>
        _start;

    internal ElevatedAppLaunchService(
        Action<ProcessStartInfo>? start = null)
    {
        _start = start ?? StartProcess;
    }

    internal ElevatedAppLaunchStatus Launch(
        AppLaunchItem app)
    {
        if (!ElevatedAppLaunchRequestBuilder.TryBuild(
                app,
                out ProcessStartInfo? startInfo))
        {
            return ElevatedAppLaunchStatus.Unsupported;
        }

        try
        {
            _start(startInfo!);
            return ElevatedAppLaunchStatus.Started;
        }
        catch (Win32Exception exception)
            when (exception.NativeErrorCode
                  == ErrorCancelled)
        {
            return ElevatedAppLaunchStatus.Cancelled;
        }
        catch
        {
            return ElevatedAppLaunchStatus.Failed;
        }
    }

    private static void StartProcess(
        ProcessStartInfo startInfo) =>
        Process.Start(startInfo);
}

internal readonly record struct
    ElevatedAppLaunchCompletion(
        long Revision,
        ElevatedAppLaunchStatus Status);

internal sealed class ElevatedAppLaunchCoordinator
{
    private readonly Func<
        AppLaunchItem,
        ElevatedAppLaunchStatus> _launch;
    private long _revision;

    internal ElevatedAppLaunchCoordinator(
        Func<AppLaunchItem, ElevatedAppLaunchStatus>
            launch)
    {
        _launch =
            launch
            ?? throw new ArgumentNullException(
                nameof(launch));
    }

    internal Task<ElevatedAppLaunchCompletion>
        LaunchAsync(AppLaunchItem app)
    {
        ArgumentNullException.ThrowIfNull(app);
        AppLaunchItem detached =
            CaptureLaunch(app);
        long revision =
            Interlocked.Increment(
                ref _revision);
        return Task.Run(
            () =>
            {
                ElevatedAppLaunchStatus status;
                try
                {
                    status = _launch(detached);
                }
                catch
                {
                    status =
                        ElevatedAppLaunchStatus
                            .Failed;
                }
                return new
                    ElevatedAppLaunchCompletion(
                        revision,
                        status);
            });
    }

    internal bool IsCurrent(long revision) =>
        revision
        == Volatile.Read(ref _revision);

    private static AppLaunchItem CaptureLaunch(
        AppLaunchItem source) =>
        new()
        {
            DisplayName = source.DisplayName,
            LaunchKind = source.LaunchKind,
            LaunchTarget = source.LaunchTarget,
            Arguments = source.Arguments,
            IconKey = source.IconKey,
            IdentityKey = source.IdentityKey,
            ApplicationUserModelId =
                source.ApplicationUserModelId
        };
}
