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
    bool Launch(AppLaunchItem app);
    bool SetPinned(AppLaunchItem app, bool pinned);
    bool MovePinned(AppLaunchItem app, int newIndex);
    void Refresh();
}
