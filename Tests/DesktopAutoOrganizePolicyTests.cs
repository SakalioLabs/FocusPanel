using System;
using System.Collections.Generic;
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
        Assert.Contains("photo.png:图片", collected);
        Assert.Contains("folder:文件夹", collected);
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

    [Theory]
    [InlineData(1, 1, 0, 0, "已自动收纳 1 个新增项目")]
    [InlineData(
        3,
        1,
        1,
        1,
        "已收纳 1 个；1 个公共桌面项目需手动授权；1 个暂时失败")]
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
}
