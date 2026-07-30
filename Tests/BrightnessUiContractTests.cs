using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class BrightnessUiContractTests
{
    [Fact]
    public void StatusCenter_ContainsDirectBrightnessControl()
    {
        string root =
            FindRepositoryRoot();
        string xaml =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Views",
                    "MainWindow.xaml"));

        Assert.Contains(
            "Value=\"{Binding BrightnessPercent, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            xaml);
        Assert.Contains(
            "IsEnabled=\"{Binding IsBrightnessAvailable}\"",
            xaml);
        Assert.Contains(
            "Text=\"{Binding BrightnessStatusText}\"",
            xaml);
        Assert.DoesNotContain(
            "ManagementObjectSearcher",
            xaml);
    }

    [Fact]
    public void ViewModel_UsesBackgroundCoalescingBoundary()
    {
        string root =
            FindRepositoryRoot();
        string viewModel =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "ViewModels",
                    "MainViewModel.cs"));

        Assert.Contains(
            "BrightnessControlCoordinator",
            viewModel);
        Assert.Contains(
            "_brightnessControl.Queue(",
            viewModel);
        Assert.Contains(
            "ExecuteBrightnessSearchCommand(",
            viewModel);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory =
            new(
                AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(
                    Path.Combine(
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
