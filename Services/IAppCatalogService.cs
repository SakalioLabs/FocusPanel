using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FocusPanel.Models;

namespace FocusPanel.Services;

public interface IAppCatalogService : IDisposable
{
    event EventHandler? CatalogChanged;
    bool IsIndexing { get; }
    IReadOnlyList<AppLaunchItem> Search(string query, int limit = 24);
    IReadOnlyList<AppLaunchItem> GetPinned();
    bool Launch(AppLaunchItem app);
    Task<bool> SetPinnedAsync(
        AppLaunchItem app,
        bool pinned);
    Task<bool> MovePinnedAsync(
        AppLaunchItem app,
        int newIndex);
    void Refresh();
}
