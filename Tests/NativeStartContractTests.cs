using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class NativeStartContractTests
{
    [Fact]
    public void StartButton_UsesOnlyPanelLauncherAndUnifiedSearch()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string shell = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));
        string viewModel = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));
        string statusContract = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "ISystemStatusService.cs"));
        string shortcutMap = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "WindowsShellShortcut.cs"));

        Assert.Contains(
            "Click=\"StartButton_Click\"",
            xaml);
        Assert.Contains(
            "按 Enter 打开 Panel 全部应用",
            xaml);
        Assert.Contains(
            "Header=\"Panel 统一搜索\"",
            xaml);
        Assert.Contains(
            "InputGestureText=\"Shift+单击\"",
            xaml);
        Assert.Contains(
            "PreviewTextInput=\"Window_PreviewTextInput\"",
            xaml);
        Assert.Contains(
            "x:Name=\"CompactDock\"",
            xaml);
        Assert.Contains(
            "IsApplicationLauncherOpen",
            xaml);
        Assert.Contains(
            "IsUnifiedSearchEntryActive",
            xaml);
        Assert.Contains(
            "开始 · 全部应用",
            viewModel);
        Assert.Contains(
            "SearchPlaceholder",
            viewModel);

        int start = shell.IndexOf(
            "private void StartButton_Click(",
            StringComparison.Ordinal);
        int end = shell.IndexOf(
            "private void CloseCurrentVirtualDesktop_Click(",
            start,
            StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        string handler = shell[start..end];
        Assert.Contains(
            "PanelStartEntryPolicy.Decide(",
            handler);
        Assert.Contains(
            "PrepareApplicationLauncher()",
            handler);
        Assert.Contains(
            "PrepareUnifiedSearch(",
            handler);
        Assert.Contains(
            "CloseOverlayPanels();",
            handler);
        Assert.Contains(
            "QueueOverlayFocus(",
            handler);
        Assert.Contains(
            "OpenUnifiedSearchMenuItem_Click",
            xaml);
        Assert.DoesNotContain(
            "OpenStartMenu",
            xaml);
        Assert.DoesNotContain(
            "OpenStartMenu",
            viewModel);
        Assert.DoesNotContain(
            "OpenStartMenu",
            statusContract);
        Assert.DoesNotContain(
            "WindowsShellAction.StartMenu",
            shortcutMap);
        Assert.DoesNotContain(
            "WindowsShellAction.Search",
            shortcutMap);
        Assert.Contains(
            "CompactTypeToSearchPolicy",
            shell);
        Assert.DoesNotContain(
            "ScheduleAutoHide",
            handler);
        Assert.DoesNotContain(
            "OpenWindowsStartMenu",
            handler);
    }

    [Fact]
    public void StartContextMenu_KeepsWinXAndVirtualDesktopActions()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string shell = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));

        foreach (string tool in new[]
                 {
                     "InstalledApps",
                     "PowerOptions",
                     "EventViewer",
                     "SystemAbout",
                     "DeviceManager",
                     "NetworkConnections",
                     "DiskManagement",
                     "ComputerManagement",
                     "Terminal",
                     "TerminalAdministrator",
                     "TaskManager",
                     "Settings",
                     "FileExplorer"
                 })
        {
            Assert.Contains(
                $"SystemManagementTool.{tool}",
                xaml);
        }

        Assert.Contains(
            "Header=\"虚拟桌面\"",
            xaml);
        Assert.Contains(
            "VirtualDesktopDirection.Previous",
            xaml);
        Assert.Contains(
            "VirtualDesktopDirection.Next",
            xaml);
        Assert.Contains(
            "CreateVirtualDesktopCommand",
            xaml);
        Assert.Contains(
            "Click=\"CloseCurrentVirtualDesktop_Click\"",
            xaml);
        Assert.Contains(
            "其中的应用不会被关闭",
            shell);
        Assert.Contains(
            "MessageBoxButton.YesNo",
            shell);
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
