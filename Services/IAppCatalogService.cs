using System;
using System.Collections.Generic;
using FocusPanel.Models;

namespace FocusPanel.Services;

public interface IAppCatalogService : IDisposable
{
    event EventHandler? CatalogChanged;
    bool IsIndexing { get; }
    IReadOnlyList<AppLaunchItem> Search(string query, int limit = 24);
    IReadOnlyList<AppLaunchItem> GetPinned();
    void Launch(AppLaunchItem app);
    void SetPinned(AppLaunchItem app, bool pinned);
    void MovePinned(AppLaunchItem app, int newIndex);
    void Refresh();
}
