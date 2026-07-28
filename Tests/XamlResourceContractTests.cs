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
            .Single(element => (string?)element.Attribute(x + "Key") == "FocusTextBox");
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
        Assert.Contains(
            "x:Name=\"TextInputChrome\"",
            theme);
        Assert.Contains(
            "Property=\"SelectionBrush\"",
            theme);
        Assert.Contains(
            "Property=\"SelectionTextBrush\"",
            theme);
        Assert.Contains(
            "Property=\"IsKeyboardFocusWithin\"",
            theme);
        Assert.Contains(
            "Property=\"IsReadOnly\"",
            theme);
        Assert.Contains(
            "Value=\"{x:Null}\"",
            theme);
        Assert.Contains(
            "x:Key=\"FocusSearchBox\"",
            theme);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusTextBox}\"",
            theme);
    }

    [Fact]
    public void PasswordBoxes_UseTheSameDynamicInputStates()
    {
        string root = FindRepositoryRoot();
        string theme = File.ReadAllText(
            Path.Combine(
                root,
                "Themes",
                "FocusTheme.xaml"));
        string views = string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    Path.Combine(
                        root,
                        "Views"),
                    "*.xaml")
                .Select(File.ReadAllText));

        Assert.Contains(
            "x:Key=\"FocusPasswordBox\"",
            theme);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusPasswordBox}\"",
            theme);
        Assert.Contains(
            "x:Name=\"PasswordInputChrome\"",
            theme);
        Assert.Contains(
            "Property=\"CaretBrush\"",
            theme);
        Assert.Contains(
            "Property=\"SelectionBrush\"",
            theme);
        Assert.Contains(
            "Property=\"SelectionTextBrush\"",
            theme);
        Assert.Contains(
            "Property=\"IsEnabled\"",
            theme);
        Assert.Equal(
            2,
            Regex.Matches(
                views,
                "<PasswordBox").Count);
        Assert.Equal(
            2,
            Regex.Matches(
                views,
                "Style=\"\\{StaticResource FocusPasswordBox\\}\"").Count);
    }

    [Fact]
    public void StatusCenter_ExposesSupportedSystemControls()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(Path.Combine(root, "Views", "MainWindow.xaml"));

        Assert.Contains("OpenQuickSettingsCommand", mainWindow);
        Assert.Contains("OpenNotificationsCommand", mainWindow);
        Assert.Contains("OpenInputSwitcherCommand", mainWindow);
        Assert.Contains(
            "Content=\"{Binding InputSwitcherLabel}\"",
            mainWindow);
        Assert.Contains(
            "ToolTip=\"{Binding InputSwitcherSummary}\"",
            mainWindow);
        Assert.Contains("Value=\"{Binding MasterVolume, Mode=TwoWay", mainWindow);
        Assert.Contains("ToggleMuteCommand", mainWindow);
        Assert.Contains("IsEnabled=\"{Binding IsAudioAvailable}\"", mainWindow);
        Assert.Contains("Text=\"{Binding AudioStatusText}\"", mainWindow);
        Assert.Contains("Text=\"{Binding AudioGlyph}\"", mainWindow);
        Assert.Contains("ToolTip=\"{Binding AudioToggleLabel}\"", mainWindow);
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding StatusCenterAutomationName}\"",
            mainWindow);
        Assert.Contains("ToolTip=\"{Binding StatusCenterSummary}\"", mainWindow);
        Assert.Contains("Text=\"{Binding BatteryGlyph}\"", mainWindow);
        Assert.Contains("Text=\"{Binding BatteryValueText}\"", mainWindow);
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding BatterySummary}\"",
            mainWindow);
        Assert.Contains("NetworkDetail", mainWindow);
        Assert.Contains("Text=\"{Binding NetworkGlyph}\"", mainWindow);
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding NetworkSummary}\"",
            mainWindow);
        Assert.Contains("LockComputerCommand", mainWindow);
        Assert.Contains("SleepComputerCommand", mainWindow);
        Assert.Contains("ShowDesktopCommand", mainWindow);
        Assert.Contains("Text=\"{Binding SystemActionMessage}\"", mainWindow);
        Assert.Contains("Content=\"快捷设置\"", mainWindow);
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

        string statusContract = File.ReadAllText(
            Path.Combine(root, "Services", "ISystemStatusService.cs"));
        Assert.Contains("bool ShowDesktop()", statusContract);
        Assert.Contains("bool Lock()", statusContract);
        Assert.Contains("bool Sleep()", statusContract);
        Assert.Contains("bool Restart()", statusContract);
        Assert.Contains("bool Shutdown()", statusContract);
        Assert.Contains("AudioStatusSnapshot GetAudioStatus()", statusContract);
        Assert.Contains("bool TrySetMasterVolume(float value)", statusContract);
        Assert.Contains("bool TrySetMuted(bool value)", statusContract);
        Assert.Contains(
            "BatteryStatusSnapshot GetBatteryStatus()",
            statusContract);
        Assert.Contains(
            "NetworkStatusSnapshot GetNetworkStatus()",
            statusContract);
        Assert.Contains(
            "InputMethodStatusSnapshot GetInputMethodStatus()",
            statusContract);
        Assert.DoesNotContain(
            "string InputLanguageDisplay { get; }",
            statusContract);
        Assert.DoesNotContain(
            "string InputMethodDisplay { get; }",
            statusContract);
        Assert.DoesNotContain(
            "bool IsNetworkAvailable { get; }",
            statusContract);
        Assert.DoesNotContain(
            "string NetworkDisplayName { get; }",
            statusContract);
        Assert.DoesNotContain(
            "string NetworkDetail { get; }",
            statusContract);
        Assert.DoesNotContain(
            "bool HasBattery { get; }",
            statusContract);
        Assert.DoesNotContain(
            "int BatteryPercent { get; }",
            statusContract);
        Assert.DoesNotContain(
            "bool IsCharging { get; }",
            statusContract);
        Assert.DoesNotContain("float MasterVolume { get; set; }", statusContract);
        Assert.DoesNotContain("bool IsMuted { get; set; }", statusContract);

        string viewModel = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));
        Assert.Contains("CompleteSystemAction(", viewModel);
        Assert.Contains("IsStatusCenterOpen = true", viewModel);
        Assert.Contains("AdjustMasterVolume(float step)", viewModel);

        string windowCode = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));
        Assert.Contains("_viewModel.AdjustMasterVolume(step)", windowCode);
        Assert.DoesNotContain(
            "_viewModel.MasterVolume = Math.Clamp",
            windowCode);
    }

    [Fact]
    public void Settings_UsesZeroConfigurationGitHubUpdates()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(Path.Combine(root, "Views", "MainWindow.xaml"));
        string viewModel = File.ReadAllText(Path.Combine(root, "ViewModels", "MainViewModel.cs"));
        string windowCode = File.ReadAllText(Path.Combine(root, "Views", "MainWindow.xaml.cs"));

        Assert.Contains("GitHub Releases · 静态清单", mainWindow);
        Assert.Contains("CheckAndInstallUpdateCommand", mainWindow);
        Assert.Contains("OpenUpdateDownloadPageCommand", mainWindow);
        Assert.Contains("打开官方下载页", mainWindow);
        Assert.DoesNotContain("LanUpdateSource", mainWindow);
        Assert.DoesNotContain("SaveUpdateSourceCommand", mainWindow);
        Assert.DoesNotContain("Update.SourceMode", viewModel);
        Assert.DoesNotContain("Update.LanLocation", viewModel);
        Assert.Contains("TimeSpan.FromHours(6)", viewModel);
        Assert.Contains("CheckForUpdatesInBackgroundAsync", windowCode);
        Assert.Contains("ShowBalloonTip", windowCode);
    }

    [Fact]
    public void ReleasePublishing_MarksAndVerifiesLatestGitHubRelease()
    {
        string root = FindRepositoryRoot();
        string publisher = File.ReadAllText(
            Path.Combine(root, "scripts", "publish-github-release.ps1"));
        string packager = File.ReadAllText(
            Path.Combine(root, "scripts", "package-release.ps1"));

        Assert.Contains("make_latest = 'true'", publisher);
        Assert.Contains("/releases/latest", publisher);
        Assert.Contains("'releases.win.json'", publisher);
        Assert.Contains(
            "New-Object System.Text.UnicodeEncoding($false, $true)",
            packager);
        Assert.Contains(
            "release-notes-unicode.md",
            packager);
        Assert.Contains(
            "Release notes changed while packaging",
            packager);
        Assert.Contains(
            "$manifestNotes -cne $expectedNotes",
            packager);
        Assert.Contains("'RELEASES'", publisher);
        Assert.Contains("-full\\.nupkg", publisher);
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
            "AutomationProperties.Name=\"{Binding AccessibleName}\"",
            mainWindow);
        Assert.Contains(
            "AutomationProperties.HelpText=\"{Binding InteractionHint}\"",
            mainWindow);
        Assert.Contains(
            "PreviewMouseDown=\"TaskbarApp_PreviewMouseDown\"",
            mainWindow);
        Assert.Contains(
            "Text=\"{Binding InteractionHint}\"",
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
    public void TaskbarApps_ShowDistinctRunningAndActiveStates()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml"));
        string model = File.ReadAllText(
            Path.Combine(
                root,
                "Models",
                "TaskbarAppItem.cs"));

        Assert.Contains(
            "Text=\"{Binding StatusSummary}\"",
            mainWindow);
        Assert.Contains(
            "Binding=\"{Binding IsActive}\"",
            mainWindow);
        Assert.Contains(
            "Value=\"{DynamicResource FocusSurfaceSoftBrush}\"",
            mainWindow);
        Assert.Contains(
            "IsHitTestVisible=\"False\"",
            mainWindow);
        Assert.Contains(
            "<Setter Property=\"Height\" Value=\"12\"/>",
            mainWindow);
        Assert.Contains(
            "<Setter Property=\"Height\" Value=\"24\"/>",
            mainWindow);
        Assert.Contains(
            "CornerRadius=\"2\"",
            mainWindow);
        Assert.DoesNotContain(
            "<Ellipse Width=\"6\"",
            mainWindow);
        Assert.Contains(
            "public string StatusSummary",
            model);
        Assert.Contains(
            "public string AccessibleName",
            model);
        Assert.Contains(
            "public string InteractionHint",
            model);
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
        Assert.Contains("IsInputFocusActive()", mainWindowCode);
        Assert.Contains("TextBoxBase or PasswordBox", mainWindowCode);
        Assert.Contains("ComboBox or ComboBoxItem", mainWindowCode);
        Assert.DoesNotContain(
            "ShellBorder.IsKeyboardFocusWithin,\n",
            mainWindowCode.Replace("\r\n", "\n"));
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
        Assert.Contains("TaskbarApp_PreviewMouseDown", mainWindow);
        Assert.Contains("MouseButton.Middle", codeBehind);
        Assert.Contains("Keyboard.Modifiers", codeBehind);
        Assert.Contains("LaunchNewTaskbarAppCommand.Execute(task)", codeBehind);
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
        Assert.Contains("TrySetPinned(launch, true)", viewModel);
        Assert.Contains("TryMovePinned(", viewModel);
    }

    [Fact]
    public void MultiWindowLeftClick_OpensDirectWindowList()
    {
        string root = FindRepositoryRoot();
        string codeBehind = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));
        string tracker = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "WindowTracker.cs"));
        string synchronizer = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "TaskbarAppCollectionSynchronizer.cs"));

        Assert.Contains(
            "PopulateTaskbarWindowList(button, task);",
            codeBehind);
        Assert.Contains(
            "PopulateTaskbarAppContextMenu(button, task);",
            codeBehind);
        Assert.Contains(
            "_viewModel.ActivateWindowCommand",
            codeBehind);
        Assert.Contains(
            "CommandParameter = window",
            codeBehind);
        Assert.Contains(
            "IsChecked = window.IsActive",
            codeBehind);
        Assert.Contains(
            "TextTrimming.CharacterEllipsis",
            codeBehind);
        Assert.Contains(
            "当前窗口，",
            codeBehind);
        Assert.Contains(
            "item.IsActive))",
            tracker);
        Assert.Contains(
            "left.IsActive == right.IsActive",
            synchronizer);
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
    public void AppSearch_ProvidesCompleteKeyboardLaunchPath()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));
        XDocument document = XDocument.Parse(mainWindow);
        XNamespace xaml =
            "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement resultsList = document
            .Descendants()
            .Single(element =>
                (string?)element.Attribute(
                    xaml + "Name") == "SearchResultsList");

        Assert.Contains(
            "PreviewKeyDown=\"SearchBox_PreviewKeyDown\"",
            mainWindow);
        Assert.Contains(
            "SelectedItem=\"{Binding SelectedSearchResult, Mode=TwoWay}\"",
            mainWindow);
        Assert.Contains(
            "使用上下方向键选择，按回车启动",
            mainWindow);
        Assert.Contains(
            "AppSearchSelectionPolicy.Move(",
            codeBehind);
        Assert.Contains(
            "_viewModel.LaunchAppCommand.Execute(app);",
            codeBehind);
        Assert.Contains(
            "SearchBox.SelectAll();",
            codeBehind);
        Assert.Contains(
            "new Action(() => SearchButton.Focus())",
            codeBehind);
        Assert.Contains(
            "SelectedSearchResult?.IdentityKey",
            viewModel);
        Assert.Equal(
            "{DynamicResource FocusTextBrush}",
            (string?)resultsList.Attribute("Foreground"));
        Assert.Contains(
            resultsList.Descendants(),
            element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("Foreground")
                    == "{DynamicResource FocusTextBrush}");
        Assert.Contains(
            resultsList.Descendants(),
            element => element.Name.LocalName == "TextBlock"
                && (string?)element.Attribute("Foreground")
                    == "{DynamicResource FocusTextBrush}");
        Assert.Contains(
            "<Trigger Property=\"IsSelected\" Value=\"True\">",
            mainWindow);
        Assert.Contains(
            "Value=\"{DynamicResource FocusSurfaceSoftBrush}\"",
            mainWindow);
    }

    [Fact]
    public void ContextMenus_UseOneFluentThemeForStaticAndDynamicItems()
    {
        string root = FindRepositoryRoot();
        string theme = File.ReadAllText(
            Path.Combine(
                root,
                "Themes",
                "FocusTheme.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));

        Assert.Contains(
            "x:Key=\"FocusContextMenu\"",
            theme);
        Assert.Contains(
            "x:Key=\"FocusMenuItem\"",
            theme);
        Assert.Contains(
            "x:Key=\"FocusMenuSeparator\"",
            theme);
        Assert.Contains(
            "<Style TargetType=\"ContextMenu\"",
            theme);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusContextMenu}\"",
            theme);
        Assert.Contains(
            "<Style TargetType=\"MenuItem\"",
            theme);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusMenuItem}\"",
            theme);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusMenuSeparator}\"",
            theme);
        Assert.Contains(
            "x:Name=\"MenuSurface\"",
            theme);
        Assert.Contains(
            "x:Name=\"PART_Popup\"",
            theme);
        Assert.Contains(
            "Property=\"IsHighlighted\"",
            theme);
        Assert.Contains(
            "Property=\"IsChecked\"",
            theme);
        Assert.Contains(
            "Property=\"HasItems\"",
            theme);
        Assert.DoesNotContain(
            "SystemColors.Highlight",
            theme);
        Assert.Contains(
            "ContextMenu menu = button.ContextMenu ?? new ContextMenu();",
            codeBehind);
        Assert.Contains(
            "menu.Items.Add(new MenuItem",
            codeBehind);
    }

    [Fact]
    public void ToolTips_UseOneRoundedDynamicTheme()
    {
        string root = FindRepositoryRoot();
        string theme = File.ReadAllText(
            Path.Combine(
                root,
                "Themes",
                "FocusTheme.xaml"));

        Assert.Contains(
            "x:Key=\"FocusToolTip\"",
            theme);
        Assert.Contains(
            "<Style TargetType=\"ToolTip\"",
            theme);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusToolTip}\"",
            theme);
        Assert.Contains(
            "x:Name=\"ToolTipSurface\"",
            theme);
        Assert.Contains(
            "Value=\"{DynamicResource FocusSurfaceStrongBrush}\"",
            theme);
        Assert.Contains(
            "Value=\"{DynamicResource FocusTextBrush}\"",
            theme);
        Assert.Contains(
            "Value=\"{DynamicResource FocusStrokeBrush}\"",
            theme);
        Assert.Contains(
            "Property=\"HasDropShadow\" Value=\"False\"",
            theme);
        Assert.DoesNotContain(
            "<Setter Property=\"Background\" Value=\"#F02A2E38\"",
            theme);
    }

    [Fact]
    public void ComboBoxes_UseOneRoundedDynamicPopupTheme()
    {
        string root = FindRepositoryRoot();
        string theme = File.ReadAllText(
            Path.Combine(
                root,
                "Themes",
                "FocusTheme.xaml"));
        string mainWindow = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml"));

        Assert.Contains(
            "x:Key=\"FocusComboBox\"",
            theme);
        Assert.Contains(
            "x:Key=\"FocusComboBoxItem\"",
            theme);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusComboBox}\"",
            theme);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusComboBoxItem}\"",
            theme);
        Assert.Contains(
            "x:Name=\"DropDownToggle\"",
            theme);
        Assert.Contains(
            "x:Name=\"PART_Popup\"",
            theme);
        Assert.Contains(
            "x:Name=\"DropDownSurface\"",
            theme);
        Assert.Contains(
            "x:Name=\"SelectionIndicator\"",
            theme);
        Assert.Contains(
            "CornerRadius=\"{StaticResource FocusCardCornerRadius}\"",
            theme);
        Assert.Contains(
            "Value=\"{DynamicResource FocusSurfaceStrongBrush}\"",
            theme);
        Assert.Contains(
            "<ComboBox SelectedValuePath=\"Tag\"",
            mainWindow);
        Assert.DoesNotContain(
            "IsEditable=\"True\"",
            mainWindow);
    }

    [Fact]
    public void CheckBoxes_UseOneRoundedDynamicTheme()
    {
        string root = FindRepositoryRoot();
        string theme = File.ReadAllText(
            Path.Combine(
                root,
                "Themes",
                "FocusTheme.xaml"));
        string views = string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    Path.Combine(
                        root,
                        "Views"),
                    "*.xaml")
                .Select(File.ReadAllText));

        Assert.Contains(
            "x:Key=\"FocusCheckBox\"",
            theme);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusCheckBox}\"",
            theme);
        Assert.Contains(
            "x:Name=\"InteractionSurface\"",
            theme);
        Assert.Contains(
            "x:Name=\"CheckBoxChrome\"",
            theme);
        Assert.Contains(
            "x:Name=\"CheckGlyph\"",
            theme);
        Assert.Contains(
            "x:Name=\"IndeterminateGlyph\"",
            theme);
        Assert.Contains(
            "<Setter Property=\"MinHeight\" Value=\"44\"/>",
            theme);
        Assert.Contains(
            "Value=\"{DynamicResource FocusAccentBrush}\"",
            theme);
        Assert.Contains(
            "Property=\"IsChecked\"",
            theme);
        Assert.Contains(
            "Property=\"IsEnabled\"",
            theme);
        Assert.Contains(
            "<CheckBox",
            views);
        Assert.DoesNotContain(
            "IsThreeState=\"True\"",
            views);
    }

    [Fact]
    public void ScrollBars_UseOneSlimRoundedDynamicTheme()
    {
        string root = FindRepositoryRoot();
        string theme = File.ReadAllText(
            Path.Combine(
                root,
                "Themes",
                "FocusTheme.xaml"));

        Assert.Contains(
            "x:Key=\"FocusScrollBar\"",
            theme);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusScrollBar}\"",
            theme);
        Assert.Contains(
            "x:Name=\"PART_Track\"",
            theme);
        Assert.Contains(
            "Orientation=\"{TemplateBinding Orientation}\"",
            theme);
        Assert.Contains(
            "x:Name=\"ScrollThumb\"",
            theme);
        Assert.Contains(
            "x:Name=\"ThumbSurface\"",
            theme);
        Assert.Contains(
            "Value=\"{DynamicResource FocusMutedTextBrush}\"",
            theme);
        Assert.Contains(
            "Value=\"{DynamicResource FocusAccentBrightBrush}\"",
            theme);
        Assert.Contains(
            "<Setter Property=\"Width\"",
            theme);
        Assert.Contains(
            "Value=\"10\"",
            theme);
        Assert.Contains(
            "Command=\"{x:Static ScrollBar.PageUpCommand}\"",
            theme);
        Assert.Contains(
            "Value=\"{x:Static ScrollBar.PageRightCommand}\"",
            theme);
        Assert.DoesNotContain(
            "SystemColors.ScrollBar",
            theme);
    }

    [Fact]
    public void SlidersAndProgressBars_UseDynamicFluentStates()
    {
        string root = FindRepositoryRoot();
        string theme = File.ReadAllText(
            Path.Combine(
                root,
                "Themes",
                "FocusTheme.xaml"));
        string views = string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    Path.Combine(
                        root,
                        "Views"),
                    "*.xaml")
                .Select(File.ReadAllText));

        Assert.Contains(
            "x:Key=\"FocusSlider\"",
            theme);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusSlider}\"",
            theme);
        Assert.Contains(
            "x:Name=\"SliderThumbSurface\"",
            theme);
        Assert.Contains(
            "x:Name=\"DecreaseTrackButton\"",
            theme);
        Assert.Contains(
            "x:Name=\"IncreaseTrackButton\"",
            theme);
        Assert.Contains(
            "Command=\"{x:Static Slider.DecreaseLarge}\"",
            theme);
        Assert.Contains(
            "Orientation=\"{TemplateBinding Orientation}\"",
            theme);
        Assert.Contains(
            "x:Key=\"FocusLinearProgress\"",
            theme);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusLinearProgress}\"",
            theme);
        Assert.Contains(
            "x:Name=\"PART_Track\"",
            theme);
        Assert.Contains(
            "x:Name=\"PART_Indicator\"",
            theme);
        Assert.Contains(
            "x:Name=\"IndeterminateIndicator\"",
            theme);
        Assert.Contains(
            "<Trigger Property=\"IsIndeterminate\"",
            theme);
        Assert.Contains(
            "<Slider",
            views);
        Assert.Contains(
            "<ProgressBar",
            views);
        Assert.DoesNotContain(
            "SystemColors.HighlightBrushKey",
            theme);
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
    public void DesktopOrganizer_WatcherUsesPathLevelRefresh()
    {
        string root = FindRepositoryRoot();
        string service = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "FileOrganizerService.cs"));
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "FileOrganizerViewModel.cs"));

        Assert.Contains(
            "_desktopWatcher.Changed += OnChanged",
            service);
        Assert.Contains(
            "SchedulePathRefresh(e.FullPath)",
            service);
        Assert.Contains(
            "RefreshChangedPaths(batch.Paths)",
            service);
        Assert.Contains(
            "_storageWatcher.Error += OnWatcherError",
            service);
        Assert.Contains(
            "ScheduleFullRefresh()",
            service);
        Assert.Contains(
            "public void Dispose()",
            service);
        Assert.Contains(
            "_fileService.Dispose()",
            viewModel);
    }

    [Fact]
    public void DesktopOrganizer_UsesViewportVirtualizationForBothModes()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "FileOrganizerView.xaml"));
        string panel = File.ReadAllText(
            Path.Combine(
                root,
                "Controls",
                "ViewportVirtualizingPanel.cs"));

        Assert.Equal(
            2,
            CountOccurrences(
                xaml,
                "<controls:ViewportVirtualizingPanel"));
        Assert.Equal(
            2,
            CountOccurrences(
                xaml,
                "VirtualizingPanel.IsVirtualizing=\"True\""));
        Assert.Equal(
            2,
            CountOccurrences(
                xaml,
                "VirtualizingPanel.VirtualizationMode=\"Recycling\""));
        Assert.DoesNotContain(
            "<WrapPanel Orientation=\"Horizontal\"/>",
            xaml);
        Assert.Contains(
            "ScrollOwner_ScrollChanged",
            panel);
        Assert.Contains(
            "IRecyclingItemContainerGenerator",
            panel);
    }

    [Fact]
    public void WindowClosing_UsesWmCloseWithoutProcessTermination()
    {
        string root = FindRepositoryRoot();
        string windowTracker = File.ReadAllText(
            Path.Combine(root, "Services", "WindowTracker.cs"));
        string commandBoundary = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "WindowsWindowCommandBoundary.cs"));

        Assert.Contains("_commands.Close(handle)", windowTracker);
        Assert.Contains("PostMessage(", commandBoundary);
        Assert.Contains("WmClose", commandBoundary);
        Assert.DoesNotContain(".Kill(", windowTracker);
        Assert.DoesNotContain(".Kill(", commandBoundary);
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
        Assert.Contains(
            "bool becameVisible =",
            viewModel);
        Assert.Contains(
            "if (becameVisible)",
            viewModel);
        Assert.Contains(
            "RefreshSystemStatus();",
            viewModel);
        Assert.Contains("_windowTracker.SetTrackingActive(isVisible)", viewModel);
        Assert.Contains("ShellRefreshActivityPolicy.GetActivity", viewModel);
        Assert.Contains("if (_trackingActive)", tracker);
        Assert.Contains(
            "WindowTrackingActivityPolicy.ShouldProcessWindowEvent",
            tracker);
    }

    [Fact]
    public void WindowTracker_ObservesCompleteTopLevelLifecycle()
    {
        string root = FindRepositoryRoot();
        string tracker = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "WindowTracker.cs"));
        string policy = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "WindowTrackingEventPolicy.cs"));

        Assert.Contains(
            "WindowTrackingEventPolicy.EventObjectCreate",
            tracker);
        Assert.Contains(
            "WindowTrackingEventPolicy.EventObjectHide",
            tracker);
        Assert.Contains(
            "WindowTrackingEventPolicy.EventObjectNameChange",
            tracker);
        Assert.Contains(
            "WineventSkipOwnProcess",
            tracker);
        Assert.Contains(
            "WindowTrackingEventPolicy.ShouldQueueRefresh",
            tracker);
        Assert.Contains(
            "EventObjectDestroy = 0x8001",
            policy);
        Assert.Contains(
            "objectId != ObjectIdWindow",
            policy);
        Assert.DoesNotContain(
            "eventType >= EventObjectShow",
            tracker);
    }

    [Fact]
    public void AppLaunchFailures_AreContainedAndShownInStatusCenter()
    {
        string root = FindRepositoryRoot();
        string contract = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "IAppCatalogService.cs"));
        string catalog = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "AppCatalogService.cs"));
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));
        string mainWindow = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml"));

        Assert.Contains(
            "bool Launch(AppLaunchItem app)",
            contract);
        Assert.Contains(
            "AppLaunchRequestBuilder.TryBuild",
            catalog);
        Assert.Contains(
            "AppLaunchExecution.TryStart",
            catalog);
        Assert.Contains(
            "SystemActionExecution.Try(",
            viewModel);
        Assert.Contains(
            "请在搜索中重新固定",
            viewModel);
        Assert.Contains(
            "IsStatusCenterOpen = true",
            viewModel);
        Assert.Contains(
            "Text=\"{Binding SystemActionMessage}\"",
            mainWindow);
        Assert.Contains(
            "AutomationProperties.LiveSetting=\"Assertive\"",
            mainWindow);
    }

    [Fact]
    public void TaskbarWindowAndPinActions_ReportFailures()
    {
        string root = FindRepositoryRoot();
        string windowContract = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "IWindowTracker.cs"));
        string appContract = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "IAppCatalogService.cs"));
        string catalog = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "AppCatalogService.cs"));
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));

        Assert.Contains(
            "bool ActivateOrMinimize(WindowTaskItem task)",
            windowContract);
        Assert.Contains(
            "bool Activate(IntPtr handle)",
            windowContract);
        Assert.Contains(
            "bool Close(IntPtr handle)",
            windowContract);
        Assert.Contains(
            "bool SetPinned(AppLaunchItem app, bool pinned)",
            appContract);
        Assert.Contains(
            "bool MovePinned(AppLaunchItem app, int newIndex)",
            appContract);
        Assert.Contains(
            "SystemActionExecution.Try(",
            catalog);
        Assert.Contains(
            "CompleteTaskbarWindowAction(",
            viewModel);
        Assert.Contains(
            "TrySetPinned(",
            viewModel);
        Assert.Contains(
            "TryMovePinned(",
            viewModel);
        Assert.Contains(
            "Windows 暂时阻止了前台切换",
            viewModel);
        Assert.Contains(
            "无法保存“",
            viewModel);
        Assert.Contains(
            "ReportTaskbarActionFailure(",
            viewModel);
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

    private static int CountOccurrences(
        string source,
        string value)
    {
        int count = 0;
        int startIndex = 0;
        while ((startIndex = source.IndexOf(
                   value,
                   startIndex,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += value.Length;
        }
        return count;
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
