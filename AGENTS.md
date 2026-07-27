# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build
dotnet build FocusPanel.csproj

# Run (from project directory)
dotnet run --project FocusPanel.csproj
```

There is no `.sln` file — `FocusPanel.csproj` is the sole project. Open it in Visual Studio 2022 or VS Code.

## Architecture

**MVVM** via CommunityToolkit.Mvvm (source generators: `[ObservableProperty]`, `[RelayCommand]`). No DI container — ViewModels directly instantiate `AppDbContext` and services as needed.

### Project Layout

| Directory | Role |
|-----------|------|
| `Views/` | WPF windows and user controls (`.xaml` + `.xaml.cs` code-behind) |
| `ViewModels/` | Observable objects bound to views. `MainViewModel` is the navigation hub |
| `Models/` | EF Core entities and DTOs |
| `Services/` | Business logic |
| `Data/` | `AppDbContext` — EF Core SQLite context with manual migration via `EnsureSchema()` |
| `Helpers/` | Win32 interop (`DesktopHelper`, `IconHelper`) |
| `Converters/` | WPF `IValueConverter` implementations |

### Database

EF Core 7 with SQLite (`Microsoft.EntityFrameworkCore.Sqlite` 7.0.16). Database at `%APPDATA%/FocusPanel/focuspanel.db`. Schema managed by `EnsureSchema()` in `Data/AppDbContext.cs` — raw `CREATE TABLE IF NOT EXISTS` + `ALTER TABLE` for migrations. No EF migrations tool at runtime.

`App.xaml.cs` handles startup: sets working directory, initializes DB with corruption detection (archives corrupted DB, restores latest backup, or recreates from scratch). Crash recovery for "no such table" errors triggers automatic DB reset. `DatabaseBackupService` keeps up to 5 rolling backups in AppData and local `Backups/` folders. `--restore` flag restores from latest backup on startup.

### Key Models

- **`TodoItem`** — self-referencing hierarchy (`ParentId` → `Parent`/`Children`). Root items are projects with `ViewMode` (List/Board), `ColumnsJson`, `CustomFieldsJson`. Children are tasks with `Status`, `CustomValuesJson`.
- **`DesktopFile`** — in-memory only (not an entity). Created by `FileOrganizerService` scanning the desktop. `IsHidden` flag + `CustomPartition` for categorization.
- **`DesktopPartition`** — user-defined file categories (columns for the masonry layout).
- **`DesktopFilePreference`** — persists per-file settings: which partition, whether hidden from desktop.
- **`PomodoroSession`** — completed or interrupted focus sessions.
- **`AppConfig`** — key-value configuration store.
- **OKR entities** — `OkrObjective`, `OkrKeyResult`, `OkrSyncLog` (EF entities); `OkrSyncResult` (DTO); `OkrSyncStatus` (enum). Bi-directional sync with Feishu OKR API.
- **Feishu DTOs** — `FeishuTokenResponse`, `FeishuApiResponse<T>`, `FeishuOkrDtos` (API request/response types), `FeishuApiException`.

### MainWindow Behavior

- **Desktop-only visibility**: Panel only appears when the user is on the desktop (foreground is `Progman`, `WorkerW`, or `Shell_TrayWnd`). When any other app is foreground (browser, game, etc.), the panel hides completely via `Visibility.Collapsed`. Managed by `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` in `MainWindow.xaml.cs`.
- **Collapsed**: 80px wide strip. **Expanded**: 800px wide. Switching is instant (no animations).
- **Mouse enter**: sidebar expands. **Mouse leave**: sidebar collapses UNLESS a child control has keyboard focus (e.g., user is typing in a text field — sidebar stays open).
- Mouse enter does NOT call `Activate()`, so the panel doesn't steal focus from other apps.
- Closing hides to system tray (`_hiddenToTray` flag prevents foreground hook from re-showing). `ForceClose()` is the only true exit.
- Inbox root item (Id=1) is seeded and protected from deletion.

### Navigation

`MainViewModel.Navigate()` switches `CurrentViewModel`. Navigation targets: `Dashboard`, `Tasks`, `Pomodoro`, `Files`, `OKR`, `AI`. Expensive ViewModels are cached as fields (`_tasksViewModel`, `_okrViewModel`, `_pomodoroViewModel`, `_fileOrganizerViewModel`).

### Desktop Icon Management

Per-file icon hiding uses `FileAttributes.Hidden` on the desktop filesystem. When a file is dragged into a panel partition, `HideFileToPanelCommand` sets the Hidden attribute (icon disappears from desktop, file stays physically on desktop). `RestoreFileFromPanel` removes the attribute. The file scan in `FileOrganizerService.RefreshFiles()` includes hidden files that are FocusPanel-managed (tracked in `DesktopFilePreference.IsHiddenFromDesktop`) while still filtering system-hidden files.

Context menu semantics:
- "Move to" → `AssignToPartitionCommand` — categorize only, icon stays visible
- "收纳到面板" → `HideFileToPanelCommand` — categorize + hide from desktop
- "取消收纳" → `RestoreFileFromPanelCommand` — restore desktop icon

### Feishu OKR Integration

`FeishuAuthService` manages tenant access tokens (cached in `AppConfig`, auto-refreshed). `FeishuOkrApiService` wraps the Feishu OKR v1 API with retry logic (3 attempts, exponential backoff on 429). `OkrSyncService` handles bi-directional sync: pulls from Feishu to local DB, pushes locally-dirty items. Configurable auto-sync timer (default 30 min). `OkrSyncStatus` enum tracks per-entity state: `Synced`, `LocalCreated`, `LocalModified`, `LocalDeleted`. Conflict resolution: server wins. `IOkrDataProvider` is an interface for future AI assistant integration.
