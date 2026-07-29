using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FocusPanel.Services;

/// <summary>
/// Coalesces repeated saves for the same object while preserving the
/// first-seen order of distinct objects. A single consumer invokes the
/// supplied save delegate, so callers never overlap persistence work.
/// </summary>
internal sealed class CoalescingAsyncSaveQueue<T>
    where T : class
{
    private readonly object _sync = new();
    private readonly Func<T, Task> _saveAsync;
    private readonly TimeSpan _settleDelay;
    private readonly List<T> _pendingOrder = new();
    private readonly HashSet<T> _pendingSet =
        new(ReferenceComparer.Instance);
    private Task _processor = Task.CompletedTask;
    private bool _isRunning;
    private bool _isAccepting = true;

    internal CoalescingAsyncSaveQueue(
        Func<T, Task> saveAsync,
        TimeSpan settleDelay)
    {
        _saveAsync =
            saveAsync
            ?? throw new ArgumentNullException(
                nameof(saveAsync));
        if (settleDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(settleDelay));

        _settleDelay = settleDelay;
    }

    internal event Action<T>? ItemSaved;

    internal event Action<T, Exception>? ItemSaveFailed;

    internal bool Enqueue(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_sync)
        {
            if (!_isAccepting)
                return false;

            if (_pendingSet.Add(item))
                _pendingOrder.Add(item);

            if (!_isRunning)
            {
                _isRunning = true;
                _processor = ProcessAsync();
            }

            return true;
        }
    }

    internal bool Discard(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_sync)
        {
            if (!_pendingSet.Remove(item))
                return false;

            _pendingOrder.RemoveAll(
                candidate =>
                    ReferenceEquals(
                        candidate,
                        item));
            return true;
        }
    }

    /// <summary>
    /// Waits for the work that is currently queued. Callers should first
    /// detach producers when they require a strict navigation barrier.
    /// </summary>
    internal Task FlushAsync()
    {
        lock (_sync)
            return _processor;
    }

    /// <summary>
    /// Stops accepting new work and drains all current or trailing saves.
    /// </summary>
    internal Task CompleteAsync()
    {
        lock (_sync)
        {
            _isAccepting = false;
            return _processor;
        }
    }

    private async Task ProcessAsync()
    {
        while (true)
        {
            if (_settleDelay > TimeSpan.Zero)
            {
                await Task.Delay(
                        _settleDelay)
                    .ConfigureAwait(false);
            }

            List<T> batch;
            lock (_sync)
            {
                if (_pendingOrder.Count == 0)
                {
                    _isRunning = false;
                    return;
                }

                batch = new List<T>(
                    _pendingOrder);
                _pendingOrder.Clear();
                _pendingSet.Clear();
            }

            foreach (T item in batch)
            {
                try
                {
                    await _saveAsync(item)
                        .ConfigureAwait(false);
                    NotifySafely(
                        ItemSaved,
                        item);
                }
                catch (Exception ex)
                {
                    NotifySafely(
                        ItemSaveFailed,
                        item,
                        ex);
                }
            }
        }
    }

    private static void NotifySafely(
        Action<T>? handlers,
        T item)
    {
        if (handlers == null)
            return;

        foreach (Delegate handler in
                 handlers.GetInvocationList())
        {
            try
            {
                ((Action<T>)handler)(item);
            }
            catch
            {
                // Persistence must continue even if a UI observer is gone.
            }
        }
    }

    private static void NotifySafely(
        Action<T, Exception>? handlers,
        T item,
        Exception error)
    {
        if (handlers == null)
            return;

        foreach (Delegate handler in
                 handlers.GetInvocationList())
        {
            try
            {
                ((Action<T, Exception>)handler)(
                    item,
                    error);
            }
            catch
            {
                // Persistence must continue even if a UI observer is gone.
            }
        }
    }

    private sealed class ReferenceComparer
        : IEqualityComparer<T>
    {
        internal static ReferenceComparer Instance { get; } =
            new();

        public bool Equals(T? x, T? y) =>
            ReferenceEquals(x, y);

        public int GetHashCode(T obj) =>
            RuntimeHelpers.GetHashCode(obj);
    }
}
