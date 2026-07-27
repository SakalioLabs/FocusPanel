using System;

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

    public void Dispose()
    {
        Taskbar.Dispose();
        Windows.Dispose();
        SystemStatus.Dispose();
        Apps.Dispose();
        Updates.Dispose();
    }
}
