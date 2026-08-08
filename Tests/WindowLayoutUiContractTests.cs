using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowLayoutUiContractTests
{
    [Fact]
    public void EveryWindowMenuUsesPanelLayoutSubmenu()
    {
        string root = FindRepositoryRoot();
        string shell = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));
        string tracker = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "IWindowTracker.cs"));

        Assert.Contains(
            "Header = \"排列窗口\"",
            shell);
        Assert.Contains(
            "Enum.GetValues<\n                     WindowLayoutTarget>()",
            shell);
        Assert.Contains(
            "ArrangeWindowCommand",
            shell);
        Assert.True(
            Regex.Matches(
                    shell,
                    "AddWindowStateMenuItems\\(")
                .Count >= 3);
        Assert.Contains(
            "_windowTracker.Arrange(",
            viewModel);
        Assert.Contains(
            "bool Arrange(",
            tracker);
        Assert.DoesNotContain(
            "Win+Z",
            shell);
        Assert.DoesNotContain(
            "WindowsShellAction.Snap",
            shell);
    }

    [Fact]
    public void LayoutMenuContainsSixExplicitTargets()
    {
        string root = FindRepositoryRoot();
        string presentation = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "WindowLayoutPresentation.cs"));

        foreach (string label in new[]
                 {
                     "左半屏",
                     "右半屏",
                     "左上四分区",
                     "右上四分区",
                     "左下四分区",
                     "右下四分区"
                 })
        {
            Assert.Contains(label, presentation);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory =
            new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(
                    Path.Combine(
                        directory.FullName,
                        "FocusPanel.csproj")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException();
    }
}
