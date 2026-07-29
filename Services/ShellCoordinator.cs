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
    }

    public ITaskbarController Taskbar { get; }
    public IAppCatalogService Apps { get; }
    public IWindowTracker Windows { get; }
    public ISystemStatusService SystemStatus { get; }
    public IAppUpdateService Updates { get; }

    public bool TryEnableTaskbarReplacement(out string? error) =>
        Taskbar.TryEnableReplacement(out error);

    public void RestoreTaskbar() => Taskbar.Restore();

    internal async Task DisposeAsync()
    {
        Taskbar.Dispose();
        Windows.Dispose();
        SystemStatus.Dispose();
        Updates.Dispose();
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
