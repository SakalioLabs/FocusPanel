using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FocusPanel.Data;
using FocusPanel.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.Services;

internal sealed record OrganizerLayoutOptions(
    double IconScale,
    bool IsListView,
    bool IsPersonalizedView,
    bool IsAutoOrganizeEnabled);

internal sealed record OrganizerLegacyLayout(
    OrganizerLayoutOptions FallbackOptions,
    IReadOnlyList<string> PartitionNames,
    IReadOnlyDictionary<string, string> FilePartitions);

internal sealed record OrganizerPartitionSnapshot(
    string Name,
    int ColumnIndex,
    int OrderIndex);

internal sealed record OrganizerFilePreferenceSnapshot(
    string FilePath,
    string PartitionName);

internal sealed record OrganizerLayoutSnapshot(
    bool IsValid,
    OrganizerLayoutOptions Options,
    IReadOnlyList<OrganizerPartitionSnapshot> Partitions,
    IReadOnlyList<OrganizerFilePreferenceSnapshot> Preferences)
{
    internal static OrganizerLayoutSnapshot Invalid { get; } =
        new(
            false,
            new OrganizerLayoutOptions(
                1,
                false,
                true,
                false),
            Array.Empty<OrganizerPartitionSnapshot>(),
            Array.Empty<OrganizerFilePreferenceSnapshot>());
}

internal interface IOrganizerLayoutRepository
{
    OrganizerLayoutSnapshot Load(
        OrganizerLegacyLayout legacy);

    void SaveOptions(
        OrganizerLayoutOptions options);
}

internal sealed class OrganizerLayoutRepository
    : IOrganizerLayoutRepository
{
    private const string IconScaleKey =
        "FileOrganizer_IconScale";
    private const string ListViewKey =
        "FileOrganizer_IsListView";
    private const string PersonalizedViewKey =
        "FileOrganizer_IsPersonalizedView";
    private const string AutoOrganizeKey =
        "FileOrganizer_AutoOrganize";

    private readonly Func<
        OrganizerLegacyLayout,
        OrganizerLayoutSnapshot> _load;
    private readonly Action<OrganizerLayoutOptions>
        _saveOptions;
    private readonly SemaphoreSlim _gate =
        new(1, 1);

    internal OrganizerLayoutRepository()
        : this(LoadCore, SaveOptionsCore)
    {
    }

    internal OrganizerLayoutRepository(
        Func<
            OrganizerLegacyLayout,
            OrganizerLayoutSnapshot> load,
        Action<OrganizerLayoutOptions>? saveOptions = null)
    {
        _load =
            load
            ?? throw new ArgumentNullException(
                nameof(load));
        _saveOptions =
            saveOptions
            ?? SaveOptionsCore;
    }

    public OrganizerLayoutSnapshot Load(
        OrganizerLegacyLayout legacy)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        _gate.Wait();
        try
        {
            return Normalize(_load(legacy));
        }
        catch
        {
            return OrganizerLayoutSnapshot.Invalid;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void SaveOptions(
        OrganizerLayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _gate.Wait();
        try
        {
            _saveOptions(Normalize(options));
        }
        finally
        {
            _gate.Release();
        }
    }

    private static OrganizerLayoutSnapshot Normalize(
        OrganizerLayoutSnapshot snapshot)
    {
        if (!snapshot.IsValid)
            return OrganizerLayoutSnapshot.Invalid;

        return snapshot with
        {
            Options = Normalize(snapshot.Options),
            Partitions =
                snapshot.Partitions
                ?? Array.Empty<
                    OrganizerPartitionSnapshot>(),
            Preferences =
                snapshot.Preferences
                ?? Array.Empty<
                    OrganizerFilePreferenceSnapshot>()
        };
    }

    private static OrganizerLayoutOptions Normalize(
        OrganizerLayoutOptions options) =>
        options with
        {
            IconScale = Math.Clamp(
                options.IconScale <= 0
                    ? 1
                    : options.IconScale,
                0.5,
                2)
        };

    private static OrganizerLayoutSnapshot LoadCore(
        OrganizerLegacyLayout legacy)
    {
        using var context = new AppDbContext();
        if (!context.DesktopPartitions.Any()
            && legacy.PartitionNames.Count > 0)
        {
            int index = 0;
            foreach (string name in
                     legacy.PartitionNames.Where(
                         value =>
                             !string.IsNullOrWhiteSpace(
                                 value)))
            {
                context.DesktopPartitions.Add(
                    new DesktopPartition
                    {
                        Name = name,
                        OrderIndex = index++
                    });
            }

            foreach (KeyValuePair<string, string> item
                     in legacy.FilePartitions)
            {
                context.DesktopFilePreferences.Add(
                    new DesktopFilePreference
                    {
                        FilePath = item.Key,
                        PartitionName = item.Value
                    });
            }
            context.SaveChanges();
        }

        string[] keys =
        {
            IconScaleKey,
            ListViewKey,
            PersonalizedViewKey,
            AutoOrganizeKey
        };
        Dictionary<string, string> config =
            context.AppConfigs
                .AsNoTracking()
                .Where(item => keys.Contains(item.Key))
                .ToDictionary(
                    item => item.Key,
                    item => item.Value);
        OrganizerLayoutOptions fallback =
            legacy.FallbackOptions;
        double iconScale =
            config.TryGetValue(
                IconScaleKey,
                out string? iconScaleText)
            && double.TryParse(
                iconScaleText,
                out double parsedIconScale)
                ? parsedIconScale
                : fallback.IconScale;
        bool listView =
            TryReadBoolean(
                config,
                ListViewKey,
                fallback.IsListView);
        bool personalizedView =
            TryReadBoolean(
                config,
                PersonalizedViewKey,
                fallback.IsPersonalizedView);
        bool autoOrganize =
            TryReadBoolean(
                config,
                AutoOrganizeKey,
                fallback.IsAutoOrganizeEnabled);

        OrganizerPartitionSnapshot[] partitions =
            context.DesktopPartitions
                .AsNoTracking()
                .OrderBy(item => item.OrderIndex)
                .Select(
                    item =>
                        new OrganizerPartitionSnapshot(
                            item.Name,
                            item.ColumnIndex,
                            item.OrderIndex))
                .ToArray();
        OrganizerFilePreferenceSnapshot[] preferences =
            context.DesktopFilePreferences
                .AsNoTracking()
                .Select(
                    item =>
                        new OrganizerFilePreferenceSnapshot(
                            item.FilePath,
                            item.PartitionName))
                .ToArray();

        return new OrganizerLayoutSnapshot(
            true,
            new OrganizerLayoutOptions(
                iconScale,
                listView,
                personalizedView,
                autoOrganize),
            partitions,
            preferences);
    }

    private static bool TryReadBoolean(
        IReadOnlyDictionary<string, string> config,
        string key,
        bool fallback) =>
        config.TryGetValue(
            key,
            out string? text)
        && bool.TryParse(text, out bool value)
            ? value
            : fallback;

    private static void SaveOptionsCore(
        OrganizerLayoutOptions options)
    {
        using var context = new AppDbContext();
        Upsert(
            context,
            IconScaleKey,
            options.IconScale.ToString());
        Upsert(
            context,
            ListViewKey,
            options.IsListView.ToString());
        Upsert(
            context,
            PersonalizedViewKey,
            options.IsPersonalizedView.ToString());
        Upsert(
            context,
            AutoOrganizeKey,
            options.IsAutoOrganizeEnabled.ToString());
        context.SaveChanges();
    }

    private static void Upsert(
        AppDbContext context,
        string key,
        string value)
    {
        AppConfig? config = context.AppConfigs.Find(key);
        if (config == null)
        {
            context.AppConfigs.Add(
                new AppConfig
                {
                    Key = key,
                    Value = value
                });
            return;
        }

        config.Value = value;
    }
}

internal sealed class OrganizerLayoutSaveState
{
    private readonly object _sync = new();
    private OrganizerLayoutOptions _options;

    internal OrganizerLayoutSaveState(
        OrganizerLayoutOptions options)
    {
        _options = options;
    }

    internal void Update(
        OrganizerLayoutOptions options)
    {
        lock (_sync)
            _options = options;
    }

    internal OrganizerLayoutOptions Read()
    {
        lock (_sync)
            return _options;
    }
}
