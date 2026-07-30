using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class SystemRadioUiContractTests
{
    [Fact]
    public void StatusCenter_ContainsDirectRadioControls()
    {
        string xaml =
            ReadRepositoryFile(
                "Views",
                "MainWindow.xaml");

        Assert.Contains(
            "Content=\"{Binding WiFiRadioActionText}\"",
            xaml);
        Assert.Contains(
            "Command=\"{Binding ToggleWiFiRadioCommand}\"",
            xaml);
        Assert.Contains(
            "Content=\"{Binding BluetoothRadioActionText}\"",
            xaml);
        Assert.Contains(
            "Command=\"{Binding ToggleBluetoothRadioCommand}\"",
            xaml);
        Assert.Contains(
            "Text=\"{Binding RadioStatusText}\"",
            xaml);
    }

    [Fact]
    public void RadioOperations_AreObservedAndDrained()
    {
        string viewModel =
            ReadRepositoryFile(
                "ViewModels",
                "MainViewModel.cs");

        Assert.Contains(
            "_radioOperations = new();",
            viewModel);
        Assert.Contains(
            "_radioOperations.TryStart(",
            viewModel);
        Assert.Contains(
            "_radioOperations.CompleteAsync()",
            viewModel);
        Assert.Contains(
            "_radios.SetEnabledAsync(",
            viewModel);
    }

    private static string ReadRepositoryFile(
        params string[] segments)
    {
        DirectoryInfo? directory =
            new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(
                    Path.Combine(
                        directory.FullName,
                        "FocusPanel.csproj")))
            {
                string path = directory.FullName;
                foreach (string segment in segments)
                    path = Path.Combine(path, segment);
                return File.ReadAllText(path);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException();
    }
}
