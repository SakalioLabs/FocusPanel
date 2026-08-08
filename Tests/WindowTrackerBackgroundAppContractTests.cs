using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowTrackerBackgroundAppContractTests
{
    [Fact]
    public void Tracker_ReusesTopLevelEventSnapshotWithoutProcessPolling()
    {
        string root = FindRepositoryRoot();
        string tracker = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "WindowTracker.cs"));

        Assert.Contains(
            "CaptureBackgroundOwner(",
            tracker);
        Assert.Contains(
            "BackgroundAppVisibilityPolicy",
            tracker);
        Assert.Contains(
            "BackgroundAppSnapshotComposer.Append(",
            tracker);
        Assert.DoesNotContain(
            "Process.GetProcesses()",
            tracker);
        Assert.DoesNotContain(
            "Thread.Sleep(",
            tracker);
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(
                    Path.Combine(
                        current,
                        "FocusPanel.csproj")))
            {
                return current;
            }
            current = Directory.GetParent(current)
                ?.FullName;
        }
        throw new DirectoryNotFoundException(
            "未找到 FocusPanel 仓库根目录。");
    }
}
