using System;
using System.Collections.Generic;
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

    public static async Task<DesktopOrganizeResult> ExecuteAsync(
        IReadOnlyList<DesktopAutoOrganizeItem> items,
        bool allowCommonDesktopElevation,
        Func<DesktopAutoOrganizeItem, string, bool, Task> collect)
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
        }

        return new DesktopOrganizeResult(
            items.Count,
            collected,
            authorizationRequired,
            failedItems.Count,
            failedItems);
    }
}

public sealed record DesktopAutoOrganizeItem(
    string Name,
    string FullPath,
    string FileType);

public sealed record DesktopOrganizeResult(
    int Attempted,
    int Collected,
    int AuthorizationRequired,
    int Failed,
    IReadOnlyList<string> FailedItems);
