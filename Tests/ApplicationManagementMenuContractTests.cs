using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    ApplicationManagementMenuContractTests
{
    [Fact]
    public void TaskbarAndStartResultsShareLocationActions()
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

        Assert.Contains(
            "PopulateSearchApplicationContextMenu(",
            shell);
        Assert.Contains(
            "AppLocationPolicy.TryResolve(",
            shell);
        Assert.Contains(
            "OpenTaskbarAppLocationCommand",
            shell);
        Assert.Contains(
            "OpenSearchApplicationLocationCommand",
            shell);
        Assert.Contains(
            "LaunchElevatedSearchApplicationCommand",
            shell);
        Assert.Contains(
            "private async Task OpenTaskbarAppLocation(",
            viewModel);
        Assert.Contains(
            "OpenSearchApplicationLocation(",
            viewModel);
        Assert.Contains(
            "private async Task OpenAppLocationAsync(",
            viewModel);
    }

    [Fact]
    public void ApplicationResultsAndWindowsKeepDistinctMenus()
    {
        string shell = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "Views",
                "MainWindow.xaml.cs"));

        Assert.Contains(
            "if (result.Window != null)",
            shell);
        Assert.Contains(
            "PopulateSearchWindowContextMenu(",
            shell);
        Assert.Contains(
            "PopulateSearchApplicationContextMenu(",
            shell);
        Assert.Contains(
            "result.Window == null\n"
            + "            && result.Application == null",
            shell.Replace("\r\n", "\n"));
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
