using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class NativeStartContractTests
{
    [Fact]
    public void StartButton_UsesPanelLauncherAndKeepsWindowsFallback()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string shell = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));
        string viewModel = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));
        string systemStatus = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "SystemStatusService.cs"));

        Assert.Contains(
            "Click=\"StartButton_Click\"",
            xaml);
        Assert.Contains(
            "按 Enter 打开 Panel 全部应用",
            xaml);
        Assert.Contains(
            "Header=\"Windows 开始菜单\"",
            xaml);
        Assert.Contains(
            "InputGestureText=\"Shift+单击\"",
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
            "private async void StartButton_Click(",
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
            "CloseOverlayPanels();",
            handler);
        Assert.Contains(
            "QueueOverlayFocus(",
            handler);
        Assert.Contains(
            "_viewModel.OpenStartMenuCommand",
            handler);
        Assert.DoesNotContain(
            "ScheduleAutoHide",
            handler);
        Assert.Contains(
            "WindowsShellAction.StartMenu",
            systemStatus);
        Assert.DoesNotContain(
            "Shell_TrayWnd",
            systemStatus);
        Assert.DoesNotContain(
            "ScTaskList",
            systemStatus);
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
