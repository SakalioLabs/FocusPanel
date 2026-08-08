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

    [Fact]
    public void GlobalShortcut_IsRegisteredHandledReportedAndUnregistered()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string code = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));
        string viewModel = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));

        Assert.Contains("WindowFocusHotkeyId", code);
        Assert.Contains("RegisterWindowFocus", code);
        Assert.Contains("ToggleWindowFocusFromHotkey();", code);
        Assert.Contains(
            "UnregisterHotKey(\n                hwnd,\n                WindowFocusHotkeyId)",
            code.Replace("\r\n", "\n"));
        Assert.Contains("WindowFocusShortcutText", xaml);
        Assert.Contains("SetWindowFocusShortcutStatus", viewModel);
        Assert.Contains("GetWindowFocusShortcutTarget()", viewModel);
        Assert.DoesNotContain("_viewModel.TaskbarApps", code);
        Assert.DoesNotContain("Activate();\n        ToggleWindowFocusFromHotkey", code);
    }

    [Fact]
    public void CompactDock_ShowsTransientRestoreWithoutGrowingMinimumHeight()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string viewModel = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));

        Assert.Contains(
            "Text=\"恢复\"",
            xaml);
        Assert.Contains(
            "Text=\"{Binding WindowFocusHiddenWindowCountText}\"",
            xaml);
        Assert.Contains(
            "AutomationProperties.HelpText=\"单击恢复本次窗口专注收起的窗口；也可再次按窗口专注全局快捷键\"",
            xaml);
        Assert.True(
            CountOccurrences(
                xaml,
                "Command=\"{Binding RestoreWindowFocusSessionCommand}\"")
            >= 2);
        Assert.Contains(
            "<Setter Property=\"MinHeight\" Value=\"98\"/>",
            xaml);
        Assert.Contains(
            "<Setter Property=\"MinHeight\" Value=\"52\"/>",
            xaml);
        Assert.Contains(
            "WindowFocusHiddenWindowCountText =",
            viewModel);
        Assert.Contains(
            "_windowFocusSession.HiddenWindowCount",
            viewModel);
        Assert.Contains(
            "? \"99+\"",
            viewModel);
    }

    private static int CountOccurrences(
        string source,
        string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(
                   value,
                   index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
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
