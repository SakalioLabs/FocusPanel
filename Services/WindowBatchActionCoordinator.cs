using System;
using System.Collections.Generic;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal readonly record struct
    WindowBatchActionResult(
        int EligibleCount,
        int SucceededCount,
        int FailedCount)
{
    internal bool HasWork =>
        EligibleCount > 0
        || FailedCount > 0;

    internal bool Succeeded =>
        EligibleCount > 0
        && SucceededCount
            == EligibleCount
        && FailedCount == 0;
}

internal static class
    WindowBatchActionCoordinator
{
    internal static WindowBatchActionResult
        Execute(
            IReadOnlyList<WindowReference>
                windows,
            Func<WindowReference, bool>
                isEligible,
            Func<IntPtr, bool> action)
    {
        ArgumentNullException.ThrowIfNull(
            windows);
        ArgumentNullException.ThrowIfNull(
            isEligible);
        ArgumentNullException.ThrowIfNull(
            action);

        int eligible = 0;
        int succeeded = 0;
        int failed = 0;
        var visited = new HashSet<IntPtr>();
        foreach (WindowReference window
                 in windows)
        {
            if (window.Handle == IntPtr.Zero
                || !visited.Add(
                    window.Handle))
            {
                continue;
            }

            bool shouldRun;
            try
            {
                shouldRun =
                    isEligible(window);
            }
            catch
            {
                failed++;
                continue;
            }

            if (!shouldRun)
                continue;

            eligible++;
            if (SystemActionExecution.Try(
                    () => action(
                        window.Handle)))
            {
                succeeded++;
            }
            else
            {
                failed++;
            }
        }

        return new WindowBatchActionResult(
            eligible,
            succeeded,
            failed);
    }
}
