using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
}
