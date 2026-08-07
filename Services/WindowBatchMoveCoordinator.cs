using System;
using System.Collections.Generic;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal readonly record struct WindowBatchMoveResult(
    int EligibleCount,
    int MovedCount,
    int FailedCount)
{
    internal bool HasWork =>
        EligibleCount > 0
        || FailedCount > 0;

    internal bool Succeeded =>
        EligibleCount > 0
        && MovedCount == EligibleCount
        && FailedCount == 0;
}

internal static class WindowBatchMoveCoordinator
{
    internal static WindowBatchMoveResult Execute(
        IReadOnlyList<WindowReference> windows,
        Func<IntPtr, bool> canMove,
        Func<IntPtr, bool> move)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(canMove);
        ArgumentNullException.ThrowIfNull(move);

        int eligible = 0;
        int moved = 0;
        int failed = 0;
        var visited = new HashSet<IntPtr>();
        foreach (WindowReference window in windows)
        {
            IntPtr handle = window.Handle;
            if (handle == IntPtr.Zero
                || !visited.Add(handle))
            {
                continue;
            }

            bool isEligible;
            try
            {
                isEligible = canMove(handle);
            }
            catch
            {
                failed++;
                continue;
            }

            if (!isEligible)
                continue;

            eligible++;
            if (SystemActionExecution.Try(
                    () => move(handle)))
            {
                moved++;
            }
            else
            {
                failed++;
            }
        }

        return new WindowBatchMoveResult(
            eligible,
            moved,
            failed);
    }
}
