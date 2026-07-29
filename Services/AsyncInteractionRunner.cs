using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FocusPanel.Services;

internal static class AsyncInteractionRunner
{
    internal static void Start(
        Func<Task> operation,
        Action<Exception>? onFailure = null,
        Action? onCompleted = null)
    {
        _ = RunAsync(
            operation,
            onFailure,
            onCompleted);
    }

    internal static async Task RunAsync(
        Func<Task> operation,
        Action<Exception>? onFailure = null,
        Action? onCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            try
            {
                onFailure?.Invoke(ex);
            }
            catch (Exception feedbackError)
            {
                Debug.WriteLine(
                    "Async interaction feedback failed: "
                    + feedbackError.Message);
            }
        }
        finally
        {
            try
            {
                onCompleted?.Invoke();
            }
            catch (Exception completionError)
            {
                Debug.WriteLine(
                    "Async interaction cleanup failed: "
                    + completionError.Message);
            }
        }
    }
}
