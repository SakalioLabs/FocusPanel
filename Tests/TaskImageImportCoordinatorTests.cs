using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskImageImportCoordinatorTests
{
    [Fact]
    public async Task Import_RunsOffCallingThreadWithoutBlocking()
    {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        int callingThread =
            Environment.CurrentManagedThreadId;
        int importThread =
            callingThread;
        var coordinator =
            new TaskImageImportCoordinator(
                (source, destination) =>
                {
                    importThread =
                        Environment
                            .CurrentManagedThreadId;
                    started.Set();
                    release.Wait(
                        TimeSpan.FromSeconds(5));
                    return destination
                        + "\\saved.png";
                });

        Task<TaskImageImportResult> operation =
            coordinator.ImportAsync(
                @"C:\Source\large.png",
                @"D:\TaskImages");

        Assert.True(
            started.Wait(
                TimeSpan.FromSeconds(2)));
        Assert.False(operation.IsCompleted);
        Assert.NotEqual(
            callingThread,
            importThread);

        release.Set();
        Assert.True(
            (await operation).Succeeded);
    }

    [Fact]
    public async Task Import_UsesTrimmedDetachedPaths()
    {
        string? observedSource = null;
        string? observedDestination = null;
        var coordinator =
            new TaskImageImportCoordinator(
                (source, destination) =>
                {
                    observedSource = source;
                    observedDestination =
                        destination;
                    return destination
                        + "\\saved.jpg";
                });

        TaskImageImportResult result =
            await coordinator.ImportAsync(
                "  C:\\Source\\图片.jpg  ",
                "  D:\\TaskImages  ");

        Assert.True(result.Succeeded);
        Assert.Equal(
            @"C:\Source\图片.jpg",
            observedSource);
        Assert.Equal(
            @"D:\TaskImages",
            observedDestination);
    }

    [Theory]
    [InlineData("", @"D:\TaskImages", "没有可导入")]
    [InlineData(@"C:\Source\a.png", "", "选择图片保存位置")]
    public async Task InvalidInput_FailsWithoutCallingStorage(
        string source,
        string destination,
        string expectedError)
    {
        int calls = 0;
        var coordinator =
            new TaskImageImportCoordinator(
                (_, _) =>
                {
                    calls++;
                    return "unexpected";
                });

        TaskImageImportResult result =
            await coordinator.ImportAsync(
                source,
                destination);

        Assert.False(result.Succeeded);
        Assert.Contains(
            expectedError,
            result.Error);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task StorageExceptionBecomesRecoverableFailure()
    {
        var coordinator =
            new TaskImageImportCoordinator(
                (_, _) =>
                    throw new InvalidOperationException(
                        "cloud file unavailable"));

        TaskImageImportResult result =
            await coordinator.ImportAsync(
                @"C:\Source\cloud.png",
                @"D:\TaskImages");

        Assert.False(result.Succeeded);
        Assert.Empty(result.SavedPath);
        Assert.Equal(
            "cloud file unavailable",
            result.Error);
    }

    [Fact]
    public async Task ConcurrentImports_KeepBothResults()
    {
        using var firstStarted =
            new ManualResetEventSlim();
        using var releaseFirst =
            new ManualResetEventSlim();
        var coordinator =
            new TaskImageImportCoordinator(
                (source, destination) =>
                {
                    if (source.EndsWith(
                            "first.png",
                            StringComparison.Ordinal))
                    {
                        firstStarted.Set();
                        releaseFirst.Wait(
                            TimeSpan.FromSeconds(5));
                    }

                    return destination
                        + "\\"
                        + source[
                            (source.LastIndexOf(
                                '\\') + 1)..];
                });

        Task<TaskImageImportResult> first =
            coordinator.ImportAsync(
                @"C:\Source\first.png",
                @"D:\TaskImages");
        Assert.True(
            firstStarted.Wait(
                TimeSpan.FromSeconds(2)));
        TaskImageImportResult second =
            await coordinator.ImportAsync(
                @"C:\Source\second.png",
                @"D:\TaskImages");

        releaseFirst.Set();
        TaskImageImportResult firstResult =
            await first;

        Assert.True(firstResult.Succeeded);
        Assert.True(second.Succeeded);
        Assert.EndsWith(
            "first.png",
            firstResult.SavedPath,
            StringComparison.Ordinal);
        Assert.EndsWith(
            "second.png",
            second.SavedPath,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DefaultImport_CreatesFolderAndPreservesSource()
    {
        string testRoot =
            Path.Combine(
                Path.GetTempPath(),
                "FocusPanel.Tests",
                Guid.NewGuid().ToString("N"));
        string sourceDirectory =
            Path.Combine(
                testRoot,
                "source");
        string destinationDirectory =
            Path.Combine(
                testRoot,
                "destination");
        Directory.CreateDirectory(
            sourceDirectory);
        string sourcePath =
            Path.Combine(
                sourceDirectory,
                "sample.png");
        byte[] expected =
        {
            0x89,
            0x50,
            0x4E,
            0x47
        };
        await File.WriteAllBytesAsync(
            sourcePath,
            expected);

        try
        {
            var coordinator =
                new TaskImageImportCoordinator();

            TaskImageImportResult result =
                await coordinator.ImportAsync(
                    sourcePath,
                    destinationDirectory);

            Assert.True(result.Succeeded);
            Assert.True(File.Exists(sourcePath));
            Assert.True(
                File.Exists(result.SavedPath));
            Assert.StartsWith(
                destinationDirectory,
                result.SavedPath,
                StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(
                "_sample.png",
                result.SavedPath,
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                expected,
                await File.ReadAllBytesAsync(
                    result.SavedPath));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(
                    testRoot,
                    recursive: true);
            }
        }
    }
}
