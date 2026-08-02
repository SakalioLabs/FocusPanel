using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FocusPanel.Data;
using FocusPanel.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.Services;

internal sealed record ShellPreferenceSnapshot(
    bool FirstRunAccepted,
    bool ReplacementEnabled,
    string ThemeMode,
    bool DisableHotZoneInFullscreen,
    bool EnableTaskbarSlotHotkeys,
    string DisplayTargetMode,
    int AutoHideDelayMilliseconds,
    int HotZoneDwellMilliseconds,
    bool KeepCompactDockVisible)
{
    internal static ShellPreferenceSnapshot Default { get; } =
        new(
            false,
            false,
            "System",
            true,
            false,
            ShellDisplayTarget
                .OutermostRightValue,
            ShellAutoHideDelayPolicy
                .DefaultMilliseconds,
            EdgeHotZoneSensitivityPolicy
                .DefaultDwellMilliseconds,
            false);
}

internal static class PersistentCompactDockDefaultPolicy
{
    internal static bool Resolve(
        bool firstRunAccepted,
        string? storedValue)
    {
        if (bool.TryParse(
                storedValue,
                out bool explicitValue))
        {
            return explicitValue;
        }

        // A visible compact dock is the humane first-run default. Existing
        // users who predate this preference retain the former auto-hide
        // behavior until they opt in themselves.
        return !firstRunAccepted;
    }
}

internal interface IShellPreferenceRepository
    : IDisposable
{
    Task<ShellPreferenceSnapshot> LoadAsync();

    bool QueueSave(
        string key,
        string value);

    Task CompleteAsync();

    event Action<string, Exception>? SaveFailed;
}

internal sealed class ShellPreferenceRepository
    : IShellPreferenceRepository
{
    internal const string FirstRunAcceptedKey =
        "Shell.FirstRunAccepted";
    internal const string ReplacementEnabledKey =
        "Shell.ReplacementEnabled";
    internal const string ThemeModeKey =
        "Shell.Theme";
    internal const string FullscreenHotZoneKey =
        "Shell.DisableHotZoneInFullscreen";
    internal const string TaskbarSlotHotkeysKey =
        "Shell.EnableTaskbarSlotHotkeys";
    internal const string DisplayTargetModeKey =
        "Shell.DisplayTargetMode";
    internal const string AutoHideDelayKey =
        "Shell.AutoHideDelayMilliseconds";
    internal const string HotZoneDwellKey =
        "Shell.HotZoneDwellMilliseconds";
    internal const string KeepCompactDockVisibleKey =
        "Shell.KeepCompactDockVisible";

    private static readonly string[] Keys =
    {
        FirstRunAcceptedKey,
        ReplacementEnabledKey,
        ThemeModeKey,
        FullscreenHotZoneKey,
        TaskbarSlotHotkeysKey,
        DisplayTargetModeKey,
        AutoHideDelayKey,
        HotZoneDwellKey,
        KeepCompactDockVisibleKey
    };

    private readonly object _sync = new();
    private readonly Func<ShellPreferenceSnapshot> _load;
    private readonly Action<string, string> _save;
    private readonly Queue<string> _pendingOrder = new();
    private readonly Dictionary<string, string> _pendingValues =
        new(StringComparer.Ordinal);
    private Task _processor = Task.CompletedTask;
    private Task<ShellPreferenceSnapshot>?
        _loadTask;
    private bool _isRunning;
    private bool _isAccepting = true;
    private bool _isDisposed;

    internal ShellPreferenceRepository()
        : this(
            LoadCore,
            SaveCore)
    {
    }

    internal ShellPreferenceRepository(
        Func<ShellPreferenceSnapshot> load,
        Action<string, string> save)
    {
        _load =
            load
            ?? throw new ArgumentNullException(
                nameof(load));
        _save =
            save
            ?? throw new ArgumentNullException(
                nameof(save));
    }

    public event Action<string, Exception>? SaveFailed;

    public Task<ShellPreferenceSnapshot> LoadAsync()
    {
        lock (_sync)
        {
            if (_isDisposed)
            {
                return Task.FromResult(
                    ShellPreferenceSnapshot.Default);
            }

            return _loadTask
                ??= Task.Run(LoadSafely);
        }
    }

    private ShellPreferenceSnapshot LoadSafely()
    {
        try
        {
            return Normalize(_load());
        }
        catch
        {
            return ShellPreferenceSnapshot.Default;
        }
    }

    public bool QueueSave(
        string key,
        string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException(
                "Preference key is required.",
                nameof(key));

        value ??= string.Empty;
        lock (_sync)
        {
            if (!_isAccepting || _isDisposed)
                return false;

            if (!_pendingValues.ContainsKey(key))
                _pendingOrder.Enqueue(key);
            _pendingValues[key] = value;

            if (!_isRunning)
            {
                _isRunning = true;
                _processor = ProcessAsync();
            }

            return true;
        }
    }

    public Task CompleteAsync()
    {
        lock (_sync)
        {
            _isAccepting = false;
            return _loadTask == null
                ? _processor
                : Task.WhenAll(
                    _processor,
                    _loadTask);
        }
    }

    private async Task ProcessAsync()
    {
        while (true)
        {
            string key;
            string value;
            lock (_sync)
            {
                if (_pendingOrder.Count == 0)
                {
                    _isRunning = false;
                    return;
                }

                key = _pendingOrder.Dequeue();
                value = _pendingValues[key];
                _pendingValues.Remove(key);
            }

            try
            {
                await Task.Run(
                        () => _save(key, value))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                NotifySaveFailed(
                    key,
                    ex);
            }
        }
    }

    private void NotifySaveFailed(
        string key,
        Exception error)
    {
        Action<string, Exception>? handlers =
            SaveFailed;
        if (handlers == null)
            return;

        foreach (Delegate handler in
                 handlers.GetInvocationList())
        {
            try
            {
                ((Action<string, Exception>)handler)(
                    key,
                    error);
            }
            catch
            {
                // A detached UI observer must not stop later saves.
            }
        }
    }

    private static ShellPreferenceSnapshot LoadCore()
    {
        using var context = new AppDbContext();
        Dictionary<string, string> values =
            context.AppConfigs
                .AsNoTracking()
                .Where(item =>
                    Keys.Contains(item.Key))
                .ToDictionary(
                    item => item.Key,
                    item => item.Value,
                    StringComparer.Ordinal);

        bool firstRunAccepted =
            ReadBoolean(
                values,
                FirstRunAcceptedKey,
                false);
        values.TryGetValue(
            KeepCompactDockVisibleKey,
            out string? storedCompactDockPreference);

        return new ShellPreferenceSnapshot(
            firstRunAccepted,
            ReadBoolean(
                values,
                ReplacementEnabledKey,
                false),
            NormalizeTheme(
                ReadString(
                    values,
                    ThemeModeKey,
                    "System")),
            ReadBoolean(
                values,
                FullscreenHotZoneKey,
                true),
            ReadBoolean(
                values,
                TaskbarSlotHotkeysKey,
                false),
            ShellDisplayTarget.NormalizeValue(
                ReadString(
                    values,
                    DisplayTargetModeKey,
                    ShellDisplayTarget
                        .OutermostRightValue)),
            ShellAutoHideDelayPolicy.Normalize(
                ReadInt32(
                    values,
                    AutoHideDelayKey,
                    ShellAutoHideDelayPolicy
                        .DefaultMilliseconds)),
            EdgeHotZoneSensitivityPolicy
                .NormalizeDwell(
                    ReadInt32(
                        values,
                        HotZoneDwellKey,
                        EdgeHotZoneSensitivityPolicy
                            .DefaultDwellMilliseconds)),
            PersistentCompactDockDefaultPolicy.Resolve(
                firstRunAccepted,
                storedCompactDockPreference));
    }

    private static void SaveCore(
        string key,
        string value)
    {
        using var context = new AppDbContext();
        AppConfig? config =
            context.AppConfigs.Find(key);
        if (config == null)
        {
            context.AppConfigs.Add(
                new AppConfig
                {
                    Key = key,
                    Value = value
                });
        }
        else
        {
            config.Value = value;
        }

        context.SaveChanges();
    }

    private static ShellPreferenceSnapshot Normalize(
        ShellPreferenceSnapshot snapshot) =>
        snapshot with
        {
            ThemeMode =
                NormalizeTheme(
                    snapshot.ThemeMode),
            DisplayTargetMode =
                ShellDisplayTarget.NormalizeValue(
                    snapshot.DisplayTargetMode),
            AutoHideDelayMilliseconds =
                ShellAutoHideDelayPolicy.Normalize(
                    snapshot
                        .AutoHideDelayMilliseconds),
            HotZoneDwellMilliseconds =
                EdgeHotZoneSensitivityPolicy
                    .NormalizeDwell(
                        snapshot
                            .HotZoneDwellMilliseconds)
        };

    private static string NormalizeTheme(
        string? value) =>
        value is "Light" or "Dark"
            ? value
            : "System";

    private static bool ReadBoolean(
        IReadOnlyDictionary<string, string> values,
        string key,
        bool fallback) =>
        values.TryGetValue(
                key,
                out string? raw)
            && bool.TryParse(
                raw,
                out bool value)
            ? value
            : fallback;

    private static int ReadInt32(
        IReadOnlyDictionary<string, string> values,
        string key,
        int fallback) =>
        values.TryGetValue(
                key,
                out string? raw)
            && int.TryParse(
                raw,
                out int value)
            ? value
            : fallback;

    private static string ReadString(
        IReadOnlyDictionary<string, string> values,
        string key,
        string fallback) =>
        values.TryGetValue(
                key,
                out string? value)
            && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;

    public void Dispose()
    {
        Task completion;
        lock (_sync)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _isAccepting = false;
            completion = _loadTask == null
                ? _processor
                : Task.WhenAll(
                    _processor,
                    _loadTask);
        }

        completion.GetAwaiter()
            .GetResult();
    }
}
