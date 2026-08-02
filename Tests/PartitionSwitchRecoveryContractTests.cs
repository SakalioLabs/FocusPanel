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
            "if (!_fatalShutdown",
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

    [Fact]
    public void CollectedCardPartitionSwitch_UsesMetadataOnly()
    {
        string root = FindRepositoryRoot();
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "FileOrganizerViewModel.cs"));

        int guard = viewModel.IndexOf(
            "if (file.IsHidden)",
            StringComparison.Ordinal);
        int metadataMutation = viewModel.IndexOf(
            "await AssignFileToPartition(",
            guard,
            StringComparison.Ordinal);
        int visibilityMutation = viewModel.IndexOf(
            "await _fileService.HideFileFromDesktop(",
            guard,
            StringComparison.Ordinal);

        Assert.True(guard >= 0);
        Assert.True(metadataMutation > guard);
        Assert.True(visibilityMutation > metadataMutation);
    }

    [Fact]
    public void StartupRecovery_IsAutomaticAndNonBlocking()
    {
        string root = FindRepositoryRoot();
        string app = File.ReadAllText(
            Path.Combine(root, "App.xaml.cs"));
        string shell = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));

        Assert.Contains(
            "RestoreKnownCrashResidueOnce(",
            app);
        Assert.Contains(
            "mainWindow.ShowDesktopRecoveryNotice(",
            app);
        Assert.DoesNotContain(
            "FocusPanel 桌面恢复",
            app);
        Assert.Contains(
            "桌面图标已自动恢复",
            shell);
        Assert.Contains(
            "FocusToastNotification(",
            shell);
    }

    [Fact]
    public void WatchdogProcess_DoesNotDiscardFailedDesktopRecoveryMarker()
    {
        string root = FindRepositoryRoot();
        string app = File.ReadAllText(
            Path.Combine(root, "App.xaml.cs"));
        string watchdog = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "TaskbarWatchdog.cs"));

        Assert.Contains(
            "_isWatchdogProcess = true;",
            app);
        Assert.Contains(
            "&& !_isWatchdogProcess",
            app);
        Assert.Equal(
            2,
            watchdog.Split(
                    "keepDesktopRecoveryArmed: false",
                    StringSplitOptions.None)
                .Length - 1);
        Assert.Equal(
            1,
            watchdog.Split(
                    "keepDesktopRecoveryArmed: true",
                    StringSplitOptions.None)
                .Length - 1);
        Assert.Contains(
            "RestoreIfRequested(",
            watchdog);
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
