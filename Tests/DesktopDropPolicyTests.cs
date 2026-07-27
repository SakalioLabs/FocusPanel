using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DesktopDropPolicyTests
{
    [Theory]
    [InlineData(@"C:\Users\Test\Desktop\report.docx", true)]
    [InlineData(@"c:\users\test\desktop\REPORT.DOCX", true)]
    [InlineData(@"C:\Users\Test\Desktop\Folder\report.docx", false)]
    [InlineData(@"C:\Users\Test\Downloads\report.docx", false)]
    [InlineData("", false)]
    public void OnlyDesktopRootItemsCanBeCollectedWithoutMoving(
        string path,
        bool expected)
    {
        Assert.Equal(
            expected,
            DesktopDropPolicy.IsDesktopRootItem(path, @"C:\Users\Test\Desktop"));
    }

    [Theory]
    [InlineData(@"C:\Users\Test\Desktop\report.docx", DesktopDropLocation.UserDesktop)]
    [InlineData(@"C:\Users\Public\Desktop\Browser.lnk", DesktopDropLocation.CommonDesktop)]
    [InlineData(@"C:\Users\Test\Desktop\Folder\report.docx", DesktopDropLocation.OutsideDesktop)]
    [InlineData(@"C:\Users\Test\Downloads\report.docx", DesktopDropLocation.OutsideDesktop)]
    public void ClassifiesMergedWindowsDesktopRoots(
        string path,
        DesktopDropLocation expected)
    {
        Assert.Equal(
            expected,
            DesktopDropPolicy.Classify(
                path,
                @"C:\Users\Test\Desktop",
                @"C:\Users\Public\Desktop"));
    }

    [Fact]
    public void ElevatedHelperRejectsPathsOutsideCommonDesktop()
    {
        int exitCode = DesktopVisibilityElevatedHelper.Run(new[]
        {
            DesktopVisibilityElevatedHelper.Command,
            @"C:\Windows\notepad.exe",
            "6"
        });

        Assert.Equal(4, exitCode);
    }
}
