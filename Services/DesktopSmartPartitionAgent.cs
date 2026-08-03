using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Data;
using FocusPanel.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.Services;

internal sealed record SmartPartitionAssignment(
    int PreferenceId,
    string FileName,
    string SourcePartition,
    string TargetPartition);

internal sealed record SmartPartitionPlan(
    IReadOnlyList<SmartPartitionAssignment> Assignments,
    int CandidateCount,
    string Message)
{
    internal bool HasChanges => Assignments.Count > 0;
}

internal interface IDesktopSmartPartitionAgent
{
    event Action<int>? Applied;

    Task<SmartPartitionPlan> CreatePlanAsync(
        CancellationToken cancellationToken = default);

    Task<int> ApplyAsync(
        SmartPartitionPlan plan,
        CancellationToken cancellationToken = default);
}

internal sealed class DesktopSmartPartitionAgent :
    IDesktopSmartPartitionAgent
{
    internal static DesktopSmartPartitionAgent Shared { get; } =
        new();

    private readonly IAiSettingsService _settings;
    private readonly IAiDesktopPartitionService _ai;
    private readonly IOrganizerLayoutRepository _layout;
    private readonly SemaphoreSlim _gate = new(1, 1);

    internal DesktopSmartPartitionAgent()
        : this(
            new AiSettingsService(),
            new AiDesktopPartitionService(),
            new OrganizerLayoutRepository())
    {
    }

    internal DesktopSmartPartitionAgent(
        IAiSettingsService settings,
        IAiDesktopPartitionService ai,
        IOrganizerLayoutRepository layout)
    {
        _settings = settings;
        _ai = ai;
        _layout = layout;
    }

    public event Action<int>? Applied;

    public async Task<SmartPartitionPlan> CreatePlanAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await _settings.LoadStateAsync();
            string? apiKey = await _settings.LoadApiKeyAsync();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Empty(
                    "请先在 AI 助手配置中保存当前提供方的 API Key。");
            }

            SmartPartitionCandidateSnapshot snapshot =
                await Task.Run(
                    LoadCandidates,
                    cancellationToken);
            if (snapshot.Partitions.Count < 2)
            {
                return Empty(
                    "至少需要两个未锁定收纳盒，才能进行智能重排。");
            }
            if (snapshot.Items.Count == 0)
            {
                return Empty(
                    "未找到位于未锁定收纳盒中的已收纳项目。");
            }

            IReadOnlyDictionary<string, string> resolved =
                await _ai.ResolveExplicitAsync(
                    snapshot.Items.Select(item => item.Item).ToArray(),
                    snapshot.Partitions,
                    cancellationToken);
            var assignments = new List<SmartPartitionAssignment>();
            foreach (SmartPartitionCandidate candidate in snapshot.Items)
            {
                if (!resolved.TryGetValue(
                        candidate.Item.FullPath,
                        out string? target)
                    || string.Equals(
                        candidate.SourcePartition,
                        target,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                assignments.Add(
                    new SmartPartitionAssignment(
                        candidate.PreferenceId,
                        candidate.Item.Name,
                        candidate.SourcePartition,
                        target));
            }

            return assignments.Count == 0
                ? new SmartPartitionPlan(
                    Array.Empty<SmartPartitionAssignment>(),
                    snapshot.Items.Count,
                    "AI 已检查未锁定收纳盒，没有建议需要移动的项目。")
                : new SmartPartitionPlan(
                    assignments,
                    snapshot.Items.Count,
                    $"AI 建议调整 {assignments.Count} 个项目；锁定收纳盒未参与。只会修改分类数据。" );
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> ApplyAsync(
        SmartPartitionPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.HasChanges)
            return 0;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            int changed = await Task.Run(
                () => _layout.ApplySmartPartitionAssignments(
                    plan.Assignments),
                cancellationToken);
            if (changed > 0)
                NotifyApplied(changed);
            return changed;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static SmartPartitionCandidateSnapshot LoadCandidates()
    {
        using var context = new AppDbContext();
        context.EnsureSchema();
        string[] partitions = context.DesktopPartitions
            .AsNoTracking()
            .Where(item => !item.IsLocked)
            .OrderBy(item => item.OrderIndex)
            .Select(item => item.Name)
            .ToArray();
        var allowed = new HashSet<string>(
            partitions,
            StringComparer.OrdinalIgnoreCase);
        SmartPartitionCandidate[] items =
            context.DesktopFilePreferences
                .AsNoTracking()
                .Where(item =>
                    item.IsHiddenFromDesktop
                    && item.OperationState
                        == DesktopVisibilityOperation.Stable)
                .ToArray()
                .Where(item => allowed.Contains(item.PartitionName))
                .Take(80)
                .Select(CreateCandidate)
                .ToArray();
        return new SmartPartitionCandidateSnapshot(
            partitions,
            items);
    }

    private static SmartPartitionCandidate CreateCandidate(
        DesktopFilePreference preference)
    {
        string path = preference.ManagedPath
            ?? Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.Desktop),
                preference.FilePath);
        bool isFolder = Directory.Exists(path);
        string type = isFolder
            ? "Folder"
            : FileOrganizerService.ClassifyFileStatic(
                Path.GetExtension(preference.FilePath));
        return new SmartPartitionCandidate(
            preference.Id,
            preference.PartitionName,
            new DesktopAutoOrganizeItem(
                preference.FilePath,
                path,
                type));
    }

    private static SmartPartitionPlan Empty(string message) =>
        new(
            Array.Empty<SmartPartitionAssignment>(),
            0,
            message);

    private void NotifyApplied(int changed)
    {
        if (Applied == null)
            return;
        foreach (Delegate observer in Applied.GetInvocationList())
        {
            try
            {
                ((Action<int>)observer)(changed);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Smart partition observer failed: " + ex.Message);
            }
        }
    }

    private sealed record SmartPartitionCandidate(
        int PreferenceId,
        string SourcePartition,
        DesktopAutoOrganizeItem Item);

    private sealed record SmartPartitionCandidateSnapshot(
        IReadOnlyList<string> Partitions,
        IReadOnlyList<SmartPartitionCandidate> Items);
}

internal static class SmartPartitionAgentIntent
{
    internal static bool IsRequested(string text)
    {
        string value = text.Trim();
        if (!value.Contains("分区", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("收纳盒", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] actions =
        {
            "智能分区", "重新分区", "帮我分区", "整理分区",
            "调整分区", "重新整理收纳盒", "智能整理收纳盒"
        };
        return actions.Any(action =>
            value.Contains(action, StringComparison.OrdinalIgnoreCase));
    }
}
