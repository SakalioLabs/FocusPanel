using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AsyncShutdownContractTests
{
    [Fact]
    public void MainWindow_UsesTwoPhaseAsyncShutdown()
    {
        string root = FindRepositoryRoot();
        string window = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));

        Assert.Contains(
            "ShellShutdownPolicy.Decide",
            window);
        Assert.Contains(
            "e.Cancel = true;",
            window);
        Assert.Contains(
            "CompleteShutdownAsync",
            window);
        Assert.Contains(
            "_viewModel.DisposeAsync()",
            window);
        Assert.Contains(
            "_coordinator.DisposeAsync()",
            window);
        Assert.Contains(
            "_notificationCenter.CompleteAsync()",
            window);
        Assert.Contains(
            "await _notificationCenter.FlushAsync();",
            window);
        Assert.Contains(
            "_shutdownCompleted = true;",
            window);
    }

    [Fact]
    public void ViewModels_ExposeAsyncQueueDrainBeforeSyncCompatibilityWrapper()
    {
        string root = FindRepositoryRoot();
        string main = Read(
            root,
            "ViewModels",
            "MainViewModel.cs");
        string tasks = Read(
            root,
            "ViewModels",
            "TasksViewModel.cs");
        string organizer = Read(
            root,
            "ViewModels",
            "FileOrganizerViewModel.cs");
        string pomodoro = Read(
            root,
            "ViewModels",
            "PomodoroViewModel.cs");

        Assert.Contains(
            "internal Task DisposeAsync()",
            main);
        Assert.Contains(
            "await Task.WhenAll",
            main);
        Assert.DoesNotContain(
            "_autoStartup.CompleteAsync()\n            .GetAwaiter()",
            main);
        Assert.Contains(
            "internal Task DisposeAsync()",
            tasks);
        Assert.Contains(
            "private async Task CompleteDisposeAsync()",
            tasks);
        Assert.Contains(
            "internal Task DisposeAsync()",
            organizer);
        Assert.Contains(
            "private async Task CompleteDisposeAsync()",
            organizer);
        Assert.Contains(
            "internal Task DisposeAsync()",
            pomodoro);
    }

    [Fact]
    public void ShellCoordinator_AwaitsPinnedAppWrites()
    {
        string root = FindRepositoryRoot();
        string shell = Read(
            root,
            "Services",
            "ShellCoordinator.cs");
        string catalog = Read(
            root,
            "Services",
            "AppCatalogService.cs");

        Assert.Contains(
            "internal async Task DisposeAsync()",
            shell);
        Assert.Contains(
            "await catalog",
            shell);
        Assert.Contains(
            "internal Task DisposeAsync()",
            catalog);
        Assert.Contains(
            "await _pinnedWriteGate",
            catalog);
    }

    private static string Read(
        string root,
        params string[] parts)
    {
        string path = root;
        foreach (string part in parts)
            path = Path.Combine(path, part);
        return File.ReadAllText(path);
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
