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
        Assert.True(
            Regex.Matches(
                service,
                @"await InvokeOnUiAsync\(\(\) =>")
            .Count >= 4);
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
