using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class BluetoothDeviceUiContractTests
{
    [Fact]
    public void StatusCenter_HasInlineBluetoothDeviceManager()
    {
        string xaml = File.ReadAllText(
            Path.Combine(ProjectRoot(), "Views", "MainWindow.xaml"));

        Assert.Contains("ItemsSource=\"{Binding BluetoothDevices}\"", xaml);
        Assert.Contains("RefreshBluetoothDevicesCommand", xaml);
        Assert.Contains("ManageBluetoothDeviceCommand", xaml);
        Assert.Contains("不打开任务栏蓝牙浮层", xaml);
    }

    [Fact]
    public void BluetoothManager_UsesReplaceableObservedBoundary()
    {
        string viewModel = File.ReadAllText(
            Path.Combine(ProjectRoot(), "ViewModels", "MainViewModel.cs"));
        string coordinator = File.ReadAllText(
            Path.Combine(ProjectRoot(), "Services", "ShellCoordinator.cs"));

        Assert.Contains("IBluetoothDeviceService", viewModel);
        Assert.Contains("_bluetoothDeviceOperations", viewModel);
        Assert.Contains("new BluetoothDeviceService()", coordinator);
        Assert.Contains("BluetoothDevices.Dispose()", coordinator);
    }

    [Fact]
    public void AutomatedTests_DoNotCreateNativeBluetoothBoundary()
    {
        string tests = File.ReadAllText(
            Path.Combine(ProjectRoot(), "Tests", "BluetoothDeviceServiceTests.cs"));

        Assert.Contains("FakeBluetoothNativeApi", tests);
        Assert.DoesNotContain("new WinRtBluetoothDeviceNativeApi", tests);
        Assert.DoesNotContain("DeviceInformation.FindAllAsync", tests);
    }

    private static string ProjectRoot() =>
        Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", ".."));
}
