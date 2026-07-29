using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FocusPanel.Services;

internal sealed class InFlightTaskTracker
{
    private readonly object _sync = new();
    private readonly HashSet<Task> _tasks = new();
    private bool _isAccepting = true;

    internal Task<T>? TryStart<T>(
        Func<Task<T>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Task<T> task;
        lock (_sync)
        {
            if (!_isAccepting)
                return null;

            task = factory();
            _tasks.Add(task);
        }

        _ = task.ContinueWith(
            completed =>
            {
                lock (_sync)
                    _tasks.Remove(completed);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return task;
    }

    internal Task CompleteAsync()
    {
        lock (_sync)
        {
            _isAccepting = false;
            return _tasks.Count == 0
                ? Task.CompletedTask
                : Task.WhenAll(_tasks.ToArray());
        }
    }
}
