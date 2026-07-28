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
    public void AiAssistant_IsChineseFluentAndPrivacyExplicit()
    {
        string root = FindRepositoryRoot();
        string view = File.ReadAllText(
            Path.Combine(root, "Views", "AIAssistantView.xaml"));
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "AIAssistantViewModel.cs"));
        string mainViewModel = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));

        Assert.Contains("AI 助手", view);
        Assert.Contains("FocusCard", view);
        Assert.Contains("OpenAI API 配置", view);
        Assert.Contains("不读取文件内容", view);
        Assert.Contains("IncludeLocalContext", viewModel);
        Assert.Contains("StopCommand", view);
        Assert.Contains(
            "_aiAssistantViewModel?.Dispose()",
            mainViewModel);
        Assert.DoesNotContain("MaterialDesign", view);
        Assert.DoesNotContain(
            "Text=\"AI Assistant\"",
            view);
    }

    [Fact]
    public void Dashboard_IsReachableActionableAndNotPlaceholder()
    {
        string root = FindRepositoryRoot();
        string view = File.ReadAllText(
            Path.Combine(root, "Views", "DashboardView.xaml"));
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string mainViewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));

        Assert.Contains("今日概览", mainWindow);
        Assert.Contains(
            "CommandParameter=\"Dashboard\"",
            mainWindow);
        Assert.Contains(
            "DataType=\"{x:Type vm:DashboardViewModel}\"",
            mainWindow);
        Assert.Contains(
            "case \"Dashboard\":",
            mainViewModel);
        Assert.Contains("PriorityTasks", view);
        Assert.Contains("ActiveObjectives", view);
        Assert.Contains("FocusLinearProgress", view);
        Assert.Contains("CommandParameter=\"Pomodoro\"", view);
        Assert.Contains("CommandParameter=\"AI\"", view);
        Assert.DoesNotContain(
            "Text=\"Dashboard\"",
            view);
        Assert.DoesNotContain("MaterialDesign", view);
    }

    [Fact]
    public void MainWindow_TimeEntryContainsNavigableMonthCalendar()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml"));
        string calendar = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "CalendarPanelView.xaml"));

        Assert.Contains(
            "<local:CalendarPanelView/>",
            mainWindow);
        Assert.Contains(
            "ItemsSource=\"{Binding CalendarDays}\"",
            calendar);
        Assert.Contains(
            "ShowPreviousCalendarMonthCommand",
            calendar);
        Assert.Contains(
            "ShowNextCalendarMonthCommand",
            calendar);
        Assert.Contains(
            "ShowTodayInCalendarCommand",
            calendar);
        Assert.Contains(
            "SelectCalendarDateCommand",
            calendar);
        Assert.Contains(
            "SelectedDayFocusSummary",
            calendar);
        Assert.Contains(
            "<UniformGrid Columns=\"7\"",
            calendar);
        Assert.Contains(
            "<UniformGrid Columns=\"2\"",
            calendar);
    }

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
        string backdropService = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "WindowBackdropService.cs"));
        Assert.DoesNotContain("SetWindowRgn", codeBehind);
        Assert.DoesNotContain("CreateRoundRectRgn", codeBehind);
        Assert.Contains(
            "WindowBackdropService.Apply",
            codeBehind);
        Assert.DoesNotContain(
            "SetWindowRgn",
            backdropService);
        Assert.DoesNotContain(
            "CreateRoundRectRgn",
            backdropService);
        Assert.Contains(
            "DwmcpRound",
            backdropService);
        Assert.Contains(
            "DwmsbtTransientWindow",
            backdropService);
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
    public void Settings_UsesZeroConfigurationGitHubUpdates()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(Path.Combine(root, "Views", "MainWindow.xaml"));
        string viewModel = File.ReadAllText(Path.Combine(root, "ViewModels", "MainViewModel.cs"));
        string windowCode = File.ReadAllText(Path.Combine(root, "Views", "MainWindow.xaml.cs"));

        Assert.Contains("GitHub Releases · 自动检查", mainWindow);
        Assert.Contains("CheckAndInstallUpdateCommand", mainWindow);
        Assert.DoesNotContain("LanUpdateSource", mainWindow);
        Assert.DoesNotContain("SaveUpdateSourceCommand", mainWindow);
        Assert.DoesNotContain("Update.SourceMode", viewModel);
        Assert.DoesNotContain("Update.LanLocation", viewModel);
        Assert.Contains("TimeSpan.FromHours(6)", viewModel);
        Assert.Contains("CheckForUpdatesInBackgroundAsync", windowCode);
        Assert.Contains("ShowBalloonTip", windowCode);
    }

    [Fact]
    public void UpdateAvailability_IsVisibleFromCompactAndFocusCenters()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string viewModel = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));

        Assert.True(
            mainWindow.Split(
                "Visibility=\"{Binding IsUpdateAvailable",
                StringSplitOptions.None).Length - 1 >= 2);
        Assert.Contains(
            "Text=\"{Binding AvailableUpdateVersion, StringFormat=可更新到 v{0}}\"",
            mainWindow);
        Assert.Contains("打开设置一键安装", mainWindow);
        Assert.Contains("ApplyUpdateAvailability(update)", viewModel);
        Assert.Contains("ApplyUpdateAvailability(null)", viewModel);
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
    public void CompactDock_ShowsNavigationWhenApplicationListOverflows()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"TaskbarAppsScrollViewer\"", mainWindow);
        Assert.Contains("x:Name=\"TaskbarScrollUpButton\"", mainWindow);
        Assert.Contains("x:Name=\"TaskbarScrollDownButton\"", mainWindow);
        Assert.Contains(
            "ScrollChanged=\"TaskbarAppsScrollViewer_ScrollChanged\"",
            mainWindow);
        Assert.Contains(
            "CompactTaskbarScrollPolicy.GetState",
            codeBehind);
        Assert.Contains(
            "TaskbarAppsScrollViewer.ScrollToVerticalOffset",
            codeBehind);
    }

    [Fact]
    public void CompactDock_SupportsKeyboardSummonAndAccessibleNavigation()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));

        Assert.Contains(
            "KeyboardNavigation.TabNavigation=\"Cycle\"",
            mainWindow);
        Assert.Contains(
            "KeyboardNavigation.DirectionalNavigation=\"Cycle\"",
            mainWindow);
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding DisplayName}\"",
            mainWindow);
        Assert.Contains(
            "AutomationProperties.HelpText=\"{Binding WindowSummary}\"",
            mainWindow);
        Assert.Contains(
            "AutomationProperties.Name=\"开始\"",
            mainWindow);
        Assert.Contains("private void FocusCompactDock()", codeBehind);
        Assert.True(
            codeBehind.Split(
                "FocusCompactDock();",
                StringSplitOptions.None).Length - 1 >= 2);
        Assert.Contains("SearchButton.Focus();", codeBehind);
    }

    [Fact]
    public void FluentControls_UseOneRoundedKeyboardFocusVisual()
    {
        string root = FindRepositoryRoot();
        string theme = File.ReadAllText(
            Path.Combine(root, "Themes", "FocusTheme.xaml"));
        string themeService = File.ReadAllText(
            Path.Combine(root, "Services", "ThemeService.cs"));

        Assert.Contains(
            "x:Key=\"FocusRoundedKeyboardVisual\"",
            theme);
        Assert.Contains(
            "CornerRadius=\"{StaticResource FocusControlCornerRadius}\"",
            theme);
        Assert.Contains(
            "BorderBrush=\"{DynamicResource FocusKeyboardFocusBrush}\"",
            theme);
        Assert.True(
            Regex.Matches(
                theme,
                "FocusVisualStyle\"\\s+Value=\"\\{StaticResource FocusRoundedKeyboardVisual\\}\"")
                .Count >= 5);
        Assert.DoesNotContain(
            "IsKeyboardFocused",
            theme);
        Assert.Contains(
            "SystemParameters.HighContrast",
            themeService);
        Assert.Contains(
            "SystemColors.HighlightColor",
            themeService);
    }

    [Fact]
    public void StartupSetting_ReportsFailureAndRollsBackUiState()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string viewModel = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));

        Assert.Contains(
            "Text=\"{Binding StartupStatus}\"",
            mainWindow);
        Assert.Contains(
            "AutoStartupService.TrySetStartup",
            viewModel);
        Assert.Contains(
            "AutoStartupService.IsStartupEnabled()",
            viewModel);
        Assert.Contains(
            "_updatingStartupState",
            viewModel);
        Assert.DoesNotContain(
            "AutoStartupService.SetStartup",
            viewModel);
    }

    [Fact]
    public void DatabaseRestore_UsesExitHandoffAndValidatedBackups()
    {
        string root = FindRepositoryRoot();
        string program = File.ReadAllText(
            Path.Combine(root, "Program.cs"));
        string app = File.ReadAllText(
            Path.Combine(root, "App.xaml.cs"));
        string viewModel = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));
        string backupService = File.ReadAllText(
            Path.Combine(root, "Services", "DatabaseBackupService.cs"));

        Assert.Contains("--restore-after-exit", program);
        Assert.Contains("--restore-after-exit", viewModel);
        Assert.Contains(
            "RestoreRestartCoordinator.Run",
            program);
        Assert.Contains(
            "RequestClose?.Invoke();",
            viewModel);
        Assert.Contains(
            "TryRestoreLatestBackup",
            app);
        Assert.Contains(
            "PRAGMA quick_check",
            backupService);
        Assert.Contains(
            "File.Move(",
            backupService);
        Assert.DoesNotContain(
            "ProcessStartInfo(executable, \"--restore\")",
            viewModel);
        Assert.DoesNotContain(
            "Application.Current.Shutdown();",
            viewModel);
    }

    [Fact]
    public void ShellAutoHide_WaitsForMenusPopupsAndMouseCapture()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string mainWindowCode = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));
        string organizer = File.ReadAllText(
            Path.Combine(root, "Views", "FileOrganizerView.xaml"));

        Assert.Equal(
            3,
            Regex.Matches(mainWindow, "Opened=\"TransientContextMenu_Opened\"").Count);
        Assert.Equal(
            3,
            Regex.Matches(mainWindow, "Closed=\"TransientContextMenu_Closed\"").Count);
        Assert.Contains("Mouse.Captured != null", mainWindowCode);
        Assert.Contains("_transientInteractionDepth > 0", mainWindowCode);
        Assert.Contains("Opened=\"TransientSurface_Opened\"", organizer);
        Assert.Contains("Closed=\"TransientSurface_Closed\"", organizer);
        Assert.Equal(
            4,
            Regex.Matches(organizer, "Opened=\"TransientPopup_Opened\"").Count);
        Assert.Equal(
            4,
            Regex.Matches(organizer, "Closed=\"TransientPopup_Closed\"").Count);
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
    public void SearchAndTaskbar_UseTheSameNonBlankIconPresenter()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string presenter = File.ReadAllText(
            Path.Combine(
                root,
                "Controls",
                "AppIconPresenter.xaml"));

        Assert.Equal(
            2,
            Regex.Matches(
                mainWindow,
                "<controls:AppIconPresenter").Count);
        Assert.DoesNotContain(
            "<Image Source=\"{Binding Icon}\"",
            mainWindow);
        Assert.Contains(
            "DisplayName=\"{Binding DisplayName}\"",
            mainWindow);
        Assert.Contains(
            "Value=\"{x:Null}\"",
            presenter);
        Assert.Contains(
            "FocusSurfaceSoftBrush",
            presenter);
        Assert.Contains(
            "FocusControlCornerRadius",
            presenter);
    }

    [Fact]
    public void AppSearch_ShowsBackgroundLoadingAndEmptyStates()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));
        string catalog = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "AppCatalogService.cs"));

        Assert.Contains(
            "x:Name=\"SearchResultsList\"",
            mainWindow);
        Assert.Contains(
            "Text=\"{Binding AppSearchStatusText}\"",
            mainWindow);
        Assert.Contains(
            "Visibility=\"{Binding IsAppSearchStatusVisible",
            mainWindow);
        Assert.Contains(
            "正在载入应用目录…",
            viewModel);
        Assert.Contains(
            "没有找到匹配的应用",
            viewModel);
        Assert.Contains(
            "Name = \"FocusPanel.AppCatalog\"",
            catalog);
        Assert.Contains(
            "Name = \"FocusPanel.AppIcons\"",
            catalog);
    }

    [Fact]
    public void DesktopOrganizer_RefreshesPartitionsWithoutClearingVisualTree()
    {
        string root = FindRepositoryRoot();
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "FileOrganizerViewModel.cs"));
        string synchronizer = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "PartitionCollectionSynchronizer.cs"));

        Assert.Contains(
            "PartitionCollectionSynchronizer.Synchronize",
            viewModel);
        Assert.DoesNotContain(
            "PartitionsCol1.Clear()",
            viewModel);
        Assert.DoesNotContain(
            "PartitionsCol2.Clear()",
            viewModel);
        Assert.DoesNotContain(
            "AllPartitions.Clear()",
            viewModel);
        Assert.Contains(
            "destination.Move(",
            synchronizer);
        Assert.Contains(
            "private bool isExpanded = true",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "ViewModels",
                    "PartitionViewModel.cs")));
        Assert.DoesNotContain(
            "current.IsExpanded =",
            synchronizer);
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
        string backdropService = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "WindowBackdropService.cs"));
        string theme = File.ReadAllText(
            Path.Combine(root, "Themes", "FocusTheme.xaml"));

        Assert.Contains(
            "Background=\"{DynamicResource FocusShellTintBrush}\"",
            mainWindow);
        Assert.Contains(
            "WindowBackdropService.Apply",
            codeBehind);
        Assert.Contains(
            "DwmsbtTransientWindow",
            backdropService);
        Assert.Contains(
            "backdropResult == 0",
            backdropService);
        Assert.Contains(
            "ThemeService.SetNativeBackdropActive(backdropActive)",
            backdropService);
        Assert.Contains("FocusShellTintBrush", theme);
    }

    [Fact]
    public void Pomodoro_UsesAccurateCountdownAndNoFullscreenOverlay()
    {
        string root = FindRepositoryRoot();
        string view = File.ReadAllText(
            Path.Combine(root, "Views", "PomodoroView.xaml"));
        string floating = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "PomodoroFloatingWindow.xaml"));
        string manager = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "PomodoroWindowManager.cs"));
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "PomodoroViewModel.cs"));
        string mainViewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));
        string mainWindow = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));

        Assert.DoesNotContain(
            "materialDesign:",
            view,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "FocusLinearProgress",
            view);
        Assert.Contains(
            "FocusLinearProgress",
            floating);
        Assert.Contains(
            "WindowBackdropService.Apply",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Views",
                    "PomodoroFloatingWindow.xaml.cs")));
        Assert.DoesNotContain(
            "ScreenBorderWindow",
            manager);
        Assert.False(
            File.Exists(
                Path.Combine(
                    root,
                    "Views",
                    "ScreenBorderWindow.xaml")));
        Assert.Contains(
            "_countdown.Tick()",
            viewModel);
        Assert.Contains(
            "SessionCompleted?.Invoke",
            viewModel);
        Assert.Contains(
            "PomodoroCompleted?.Invoke",
            mainViewModel);
        Assert.Contains(
            "ViewModel_PomodoroCompleted",
            mainWindow);
        Assert.Contains(
            "SystemSounds.Asterisk.Play()",
            mainWindow);
        Assert.Contains(
            "MyNotifyIcon.ShowBalloonTip",
            mainWindow);
    }

    [Fact]
    public void TaskModule_UsesFluentSurfacesRealKanbanAndSafeDetailLifecycle()
    {
        string root = FindRepositoryRoot();
        string tasksView = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "TasksView.xaml"));
        string detailView = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "TaskDetailWindow.xaml"));
        string detailCode = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "TaskDetailWindow.xaml.cs"));
        string tasksCode = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "TasksView.xaml.cs"));
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "TasksViewModel.cs"));

        Assert.DoesNotContain(
            "materialDesign:",
            tasksView,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "materialDesign:",
            detailView,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "DropShadowEffect",
            detailView);
        Assert.Contains(
            "AllowsTransparency=\"False\"",
            detailView);
        Assert.Contains(
            "WindowBackdropService.Apply",
            detailCode);
        Assert.Contains(
            "ItemsSource=\"{Binding BoardColumns}\"",
            tasksView);
        Assert.Contains(
            "MoveTaskPrevCommand",
            tasksView);
        Assert.Contains(
            "MoveTaskNextCommand",
            tasksView);
        Assert.Contains(
            "TaskDetailWindow? _detailWindow",
            tasksCode);
        Assert.Contains(
            "Unloaded +=",
            tasksCode);
        Assert.Contains(
            "AttachViewModel(null)",
            tasksCode);
        Assert.Contains(
            "TaskBoardComposer.Compose",
            viewModel);
        Assert.Contains(
            "IDisposable",
            viewModel);
    }

    [Fact]
    public void OkrModule_UsesFluentLocalFirstWorkflowAndReleasesSyncLifecycle()
    {
        string root = FindRepositoryRoot();
        string view = File.ReadAllText(
            Path.Combine(root, "Views", "OkrView.xaml"));
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "OkrViewModel.cs"));
        string viewCode = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "OkrView.xaml.cs"));
        string mainViewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));
        string syncService = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "OkrSyncService.cs"));

        Assert.DoesNotContain(
            "materialDesign:",
            view,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "MaterialDesign",
            view,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "目标与关键结果",
            view);
        Assert.Contains(
            "本地 OKR",
            view);
        Assert.Contains(
            "Style=\"{StaticResource FocusCard}\"",
            view);
        Assert.Contains(
            "AddKeyResultCommand",
            view);
        Assert.Contains(
            "UpdateKeyResultCommand",
            view);
        Assert.Contains(
            "DeleteKeyResultCommand",
            view);
        Assert.Contains(
            "SelectedObjective.KeyResults",
            view);
        Assert.Contains(
            "ObjectiveEditor.BringIntoView",
            viewCode);
        Assert.Contains(
            "OnSyncIntervalMinutesChanged",
            viewModel);
        Assert.Contains(
            "storedObjective.Progress",
            viewModel);
        Assert.Contains(
            "CalculateObjectiveProgress",
            viewModel);
        Assert.Contains(
            "!result.IsDeleted",
            viewModel);
        Assert.Contains(
            "DispatchToUi",
            viewModel);
        Assert.Contains(
            "IOkrDataProvider, IDisposable",
            viewModel);
        Assert.Contains(
            "_syncService.ProgressChanged -= OnSyncProgress",
            viewModel);
        Assert.Contains(
            "_okrViewModel?.Dispose()",
            mainViewModel);
        Assert.Contains(
            "正在从飞书拉取目标",
            syncService);
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
    public void OrganizerDrag_UsesNamedViewportAndStopsOnEveryExitPath()
    {
        string root = FindRepositoryRoot();
        string organizer = File.ReadAllText(
            Path.Combine(root, "Views", "FileOrganizerView.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(root, "Views", "FileOrganizerView.xaml.cs"));

        Assert.Contains("x:Name=\"OrganizerScrollViewer\"", organizer);
        Assert.Contains("PreviewDragOver=\"Organizer_PreviewDragOver\"", organizer);
        Assert.Contains("PreviewDragLeave=\"Organizer_PreviewDragLeave\"", organizer);
        Assert.Contains("PreviewDrop=\"Organizer_PreviewDrop\"", organizer);
        Assert.Contains(
            "PreviewMouseLeftButtonUp=\"FileDrag_PreviewMouseLeftButtonUp\"",
            organizer);
        Assert.Contains("Unloaded=\"UserControl_Unloaded\"", organizer);
        Assert.Contains(
            "OrganizerDragInteractionPolicy.HasExceededDragThreshold",
            codeBehind);
        Assert.Contains(
            "OrganizerDragInteractionPolicy.GetAutoScrollStep",
            codeBehind);
        Assert.Contains(
            "Mouse.LeftButton != MouseButtonState.Pressed",
            codeBehind);
        Assert.DoesNotContain("FindVisualChild<ScrollViewer>", codeBehind);
        Assert.Contains("private void StopAutoScroll()", codeBehind);
        Assert.Contains("private async void Partition_Drop", codeBehind);
        Assert.Contains("private void Column_Drop", codeBehind);
        Assert.True(
            codeBehind.Split("StopAutoScroll();", StringSplitOptions.None).Length >= 7);
    }

    [Fact]
    public void Organizer_UsesOnlySharedFluentControls()
    {
        string root = FindRepositoryRoot();
        string organizer = File.ReadAllText(
            Path.Combine(root, "Views", "FileOrganizerView.xaml"));
        string theme = File.ReadAllText(
            Path.Combine(root, "Themes", "FocusTheme.xaml"));

        Assert.DoesNotContain(
            "materialDesign:",
            organizer,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "MaterialDesign",
            organizer,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "MaterialDesign",
            theme,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "ToolbarToggleButtonStyle",
            organizer);
        Assert.Contains(
            "FocusSegmentRadioButton",
            organizer);
        Assert.Contains(
            "FocusMenuItem",
            organizer);
        Assert.Contains(
            "Opened=\"TransientPopup_Opened\"",
            organizer);
        Assert.False(
            File.Exists(
                Path.Combine(
                    root,
                    "Controls",
                    "MaterialCompat.cs")));
    }

    [Fact]
    public void HiddenShell_PausesWindowTrackingAndDetailRefreshTimers()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));
        string viewModel = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));
        string tracker = File.ReadAllText(
            Path.Combine(root, "Services", "WindowTracker.cs"));

        Assert.Contains("_viewModel.SetShellVisible(false)", mainWindow);
        Assert.Contains("_viewModel.SetShellVisible(true)", mainWindow);
        Assert.Contains("_windowTracker.SetTrackingActive(isVisible)", viewModel);
        Assert.Contains("ShellRefreshActivityPolicy.GetActivity", viewModel);
        Assert.Contains("if (_trackingActive)", tracker);
        Assert.Contains(
            "WindowTrackingActivityPolicy.ShouldProcessWindowEvent",
            tracker);
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
