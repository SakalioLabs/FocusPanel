using System;
using System.Threading.Tasks;

namespace FocusPanel.Services;

public sealed class ShellCoordinator : IDisposable
{
    public ShellCoordinator()
    {
        Taskbar = new TaskbarController();
        Apps = new AppCatalogService();
        Windows = new WindowTracker();
        SystemStatus = new SystemStatusService();
        Updates = new VelopackUpdateService();
        JumpLists =
            new AppJumpListService();
        AppFiles =
            new AppFileLaunchService();
    }

    public ITaskbarController Taskbar { get; }
    public IAppCatalogService Apps { get; }
    public IWindowTracker Windows { get; }
    public ISystemStatusService SystemStatus { get; }
    public IAppUpdateService Updates { get; }
    internal IAppJumpListService
        JumpLists
    {
        get;
    }
    internal IAppFileLaunchService
        AppFiles
    {
        get;
    }

    public bool TryEnableTaskbarReplacement(out string? error) =>
        Taskbar.TryEnableReplacement(out error);

    public void RestoreTaskbar() => Taskbar.Restore();

    internal async Task DisposeAsync()
    {
        Taskbar.Dispose();
        Windows.Dispose();
        SystemStatus.Dispose();
        Updates.Dispose();
        JumpLists.Dispose();
        await AppFiles
            .CompleteAsync()
            .ConfigureAwait(false);
        if (Apps is AppCatalogService catalog)
        {
            await catalog
                .DisposeAsync()
                .ConfigureAwait(false);
        }
        else
        {
            Apps.Dispose();
        }
    }

    public void Dispose() =>
        DisposeAsync()
            .GetAwaiter()
            .GetResult();
}
