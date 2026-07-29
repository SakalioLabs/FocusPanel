using System;
using System.Collections.Generic;
using System.Threading;

namespace FocusPanel.Services;

internal sealed class ResilientSnapshotStore<T>
{
    private IReadOnlyList<T> _current =
        Array.Empty<T>();

    internal IReadOnlyList<T> Current =>
        Volatile.Read(ref _current);

    internal bool TryRefresh(
        Func<IReadOnlyList<T>> capture,
        out Exception? failure)
    {
        ArgumentNullException.ThrowIfNull(capture);

        try
        {
            IReadOnlyList<T> next =
                capture()
                ?? throw new InvalidOperationException(
                    "快照提供器返回了空集合。");
            Volatile.Write(
                ref _current,
                next);
            failure = null;
            return true;
        }
        catch (Exception ex)
        {
            failure = ex;
            return false;
        }
    }
}
