using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WifiNetworkUiContractTests
{
    [Fact]
    public void StatusCenter_HasInlineWifiChooser()
    {
        string xaml =
            ReadRepositoryFile(
                "Views",
                "MainWindow.xaml");

        Assert.Contains(
            "ItemsSource=\"{Binding WifiNetworks}\"",
            xaml);
        Assert.Contains(
            "Command=\"{Binding RefreshWifiNetworksCommand}\"",
            xaml);
        Assert.Contains(
            "ConnectWifiNetworkCommand",
            xaml);
        Assert.Contains(
            "ForgetWifiNetworkCommand",
            xaml);
        Assert.Contains(
            "Content=\"忘记\"",
            xaml);
        Assert.Contains(
            "Text=\"{Binding SignalText}\"",
            xaml);
        Assert.Contains(
            "Text=\"{Binding SecurityText}\"",
            xaml);
        Assert.Contains(
            "AutomationProperties.Name=\"附近 Wi-Fi 网络\"",
            xaml);
        Assert.Contains(
            "Command=\"{Binding OpenWifiLocationSettingsCommand}\"",
            xaml);
        Assert.Contains(
            "WifiCredentialWindow",
            ReadRepositoryFile(
                "Views",
                "MainWindow.xaml.cs"));
        Assert.Contains(
            "PasswordBox x:Name=\"PasswordInput\"",
            ReadRepositoryFile(
                "Views",
                "WifiCredentialWindow.xaml"));
        Assert.Contains(
            "ConnectWithCredentialsAsync",
            ReadRepositoryFile(
                "Services",
                "WifiNetworkService.cs"));
        Assert.Contains(
            "WlanDisconnect",
            ReadRepositoryFile(
                "Services",
                "WifiNetworkService.cs"));
        Assert.Contains(
            "WlanGetProfileList",
            ReadRepositoryFile(
                "Services",
                "WifiNetworkService.cs"));
        Assert.Contains(
            "IsSavedOutOfRange",
            ReadRepositoryFile(
                "Services",
                "WifiNetworkService.cs"));
    }

    [Fact]
    public void WifiChooser_UsesReplaceableObservedBoundary()
    {
        string viewModel =
            ReadRepositoryFile(
                "ViewModels",
                "MainViewModel.cs");
        string coordinator =
            ReadRepositoryFile(
                "Services",
                "ShellCoordinator.cs");

        Assert.Contains(
            "IWifiNetworkService",
            viewModel);
        Assert.Contains(
            "_wifiNetworkOperations.TryStart(",
            viewModel);
        Assert.Contains(
            "_wifiNetworkOperations",
            viewModel);
        Assert.Contains(
            ".CompleteAsync()",
            viewModel);
        Assert.Contains(
            "new WifiNetworkService()",
            coordinator);
    }

    [Fact]
    public void AutomatedTests_DoNotCreateNativeWifiBoundary()
    {
        string tests =
            ReadRepositoryFile(
                "Tests",
                "WifiNetworkServiceTests.cs");

        Assert.Contains(
            "FakeWifiNetworkNativeApi",
            tests);
        Assert.DoesNotContain(
            "new NativeWifiNetworkApi",
            tests);
        Assert.DoesNotContain(
            "WlanScan(",
            tests);
        Assert.DoesNotContain(
            "WlanConnect(",
            tests);
        Assert.DoesNotContain(
            "WlanDisconnect(",
            tests);
        Assert.DoesNotContain(
            "WlanDeleteProfile(",
            tests);
        Assert.DoesNotContain(
            "WlanGetProfileList(",
            tests);
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
