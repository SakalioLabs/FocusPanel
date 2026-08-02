using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PartitionSwitchRecoveryContractTests
{
    [Fact]
    public void PartitionMenu_DefersMutationBeyondMenuClickStack()
    {
        string root = FindRepositoryRoot();
        string code = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "FileOrganizerView.xaml.cs"));

        Assert.Contains(
            "private static void MoveToPartition_Click(",
            code);
        Assert.Contains(
            "private static void CollectToPartition_Click(",
            code);
        Assert.DoesNotContain(
            "private static async void MoveToPartition_Click(",
            code);
        Assert.Contains(
            "DispatcherPriority.Background",
            code);
        Assert.Contains(
            "AsyncInteractionRunner.Start(",
            code);
        Assert.Contains(
            "ReportPartitionActionFailure",
            code);
    }

    [Fact]
    public void FatalExit_RestoresManagedItemsAndKeepsRecoveryMarker()
    {
        string root = FindRepositoryRoot();
        string app = File.ReadAllText(
            Path.Combine(root, "App.xaml.cs"));

        Assert.Contains(
            "_desktopCrashRecovery.RestoreIfRequested(",
            app);
        Assert.Equal(
            2,
            app.Split(
                    "RestoreCollectedItems();",
                    StringSplitOptions.None)
                .Length - 1);
        Assert.Contains(
            "if (!_fatalShutdown)",
            app);
        Assert.Contains(
            "_desktopCrashRecovery.Disarm();",
            app);
        Assert.Contains(
            "TaskbarController.HasOrphanedSession()",
            app);
    }

    [Fact]
    public void Organizer_ExposesExplicitEmergencyIconRecovery()
    {
        string root = FindRepositoryRoot();
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "FileOrganizerViewModel.cs"));
        string view = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "FileOrganizerView.xaml"));

        Assert.Contains(
            "private async Task RestoreAllCollectedDesktopItems()",
            viewModel);
        Assert.Contains(
            "recovery.RestoreCollectedItems",
            viewModel);
        Assert.Contains("recovery.Arm();", viewModel);
        Assert.Contains(
            "RestoreAllCollectedDesktopItemsCommand",
            view);
        Assert.Contains(
            "&#x7D27;&#x6025;&#x6062;&#x590D;&#x5168;&#x90E8;&#x56FE;&#x6807;",
            view);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory =
            new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "FocusPanel.csproj")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "未找到 FocusPanel 仓库根目录。");
    }
}
