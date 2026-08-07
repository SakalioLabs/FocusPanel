using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class VirtualDesktopPlacementContractTests
{
    [Fact]
    public void WindowOverview_ExposesDirectVirtualDesktopControls()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));

        Assert.Contains(
            "AutomationProperties.Name=\"虚拟桌面快捷操作\"",
            xaml);
        Assert.Contains(
            "Visibility=\"{Binding IsWindowSearchScope, Converter={StaticResource BooleanToVisibilityConverter}}\"",
            xaml);
        Assert.Contains(
            "Command=\"{Binding SwitchVirtualDesktopCommand}\"",
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
    }

    [Fact]
    public void Shell_RehomesItselfWhenTheCurrentDesktopChanges()
    {
        string root = FindRepositoryRoot();
        string shell = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));
        string coordinator = File.ReadAllText(
            Path.Combine(root, "Services", "ShellCoordinator.cs"));
        string service = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "VirtualDesktopPlacementService.cs"));

        Assert.Contains(
            "VirtualDesktops =",
            coordinator);
        Assert.Contains(
            "_coordinator.Windows.SnapshotChanged +=",
            shell);
        Assert.Contains(
            "_coordinator.Windows.SnapshotChanged -=",
            shell);
        Assert.Contains(
            "WindowTracker_SnapshotChanged",
            shell);
        Assert.Contains(
            "EnsureShellOnCurrentVirtualDesktop();",
            shell);
        Assert.Contains(
            "IVirtualDesktopManagerCom",
            service);
        Assert.Contains(
            "MoveWindowToDesktop(",
            service);
        Assert.DoesNotContain(
            "IVirtualDesktopManagerInternal",
            service);
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
