using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PanelRunContractTests
{
    [Fact]
    public void StartAndSearchUsePanelRunInsteadOfWinR()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml"));
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
        string statusContract =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Services",
                    "ISystemStatusService.cs"));
        string shortcuts = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "WindowsShellShortcut.cs"));
        string shellCatalog =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Services",
                    "WindowsShellSearchCatalog.cs"));

        Assert.Contains(
            "Header=\"Panel 运行…\"",
            xaml);
        Assert.Contains(
            "Click=\"OpenPanelRunMenuItem_Click\"",
            xaml);
        Assert.Contains(
            "Tag=\">\"",
            xaml);
        Assert.Contains(
            "PanelRunCommandParser.Prefix",
            shell);
        Assert.Contains(
            "SearchScope.System",
            shell);
        Assert.Contains(
            "result?.RunCommand",
            viewModel);
        Assert.Contains(
            "_panelRun.RunAsync(",
            viewModel);
        Assert.DoesNotContain(
            "OpenRunDialog",
            statusContract);
        Assert.DoesNotContain(
            "WindowsShellAction.RunDialog",
            shortcuts);
        Assert.DoesNotContain(
            "WindowsShellAction.RunDialog",
            shellCatalog);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current =
            new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(
                    Path.Combine(
                        current.FullName,
                        "FocusPanel.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "FocusPanel repository root was not found.");
    }
}
