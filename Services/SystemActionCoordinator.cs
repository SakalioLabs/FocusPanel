using System;
using System.Threading;
using System.Threading.Tasks;

namespace FocusPanel.Services;

internal readonly record struct SystemActionCompletion(
    long Revision,
    bool Succeeded);

internal sealed class SystemActionCoordinator
{
    private long _revision;

    internal Task<SystemActionCompletion> ExecuteAsync(
        Func<bool> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        long revision =
            Interlocked.Increment(
                ref _revision);
        return Task.Run(
            () =>
            {
                bool succeeded;
                try
                {
                    succeeded = action();
                }
                catch
                {
                    succeeded = false;
                }

                return new SystemActionCompletion(
                    revision,
                    succeeded);
            });
    }

    internal bool IsCurrent(long revision) =>
        revision
        == Volatile.Read(ref _revision);
}
