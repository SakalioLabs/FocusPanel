using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FocusPanel.Services;

internal readonly record struct ShellPathOpenCompletion(
    long Revision,
    bool Succeeded);

internal sealed class ShellPathOpenCoordinator
{
    private readonly Func<string, bool> _open;
    private long _revision;

    internal ShellPathOpenCoordinator(
        Func<string, bool>? open = null)
    {
        _open = open ?? OpenPath;
    }

    internal Task<ShellPathOpenCompletion> OpenAsync(
        string path)
    {
        string detachedPath =
            path?.Trim()
            ?? string.Empty;
        long revision =
            Interlocked.Increment(ref _revision);
        if (detachedPath.Length == 0)
        {
            return Task.FromResult(
                new ShellPathOpenCompletion(
                    revision,
                    false));
        }

        return Task.Run(
            () =>
            {
                bool succeeded;
                try
                {
                    succeeded = _open(detachedPath);
                }
                catch
                {
                    succeeded = false;
                }
                return new ShellPathOpenCompletion(
                    revision,
                    succeeded);
            });
    }

    internal bool IsCurrent(long revision) =>
        revision == Volatile.Read(ref _revision);

    private static bool OpenPath(string path) =>
        AppLaunchExecution.TryStart(
            new ProcessStartInfo(path)
            {
                UseShellExecute = true
            });
}
