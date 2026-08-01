using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DesktopAutoOrganizePolicyTests
{
    [Theory]
    [InlineData("Image", "图片")]
    [InlineData("Document", "文档")]
    [InlineData("Folder", "文件夹")]
    [InlineData("Unknown", "其他")]
    public void MapsFileTypeToStablePartition(string fileType, string expected)
    {
        Assert.Equal(expected, DesktopAutoOrganizePolicy.GetPartitionName(fileType));
    }

    [Fact]
    public async Task ExecuteContinuesAfterAuthorizationAndItemFailures()
    {
        var items = new List<DesktopAutoOrganizeItem>
        {
            new("photo.png", @"C:\Desktop\photo.png", "Image"),
            new("public.lnk", @"C:\Public\Desktop\public.lnk", "File"),
            new("locked.docx", @"C:\Desktop\locked.docx", "Document"),
            new("folder", @"C:\Desktop\folder", "Folder")
        };
        var collected = new List<string>();

        DesktopOrganizeResult result = await DesktopAutoOrganizePolicy.ExecuteAsync(
            items,
            false,
            (item, partition, _) =>
            {
                if (item.Name == "public.lnk")
                    throw new CommonDesktopElevationRequiredException(item.FullPath);
                if (item.Name == "locked.docx")
                    throw new UnauthorizedAccessException();
                collected.Add($"{item.Name}:{partition}");
                return Task.CompletedTask;
            });

        Assert.Equal(4, result.Attempted);
        Assert.Equal(2, result.Collected);
        Assert.Equal(1, result.AuthorizationRequired);
        Assert.Equal(1, result.Failed);
        Assert.Contains("locked.docx", result.FailedItems);
        Assert.Contains(
            @"C:\Public\Desktop\public.lnk",
            result.AuthorizationRequiredPaths!);
        Assert.Contains("photo.png:图片", collected);
        Assert.Contains("folder:文件夹", collected);
    }

    [Fact]
    public async Task ExecuteReportsStableProgressAfterEveryItem()
    {
        var items = new[]
        {
            new DesktopAutoOrganizeItem(
                "one.png",
                @"C:\Desktop\one.png",
                "Image"),
            new DesktopAutoOrganizeItem(
                "public.lnk",
                @"C:\Public\Desktop\public.lnk",
                "Application"),
            new DesktopAutoOrganizeItem(
                "locked.txt",
                @"C:\Desktop\locked.txt",
                "Document")
        };
        var reports =
            new List<DesktopOrganizeProgress>();
        var progress =
            new InlineProgress<DesktopOrganizeProgress>(
                reports.Add);

        DesktopOrganizeResult result =
            await DesktopAutoOrganizePolicy
                .ExecuteAsync(
                    items,
                    false,
                    (item, _, _) =>
                    {
                        if (item.Name == "public.lnk")
                        {
                            throw new CommonDesktopElevationRequiredException(
                                item.FullPath);
                        }
                        if (item.Name == "locked.txt")
                            throw new IOException("locked");
                        return Task.CompletedTask;
                    },
                    progress);

        Assert.Equal(
            new[] { 1, 2, 3 },
            reports.Select(
                report => report.Processed));
        Assert.All(
            reports,
            report =>
                Assert.Equal(3, report.Total));
        Assert.Equal(1, reports[^1].Collected);
        Assert.Equal(
            1,
            reports[^1]
                .AuthorizationRequired);
        Assert.Equal(1, reports[^1].Failed);
        Assert.Equal(
            "locked.txt",
            reports[^1].CurrentItemName);
        Assert.Equal(1, result.Collected);
    }

    [Fact]
    public async Task ProgressObserverFailure_DoesNotUndoCollection()
    {
        var items = new[]
        {
            new DesktopAutoOrganizeItem(
                "safe.txt",
                @"C:\Desktop\safe.txt",
                "Document")
        };
        var progress =
            new InlineProgress<DesktopOrganizeProgress>(
                _ => throw new InvalidOperationException(
                    "presentation failed"));

        DesktopOrganizeResult result =
            await DesktopAutoOrganizePolicy
                .ExecuteAsync(
                    items,
                    false,
                    (_, _, _) =>
                        Task.CompletedTask,
                    progress);

        Assert.Equal(1, result.Collected);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public void SelectCreatedItems_OnlyReturnsMatchingVisibleItems()
    {
        var items = new[]
        {
            new DesktopAutoOrganizeItem(
                "new.png",
                @"C:\Desktop\new.png",
                "Image"),
            new DesktopAutoOrganizeItem(
                "old.txt",
                @"C:\Desktop\old.txt",
                "Document"),
            new DesktopAutoOrganizeItem(
                "collected.pdf",
                @"C:\Desktop\collected.pdf",
                "Document",
                IsCollected: true),
            new DesktopAutoOrganizeItem(
                "missing.zip",
                @"C:\Desktop\missing.zip",
                "Archive",
                NeedsRecovery: true)
        };

        var selected =
            DesktopAutoOrganizePolicy.SelectCreatedItems(
                items,
                new[]
                {
                    @"c:\desktop\NEW.png",
                    @"C:\Desktop\collected.pdf",
                    @"C:\Desktop\missing.zip"
                });

        Assert.Equal(
            new[] { "new.png" },
            selected.Select(item => item.Name));
    }

    [Fact]
    public void SelectCreatedItems_IgnoresMalformedAndDuplicatePaths()
    {
        var items = new[]
        {
            new DesktopAutoOrganizeItem(
                "one.txt",
                @"C:\Desktop\one.txt",
                "Document"),
            new DesktopAutoOrganizeItem(
                "duplicate.txt",
                @"c:\desktop\ONE.txt",
                "Document")
        };

        var selected =
            DesktopAutoOrganizePolicy.SelectCreatedItems(
                items,
                new[]
                {
                    "",
                    "\0invalid",
                    @"C:\Desktop\one.txt"
                });

        Assert.Single(selected);
        Assert.Equal("one.txt", selected[0].Name);
    }

    [Fact]
    public void SelectCreatedItems_NeverCollectsProtectedPanelLauncher()
    {
        var items = new[]
        {
            new DesktopAutoOrganizeItem(
                "FocusPanel.lnk",
                @"C:\Desktop\FocusPanel.lnk",
                "Application",
                IsProtectedPanelLauncher: true),
            new DesktopAutoOrganizeItem(
                "notes.txt",
                @"C:\Desktop\notes.txt",
                "Document")
        };

        IReadOnlyList<DesktopAutoOrganizeItem> selected =
            DesktopAutoOrganizePolicy
                .SelectCreatedItems(
                    items,
                    items.Select(item => item.FullPath));

        Assert.Single(selected);
        Assert.Equal("notes.txt", selected[0].Name);
    }

    [Theory]
    [InlineData(
        "FocusPanel.lnk",
        @"C:\Desktop\FocusPanel.lnk",
        @"D:\Apps\FocusPanel\FocusPanel.exe",
        true)]
    [InlineData(
        "FocusPanel.exe",
        @"D:\Apps\FocusPanel\FocusPanel.exe",
        @"d:\apps\focuspanel\FocusPanel.exe",
        true)]
    [InlineData(
        "another.lnk",
        @"C:\Desktop\another.lnk",
        @"D:\Apps\FocusPanel\FocusPanel.exe",
        false)]
    [InlineData(
        "Paint.NET.lnk",
        @"C:\Users\Public\Desktop\Paint.NET.lnk",
        @"D:\Apps\FocusPanel\FocusPanel.exe",
        false)]
    public void ProtectedLauncher_UsesOfficialNameOrExactExecutablePath(
        string name,
        string fullPath,
        string processPath,
        bool expected)
    {
        Assert.Equal(
            expected,
            DesktopAutoOrganizePolicy
                .IsProtectedPanelLauncher(
                    name,
                    fullPath,
                    processPath));
    }

    [Theory]
    [InlineData(1, 1, 0, 0, "已自动收纳 1 个新增项目")]
    [InlineData(
        3,
        1,
        1,
        1,
        "已收纳 1 个；1 个公共桌面项目待授权收纳；1 个暂时失败")]
    [InlineData(0, 0, 0, 0, "")]
    public void AutomaticResult_ProducesActionableStatus(
        int attempted,
        int collected,
        int authorizationRequired,
        int failed,
        string expected)
    {
        var result = new DesktopOrganizeResult(
            attempted,
            collected,
            authorizationRequired,
            failed,
            Array.Empty<string>());

        Assert.Equal(
            expected,
            DesktopAutoOrganizePolicy
                .DescribeAutomaticResult(result));
    }

    private sealed class InlineProgress<T>
        : IProgress<T>
    {
        private readonly Action<T> _report;

        internal InlineProgress(
            Action<T> report)
        {
            _report = report;
        }

        public void Report(T value) =>
            _report(value);
    }
}
