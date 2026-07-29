using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace FocusPanel.Tests;

public sealed class FileOrganizerLifecycleContractTests
{
    [Fact]
    public void RefreshPaths_ShareOneGateWithoutRecursiveAcquisition()
    {
        string service = ReadService();

        Assert.Contains(
            "public async Task RefreshFiles()",
            service);
        Assert.Contains(
            "await _refreshGate.WaitAsync()",
            service);
        Assert.Contains(
            "await RefreshFilesCore()",
            service);

        Match processor = Regex.Match(
            service,
            @"private async Task ProcessPendingChangesAsync\(\)(?<body>[\s\S]*?)private async Task RefreshChangedPaths");
        Assert.True(processor.Success);
        Assert.Contains(
            "RefreshFilesCore()",
            processor.Groups["body"].Value);
        Assert.DoesNotContain(
            "await RefreshFiles();",
            processor.Groups["body"].Value);
    }

    [Fact]
    public void LateRefreshCallbacks_AreIgnoredAfterDispose()
    {
        string service = ReadService();

        Match refreshCore = Regex.Match(
            service,
            @"private async Task RefreshFilesCore\(\)(?<body>[\s\S]*?)private IEnumerable<string> GetDesktopRoots");
        Assert.True(refreshCore.Success);
        Assert.True(
            Regex.Matches(
                refreshCore.Groups["body"].Value,
                @"if \(_disposed\)")
            .Count >= 2);
        Assert.Contains(
            "dispatcher.HasShutdownStarted",
            service);
        Assert.Contains(
            "dispatcher.HasShutdownFinished",
            service);
        Assert.Contains(
            "if (!_disposed)",
            service);
    }

    [Fact]
    public void QueuedAutoOrganize_IsRejectedAfterShutdown()
    {
        string service = ReadService();

        Match organize = Regex.Match(
            service,
            @"public async Task<DesktopOrganizeResult> OrganizeFiles\((?<body>[\s\S]*?)public void Dispose\(\)");
        Assert.True(organize.Success);
        string body = organize.Groups["body"].Value;
        int waitIndex = body.IndexOf(
            "await _organizeGate.WaitAsync();",
            StringComparison.Ordinal);
        int disposedIndex = body.IndexOf(
            "if (_disposed)",
            StringComparison.Ordinal);
        int executeIndex = body.IndexOf(
            "DesktopAutoOrganizePolicy.ExecuteAsync(",
            StringComparison.Ordinal);

        Assert.True(waitIndex >= 0);
        Assert.True(disposedIndex > waitIndex);
        Assert.True(executeIndex > disposedIndex);
        Assert.Contains(
            "Array.Empty<string>()",
            body);
    }

    [Fact]
    public void AttributeMutations_ShareOneLifecycleAwareGate()
    {
        string service = ReadService();

        Assert.Contains(
            "SemaphoreSlim _visibilityGate",
            service);
        Assert.Contains(
            "HideFileFromDesktopPathCore(",
            service);
        Assert.Contains(
            "RestoreFileToDesktopCore(",
            service);
        Assert.Equal(
            2,
            Regex.Matches(
                service,
                @"await RunVisibilityMutationAsync\(")
            .Count);

        Match mutation = Regex.Match(
            service,
            @"private async Task RunVisibilityMutationAsync\((?<body>[\s\S]*?)// 分区只属于");
        Assert.True(mutation.Success);
        string body = mutation.Groups["body"].Value;
        Assert.Contains(
            "await _visibilityGate.WaitAsync();",
            body);
        Assert.Contains(
            "if (_disposed)",
            body);
        Assert.Contains(
            "throw new ObjectDisposedException(",
            body);
        Assert.Contains(
            "_visibilityGate.Release();",
            body);
        Assert.DoesNotContain(
            "Application.Current.Dispatcher",
            service);
        Assert.Matches(
            @"_visibilityIo\s*\.ReadAttributesAsync\(",
            service);
        Assert.True(
            Regex.Matches(
                service,
                @"_visibilityIo[\s\S]*?\.ApplyAttributesAsync\(")
            .Count >= 2);
        Assert.True(
            Regex.Matches(
                service,
                @"await InvokeOnUiAsync\(\(\) =>")
            .Count >= 4);
    }

    [Fact]
    public void ExternalDrop_PreflightsPathsOffUiBeforeCollecting()
    {
        string root =
            FindRepositoryRoot();
        string viewModel =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "ViewModels",
                    "FileOrganizerViewModel.cs"));
        Match import = Regex.Match(
            viewModel,
            @"public async Task<DesktopImportResult> ImportFiles\((?<body>[\s\S]*?)public void Dispose\(\)");
        Assert.True(import.Success);
        string body =
            import.Groups["body"].Value;

        Assert.Contains(
            "_desktopDropPreflight",
            body);
        Assert.Contains(
            ".ResolveAsync(",
            body);
        Assert.DoesNotContain(
            "System.IO.File.Exists(",
            body);
        Assert.DoesNotContain(
            "System.IO.Directory.Exists(",
            body);
        Assert.DoesNotContain(
            "System.IO.Path.GetFullPath(",
            body);
        Assert.Contains(
            "MissingOrInvalid",
            viewModel);
        Assert.Contains(
            "SkippedDuplicates",
            viewModel);
    }

    [Fact]
    public void LayoutRefreshAndSettingsSave_AreBackgroundAndLifecycleAware()
    {
        string root = FindRepositoryRoot();
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "FileOrganizerViewModel.cs"));
        string repository = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "OrganizerLayoutRepository.cs"));

        Assert.Contains(
            "CoalescingBackgroundRefresh",
            viewModel);
        Assert.Contains(
            "CoalescingAsyncSaveQueue",
            viewModel);
        Assert.Contains(
            "OrganizerLayoutComposer.Compose",
            viewModel);
        Assert.Contains(
            "_layoutRefresh.Dispose()",
            viewModel);
        Assert.Contains(
            "_layoutSaveQueue.CompleteAsync()",
            viewModel);
        Assert.DoesNotContain(
            "BuildPartitions",
            viewModel);
        Assert.DoesNotContain(
            "Dispatcher.Invoke(",
            viewModel);
        Assert.Contains(
            "SemaphoreSlim _gate",
            repository);
        Assert.Contains(
            "_gate.Wait()",
            repository);
    }

    [Fact]
    public void OrganizeCallbacks_AreIsolatedAndDrainedBeforeServiceDispose()
    {
        string root = FindRepositoryRoot();
        string service = ReadService();
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "FileOrganizerViewModel.cs"));

        Assert.Contains(
            "NotifyFilesChanged()",
            service);
        Assert.DoesNotContain(
            "FilesChanged?.Invoke()",
            service);
        Assert.Contains(
            "SafeDispatcherProgress<",
            viewModel);
        Assert.Contains(
            "_organizeOperationTracker.TryStart(",
            viewModel);
        Assert.Contains(
            "_organizeOperationTracker",
            viewModel);

        int drainIndex = viewModel.IndexOf(
            "_organizeOperationTracker",
            viewModel.IndexOf(
                "private async Task CompleteDisposeAsync()",
                StringComparison.Ordinal),
            StringComparison.Ordinal);
        int disposeIndex = viewModel.IndexOf(
            "_fileService.Dispose();",
            drainIndex,
            StringComparison.Ordinal);
        Assert.True(drainIndex >= 0);
        Assert.True(disposeIndex > drainIndex);
    }

    [Fact]
    public void PartitionMutations_UseSharedBackgroundRepositoryAndDragLease()
    {
        string root = FindRepositoryRoot();
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "FileOrganizerViewModel.cs"));
        string repository = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "OrganizerLayoutRepository.cs"));
        string viewCode = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "FileOrganizerView.xaml.cs"));

        Assert.DoesNotContain(
            "new AppDbContext",
            viewModel);
        Assert.Contains(
            "RunLayoutMutationAsync",
            viewModel);
        Assert.Contains(
            "Task.Run(mutation)",
            viewModel);
        Assert.Contains(
            "_layoutMutationTracker.TryStart",
            viewModel);
        Assert.Contains(
            "_layoutMutationTracker.CompleteAsync()",
            viewModel);
        Assert.Contains(
            "_layoutRepository.ReorderPartition",
            viewModel);
        Assert.Contains(
            ".MovePartitionToColumn(",
            viewModel);
        Assert.Contains(
            ".AssignFileToPartition(",
            viewModel);
        Assert.Contains(
            "CreatePartitionCore",
            repository);
        Assert.Contains(
            "RenamePartitionCore",
            repository);
        Assert.Contains(
            "DeletePartitionCore",
            repository);
        Assert.Contains(
            "OrganizerPartitionOrdering.Reorder",
            repository);
        Assert.True(
            Regex.Matches(
                viewCode,
                @"StartPartitionDrop\(\s*\(\) => vm\.(ReorderPartition|MovePartitionToColumn)")
            .Count >= 3);
    }

    [Fact]
    public void ShellOpenActions_AreBackgroundConcurrentAndLatestRequestWins()
    {
        string root = FindRepositoryRoot();
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "FileOrganizerViewModel.cs"));
        string coordinator = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "ShellPathOpenCoordinator.cs"));

        Assert.Contains(
            "ShellPathOpenCoordinator",
            viewModel);
        Assert.Contains(
            "OpenShellPathAsync(",
            viewModel);
        Assert.True(
            Regex.Matches(
                viewModel,
                @"\[RelayCommand\(AllowConcurrentExecutions = true\)\]")
            .Count >= 2);
        Assert.DoesNotContain(
            "Process.Start",
            viewModel);
        Assert.DoesNotContain(
            "explorer.exe",
            viewModel);
        Assert.Contains(
            "Task.Run(",
            coordinator);
        Assert.Contains(
            "AppLaunchExecution.TryStart",
            coordinator);
        Assert.Contains(
            "IsCurrent(",
            coordinator);
    }

    private static string ReadService()
    {
        string root = FindRepositoryRoot();
        return File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "FileOrganizerService.cs"));
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
            "Could not locate FocusPanel.csproj.");
    }
}
