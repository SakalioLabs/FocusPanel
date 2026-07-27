using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace FocusPanel.Tests;

public sealed class XamlResourceContractTests
{
    private static readonly Regex ResourceKeyPattern = new(
        """x:Key\s*=\s*["']([^"']+)["']""",
        RegexOptions.Compiled);

    private static readonly Regex ResourceReferencePattern = new(
        """\{(?:StaticResource|DynamicResource)\s+([^},\s]+)""",
        RegexOptions.Compiled);

    private static readonly Regex CodeResourceReferencePattern = new(
        """(?:FindResource|TryFindResource)\(\s*"([^"]+)"\s*\)""",
        RegexOptions.Compiled);

    [Fact]
    public void EveryViewResourceReference_IsDefinedGloballyOrLocally()
    {
        string root = FindRepositoryRoot();
        var globalKeys = ReadDefinedKeys(
            Path.Combine(root, "App.xaml"),
            Path.Combine(root, "Themes", "FocusTheme.xaml"));
        var failures = new List<string>();

        foreach (string xamlPath in Directory.GetFiles(
                     Path.Combine(root, "Views"),
                     "*.xaml",
                     SearchOption.TopDirectoryOnly))
        {
            string xaml = File.ReadAllText(xamlPath);
            var availableKeys = new HashSet<string>(globalKeys, StringComparer.Ordinal);
            availableKeys.UnionWith(ReadDefinedKeys(xamlPath));

            foreach (Match match in ResourceReferencePattern.Matches(xaml))
            {
                string key = match.Groups[1].Value;
                if (!availableKeys.Contains(key))
                    failures.Add($"{Path.GetFileName(xamlPath)} -> {key}");
            }
        }

        Assert.True(
            failures.Count == 0,
            "发现未定义的 XAML 资源："
            + Environment.NewLine
            + string.Join(Environment.NewLine, failures.Distinct().Order()));
    }

    [Fact]
    public void EveryCodeBehindResourceLookup_IsDefinedGloballyOrInItsView()
    {
        string root = FindRepositoryRoot();
        var globalKeys = ReadDefinedKeys(
            Path.Combine(root, "App.xaml"),
            Path.Combine(root, "Themes", "FocusTheme.xaml"));
        var failures = new List<string>();

        foreach (string codePath in Directory.GetFiles(
                     Path.Combine(root, "Views"),
                     "*.xaml.cs",
                     SearchOption.TopDirectoryOnly))
        {
            var availableKeys = new HashSet<string>(globalKeys, StringComparer.Ordinal);
            string xamlPath = codePath[..^3];
            if (File.Exists(xamlPath))
                availableKeys.UnionWith(ReadDefinedKeys(xamlPath));

            string code = File.ReadAllText(codePath);
            foreach (Match match in CodeResourceReferencePattern.Matches(code))
            {
                string key = match.Groups[1].Value;
                if (!availableKeys.Contains(key))
                    failures.Add($"{Path.GetFileName(codePath)} -> {key}");
            }
        }

        Assert.True(
            failures.Count == 0,
            "发现未定义的代码资源查找："
            + Environment.NewLine
            + string.Join(Environment.NewLine, failures.Distinct().Order()));
    }

    [Fact]
    public void MainShell_HasOnlyOneTopLevelRoundedOutline()
    {
        string root = FindRepositoryRoot();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument mainWindow = XDocument.Load(Path.Combine(root, "Views", "MainWindow.xaml"));
        XDocument theme = XDocument.Load(Path.Combine(root, "Themes", "FocusTheme.xaml"));

        XElement shell = mainWindow
            .Descendants(presentation + "Border")
            .Single(element => (string?)element.Attribute(x + "Name") == "ShellBorder");
        XElement workspace = mainWindow
            .Descendants(presentation + "Border")
            .Single(element => (string?)element.Attribute(x + "Name") == "WorkspaceSurface");

        Assert.Equal("0", (string?)shell.Attribute("CornerRadius"));
        Assert.Equal("0", (string?)workspace.Attribute("CornerRadius"));
        Assert.Equal("Transparent", (string?)workspace.Attribute("Background"));

        bool hasImplicitBorderStyle = theme
            .Descendants(presentation + "Style")
            .Any(element =>
                (string?)element.Attribute("TargetType") == "Border"
                && element.Attribute(x + "Key") == null);
        Assert.False(hasImplicitBorderStyle);

        string codeBehind = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));
        Assert.DoesNotContain("SetWindowRgn", codeBehind);
        Assert.DoesNotContain("CreateRoundRectRgn", codeBehind);
        Assert.Contains("DwmcpRound", codeBehind);
        Assert.Contains("DwmsbtTransientWindow", codeBehind);
    }

    [Fact]
    public void TextBoxTemplate_PreservesVerticalContentAndPadding()
    {
        string root = FindRepositoryRoot();
        string themePath = Path.Combine(root, "Themes", "FocusTheme.xaml");
        string theme = File.ReadAllText(themePath);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement textBoxStyle = XDocument.Load(themePath)
            .Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key") == "FocusSearchBox");
        var setters = textBoxStyle.Elements(presentation + "Setter").ToList();

        Assert.Contains(setters, setter =>
            (string?)setter.Attribute("Property") == "MinHeight"
            && (string?)setter.Attribute("Value") == "44");
        Assert.DoesNotContain(setters, setter =>
            (string?)setter.Attribute("Property") == "Height");
        Assert.Contains("Padding=\"{TemplateBinding Padding}\"", theme);
        Assert.Contains(
            "VerticalAlignment=\"{TemplateBinding VerticalContentAlignment}\"",
            theme);
    }

    [Fact]
    public void StatusCenter_ExposesSupportedSystemControls()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(Path.Combine(root, "Views", "MainWindow.xaml"));

        Assert.Contains("OpenQuickSettingsCommand", mainWindow);
        Assert.Contains("OpenNotificationsCommand", mainWindow);
        Assert.Contains("OpenInputSwitcherCommand", mainWindow);
        Assert.Contains("Value=\"{Binding MasterVolume, Mode=TwoWay", mainWindow);
        Assert.Contains("ToggleMuteCommand", mainWindow);
        Assert.Contains("NetworkDetail", mainWindow);
        Assert.Contains("LockComputerCommand", mainWindow);
        Assert.Contains("SleepComputerCommand", mainWindow);
        Assert.Contains("ShowDesktopCommand", mainWindow);
        Assert.Contains("Visibility=\"{Binding IsCalendarOpen", mainWindow);
        Assert.Contains("Visibility=\"{Binding IsStatusCenterOpen", mainWindow);
        Assert.Contains("Visibility=\"{Binding IsFocusCenterOpen", mainWindow);
        Assert.Contains("EnableReplacementCommand", mainWindow);
        Assert.DoesNotContain("OpenNotificationOverflow", mainWindow);

        string systemStatus = File.ReadAllText(
            Path.Combine(root, "Services", "SystemStatusService.cs"));
        Assert.DoesNotContain("ms-settings:network-status", systemStatus);
        Assert.DoesNotContain("ms-settings:notifications", systemStatus);
        Assert.DoesNotContain("ms-settings:typing", systemStatus);
    }

    [Fact]
    public void CompactDock_HasExactlySixFixedEntriesAndOneApplicationList()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(Path.Combine(root, "Views", "MainWindow.xaml"));
        int dockStart = mainWindow.IndexOf("<!-- Compact app dock -->", StringComparison.Ordinal);
        int onboardingStart = mainWindow.IndexOf(
            "<!-- First-run safety onboarding -->",
            StringComparison.Ordinal);

        Assert.True(dockStart >= 0 && onboardingStart > dockStart);
        string compactDock = mainWindow[dockStart..onboardingStart];
        Assert.Equal(6, compactDock.Split("Tag=\"CompactFixedEntry\"").Length - 1);
        Assert.Equal(1, compactDock.Split("ItemsSource=\"{Binding TaskbarApps}\"").Length - 1);
        Assert.Contains("Click=\"FocusCenterButton_Click\"", compactDock);
        Assert.Contains("Click=\"StatusCenterButton_Click\"", compactDock);
        Assert.DoesNotContain("OpenNotificationOverflow", compactDock);
        Assert.DoesNotContain("OpenInputSwitcherCommand", compactDock);
        Assert.DoesNotContain("OpenNotificationsCommand", compactDock);
        Assert.DoesNotContain("ToggleSettingsCommand", compactDock);
        Assert.DoesNotContain("BatteryPercent", compactDock);

        string systemStatus = File.ReadAllText(
            Path.Combine(root, "Services", "SystemStatusService.cs"));
        string statusContract = File.ReadAllText(
            Path.Combine(root, "Services", "ISystemStatusService.cs"));
        Assert.DoesNotContain("OpenNotificationOverflow", systemStatus);
        Assert.DoesNotContain("OpenNotificationOverflow", statusContract);
        Assert.DoesNotContain("System.Windows.Automation", systemStatus);

        string onboarding = mainWindow[onboardingStart..];
        Assert.Contains("<Border Grid.Column=\"0\"", onboarding);
        Assert.DoesNotContain("Grid.ColumnSpan=\"2\"", onboarding);
    }

    [Fact]
    public void CompactDock_ExposesNativeWindowsAndMultiWindowActions()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));
        string viewModel = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));

        Assert.Contains("OpenStartMenuCommand", mainWindow);
        Assert.Contains("OpenWindowsSearchCommand", mainWindow);
        Assert.Contains("OpenTaskViewCommand", mainWindow);
        Assert.Contains("OpenWidgetsCommand", mainWindow);
        Assert.Contains("OpenRunDialogCommand", mainWindow);
        Assert.Contains("SystemManagementTool.InstalledApps", mainWindow);
        Assert.Contains("SystemManagementTool.PowerOptions", mainWindow);
        Assert.Contains("SystemManagementTool.EventViewer", mainWindow);
        Assert.Contains("SystemManagementTool.DeviceManager", mainWindow);
        Assert.Contains("SystemManagementTool.NetworkConnections", mainWindow);
        Assert.Contains("SystemManagementTool.DiskManagement", mainWindow);
        Assert.Contains("SystemManagementTool.ComputerManagement", mainWindow);
        Assert.Contains("SystemManagementTool.TerminalAdministrator", mainWindow);
        Assert.Contains("SystemManagementTool.TaskManager", mainWindow);
        Assert.Contains("TaskbarApp_Click", mainWindow);
        Assert.Contains("PopulateTaskbarAppContextMenu", codeBehind);
        Assert.Contains("CloseWindowCommand", codeBehind);
        Assert.Contains("CloseTaskCommand", codeBehind);
        Assert.Contains("VolumeButton_PreviewMouseWheel", mainWindow);
        Assert.Contains("VolumeButton_MouseRightButtonUp", mainWindow);
        Assert.Contains("foreach (WindowReference window in task.Windows)", viewModel);
    }

    [Fact]
    public void CompactDock_UsesOneUnifiedApplicationCollection()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string viewModel = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));

        Assert.True(
            Regex.Matches(mainWindow, "ItemsSource=\"\\{Binding TaskbarApps\\}\"").Count == 1);
        Assert.DoesNotContain("ItemsSource=\"{Binding PinnedApps}\"", mainWindow);
        Assert.DoesNotContain("ItemsSource=\"{Binding RunningApps}\"", mainWindow);
        Assert.Contains("ObservableCollection<TaskbarAppItem> TaskbarApps", viewModel);
        Assert.DoesNotContain("ObservableCollection<AppLaunchItem> PinnedApps", viewModel);
        Assert.DoesNotContain("ObservableCollection<WindowTaskItem> RunningApps", viewModel);
        Assert.Contains("_appCatalog.SetPinned(launch, true)", viewModel);
        Assert.Contains("_appCatalog.MovePinned(launch", viewModel);
    }

    [Fact]
    public void WindowClosing_UsesWmCloseWithoutProcessTermination()
    {
        string root = FindRepositoryRoot();
        string windowTracker = File.ReadAllText(
            Path.Combine(root, "Services", "WindowTracker.cs"));

        Assert.Contains("PostMessage(handle, WmClose", windowTracker);
        Assert.DoesNotContain(".Kill(", windowTracker);
    }

    [Fact]
    public void Shell_UsesVerifiedNativeAcrylicWithoutOpaqueRootCover()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));
        string theme = File.ReadAllText(
            Path.Combine(root, "Themes", "FocusTheme.xaml"));

        Assert.Contains(
            "Background=\"{DynamicResource FocusShellTintBrush}\"",
            mainWindow);
        Assert.Contains("DwmsbtTransientWindow", codeBehind);
        Assert.Contains("backdropResult == 0", codeBehind);
        Assert.Contains(
            "ThemeService.SetNativeBackdropActive(backdropActive)",
            codeBehind);
        Assert.Contains("FocusShellTintBrush", theme);
    }

    [Fact]
    public void Organizer_UsesOneSurfaceHierarchyAndExposesAutoOrganize()
    {
        string root = FindRepositoryRoot();
        string organizer = File.ReadAllText(
            Path.Combine(root, "Views", "FileOrganizerView.xaml"));

        Assert.Contains("<Grid Background=\"Transparent\">", organizer);
        Assert.Contains("IsAutoOrganizeEnabled", organizer);
        Assert.DoesNotContain("DropShadowEffect", organizer);
        Assert.DoesNotContain("OrganizerCardShadow", organizer);
        Assert.DoesNotContain("ToggleDesktopCommand", organizer);
    }

    [Fact]
    public void TaskbarExclusiveMode_HidesOnceAndGuardRemainsReadOnly()
    {
        string root = FindRepositoryRoot();
        string controller = File.ReadAllText(
            Path.Combine(root, "Services", "TaskbarController.cs"));
        string onboarding = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));

        Assert.Contains("AbsAutoHide", controller);
        Assert.Contains("UsesNativeAutoHide = true", controller);
        Assert.Contains("SetTaskbarVisible(taskbar, false)", controller);
        Assert.Contains("ValidateReplacement()", controller);
        Assert.DoesNotContain(
            "_native.SetWorkArea(_state.PrimaryBounds)",
            controller);
        Assert.DoesNotContain("ApplyReplacement();", controller[
            controller.IndexOf("private void GuardReplacementSafely", StringComparison.Ordinal)..]);
        Assert.Contains("守护器只验证状态，不反复改写工作区", onboarding);
    }

    [Fact]
    public void OrganizerAutoScroll_StopsOnDropLeaveAndUnload()
    {
        string root = FindRepositoryRoot();
        string organizer = File.ReadAllText(
            Path.Combine(root, "Views", "FileOrganizerView.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(root, "Views", "FileOrganizerView.xaml.cs"));

        Assert.Contains("DragLeave=\"UserControl_DragLeave\"", organizer);
        Assert.Contains("Unloaded=\"UserControl_Unloaded\"", organizer);
        Assert.Contains("private void StopAutoScroll()", codeBehind);
        Assert.Contains("private async void Partition_Drop", codeBehind);
        Assert.Contains("private void Column_Drop", codeBehind);
        Assert.True(
            codeBehind.Split("StopAutoScroll();", StringSplitOptions.None).Length >= 7);
    }

    private static HashSet<string> ReadDefinedKeys(params string[] paths)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in paths)
        {
            string content = File.ReadAllText(path);
            foreach (Match match in ResourceKeyPattern.Matches(content))
                keys.Add(match.Groups[1].Value);
        }

        return keys;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FocusPanel.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("未找到 FocusPanel 项目根目录。");
    }
}
