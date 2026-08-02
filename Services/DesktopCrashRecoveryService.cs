using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FocusPanel.Data;
using FocusPanel.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.Services;

internal sealed record DesktopCrashRecoveryItem(
    int PreferenceId,
    string FilePath,
    string? ManagedPath,
    long? OriginalAttributes);

internal readonly record struct DesktopCrashRecoveryResult(
    bool Attempted,
    int Restored,
    int Failed);

internal interface IDesktopCrashRecoveryStore
{
    IReadOnlyList<DesktopCrashRecoveryItem>
        LoadCollectedItems();
    void MarkRestored(int preferenceId);
    void MarkRecoveryRequired(int preferenceId);
}

internal sealed class DesktopCrashRecoveryService
{
    private readonly IDesktopCrashRecoveryStore _store;
    private readonly IDesktopItemVisibilityService
        _visibility;
    private readonly string _markerPath;
    private readonly string _userDesktop;
    private readonly string _commonDesktop;
    private readonly object _gate = new();

    internal DesktopCrashRecoveryService()
        : this(
            new AppDbDesktopCrashRecoveryStore(),
            new WindowsDesktopItemVisibilityService(),
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .LocalApplicationData),
                "FocusPanel",
                "desktop-recovery-required"),
            Environment.GetFolderPath(
                Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(
                Environment.SpecialFolder
                    .CommonDesktopDirectory))
    {
    }

    internal DesktopCrashRecoveryService(
        IDesktopCrashRecoveryStore store,
        IDesktopItemVisibilityService visibility,
        string markerPath,
        string userDesktop,
        string commonDesktop)
    {
        _store = store;
        _visibility = visibility;
        _markerPath = markerPath;
        _userDesktop = userDesktop;
        _commonDesktop = commonDesktop;
    }

    internal void Arm()
    {
        lock (_gate)
        {
            try
            {
                string? directory =
                    Path.GetDirectoryName(_markerPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(
                    _markerPath,
                    DateTimeOffset.Now.ToString("O"));
            }
            catch
            {
                // The marker is best effort and must not block startup.
            }
        }
    }

    internal void Disarm()
    {
        lock (_gate)
        {
            DisarmCore();
        }
    }

    internal DesktopCrashRecoveryResult
        RestoreIfRequested(bool force) =>
        force || File.Exists(_markerPath)
            ? RestoreCollectedItems()
            : new DesktopCrashRecoveryResult(
                false,
                0,
                0);

    internal DesktopCrashRecoveryResult
        RestoreCollectedItems()
    {
        lock (_gate)
            return RestoreCollectedItemsCore();
    }

    private DesktopCrashRecoveryResult
        RestoreCollectedItemsCore()
    {
        int restored = 0;
        int failed = 0;
        IReadOnlyList<DesktopCrashRecoveryItem> items;
        try
        {
            items = _store.LoadCollectedItems();
        }
        catch
        {
            return new DesktopCrashRecoveryResult(
                true,
                0,
                1);
        }

        foreach (DesktopCrashRecoveryItem item in items)
        {
            try
            {
                string path = ResolvePath(item);
                if (!_visibility.Exists(path))
                {
                    _store.MarkRecoveryRequired(
                        item.PreferenceId);
                    failed++;
                    continue;
                }

                FileAttributes attributes =
                    item.OriginalAttributes.HasValue
                        ? DesktopItemAttributePolicy
                            .Restore(
                                item.OriginalAttributes
                                    .Value)
                        : RemoveCollectionAttributes(
                            _visibility.GetAttributes(
                                path));
                _visibility.SetAttributes(
                    path,
                    attributes);
                _visibility.NotifyAttributesChanged(
                    path);
                _store.MarkRestored(
                    item.PreferenceId);
                restored++;
            }
            catch
            {
                try
                {
                    _store.MarkRecoveryRequired(
                        item.PreferenceId);
                }
                catch
                {
                }
                failed++;
            }
        }

        if (failed == 0)
            DisarmCore();
        return new DesktopCrashRecoveryResult(
            true,
            restored,
            failed);
    }

    private void DisarmCore()
    {
        try
        {
            File.Delete(_markerPath);
        }
        catch
        {
        }
    }

    private string ResolvePath(
        DesktopCrashRecoveryItem item)
    {
        if (!string.IsNullOrWhiteSpace(
                item.ManagedPath))
        {
            return Path.GetFullPath(
                item.ManagedPath);
        }
        if (Path.IsPathRooted(item.FilePath))
            return Path.GetFullPath(item.FilePath);

        string userPath = Path.Combine(
            _userDesktop,
            item.FilePath);
        string commonPath = Path.Combine(
            _commonDesktop,
            item.FilePath);
        return _visibility.Exists(userPath)
            ? userPath
            : commonPath;
    }

    private static FileAttributes
        RemoveCollectionAttributes(
            FileAttributes current)
    {
        FileAttributes restored =
            current
            & ~FileAttributes.Hidden
            & ~FileAttributes.System;
        return restored == 0
            ? FileAttributes.Normal
            : restored;
    }
}

internal sealed class AppDbDesktopCrashRecoveryStore
    : IDesktopCrashRecoveryStore
{
    public IReadOnlyList<DesktopCrashRecoveryItem>
        LoadCollectedItems()
    {
        using var context = new AppDbContext();
        context.EnsureSchema();
        return context.DesktopFilePreferences
            .AsNoTracking()
            .Where(item =>
                item.IsHiddenFromDesktop
                && item.CollectionMode
                    != DesktopCollectionMode
                        .LegacyStorage)
            .OrderBy(item => item.Id)
            .Select(item =>
                new DesktopCrashRecoveryItem(
                    item.Id,
                    item.FilePath,
                    item.ManagedPath,
                    item.OriginalAttributes))
            .ToArray();
    }

    public void MarkRestored(int preferenceId)
    {
        using var context = new AppDbContext();
        DesktopFilePreference? preference =
            context.DesktopFilePreferences.Find(
                preferenceId);
        if (preference == null)
            return;

        preference.IsHiddenFromDesktop = false;
        preference.CollectionMode =
            DesktopCollectionMode.None;
        preference.OperationState =
            DesktopVisibilityOperation.Stable;
        preference.OriginalAttributes = null;
        context.SaveChanges();
    }

    public void MarkRecoveryRequired(
        int preferenceId)
    {
        using var context = new AppDbContext();
        DesktopFilePreference? preference =
            context.DesktopFilePreferences.Find(
                preferenceId);
        if (preference == null)
            return;
        preference.OperationState =
            DesktopVisibilityOperation
                .RecoveryRequired;
        context.SaveChanges();
    }
}
