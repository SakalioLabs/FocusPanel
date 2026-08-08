using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowFocusUiContractTests
{
    [Fact]
    public void SearchOverlay_ExposesRestorableFocusSession()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));

        Assert.Contains("HasWindowFocusSession", xaml);
        Assert.Contains("WindowFocusSessionSummary", xaml);
        Assert.Contains("RestoreWindowFocusSessionCommand", xaml);
        Assert.Contains("恢复窗口", xaml);
    }

    [Fact]
    public void TaskbarMenus_ExposeWindowAndApplicationFocusActions()
    {
        string root = FindRepositoryRoot();
        string code = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));

        Assert.Contains("专注此窗口（收起其他窗口）", code);
        Assert.Contains("专注此应用（收起其他窗口）", code);
        Assert.Contains("FocusWindowCommand", code);
        Assert.Contains("FocusTaskWindowsCommand", code);
        Assert.Contains("恢复专注前的窗口", code);
    }

    [Fact]
    public void ViewModel_RestoresFocusSessionDuringDisposal()
    {
        string root = FindRepositoryRoot();
        string code = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));

        int disposeStart = code.IndexOf(
            "private async Task DisposeCoreAsync()",
            StringComparison.Ordinal);
        Assert.True(disposeStart >= 0);
        string disposeBody = code[disposeStart..];
        Assert.Contains("_windowFocusSession.Restore(", disposeBody);
        Assert.Contains("_windowTracker.Maximize(handle)", disposeBody);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
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
            "未找到 FocusPanel 项目根目录。");
    }
}
