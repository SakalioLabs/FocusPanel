using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class RecentApplicationLauncherContractTests
{
    [Fact]
    public void SuccessfulLaunch_PersistsHistoryAndEmptyLauncherUsesIt()
    {
        string root = FindRepositoryRoot();
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));
        string preferences = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "ShellPreferenceRepository.cs"));

        Assert.Contains(
            "RecordSuccessfulAppLaunch(app);",
            viewModel);
        Assert.Contains(
            "case ElevatedAppLaunchStatus.Started:",
            viewModel);
        Assert.Contains(
            "RecordSuccessfulAppLaunch(launch);",
            viewModel);
        Assert.Contains(
            "RecentAppHistoryPolicy\n"
            + "                    .OrderForLauncher(",
            viewModel.Replace("\r\n", "\n"));
        Assert.Contains(
            "string.IsNullOrWhiteSpace(\n"
            + "                SearchQuery)",
            viewModel.Replace("\r\n", "\n"));
        Assert.Contains(
            "Shell.RecentAppHistory",
            preferences);
        Assert.Contains(
            "_recentAppHistoryRevision",
            viewModel);
        Assert.Contains(
            "Interlocked.Increment(",
            viewModel);
        Assert.Contains(
            "固定应用优先，其后按最近启动排列",
            viewModel);
    }

    [Fact]
    public void Project_DoesNotReferenceLegacyUiAutomationAssemblies()
    {
        string project = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "FocusPanel.csproj"));

        Assert.DoesNotContain(
            "UIAutomationClient",
            project);
        Assert.DoesNotContain(
            "UIAutomationTypes",
            project);
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
