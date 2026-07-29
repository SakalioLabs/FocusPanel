using System;
using System.Threading;
using System.Threading.Tasks;

namespace FocusPanel.Services;

internal readonly record struct AutoStartupMutation(
    bool Succeeded,
    string Error);

internal readonly record struct AutoStartupCompletion(
    long Revision,
    bool Succeeded,
    bool Enabled,
    string Error);

internal sealed class AutoStartupCoordinator
{
    private readonly Func<bool> _read;
    private readonly Func<
        bool,
        AutoStartupMutation> _write;
    private readonly SemaphoreSlim _gate =
        new(1, 1);
    private readonly InFlightTaskTracker
        _operations = new();
    private long _revision;

    internal AutoStartupCoordinator(
        Func<bool>? read = null,
        Func<
            bool,
            AutoStartupMutation>? write = null)
    {
        _read =
            read
            ?? AutoStartupService
                .IsStartupEnabled;
        _write =
            write
            ?? SetStartup;
    }

    internal Task<AutoStartupCompletion> ReadAsync()
    {
        long revision =
            Interlocked.Increment(
                ref _revision);
        return Start(
            revision,
            () =>
            {
                try
                {
                    return new AutoStartupCompletion(
                        revision,
                        true,
                        _read(),
                        string.Empty);
                }
                catch (Exception ex)
                {
                    return new AutoStartupCompletion(
                        revision,
                        false,
                        false,
                        ex.Message);
                }
            });
    }

    internal Task<AutoStartupCompletion> SetAsync(
        bool enabled)
    {
        long revision =
            Interlocked.Increment(
                ref _revision);
        return Start(
            revision,
            () =>
            {
                AutoStartupMutation mutation;
                try
                {
                    mutation = _write(enabled);
                }
                catch (Exception ex)
                {
                    mutation =
                        new AutoStartupMutation(
                            false,
                            ex.Message);
                }

                if (mutation.Succeeded)
                {
                    return new AutoStartupCompletion(
                        revision,
                        true,
                        enabled,
                        string.Empty);
                }

                bool actualEnabled;
                try
                {
                    actualEnabled = _read();
                }
                catch
                {
                    actualEnabled = !enabled;
                }
                return new AutoStartupCompletion(
                    revision,
                    false,
                    actualEnabled,
                    mutation.Error);
            });
    }

    internal bool IsCurrent(long revision) =>
        revision
        == Volatile.Read(ref _revision);

    internal Task CompleteAsync() =>
        _operations.CompleteAsync();

    private Task<AutoStartupCompletion> Start(
        long revision,
        Func<AutoStartupCompletion> operation)
    {
        Task<AutoStartupCompletion>? task =
            _operations.TryStart(
                () => Task.Run(
                    async () =>
                    {
                        await _gate
                            .WaitAsync()
                            .ConfigureAwait(false);
                        try
                        {
                            return operation();
                        }
                        finally
                        {
                            _gate.Release();
                        }
                    }));
        return task
            ?? Task.FromResult(
                new AutoStartupCompletion(
                    revision,
                    false,
                    false,
                    "应用正在退出，未修改 Windows 启动项。"));
    }

    private static AutoStartupMutation SetStartup(
        bool enabled)
    {
        bool succeeded =
            AutoStartupService.TrySetStartup(
                enabled,
                out string? error);
        return new AutoStartupMutation(
            succeeded,
            error
            ?? string.Empty);
    }
}
