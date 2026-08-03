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
    int OrderIndex,
    bool IsLocked = false);

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

    bool CreatePartition(string name);

    bool RenamePartition(
        string oldName,
        string newName);

    bool DeletePartition(string name);

    bool ReorderPartition(
        string sourceName,
        string targetName,
        bool insertAfter);

    bool MovePartitionToColumn(
        string sourceName,
        int targetColumn);

    bool AssignFileToPartition(
        string fileName,
        string partitionName);

    bool SetPartitionLocked(
        string partitionName,
        bool isLocked);

    int ApplySmartPartitionAssignments(
        IReadOnlyList<SmartPartitionAssignment> assignments);
}

internal sealed record OrganizerLayoutMutationHandlers(
    Func<string, bool> CreatePartition,
    Func<string, string, bool> RenamePartition,
    Func<string, bool> DeletePartition,
    Func<string, string, bool, bool> ReorderPartition,
    Func<string, int, bool> MovePartitionToColumn,
    Func<string, string, bool> AssignFileToPartition)
{
    internal static OrganizerLayoutMutationHandlers
        Default { get; } =
        new(
            CreatePartitionCore,
            RenamePartitionCore,
            DeletePartitionCore,
            ReorderPartitionCore,
            MovePartitionToColumnCore,
            AssignFileToPartitionCore);

    private static bool CreatePartitionCore(
        string name) =>
        OrganizerLayoutRepository
            .CreatePartitionCore(name);

    private static bool RenamePartitionCore(
        string oldName,
        string newName) =>
        OrganizerLayoutRepository
            .RenamePartitionCore(
                oldName,
                newName);

    private static bool DeletePartitionCore(
        string name) =>
        OrganizerLayoutRepository
            .DeletePartitionCore(name);

    private static bool ReorderPartitionCore(
        string sourceName,
        string targetName,
        bool insertAfter) =>
        OrganizerLayoutRepository
            .ReorderPartitionCore(
                sourceName,
                targetName,
                insertAfter);

    private static bool MovePartitionToColumnCore(
        string sourceName,
        int targetColumn) =>
        OrganizerLayoutRepository
            .MovePartitionToColumnCore(
                sourceName,
                targetColumn);

    private static bool AssignFileToPartitionCore(
        string fileName,
        string partitionName) =>
        OrganizerLayoutRepository
            .AssignFileToPartitionCore(
                fileName,
                partitionName);
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
    private readonly OrganizerLayoutMutationHandlers
        _mutations;
    private readonly SemaphoreSlim _gate =
        new(1, 1);

    internal OrganizerLayoutRepository()
        : this(
            LoadCore,
            SaveOptionsCore,
            OrganizerLayoutMutationHandlers.Default)
    {
    }

    internal OrganizerLayoutRepository(
        Func<
            OrganizerLegacyLayout,
            OrganizerLayoutSnapshot> load,
        Action<OrganizerLayoutOptions>? saveOptions = null,
        OrganizerLayoutMutationHandlers? mutations = null)
    {
        _load =
            load
            ?? throw new ArgumentNullException(
                nameof(load));
        _saveOptions =
            saveOptions
            ?? SaveOptionsCore;
        _mutations =
            mutations
            ?? OrganizerLayoutMutationHandlers.Default;
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

    public bool CreatePartition(string name) =>
        ExecuteMutation(
            () =>
                _mutations.CreatePartition(
                    RequireName(name)));

    public bool RenamePartition(
        string oldName,
        string newName) =>
        ExecuteMutation(
            () =>
                _mutations.RenamePartition(
                    RequireName(oldName),
                    RequireName(newName)));

    public bool DeletePartition(string name) =>
        ExecuteMutation(
            () =>
                _mutations.DeletePartition(
                    RequireName(name)));

    public bool ReorderPartition(
        string sourceName,
        string targetName,
        bool insertAfter) =>
        ExecuteMutation(
            () =>
                _mutations.ReorderPartition(
                    RequireName(sourceName),
                    RequireName(targetName),
                    insertAfter));

    public bool MovePartitionToColumn(
        string sourceName,
        int targetColumn) =>
        ExecuteMutation(
            () =>
                _mutations.MovePartitionToColumn(
                    RequireName(sourceName),
                    targetColumn));

    public bool AssignFileToPartition(
        string fileName,
        string partitionName) =>
        ExecuteMutation(
            () =>
                _mutations.AssignFileToPartition(
                    RequireName(fileName),
                    partitionName?.Trim()
                    ?? string.Empty));

    public bool SetPartitionLocked(
        string partitionName,
        bool isLocked) =>
        ExecuteMutation(
            () => SetPartitionLockedCore(
                RequireName(partitionName),
                isLocked));

    public int ApplySmartPartitionAssignments(
        IReadOnlyList<SmartPartitionAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        _gate.Wait();
        try
        {
            return ApplySmartPartitionAssignmentsCore(
                assignments);
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool ExecuteMutation(
        Func<bool> mutation)
    {
        _gate.Wait();
        try
        {
            return mutation();
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string RequireName(string value)
    {
        string normalized =
            value?.Trim()
            ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new ArgumentException(
                "名称不能为空。",
                nameof(value));
        }

        return normalized;
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
                            item.OrderIndex,
                            item.IsLocked))
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

    internal static bool CreatePartitionCore(
        string name)
    {
        using var context = new AppDbContext();
        if (!CreatePartitionInContext(
                context,
                name))
        {
            return false;
        }

        context.SaveChanges();
        return true;
    }

    internal static bool RenamePartitionCore(
        string oldName,
        string newName)
    {
        using var context = new AppDbContext();
        List<DesktopPartition> partitions =
            context.DesktopPartitions.ToList();
        DesktopPartition? partition =
            FindPartition(
                partitions,
                oldName);
        if (partition == null)
            return false;

        DesktopPartition? duplicate =
            partitions.FirstOrDefault(item =>
                !ReferenceEquals(item, partition)
                && string.Equals(
                    item.Name,
                    newName,
                    StringComparison.OrdinalIgnoreCase));
        if (duplicate != null)
        {
            throw new InvalidOperationException(
                $"收纳盒“{newName}”已存在。");
        }

        if (string.Equals(
                partition.Name,
                newName,
                StringComparison.Ordinal))
        {
            return false;
        }

        string storedOldName = partition.Name;
        partition.Name = newName;
        foreach (DesktopFilePreference preference
                 in context.DesktopFilePreferences
                     .ToList()
                     .Where(item =>
                         string.Equals(
                             item.PartitionName,
                             storedOldName,
                             StringComparison.OrdinalIgnoreCase)))
        {
            preference.PartitionName = newName;
        }
        context.SaveChanges();
        return true;
    }

    internal static bool DeletePartitionCore(
        string name)
    {
        using var context = new AppDbContext();
        DesktopPartition? partition =
            FindPartition(
                context.DesktopPartitions.ToList(),
                name);
        if (partition == null)
            return false;

        context.DesktopPartitions.Remove(partition);
        foreach (DesktopFilePreference preference
                 in context.DesktopFilePreferences
                     .ToList()
                     .Where(item =>
                         string.Equals(
                             item.PartitionName,
                             partition.Name,
                             StringComparison.OrdinalIgnoreCase)))
        {
            preference.PartitionName =
                string.Empty;
        }
        context.SaveChanges();
        return true;
    }

    internal static bool ReorderPartitionCore(
        string sourceName,
        string targetName,
        bool insertAfter)
    {
        using var context = new AppDbContext();
        List<DesktopPartition> partitions =
            context.DesktopPartitions.ToList();
        if (!OrganizerPartitionOrdering.Reorder(
                partitions,
                sourceName,
                targetName,
                insertAfter))
        {
            return false;
        }

        context.SaveChanges();
        return true;
    }

    internal static bool MovePartitionToColumnCore(
        string sourceName,
        int targetColumn)
    {
        using var context = new AppDbContext();
        List<DesktopPartition> partitions =
            context.DesktopPartitions.ToList();
        if (!OrganizerPartitionOrdering.MoveToColumn(
                partitions,
                sourceName,
                targetColumn))
        {
            return false;
        }

        context.SaveChanges();
        return true;
    }

    internal static bool AssignFileToPartitionCore(
        string fileName,
        string partitionName)
    {
        using var context = new AppDbContext();
        DesktopFilePreference? preference =
            context.DesktopFilePreferences
                .ToList()
                .Where(item =>
                    string.Equals(
                        item.FilePath,
                        fileName,
                        StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Id)
                .LastOrDefault();
        if (partitionName.Length == 0)
        {
            if (preference == null)
                return false;

            if (preference.IsHiddenFromDesktop)
            {
                if (preference.PartitionName.Length == 0)
                    return false;

                preference.PartitionName =
                    string.Empty;
            }
            else
            {
                context.DesktopFilePreferences.Remove(
                    preference);
            }
            context.SaveChanges();
            return true;
        }

        bool partitionCreated =
            CreatePartitionInContext(
            context,
            partitionName);
        if (preference == null)
        {
            preference =
                new DesktopFilePreference
                {
                    FilePath = fileName
                };
            context.DesktopFilePreferences.Add(
                preference);
        }
        else if (string.Equals(
                     preference.PartitionName,
                     partitionName,
                     StringComparison.Ordinal))
        {
            if (!partitionCreated)
                return false;

            context.SaveChanges();
            return true;
        }

        preference.PartitionName = partitionName;
        context.SaveChanges();
        return true;
    }

    internal static bool SetPartitionLockedCore(
        string partitionName,
        bool isLocked)
    {
        using var context = new AppDbContext();
        DesktopPartition? partition = FindPartition(
            context.DesktopPartitions.ToList(),
            partitionName);
        if (partition == null || partition.IsLocked == isLocked)
            return false;
        partition.IsLocked = isLocked;
        context.SaveChanges();
        return true;
    }

    internal static int ApplySmartPartitionAssignmentsCore(
        IReadOnlyList<SmartPartitionAssignment> assignments)
    {
        using var context = new AppDbContext();
        using var transaction = context.Database.BeginTransaction();
        List<DesktopPartition> partitions =
            context.DesktopPartitions.ToList();
        var unlocked = new HashSet<string>(
            partitions.Where(item => !item.IsLocked)
                .Select(item => item.Name),
            StringComparer.OrdinalIgnoreCase);
        List<DesktopFilePreference> preferences =
            context.DesktopFilePreferences.ToList();
        int changed = 0;
        foreach (SmartPartitionAssignment assignment in assignments)
        {
            DesktopFilePreference? preference = preferences
                .Where(item => item.Id == assignment.PreferenceId)
                .LastOrDefault();
            if (!SmartPartitionApplyPolicy.CanApply(
                    assignment,
                    preference,
                    unlocked))
            {
                continue;
            }
            preference!.PartitionName = assignment.TargetPartition;
            changed++;
        }
        context.SaveChanges();
        transaction.Commit();
        return changed;
    }

    internal static class SmartPartitionApplyPolicy
    {
        internal static bool CanApply(
            SmartPartitionAssignment assignment,
            DesktopFilePreference? preference,
            ISet<string> unlockedPartitions) =>
            preference != null
            && preference.IsHiddenFromDesktop
            && unlockedPartitions.Contains(
                assignment.SourcePartition)
            && unlockedPartitions.Contains(
                assignment.TargetPartition)
            && string.Equals(
                preference.PartitionName,
                assignment.SourcePartition,
                StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                preference.PartitionName,
                assignment.TargetPartition,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool CreatePartitionInContext(
        AppDbContext context,
        string name)
    {
        List<DesktopPartition> partitions =
            context.DesktopPartitions.ToList();
        if (FindPartition(partitions, name) != null)
            return false;

        int maxOrder = partitions.Count == 0
            ? -1
            : partitions.Max(item =>
                item.OrderIndex);
        context.DesktopPartitions.Add(
            new DesktopPartition
            {
                Name = name,
                OrderIndex = maxOrder + 1
            });
        return true;
    }

    private static DesktopPartition? FindPartition(
        IEnumerable<DesktopPartition> partitions,
        string name) =>
        partitions.FirstOrDefault(item =>
            string.Equals(
                item.Name,
                name,
                StringComparison.OrdinalIgnoreCase));

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
