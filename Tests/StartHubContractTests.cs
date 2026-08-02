using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class StartHubContractTests
{
    [Fact]
    public void StartHub_ProvidesPinnedAppsAndCoreSystemEntries()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml"));

        Assert.Contains(
            "Visibility=\"{Binding IsStartHubOpen",
            xaml);
        Assert.Contains(
            "ItemsSource=\"{Binding StartHubApps}\"",
            xaml);
        Assert.Contains(
            "Click=\"StartHubAllApps_Click\"",
            xaml);
        Assert.Contains(
            "Click=\"StartHubApp_Click\"",
            xaml);
        Assert.Contains(
            "SystemManagementTool.FileExplorer",
            xaml);
        Assert.Contains(
            "SystemManagementTool.Settings",
            xaml);
        Assert.Contains(
            "SystemManagementTool.Terminal",
            xaml);
        Assert.Contains(
            "SystemManagementTool.TaskManager",
            xaml);
        Assert.Contains(
            "Command=\"{Binding SwitchVirtualDesktopCommand}\"",
            xaml);
        Assert.Contains(
            "VirtualDesktopDirection.Previous",
            xaml);
        Assert.Contains(
            "VirtualDesktopDirection.Next",
            xaml);
        Assert.Contains(
            "Command=\"{Binding CreateVirtualDesktopCommand}\"",
            xaml);
        Assert.Contains(
            "Click=\"CloseCurrentVirtualDesktop_Click\"",
            xaml);
        Assert.Contains(
            "Content=\"上一个\"",
            xaml);
        Assert.Contains(
            "Content=\"新建\"",
            xaml);
        Assert.Contains(
            "Content=\"下一个\"",
            xaml);
        Assert.Contains(
            "Content=\"关闭\"",
            xaml);
        Assert.Contains(
            "固定应用、虚拟桌面与常用系统入口",
            xaml);
        Assert.Contains(
            "Header=\"Windows 开始菜单\"",
            xaml);
    }

    [Fact]
    public void StartHub_IsMutuallyExclusiveAndSecondClickKeepsPanelOpen()
    {
        string root = FindRepositoryRoot();
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));
        string shell = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));

        Assert.Contains(
            "private void ToggleStartHub()",
            viewModel);
        Assert.Contains(
            "CloseTransientPanels();",
            viewModel);
        Assert.Contains(
            "IsStartHubOpen = false;",
            viewModel);
        Assert.Contains(
            "TaskbarAppCollectionSynchronizer.Synchronize(",
            viewModel);
        Assert.Contains(
            "StartHubApps,",
            viewModel);
        Assert.Contains(
            "ToggleCompactOverlay(",
            shell);
        Assert.Contains(
            "StartEntryPolicy.FromLeftClick(",
            shell);
        Assert.Contains(
            "if (_viewModel.IsStartHubOpen",
            shell);
        Assert.DoesNotContain(
            "CollapseSidebar();\n        _viewModel.ToggleStartHubCommand",
            shell);
        Assert.Contains(
            "_viewModel.CloseCurrentVirtualDesktopCommand",
            shell);
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
