using System;
using System.Threading;
using System.Threading.Tasks;

namespace FocusPanel.Services;

internal readonly record struct
    UpdateInstallPreparationCompletion(
        long Revision,
        bool Succeeded,
        string Error);

internal sealed class
    UpdateInstallPreparationCoordinator
{
    private readonly Action _prepare;
    private readonly InFlightTaskTracker
        _operations = new();
    private long _revision;

    internal UpdateInstallPreparationCoordinator(
        Action? prepare = null)
    {
        _prepare =
            prepare
            ?? PerformDatabaseBackup;
    }

    internal Task<
        UpdateInstallPreparationCompletion>
        PrepareAsync()
    {
        long revision =
            Interlocked.Increment(
                ref _revision);
        Task<
            UpdateInstallPreparationCompletion>?
            task =
                _operations.TryStart(
                    () => Task.Run(
                        () =>
                        {
                            try
                            {
                                _prepare();
                                return new
                                    UpdateInstallPreparationCompletion(
                                        revision,
                                        true,
                                        string.Empty);
                            }
                            catch (Exception ex)
                            {
                                return new
                                    UpdateInstallPreparationCompletion(
                                        revision,
                                        false,
                                        ex.Message);
                            }
                        }));
        return task
            ?? Task.FromResult(
                new
                    UpdateInstallPreparationCompletion(
                        revision,
                        false,
                        "应用正在退出，未启动更新准备。"));
    }

    internal Task CompleteAsync() =>
        _operations.CompleteAsync();

    private static void
        PerformDatabaseBackup()
    {
        new DatabaseBackupService()
            .PerformStartupBackup();
    }
}
