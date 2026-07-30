using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    ApplicationAudioUiContractTests
{
    [Fact]
    public void StatusCenter_HasDirectPerApplicationMixer()
    {
        string xaml =
            ReadRepositoryFile(
                "Views",
                "MainWindow.xaml");

        Assert.Contains(
            "ItemsSource=\"{Binding ApplicationAudioSessions}\"",
            xaml);
        Assert.Contains(
            "Value=\"{Binding Volume, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            xaml);
        Assert.Contains(
            "ToggleApplicationAudioMuteCommand",
            xaml);
        Assert.Contains(
            "AutomationProperties.Name=\"应用音量混音器\"",
            xaml);
    }

    [Fact]
    public void ApplicationMixer_UsesObservedBackgroundBoundary()
    {
        string viewModel =
            ReadRepositoryFile(
                "ViewModels",
                "MainViewModel.cs");

        Assert.Contains(
            "ApplicationAudioControlCoordinator",
            viewModel);
        Assert.Contains(
            "_applicationAudio.GetSessions()",
            viewModel);
        Assert.Contains(
            "_applicationAudioWritePending",
            viewModel);
        Assert.Contains(
            "RequestSystemStatusRefresh();",
            viewModel);
    }

    private static string ReadRepositoryFile(
        params string[] segments)
    {
        DirectoryInfo? directory =
            new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string project =
                Path.Combine(
                    directory.FullName,
                    "FocusPanel.csproj");
            if (File.Exists(project))
            {
                string path =
                    directory.FullName;
                foreach (string segment
                         in segments)
                {
                    path =
                        Path.Combine(path, segment);
                }

                return File.ReadAllText(path);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException();
    }
}
