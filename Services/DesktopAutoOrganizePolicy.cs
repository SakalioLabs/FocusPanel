using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FocusPanel.Services;

public static class DesktopAutoOrganizePolicy
{
    private static readonly IReadOnlyDictionary<string, string> TypeToPartition =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Image"] = "图片",
            ["Document"] = "文档",
            ["Video"] = "视频",
            ["Audio"] = "音频",
            ["Archive"] = "压缩包",
            ["Application"] = "应用程序",
            ["Folder"] = "文件夹",
            ["File"] = "其他"
        };

    public static string GetPartitionName(string? fileType) =>
        fileType != null && TypeToPartition.TryGetValue(fileType, out string? partition)
            ? partition
            : "其他";

    public static IReadOnlyList<DesktopAutoOrganizeItem>
        SelectCreatedItems(
            IEnumerable<DesktopAutoOrganizeItem> items,
            IEnumerable<string> createdPaths)
    {
        var normalizedPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (string path in createdPaths)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path))
                    normalizedPaths.Add(Path.GetFullPath(path));
            }
            catch
            {
                // Ignore malformed watcher paths.
            }
        }

        return items
            .Where(item =>
                !item.IsCollected
                && !item.NeedsRecovery
                && !item.IsProtectedPanelLauncher
                && IsSelectedPath(
                    item.FullPath,
                    normalizedPaths))
            .GroupBy(
                item => NormalizePath(item.FullPath),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    public static bool IsProtectedPanelLauncher(
        string? name,
        string? fullPath,
        string? processPath)
    {
        if (string.Equals(
                name,
                "FocusPanel.lnk",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string itemPath = NormalizePath(
            fullPath ?? string.Empty);
        string executablePath = NormalizePath(
            processPath ?? string.Empty);
        return itemPath.Length > 0
            && executablePath.Length > 0
            && string.Equals(
                itemPath,
                executablePath,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSelectedPath(
        string path,
        ISet<string> selectedPaths)
    {
        string normalized = NormalizePath(path);
        return normalized.Length > 0
            && selectedPaths.Contains(normalized);
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : Path.GetFullPath(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static async Task<DesktopOrganizeResult> ExecuteAsync(
        IReadOnlyList<DesktopAutoOrganizeItem> items,
        bool allowCommonDesktopElevation,
        Func<DesktopAutoOrganizeItem, string, bool, Task> collect,
        IProgress<DesktopOrganizeProgress>? progress = null)
    {
        int collected = 0;
        int authorizationRequired = 0;
        var failedItems = new List<string>();

        foreach (DesktopAutoOrganizeItem item in items)
        {
            try
            {
                await collect(
                    item,
                    GetPartitionName(item.FileType),
                    allowCommonDesktopElevation);
                collected++;
            }
            catch (CommonDesktopElevationRequiredException)
            {
                authorizationRequired++;
            }
            catch (OperationCanceledException)
            {
                authorizationRequired++;
            }
            catch
            {
                failedItems.Add(item.Name);
            }

            ReportProgress(
                progress,
                new DesktopOrganizeProgress(
                    collected
                    + authorizationRequired
                    + failedItems.Count,
                    items.Count,
                    collected,
                    authorizationRequired,
                    failedItems.Count,
                    item.Name));
        }

        return new DesktopOrganizeResult(
            items.Count,
            collected,
            authorizationRequired,
            failedItems.Count,
            failedItems);
    }

    private static void ReportProgress(
        IProgress<DesktopOrganizeProgress>? progress,
        DesktopOrganizeProgress value)
    {
        try
        {
            progress?.Report(value);
        }
        catch
        {
            // Progress is presentation-only and cannot invalidate
            // a completed file visibility transaction.
        }
    }

    public static string DescribeAutomaticResult(
        DesktopOrganizeResult result)
    {
        if (result.Attempted == 0)
            return string.Empty;

        if (result.AuthorizationRequired == 0
            && result.Failed == 0)
        {
            return $"已自动收纳 {result.Collected} 个新增项目";
        }

        var details = new List<string>();
        if (result.Collected > 0)
            details.Add($"已收纳 {result.Collected} 个");
        if (result.AuthorizationRequired > 0)
        {
            details.Add(
                $"{result.AuthorizationRequired} 个公共桌面项目需手动授权");
        }
        if (result.Failed > 0)
            details.Add($"{result.Failed} 个暂时失败");

        return string.Join("；", details);
    }
}

public sealed record DesktopAutoOrganizeItem(
    string Name,
    string FullPath,
    string FileType,
    bool IsCollected = false,
    bool NeedsRecovery = false,
    bool IsProtectedPanelLauncher = false);

public sealed record DesktopOrganizeResult(
    int Attempted,
    int Collected,
    int AuthorizationRequired,
    int Failed,
    IReadOnlyList<string> FailedItems);

public sealed record DesktopOrganizeProgress(
    int Processed,
    int Total,
    int Collected,
    int AuthorizationRequired,
    int Failed,
    string CurrentItemName);
