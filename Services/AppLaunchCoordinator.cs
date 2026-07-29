using System;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal readonly record struct AppLaunchCompletion(
    long Revision,
    bool Succeeded);

internal sealed class AppLaunchCoordinator
{
    private readonly Func<AppLaunchItem, bool> _launch;
    private long _revision;

    internal AppLaunchCoordinator(
        Func<AppLaunchItem, bool> launch)
    {
        _launch =
            launch
            ?? throw new ArgumentNullException(
                nameof(launch));
    }

    internal Task<AppLaunchCompletion> LaunchAsync(
        AppLaunchItem app)
    {
        ArgumentNullException.ThrowIfNull(app);
        AppLaunchItem detached =
            CaptureLaunch(app);
        long revision =
            Interlocked.Increment(ref _revision);
        return Task.Run(
            () =>
            {
                bool succeeded;
                try
                {
                    succeeded = _launch(detached);
                }
                catch
                {
                    succeeded = false;
                }
                return new AppLaunchCompletion(
                    revision,
                    succeeded);
            });
    }

    internal bool IsCurrent(long revision) =>
        revision == Volatile.Read(ref _revision);

    private static AppLaunchItem CaptureLaunch(
        AppLaunchItem source) =>
        new()
        {
            DisplayName = source.DisplayName,
            LaunchKind = source.LaunchKind,
            LaunchTarget = source.LaunchTarget,
            Arguments = source.Arguments,
            IconKey = source.IconKey,
            IdentityKey = source.IdentityKey
        };
}
