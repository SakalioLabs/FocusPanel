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
            "PreviewKeyDown=\"CalendarPanel_PreviewKeyDown\"",
            calendar);
        Assert.Contains(
            "IsVisibleChanged=\"CalendarPanel_IsVisibleChanged\"",
            calendar);
        Assert.Contains(
            "x:Name=\"CalendarDaysItems\"",
            calendar);
        Assert.Contains(
            "PageUp 和 PageDown",
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
        string calendarCode = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "CalendarPanelView.xaml.cs"));
        Assert.Contains(
            "NavigateCalendarCommand",
            calendarCode);
        Assert.Contains(
            "Key.PageUp",
            calendarCode);
        Assert.Contains(
            "Key.PageDown",
            calendarCode);
        Assert.Contains(
            "ModifierKeys.Control",
            calendarCode);
        Assert.Contains(
            "FocusSelectedDay",
            calendarCode);
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
    public void Buttons_UseFluentRowAndDangerActionStyles()
    {
        string root = FindRepositoryRoot();
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var unstyled = new List<string>();

        foreach (string xamlPath in Directory.GetFiles(
                     Path.Combine(root, "Views"),
                     "*.xaml",
                     SearchOption.TopDirectoryOnly))
        {
            XDocument document = XDocument.Load(xamlPath);
            foreach (XElement button in document.Descendants(
                         presentation + "Button"))
            {
                bool hasStyle =
                    button.Attribute("Style") != null
                    || button.Elements(
                            presentation + "Button.Style")
                        .Any();
                if (!hasStyle)
                {
                    unstyled.Add(
                        $"{Path.GetFileName(xamlPath)}: "
                        + ((string?)button.Attribute("Content")
                           ?? "(复合内容)"));
                }
            }
        }

        Assert.True(
            unstyled.Count == 0,
            "发现回退到 WPF 原生外观的按钮："
            + Environment.NewLine
            + string.Join(Environment.NewLine, unstyled));

        string theme = File.ReadAllText(
            Path.Combine(root, "Themes", "FocusTheme.xaml"));
        string themeService = File.ReadAllText(
            Path.Combine(root, "Services", "ThemeService.cs"));
        string main = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string ai = File.ReadAllText(
            Path.Combine(root, "Views", "AIAssistantView.xaml"));
        string organizer = File.ReadAllText(
            Path.Combine(root, "Views", "FileOrganizerView.xaml"));
        string okr = File.ReadAllText(
            Path.Combine(root, "Views", "OkrView.xaml"));

        Assert.Contains("x:Key=\"FocusRowButton\"", theme);
        Assert.Contains("x:Key=\"FocusDangerButton\"", theme);
        Assert.Contains("x:Key=\"FocusDangerSoftBrush\"", theme);
        Assert.Contains(
            "HorizontalAlignment=\"{TemplateBinding HorizontalContentAlignment}\"",
            theme);
        Assert.Contains(
            "SetBrush(\"FocusDangerSoftBrush\"",
            themeService);
        Assert.Contains(
            "Style=\"{StaticResource FocusRowButton}\"",
            main);
        Assert.True(
            CountOccurrences(
                main,
                "Style=\"{StaticResource FocusDangerButton}\"")
            >= 2);
        Assert.Contains(
            "Style=\"{StaticResource FocusDangerButton}\"",
            ai);
        Assert.Contains(
            "Style=\"{StaticResource FocusDangerButton}\"",
            organizer);
        Assert.True(
            CountOccurrences(
                okr,
                "Style=\"{StaticResource FocusDangerButton}\"")
            >= 2);
    }

    [Fact]
    public void RuntimeDialogs_UseFocusPanelFluentSurface()
    {
        string root = FindRepositoryRoot();
        var nativeDialogCalls = new List<string>();
        foreach (string directory in new[]
                 {
                     "ViewModels",
                     "Views"
                 })
        {
            foreach (string path in Directory.GetFiles(
                         Path.Combine(root, directory),
                         "*.cs",
                         SearchOption.TopDirectoryOnly))
            {
                string source = File.ReadAllText(path);
                if (source.Contains(
                        "MessageBox.Show(",
                        StringComparison.Ordinal))
                {
                    nativeDialogCalls.Add(
                        Path.GetFileName(path));
                }
            }
        }

        Assert.True(
            nativeDialogCalls.Count == 0,
            "运行期仍直接调用系统 MessageBox："
            + string.Join(", ", nativeDialogCalls));

        string dialog = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "FocusDialogWindow.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "FocusDialogWindow.xaml.cs"));
        string service = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "FocusDialogService.cs"));
        string mainWindowCode = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));
        string organizer = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "FileOrganizerViewModel.cs"));
        string tasks = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "TasksViewModel.cs"));

        Assert.Contains(
            "FocusShellTintBrush",
            dialog);
        Assert.Contains(
            "FocusDangerButton",
            codeBehind);
        Assert.Contains(
            "WindowBackdropService.Apply(this)",
            codeBehind);
        Assert.Contains(
            "CenterOwner",
            service);
        Assert.Contains(
            "Dispatcher.CheckAccess()",
            service);
        Assert.Contains(
            "FocusDialogInteractionLease.Enter(shell)",
            service);
        Assert.Contains(
            "IFocusDialogInteractionHost",
            mainWindowCode);
        Assert.DoesNotContain(
            "Rescue Desktop",
            organizer);
        Assert.DoesNotContain(
            "Failed to insert image",
            tasks);
    }

    [Fact]
    public void CompositeButtons_AreNamedAndViewSurfacesUseGeometryTokens()
    {
        string root = FindRepositoryRoot();
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var unnamed = new List<string>();
        var hardCodedCorners = new List<string>();

        foreach (string path in Directory.GetFiles(
                     Path.Combine(root, "Views"),
                     "*.xaml",
                     SearchOption.TopDirectoryOnly))
        {
            XDocument document = XDocument.Load(path);
            foreach (XElement button in document.Descendants(
                         presentation + "Button"))
            {
                bool hasDirectLabel =
                    button.Attribute("Content") != null
                    || button.Attribute("ToolTip") != null
                    || button.Attributes().Any(
                        attribute =>
                            attribute.Name.LocalName
                                == "AutomationProperties.Name");
                if (!hasDirectLabel)
                {
                    unnamed.Add(
                        Path.GetFileName(path));
                }
            }

            foreach (XElement element in document.Descendants())
            {
                string? corner =
                    (string?)element.Attribute(
                        "CornerRadius");
                if (corner == null
                    || corner.Contains(
                        "Resource",
                        StringComparison.Ordinal)
                    || corner is "0" or "2")
                {
                    continue;
                }

                hardCodedCorners.Add(
                    $"{Path.GetFileName(path)}: {corner}");
            }
        }

        Assert.True(
            unnamed.Count == 0,
            "复合按钮缺少读屏名称："
            + string.Join(
                ", ",
                unnamed.Distinct()));
        Assert.True(
            hardCodedCorners.Count == 0,
            "页面表面绕过统一圆角令牌："
            + string.Join(
                ", ",
                hardCodedCorners));
    }

    [Fact]
    public void TaskImageFolder_UsesModernShellPickerBoundary()
    {
        string root = FindRepositoryRoot();
        string tasks = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "TasksViewModel.cs"));
        string picker = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "ShellFolderPickerService.cs"));

        Assert.DoesNotContain(
            "FolderBrowserDialog",
            tasks);
        Assert.DoesNotContain(
            "System.Windows.Forms.DialogResult",
            tasks);
        Assert.Contains(
            "new ShellFolderPickerService()",
            tasks);
        Assert.Contains(
            "FolderSelectionPolicy.Resolve(result)",
            tasks);
        Assert.Contains(
            "PickFolders",
            picker);
        Assert.Contains(
            "ForceFileSystem",
            picker);
        Assert.Contains(
            "SHCreateItemFromParsingName",
            picker);
        Assert.Contains(
            "FileSystemPath",
            picker);
        Assert.Contains(
            "dialog.Show(ownerHandle)",
            picker);
        Assert.Contains(
            "FocusDialogInteractionLease.Enter(",
            picker);

        string filePicker = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "WindowsFilePickerService.cs"));
        Assert.DoesNotContain(
            "new OpenFileDialog",
            tasks);
        Assert.Contains(
            "new WindowsFilePickerService()",
            tasks);
        Assert.Contains(
            "FileSelectionPolicy.Resolve(result)",
            tasks);
        Assert.Contains(
            "图片文件 (*.png;*.jpg;*.jpeg;*.gif;*.bmp)",
            tasks);
        Assert.Contains(
            "![图片]",
            tasks);
        Assert.Contains(
            "dialog.ShowDialog(owner)",
            filePicker);
        Assert.Contains(
            "FocusDialogInteractionLease.Enter(",
            filePicker);
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
        Assert.Contains(
            "SystemStatusSnapshot GetStatusSnapshot()",
            statusContract);
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
        Assert.Contains(
            "NativeMethods.CoInitializeEx(",
            systemStatus);
        Assert.Contains(
            "NativeMethods.CoUninitialize();",
            systemStatus);
        Assert.Contains(
            "TryCreateDeviceEnumerator()",
            systemStatus);
        Assert.DoesNotContain(
            "_deviceEnumerator",
            systemStatus);
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
        Assert.Contains(
            "AudioControlCoordinator",
            viewModel);
        Assert.Contains(
            "_audioControl.QueueVolume(",
            viewModel);
        Assert.Contains(
            "_audioControl.QueueMuted(",
            viewModel);
        Assert.Contains(
            "AudioControlCompletionPolicy.Apply(",
            viewModel);
        Assert.Contains(
            "audioWritePendingBeforeCapture",
            viewModel);
        Assert.Contains(
            "audioWritePendingAfterCapture",
            viewModel);
        Assert.DoesNotContain(
            "TryApplyMasterVolume(",
            viewModel);
        Assert.DoesNotContain(
            "TryApplyMuted(",
            viewModel);

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
        Assert.Contains("FocusToastManager", windowCode);
        Assert.DoesNotContain("ShowBalloonTip", windowCode);
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
            "$expectedReleaseHeading",
            packager);
        Assert.Contains(
            "Release notes heading is",
            packager);
        Assert.Contains(
            "Release notes changed while packaging",
            packager);
        Assert.Contains(
            "$manifestNotes -cne $expectedNotes",
            packager);
        Assert.Contains(
            "[switch]$ReplaceCurrentVersion",
            packager);
        Assert.Contains(
            "CleanPackages and ReplaceCurrentVersion cannot be used together.",
            packager);
        Assert.Contains(
            "Remove-GeneratedFile",
            packager);
        Assert.Contains("'--msi', 'true'", packager);
        Assert.Contains("'--instLocation', 'Either'", packager);
        Assert.Contains(
            "CustomInstallerLauncher.cs",
            packager);
        Assert.Contains(
            "FocusPanel-win-NativeSetup.exe",
            packager);
        Assert.Contains(
            "/resource:$($msi.FullName),FocusPanelMsi",
            packager);
        Assert.Contains(
            "InstallerLocationPolicy.cs",
            packager);
        Assert.Contains(
            "msiexec.exe",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "packaging",
                    "CustomInstallerLauncher.cs")));
        Assert.Contains(
            "--verify-install-location-picker",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "packaging",
                    "CustomInstallerLauncher.cs")));
        Assert.Contains(
            "--verify-install-location-picker",
            packager);
        Assert.Contains(
            "expected 42",
            packager);
        Assert.Contains(
            "VELOPACK_INSTALLDIR=",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "packaging",
                    "CustomInstallerLauncher.cs")));
        Assert.Contains(
            "INSTALLFOLDER=",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "packaging",
                    "CustomInstallerLauncher.cs")));
        Assert.Contains(
            "WaitForUninstallCompletion(",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "packaging",
                    "CustomInstallerLauncher.cs")));
        Assert.Contains(
            "WaitForInstalledDirectory(",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "packaging",
                    "CustomInstallerLauncher.cs")));
        Assert.Contains(
            "parent.GetSubKeyNames()",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "packaging",
                    "CustomInstallerLauncher.cs")));
        Assert.Contains(
            "HasInstalledExecutable(",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "packaging",
                    "CustomInstallerLauncher.cs")));
        Assert.DoesNotContain(
            "--installto",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "packaging",
                    "CustomInstallerLauncher.cs")));
        Assert.Contains(
            "\"InstallLocation\"",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "packaging",
                    "CustomInstallerLauncher.cs")));
        Assert.Contains(
            "Arguments = \"--uninstall\"",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "packaging",
                    "CustomInstallerLauncher.cs")));
        Assert.Contains(
            "'FocusPanel-win-Setup.exe'",
            publisher);
        Assert.Contains(
            "uploads.github.com",
            publisher);
        Assert.Contains(
            "-InFile $installerPath",
            publisher);
        Assert.Contains(
            "Get-FileHash",
            publisher);
        Assert.Contains(
            "publishedInstaller.digest",
            publisher);
        Assert.Contains(
            "does not match the directory-aware installer",
            publisher);
        Assert.Contains(
            "repairing and verifying its assets",
            publisher);
        Assert.Contains(
            "$statusCode -ne 404",
            publisher);
        Assert.Contains(
            "'FocusPanel-win.msi'",
            publisher);
        Assert.Contains("'RELEASES'", publisher);
        Assert.Contains("-full\\.nupkg", publisher);
    }

    [Fact]
    public void ShellSurfaces_ShareConfiguredDisplayTarget()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));
        string indicator = File.ReadAllText(
            Path.Combine(root, "Views", "EdgeIndicatorWindow.xaml.cs"));
        string hotZone = File.ReadAllText(
            Path.Combine(root, "Services", "EdgeHotZoneMonitor.cs"));

        Assert.Contains("GetTargetDisplayBounds()", mainWindow);
        Assert.Contains("TargetValue", indicator);
        Assert.Contains("ShellDisplayTarget.GetBounds()", hotZone);
        Assert.Contains("DisplayTargetMode", mainWindow);
        Assert.Contains("RefreshDisplayBounds()", mainWindow);
        Assert.DoesNotContain("Screen.PrimaryScreen", mainWindow);
        Assert.DoesNotContain("Screen.PrimaryScreen", indicator);
        Assert.DoesNotContain("Screen.PrimaryScreen", hotZone);
        Assert.DoesNotContain(
            "DispatcherTimer",
            hotZone);
        Assert.Contains(
            "PeriodicTimer",
            hotZone);
        Assert.Contains(
            "DispatcherPriority.Input",
            hotZone);
        Assert.Contains(
            "GetTargetDpi(",
            mainWindow);
        Assert.Contains(
            "WmDpiChanged",
            mainWindow);
        Assert.Contains(
            "GetTargetDpi(",
            indicator);
    }

    [Fact]
    public void MainShell_PreparesDesktopOrganizerOutsideUiThread()
    {
        string root = FindRepositoryRoot();
        string mainViewModel = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));
        string factory = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "FileOrganizerViewModelFactory.cs"));

        Assert.Contains(
            "organizerFactory.CreateAsync",
            mainViewModel);
        Assert.Contains(
            "WorkspaceLoadApplyPolicy.CanApply",
            mainViewModel);
        Assert.DoesNotContain(
            "_fileOrganizerViewModel = new FileOrganizerViewModel()",
            mainViewModel);
        Assert.Contains("Task.Run", factory);
        Assert.Contains("new SettingsService()", factory);
        Assert.Contains("new FileOrganizerService()", factory);
    }

    [Fact]
    public void UpdateInstall_PreparesBackupOutsideUiThread()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));
        string mainViewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));
        string coordinator = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "UpdateInstallPreparationCoordinator.cs"));

        Assert.Contains(
            "await _updateInstallPreparation",
            mainWindow);
        Assert.Contains(
            "FocusDialogInteractionLease",
            mainWindow);
        Assert.DoesNotContain(
            "new DatabaseBackupService().PerformStartupBackup()",
            mainWindow);
        Assert.Contains(
            "public event Func<Task>? RequestApplyUpdate",
            mainViewModel);
        Assert.Contains(
            "await applyUpdate()",
            mainViewModel);
        Assert.Contains(
            "Task.Run",
            coordinator);
        Assert.Contains(
            "PerformStartupBackup",
            coordinator);
    }

    [Fact]
    public void MainShell_ReadsProtectedFileSettingInBackground()
    {
        string root = FindRepositoryRoot();
        string mainViewModel = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));

        Assert.Contains(
            "_protectedVisibilityRefresh.Request()",
            mainViewModel);
        Assert.Contains(
            "ApplyProtectedVisibilityAsync",
            mainViewModel);
        Assert.Contains(
            "_protectedVisibilityRefresh.Dispose()",
            mainViewModel);
        Assert.DoesNotContain(
            "ShowsProtectedSystemFiles = _desktopVisibility.ShowsProtectedSystemFiles",
            mainViewModel);
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
        int start = compactDock.IndexOf(
            "Command=\"{Binding OpenStartMenuCommand}\"",
            StringComparison.Ordinal);
        int search = compactDock.IndexOf(
            "x:Name=\"SearchButton\"",
            StringComparison.Ordinal);
        int applications = compactDock.IndexOf(
            "x:Name=\"TaskbarAppsScrollViewer\"",
            StringComparison.Ordinal);
        int taskView = compactDock.IndexOf(
            "Command=\"{Binding OpenTaskViewCommand}\"",
            StringComparison.Ordinal);
        int focusCenter = compactDock.IndexOf(
            "Click=\"FocusCenterButton_Click\"",
            StringComparison.Ordinal);
        int statusCenter = compactDock.IndexOf(
            "Click=\"StatusCenterButton_Click\"",
            StringComparison.Ordinal);
        int time = compactDock.IndexOf(
            "Click=\"CalendarPanelButton_Click\"",
            StringComparison.Ordinal);
        Assert.True(
            start >= 0
            && start < search
            && search < applications
            && applications < taskView
            && taskView < focusCenter
            && focusCenter < statusCenter
            && statusCenter < time);
        Assert.Contains("Click=\"FocusCenterButton_Click\"", compactDock);
        Assert.Contains("Click=\"StatusCenterButton_Click\"", compactDock);
        Assert.Contains(
            "x:Name=\"TaskViewButton\"",
            compactDock);
        Assert.Contains(
            "x:Name=\"StartButton\"",
            compactDock);
        Assert.Contains(
            "x:Name=\"FocusCenterButton\"",
            compactDock);
        Assert.Contains(
            "x:Name=\"StatusCenterButton\"",
            compactDock);
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
    public void TaskView_ProvidesVirtualDesktopWheelMenuAndCorrectFocusReturn()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));
        string contract = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "ISystemStatusService.cs"));
        string service = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "SystemStatusService.cs"));
        string theme = File.ReadAllText(
            Path.Combine(
                root,
                "Themes",
                "FocusTheme.xaml"));
        XDocument document =
            XDocument.Parse(mainWindow);
        XNamespace xaml =
            "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement NamedButton(
            string name) =>
            document.Descendants()
                .Single(
                    element =>
                        element.Name.LocalName
                            == "Button"
                        && (string?)element
                            .Attribute(
                                xaml + "Name")
                            == name);

        Assert.Contains(
            "PreviewMouseWheel=\"TaskViewButton_PreviewMouseWheel\"",
            mainWindow);
        Assert.Contains(
            "Header=\"上一个虚拟桌面\"",
            mainWindow);
        Assert.Contains(
            "Header=\"下一个虚拟桌面\"",
            mainWindow);
        Assert.Contains(
            "Header=\"新建虚拟桌面\"",
            mainWindow);
        Assert.Contains(
            "Header=\"关闭当前虚拟桌面\"",
            mainWindow);
        Assert.Contains(
            "VirtualDesktopWheelPolicy",
            codeBehind);
        Assert.Contains(
            ".GetAction(",
            codeBehind);
        Assert.Contains(
            "SwitchVirtualDesktopCommand",
            codeBehind);
        Assert.Matches(
            @"QueueOverlayFocus\(\s*StatusCenterButton,\s*StatusCenterQuickSettingsButton",
            codeBehind);
        Assert.Equal(
            "{Binding OpenStartMenuCommand}",
            (string?)NamedButton(
                    "StartButton")
                .Attribute("Command"));
        Assert.Equal(
            "FocusCenterButton_Click",
            (string?)NamedButton(
                    "FocusCenterButton")
                .Attribute("Click"));
        Assert.Equal(
            "StatusCenterButton_Click",
            (string?)NamedButton(
                    "StatusCenterButton")
                .Attribute("Click"));
        Assert.Contains(
            "SwitchVirtualDesktop(",
            viewModel);
        Assert.Contains(
            "CreateVirtualDesktop()",
            viewModel);
        Assert.Contains(
            "CloseCurrentVirtualDesktop()",
            viewModel);
        Assert.Contains(
            "SwitchVirtualDesktop(",
            contract);
        Assert.Contains(
            "VirtualDesktopCreate",
            service);
        Assert.Contains(
            "VirtualDesktopClose",
            service);
        Assert.Contains(
            "Text=\"{TemplateBinding InputGestureText}\"",
            theme);
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
        Assert.Contains("x:Name=\"TaskbarAppsItemsControl\"", mainWindow);
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
        Assert.Contains(
            "PreviewDragOver=\"TaskbarAppsHost_PreviewDragOver\"",
            mainWindow);
        Assert.Contains(
            "CompactTaskbarDragScrollPolicy",
            codeBehind);
        Assert.Contains(
            "TaskbarAppDropPolicy.GetPlacement",
            codeBehind);
        Assert.Contains(
            "ShowsDropBefore",
            mainWindow);
        Assert.Contains(
            "ShowsDropAfter",
            mainWindow);
        Assert.Contains(
            "ClearTaskbarDropCue();",
            codeBehind);
        Assert.Contains(
            "BeginTransientInteraction();",
            codeBehind);
        Assert.Matches(
            @"CompactTaskbarScrollPolicy\s*\.GetRevealOffset",
            codeBehind);
        Assert.Contains(
            "ActiveTaskbarIdentity",
            codeBehind);
        Assert.Contains(
            "DispatcherPriority.Render",
            codeBehind);
        Assert.Contains(
            "_viewModel.PropertyChanged +=",
            codeBehind);
        Assert.Contains(
            "_viewModel.PropertyChanged -=",
            codeBehind);
        Assert.Contains(
            "ActiveTaskbarIdentity =",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "ViewModels",
                    "MainViewModel.cs")));
    }

    [Fact]
    public void TaskbarApps_AcceptExplorerFilesWithoutHijackingReorder()
    {
        string root =
            FindRepositoryRoot();
        string mainWindow =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Views",
                    "MainWindow.xaml"));
        string codeBehind =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Views",
                    "MainWindow.xaml.cs"));
        string service =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Services",
                    "AppFileLaunchService.cs"));
        string coordinator =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Services",
                    "ShellCoordinator.cs"));

        Assert.Contains(
            "PreviewDragEnter=\"TaskbarAppsHost_PreviewDragEnter\"",
            mainWindow);
        Assert.Contains(
            "PreviewDrop=\"TaskbarAppsHost_PreviewDrop\"",
            mainWindow);
        Assert.Contains(
            "Binding=\"{Binding IsFileDropTarget}\"",
            mainWindow);
        Assert.Contains(
            "DataFormats.FileDrop",
            codeBehind);
        Assert.Contains(
            "typeof(TaskbarAppItem)",
            codeBehind);
        Assert.Contains(
            "StartTaskbarFileDrop(",
            codeBehind);
        Assert.Contains(
            "EndTaskbarExternalFileDrag",
            codeBehind);
        Assert.Contains(
            ".AppFiles",
            codeBehind);
        Assert.Contains(
            ".OpenAsync(",
            codeBehind);
        Assert.Contains(
            "IApplicationActivationManager",
            service);
        Assert.Contains(
            "ActivateForFile(",
            service);
        Assert.Contains(
            "SHCreateShellItemArrayFromIDLists",
            service);
        Assert.Contains(
            "IntPtr itemIdLists",
            service);
        Assert.DoesNotContain(
            "IReadOnlyList<IntPtr>",
            service);
        Assert.Contains(
            "TryBuildDesktopRequest(",
            service);
        Assert.DoesNotContain(
            "Shell_TrayWnd",
            service);
        Assert.Contains(
            "await AppFiles",
            coordinator);
        Assert.Contains(
            ".CompleteAsync()",
            coordinator);
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
        Assert.Contains(
            "Text=\"{Binding SummonShortcutText}\"",
            mainWindow);
        Assert.Contains(
            "ShellSummonHotkeyPolicy",
            codeBehind);
        Assert.Contains(
            "SetSummonShortcutStatus(",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "ViewModels",
                    "MainViewModel.cs")));
        Assert.DoesNotContain(
            "Text=\"主动唤出：Ctrl + Alt + Space\"",
            mainWindow);
    }

    [Fact]
    public void TaskbarSlots_RegisterOnlyForActiveReplacementAndExposeStatus()
    {
        string root =
            FindRepositoryRoot();
        string mainWindow =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Views",
                    "MainWindow.xaml"));
        string codeBehind =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Views",
                    "MainWindow.xaml.cs"));
        string viewModel =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "ViewModels",
                    "MainViewModel.cs"));

        Assert.Contains(
            "Text=\"{Binding TaskbarSlotShortcutText}\"",
            mainWindow);
        Assert.Contains(
            "IsChecked=\"{Binding EnableTaskbarSlotHotkeys}\"",
            mainWindow);
        Assert.Contains(
            "RegisterTaskbarSlotHotkeys();",
            codeBehind);
        Assert.Contains(
            "UnregisterTaskbarSlotHotkeys();",
            codeBehind);
        Assert.Contains(
            ".IsReplacementEnabled",
            codeBehind);
        Assert.Contains(
            "TaskbarSlotHotkeyPolicy",
            codeBehind);
        Assert.Contains(
            ".GetInvocation(",
            codeBehind);
        Assert.Contains(
            "ActivateTaskbarAppCommand",
            codeBehind);
        Assert.Contains(
            "LaunchNewTaskbarAppCommand",
            codeBehind);
        Assert.Contains(
            "SetTaskbarSlotShortcutStatus(",
            viewModel);
        Assert.Contains(
            "TaskbarSlotHotkeysKey",
            viewModel);
        Assert.Contains(
            "SetShortcutSlot(",
            viewModel);
    }

    [Fact]
    public void TaskbarContextMenu_LoadsFlatPublicJumpListOnDemand()
    {
        string root =
            FindRepositoryRoot();
        string mainWindow =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Views",
                    "MainWindow.xaml.cs"));
        string jumpLists =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Services",
                    "AppJumpListService.cs"));
        string coordinator =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Services",
                    "ShellCoordinator.cs"));

        Assert.Contains(
            "TryAddJumpListSection(",
            mainWindow);
        Assert.Contains(
            "LoadJumpListSectionAsync(",
            mainWindow);
        Assert.Contains(
            "menu.Items.Insert(",
            mainWindow);
        Assert.Contains(
            "CancelJumpListLoad();",
            mainWindow);
        Assert.Contains(
            "JumpListItem_Click",
            mainWindow);
        Assert.DoesNotContain(
            "recentRoot.Items.Add",
            mainWindow);
        Assert.Contains(
            "IApplicationDocumentLists",
            jumpLists);
        Assert.Contains(
            "SetAppId(",
            jumpLists);
        Assert.Contains(
            "GetList(",
            jumpLists);
        Assert.Contains(
            "IObjectArray",
            jumpLists);
        Assert.Contains(
            "IShellItem",
            jumpLists);
        Assert.Contains(
            "IShellLinkW",
            jumpLists);
        Assert.Contains(
            "JumpLists.Dispose();",
            coordinator);
    }

    [Fact]
    public void TransientPanels_MoveFocusInsideAndReturnItToTheirCompactEntry()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));

        Assert.Contains(
            "x:Name=\"FocusCenterButton\"",
            mainWindow);
        Assert.Contains(
            "x:Name=\"StatusCenterButton\"",
            mainWindow);
        Assert.Contains(
            "x:Name=\"TimeButton\"",
            mainWindow);
        Assert.Contains(
            "x:Name=\"FocusCenterLastWorkspaceButton\"",
            mainWindow);
        Assert.Contains(
            "x:Name=\"StatusCenterQuickSettingsButton\"",
            mainWindow);
        Assert.Contains(
            "x:Name=\"SettingsEnableReplacementButton\"",
            mainWindow);
        Assert.Contains(
            "x:Name=\"PowerMenuLockButton\"",
            mainWindow);
        Assert.Contains(
            "private FrameworkElement? _overlayReturnFocusTarget",
            codeBehind);
        Assert.Contains(
            "private void QueueOverlayFocus(",
            codeBehind);
        Assert.Contains(
            "FocusCenterLastWorkspaceButton",
            codeBehind);
        Assert.Contains(
            "StatusCenterQuickSettingsButton",
            codeBehind);
        Assert.Contains(
            "SettingsEnableReplacementButton",
            codeBehind);
        Assert.Contains(
            "PowerMenuLockButton",
            codeBehind);
        Assert.Contains(
            "FrameworkElement? returnTarget",
            codeBehind);
        Assert.Contains(
            "returnTarget.Focus();",
            codeBehind);
        Assert.DoesNotContain(
            "bool returnToSearch",
            codeBehind);
    }

    [Fact]
    public void TimeEntry_RightClickOpensOfficialDateAndNotificationSettings()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));

        int timeButtonStart = mainWindow.IndexOf(
            "x:Name=\"TimeButton\"",
            StringComparison.Ordinal);
        Assert.True(timeButtonStart >= 0);
        string timeButton = mainWindow[
            timeButtonStart..
            mainWindow.IndexOf(
                "</Button>",
                timeButtonStart,
                StringComparison.Ordinal)];

        Assert.Contains(
            "调整日期和时间",
            timeButton);
        Assert.Contains(
            "SystemManagementTool.DateAndTimeSettings",
            timeButton);
        Assert.Contains(
            "通知设置",
            timeButton);
        Assert.Contains(
            "SystemManagementTool.NotificationSettings",
            timeButton);
        Assert.Contains(
            "TransientContextMenu_Opened",
            timeButton);
        Assert.Contains(
            "按 Shift+F10",
            timeButton);
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
            "Text=\"{Binding WindowPreviewText}\"",
            mainWindow);
        Assert.Contains(
            "Text=\"{Binding WindowCountBadgeText}\"",
            mainWindow);
        Assert.Contains(
            "Visibility=\"{Binding HasMultipleWindows, Converter={StaticResource BooleanToVisibilityConverter}}\"",
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
        Assert.Contains(
            "public string WindowPreviewText",
            model);
        Assert.Contains(
            "public bool HasMultipleWindows",
            model);
        Assert.Contains(
            "WindowCount > 99",
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
            "AutoStartupCoordinator",
            viewModel);
        Assert.Contains(
            "LoadStartupStateAsync",
            viewModel);
        Assert.Contains(
            "_updatingStartupState",
            viewModel);
        Assert.Contains(
            "ApplyStartupPreferenceAsync",
            viewModel);
        Assert.Contains(
            "_autoStartup.CompleteAsync()",
            viewModel);
        Assert.DoesNotContain(
            "AutoStartupService.TrySetStartup",
            viewModel);
        Assert.DoesNotContain(
            "AutoStartupService.IsStartupEnabled()",
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
    public void FatalRecovery_NeverDeletesBusinessDatabaseOrRunsAssemblyDll()
    {
        string root = FindRepositoryRoot();
        string app = File.ReadAllText(
            Path.Combine(root, "App.xaml.cs"));

        Assert.Contains(
            "UnhandledExceptionRecoveryPolicy.CreateNotice",
            app);
        Assert.Contains(
            "e.Handled = true;",
            app);
        Assert.Contains(
            "Current.Shutdown(-1);",
            app);
        Assert.Contains(
            "DatabaseStartupRecoveryPolicy.Decide",
            app);
        Assert.DoesNotContain(
            "EnsureDeleted()",
            app);
        Assert.DoesNotContain(
            "ResourceAssembly.Location",
            app);
        Assert.DoesNotContain(
            "File.Delete(",
            app);
        Assert.DoesNotContain(
            "MessageBox.Show($\"Critical Error",
            app);
    }

    [Fact]
    public void ShellAutoHide_WaitsForMenusPopupsAndMouseCapture()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string mainWindowCode = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));
        string shellPreferences = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "ShellPreferenceRepository.cs"));
        string organizer = File.ReadAllText(
            Path.Combine(root, "Views", "FileOrganizerView.xaml"));

        Assert.Equal(
            5,
            Regex.Matches(mainWindow, "Opened=\"TransientContextMenu_Opened\"").Count);
        Assert.Equal(
            5,
            Regex.Matches(mainWindow, "Closed=\"TransientContextMenu_Closed\"").Count);
        Assert.Contains("Mouse.Captured != null", mainWindowCode);
        Assert.Contains("_transientInteractionDepth > 0", mainWindowCode);
        Assert.Contains("_viewModel.IsWorkspacePinned", mainWindowCode);
        Assert.Contains(
            "Command=\"{Binding ToggleWorkspacePinCommand}\"",
            mainWindow);
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding WorkspacePinActionText}\"",
            mainWindow);
        Assert.Contains(
            "_viewModel.IsWorkspacePinned = false;",
            mainWindowCode);
        Assert.Contains(
            "_viewModel.WorkspacePinChanged +=",
            mainWindowCode);
        Assert.Contains(
            "_viewModel.WorkspacePinChanged -=",
            mainWindowCode);
        Assert.Contains(
            "_autoHideTimer.Stop();",
            mainWindowCode);
        Assert.DoesNotContain(
            "WorkspacePinned",
            shellPreferences);
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
        Assert.Contains(
            "PreviewKeyDown=\"TaskbarApp_PreviewKeyDown\"",
            mainWindow);
        Assert.Contains("MouseButton.Middle", codeBehind);
        Assert.Contains("Keyboard.Modifiers", codeBehind);
        Assert.Contains(
            "MoveTaskbarAppUpCommand",
            codeBehind);
        Assert.Contains(
            "MoveTaskbarAppDownCommand",
            codeBehind);
        Assert.Contains(
            "InputGestureText = \"Alt+↑\"",
            codeBehind);
        Assert.Contains(
            "InputGestureText = \"Alt+↓\"",
            codeBehind);
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
        Assert.Contains("TrySetPinnedAsync(", viewModel);
        Assert.Contains(
            "TryMovePinnedRelativeAsync(",
            viewModel);
        Assert.Contains(
            "TryMovePinnedByOffsetAsync(",
            viewModel);
    }

    [Fact]
    public void MultiWindowLeftClick_OpensDirectWindowList()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml"));
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
            "PreviewMouseWheel=\"TaskbarApp_PreviewMouseWheel\"",
            xaml);
        Assert.Contains(
            "TaskbarWindowCyclePolicy.SelectTarget(",
            codeBehind);
        Assert.Contains(
            "InputGestureText =\n"
            + "                    \"中键 / Del 关闭\"",
            codeBehind.Replace(
                "\r\n",
                "\n"));
        Assert.Contains(
            "TaskbarWindowItem_PreviewMouseDown",
            codeBehind);
        Assert.Contains(
            "TaskbarWindowItem_PreviewKeyDown",
            codeBehind);
        Assert.Contains(
            "item.IsActive))",
            tracker);
        Assert.Contains(
            "left.IsActive == right.IsActive",
            synchronizer);
    }

    [Fact]
    public void TaskbarHover_UsesNoActivateDwmPreviewWithTextFallback()
    {
        string root =
            FindRepositoryRoot();
        string previewXaml =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Views",
                    "TaskbarWindowPreviewWindow.xaml"));
        string previewCode =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Views",
                    "TaskbarWindowPreviewWindow.xaml.cs"));
        string session =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Services",
                    "DwmThumbnailSession.cs"));
        string mainWindow =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Views",
                    "MainWindow.xaml.cs"));

        Assert.Contains(
            "ShowActivated=\"False\"",
            previewXaml);
        Assert.Contains(
            "AllowsTransparency=\"False\"",
            previewXaml);
        Assert.Contains(
            "FocusShellTintBrush",
            previewXaml);
        Assert.Contains(
            "ThumbnailSurface",
            previewXaml);
        Assert.Contains(
            "CloseWindowButton_Click",
            previewXaml);
        Assert.Contains(
            "MaximumPreviewCount = 4",
            previewCode);
        Assert.Contains(
            "WsExNoActivate",
            previewCode);
        Assert.Contains(
            "WindowBackdropService.Apply(this)",
            previewCode);
        Assert.Contains(
            "DwmRegisterThumbnail",
            session);
        Assert.Contains(
            "DwmUpdateThumbnailProperties",
            session);
        Assert.Contains(
            "DwmUnregisterThumbnail",
            session);
        Assert.Contains(
            "if (TryOpenTaskbarWindowPreview(",
            mainWindow);
        Assert.Contains(
            "PopulateTaskbarWindowList(",
            mainWindow);
        Assert.Contains(
            "_taskbarWindowPreview?.IsMouseOver",
            mainWindow);
        Assert.Contains(
            "_taskbarWindowPreview.Close();",
            mainWindow);
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
                @"<controls:AppIconPresenter(?:\s|>)").Count);
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
        string mainWindowCode = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));
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
        string searchPolicy = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "AppSearchPolicy.cs"));
        string shellSearchPolicy = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "ShellSearchPolicy.cs"));
        string systemSearchCatalog = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "SystemManagementSearchCatalog.cs"));
        string shellSearchCatalog = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "WindowsShellSearchCatalog.cs"));
        string searchResult = File.ReadAllText(
            Path.Combine(
                root,
                "Models",
                "ShellSearchResult.cs"));
        string expressionEvaluator = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "SafeExpressionEvaluator.cs"));
        string clipboardService = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "ClipboardTextService.cs"));
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
            "没有找到匹配的应用、窗口、命令或快捷结果",
            viewModel);
        Assert.Contains(
            "Name = \"FocusPanel.AppCatalog\"",
            catalog);
        Assert.Contains(
            "Name = \"FocusPanel.AppIcons\"",
            catalog);
        Assert.Contains(
            "AppSearchPolicy.Search(",
            catalog);
        Assert.Contains(
            "GetExecutableName(",
            searchPolicy);
        Assert.Contains(
            "display.Acronym.StartsWith(",
            searchPolicy);
        Assert.Contains(
            "ThenByDescending(result =>",
            searchPolicy);
        Assert.Contains(
            "ShellSearchPolicy.Compose(",
            viewModel);
        Assert.Contains(
            "window.Title",
            shellSearchPolicy);
        Assert.Contains(
            "window.IsActive",
            shellSearchPolicy);
        Assert.Contains(
            "SystemManagementSearchCatalog",
            shellSearchPolicy);
        Assert.Contains(
            "任务管理器",
            systemSearchCatalog);
        Assert.Contains(
            "设备管理器",
            systemSearchCatalog);
        Assert.Contains(
            "TerminalAdministrator",
            systemSearchCatalog);
        Assert.Contains(
            "ShellSearchResultKind.SystemCommand",
            searchResult);
        Assert.Contains(
            "WindowsShellSearchCatalog",
            shellSearchPolicy);
        Assert.Contains(
            "WindowsShellAction.RunDialog",
            shellSearchCatalog);
        Assert.Contains(
            "WindowsShellAction.ShowDesktop",
            shellSearchCatalog);
        Assert.DoesNotContain(
            "VirtualDesktopClose",
            shellSearchCatalog);
        Assert.Contains(
            "result?.ManagementTool",
            viewModel);
        Assert.Contains(
            "result?.ShellAction",
            viewModel);
        Assert.Contains(
            "ExecuteShellSearchActionAsync(",
            viewModel);
        Assert.Contains(
            "_systemStatus.OpenRunDialog",
            viewModel);
        Assert.Contains(
            "_systemStatus.ShowDesktop",
            viewModel);
        Assert.Contains(
            "_systemStatus",
            viewModel);
        Assert.Contains(
            ".OpenManagementTool(",
            viewModel);
        Assert.Contains(
            "SafeExpressionEvaluator",
            shellSearchPolicy);
        Assert.Contains(
            "AudioSearchCommandParser",
            shellSearchPolicy);
        Assert.Contains(
            "PomodoroSearchCommandParser",
            shellSearchPolicy);
        Assert.Contains(
            "TaskCaptureCommandParser",
            shellSearchPolicy);
        Assert.Contains(
            "ShellSearchResultKind.Calculation",
            searchResult);
        Assert.Contains(
            "ShellSearchResultKind.AudioCommand",
            searchResult);
        Assert.Contains(
            "ShellSearchResultKind.FocusCommand",
            searchResult);
        Assert.Contains(
            "ShellSearchResultKind.TaskCapture",
            searchResult);
        Assert.Contains(
            "MaximumExpressionLength",
            expressionEvaluator);
        Assert.DoesNotContain(
            "DataTable",
            expressionEvaluator);
        Assert.DoesNotContain(
            "Process.Start",
            expressionEvaluator);
        Assert.Contains(
            "Clipboard.SetDataObject(",
            clipboardService);
        Assert.Contains(
            "TrySetTextAsync(",
            viewModel);
        Assert.Contains(
            "ExecuteAudioSearchCommand(",
            viewModel);
        Assert.Contains(
            "command.Resolve(",
            viewModel);
        Assert.Contains(
            "command.RequiresCurrentVolume",
            viewModel);
        Assert.Contains(
            "当前音量尚未读取完成",
            viewModel);
        Assert.Contains(
            "ExecuteFocusSearchCommand(",
            viewModel);
        Assert.Contains(
            "TryStartQuickSession(",
            viewModel);
        Assert.Contains(
            "ExecuteTaskCaptureCommandAsync(",
            viewModel);
        Assert.Contains(
            "new TaskQuickCaptureCoordinator(",
            viewModel);
        Assert.Contains(
            "new TasksViewModel(",
            viewModel);
        Assert.True(
            Regex.Matches(
                viewModel,
                @"_taskService").Count
            >= 4);
        Assert.Contains(
            "TaskCaptured?.Invoke(",
            viewModel);
        Assert.Contains(
            "result.Item.Id,",
            viewModel);
        Assert.Contains(
            "_viewModel.TaskCaptured +=",
            mainWindowCode);
        Assert.Contains(
            "_viewModel.TaskCaptured -=",
            mainWindowCode);
        Assert.Contains(
            "ViewModel_TaskCaptured(",
            mainWindowCode);
        Assert.Contains(
            "\"已收集到 Inbox\"",
            mainWindowCode);
        Assert.Contains(
            "$\"task-captured:{taskId}\"",
            mainWindowCode);
        Assert.Contains(
            "ShowActivated=\"False\"",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Views",
                    "PomodoroFloatingWindow.xaml")));
    }

    [Fact]
    public void AppSearch_ProvidesCompleteKeyboardLaunchPath()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));
        string theme = File.ReadAllText(
            Path.Combine(
                root,
                "Themes",
                "FocusTheme.xaml"));
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
            "使用上下方向键选择，按回车执行",
            mainWindow);
        Assert.Contains(
            "AppSearchSelectionPolicy.Move(",
            codeBehind);
        Assert.Contains(
            "_viewModel.ExecuteSearchResultCommand.Execute(result);",
            codeBehind);
        Assert.Contains(
            "SearchBox.SelectAll();",
            codeBehind);
        Assert.Matches(
            @"_overlayReturnFocusTarget\s*=\s*SearchButton;",
            codeBehind);
        Assert.Contains(
            "returnTarget.Focus();",
            codeBehind);
        Assert.Contains(
            "SelectedSearchResult?.StableKey",
            viewModel);
        Assert.Contains(
            "AutomationProperties.Name=\"应用、窗口、系统命令、任务与计算结果\"",
            mainWindow);
        Assert.Contains(
            "Binding=\"{Binding UsesGlyph}\"",
            mainWindow);
        Assert.Contains(
            "Style=\"{StaticResource FocusIconText}\"",
            mainWindow);
        Assert.Contains(
            "Command=\"{Binding DataContext.ExecuteSearchResultCommand",
            mainWindow);
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
            "<Trigger Property=\"IsSelected\"",
            theme);
        Assert.Contains(
            "Value=\"True\">",
            theme);
        Assert.Contains(
            "Value=\"{DynamicResource FocusAccentSoftBrush}\"",
            theme);
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
        string mainWindow = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml"));

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
            "x:Key=\"FocusPopupSurfaceBrush\"",
            theme);
        Assert.True(
            Regex.Matches(
                theme,
                "Background=\"\\{DynamicResource FocusPopupSurfaceBrush\\}\"").Count
            >= 3);
        Assert.Contains(
            "ContextMenu menu = CreateTaskbarContextMenu();",
            codeBehind);
        Assert.Contains(
            "e.Handled = true;",
            codeBehind);
        Assert.Contains(
            "DispatcherPriority.Input",
            codeBehind);
        Assert.Contains(
            "menu.Items.Add(new MenuItem",
            codeBehind);
        Assert.Equal(
            5,
            Regex.Matches(
                mainWindow,
                "ContextMenu Style=\"\\{StaticResource FocusContextMenu\\}\"").Count);
        Assert.Contains(
            "FocusMenuTheme.Apply(menu);",
            codeBehind);
        string menuTheme = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "FocusMenuTheme.cs"));
        Assert.Contains(
            "menu.Style = contextMenuStyle;",
            menuTheme);
        Assert.Contains(
            "menuItem.Style = menuItemStyle;",
            menuTheme);
        Assert.Contains(
            "separator.Style = separatorStyle;",
            menuTheme);
        Assert.Contains(
            "menu.SetResourceReference(",
            menuTheme);
        Assert.Contains(
            "menuItem.SetResourceReference(",
            menuTheme);
        Assert.Contains(
            "header.SetResourceReference(",
            menuTheme);
        Assert.Contains(
            "menu.Resources[typeof(MenuItem)] = menuItemStyle;",
            menuTheme);
        Assert.Contains(
            "menu.Resources[SystemColors.MenuBrushKey] = surface;",
            menuTheme);
        Assert.Contains(
            "menu.Resources[SystemColors.HighlightBrushKey] = selection;",
            menuTheme);
        Assert.Contains(
            "menu.Resources[SystemColors.HighlightTextBrushKey] = text;",
            menuTheme);
        Assert.Contains(
            "if (sender is ContextMenu menu)",
            codeBehind);
        Assert.Contains(
            "FocusMenuTheme.Apply(button.ContextMenu);",
            codeBehind);
        Assert.Contains(
            "TextElement.Foreground=\"{TemplateBinding Foreground}\"",
            theme);
        Assert.True(
            Regex.Matches(
                theme,
                "Value=\"\\{DynamicResource FocusAccentSoftBrush\\}\"").Count
            >= 3);
        Assert.Contains(
            "<Setter Property=\"FontFamily\" Value=\"Segoe UI Variable Text\"/>",
            theme);
    }

    [Fact]
    public void DesktopFileContextMenu_TracksTheCurrentCardWithoutSharingState()
    {
        string root = FindRepositoryRoot();
        string organizer = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "FileOrganizerView.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "FileOrganizerView.xaml.cs"));

        Assert.Contains(
            "x:Key=\"FileContextMenu\"",
            organizer);
        Assert.Contains(
            "x:Shared=\"False\"",
            organizer);
        Assert.Contains(
            "Style=\"{StaticResource FocusContextMenu}\"",
            organizer);
        Assert.Contains(
            "DataContext=\"{Binding PlacementTarget.DataContext, RelativeSource={RelativeSource Self}}\"",
            organizer);
        Assert.Equal(
            2,
            Regex.Matches(
                organizer,
                "ContextMenu=\"\\{StaticResource FileContextMenu\\}\"").Count);
        Assert.Contains(
            "PlacementTarget:",
            codeBehind);
        Assert.Contains(
            "FrameworkElement",
            codeBehind);
        Assert.Contains(
            "DataContext:",
            codeBehind);
        Assert.Contains(
            "DesktopFile file",
            codeBehind);
        Assert.Contains(
            "viewModel.SelectFileCommand.Execute(file);",
            codeBehind);
        Assert.Contains(
            "BeginTransientSurface();",
            codeBehind);
    }

    [Fact]
    public void OrganizerPopups_AreExclusiveAndReturnKeyboardFocusOnEscape()
    {
        string root = FindRepositoryRoot();
        string organizer = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "FileOrganizerView.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "FileOrganizerView.xaml.cs"));

        Assert.Equal(
            4,
            Regex.Matches(
                organizer,
                "PreviewKeyDown=\"TransientPopup_PreviewKeyDown\"").Count);
        Assert.Equal(
            4,
            Regex.Matches(
                organizer,
                "Opened=\"TransientPopup_Opened\"").Count);
        Assert.Equal(
            4,
            Regex.Matches(
                organizer,
                "Closed=\"TransientPopup_Closed\"").Count);
        Assert.Contains(
            "ExclusiveSurfaceTracker<Popup>",
            codeBehind);
        Assert.Contains(
            "_transientPopups.Activate(popup)",
            codeBehind);
        Assert.Contains(
            "previous.IsOpen = false;",
            codeBehind);
        Assert.Contains(
            "_transientPopups.Deactivate(popup)",
            codeBehind);
        Assert.Contains(
            "_transientPopups.Clear()",
            codeBehind);
        Assert.Contains(
            "FocusNavigationDirection.First",
            codeBehind);
        Assert.Contains(
            "if (e.Key != Key.Escape",
            codeBehind);
        Assert.Contains(
            "popup.IsOpen = false;",
            codeBehind);
        Assert.Contains(
            "Keyboard.Focus(placementTarget)",
            codeBehind);
        Assert.Contains(
            "CloseActiveTransientPopup(false);",
            codeBehind);
        Assert.Contains(
            "e.Handled = true;",
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
    public void ListBoxes_UseOneReadableDynamicSelectionTheme()
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
        string tasks = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "TasksView.xaml"));
        int listItemStart = theme.IndexOf(
            "x:Key=\"FocusListBoxItem\"",
            StringComparison.Ordinal);
        int listStart = theme.IndexOf(
            "x:Key=\"FocusListBox\"",
            listItemStart + 1,
            StringComparison.Ordinal);
        Assert.True(
            listItemStart >= 0
            && listStart > listItemStart);
        string listItemTheme = theme[
            listItemStart..listStart];

        Assert.Contains(
            "x:Key=\"FocusListBox\"",
            theme);
        Assert.Contains(
            "x:Key=\"FocusListBoxItem\"",
            theme);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusListBox}\"",
            theme);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusListBoxItem}\"",
            theme);
        Assert.Contains(
            "x:Name=\"ListItemChrome\"",
            theme);
        Assert.Contains(
            "Value=\"{DynamicResource FocusAccentSoftBrush}\"",
            theme);
        Assert.Contains(
            "Value=\"{DynamicResource FocusAccentBrush}\"",
            theme);
        Assert.Contains(
            "Value=\"{DynamicResource FocusTextBrush}\"",
            listItemTheme);
        Assert.Matches(
            "<Setter Property=\"FocusVisualStyle\""
            + "\\s+Value=\"\\{x:Null\\}\"/>",
            listItemTheme);
        Assert.Contains(
            "Style=\"{StaticResource FocusListBox}\"",
            mainWindow);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusListBoxItem}\"",
            mainWindow);
        Assert.Contains(
            "Style=\"{StaticResource FocusListBox}\"",
            tasks);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusListBoxItem}\"",
            tasks);
        Assert.DoesNotContain(
            "x:Name=\"ItemChrome\"",
            mainWindow);
        Assert.DoesNotContain(
            "x:Name=\"Chrome\"",
            tasks);
    }

    [Fact]
    public void Typography_UsesOneSemanticHierarchyAcrossPrimarySurfaces()
    {
        string root = FindRepositoryRoot();
        string theme = File.ReadAllText(
            Path.Combine(
                root,
                "Themes",
                "FocusTheme.xaml"));
        string viewsRoot = Path.Combine(
            root,
            "Views");
        string[] primaryViews =
        {
            "MainWindow.xaml",
            "DashboardView.xaml",
            "FileOrganizerView.xaml",
            "TasksView.xaml",
            "PomodoroView.xaml",
            "OkrView.xaml",
            "AIAssistantView.xaml",
            "CalendarPanelView.xaml",
            "TaskDetailWindow.xaml",
            "PomodoroFloatingWindow.xaml"
        };
        string primaryContent = string.Join(
            Environment.NewLine,
            primaryViews.Select(
                file => File.ReadAllText(
                    Path.Combine(
                        viewsRoot,
                        file))));
        string[] typographyKeys =
        {
            "FocusTextBase",
            "FocusPageTitleText",
            "FocusSectionTitleText",
            "FocusEmptyStateTitleText",
            "FocusCardTitleText",
            "FocusBodyText",
            "FocusSecondaryBodyText",
            "FocusCaptionText",
            "FocusMetaText",
            "FocusMetricText",
            "FocusDisplayText"
        };

        foreach (string key in typographyKeys)
        {
            Assert.Contains(
                $"x:Key=\"{key}\"",
                theme);
        }

        Assert.Contains(
            "Style=\"{StaticResource FocusPageTitleText}\"",
            primaryContent);
        Assert.Contains(
            "Style=\"{StaticResource FocusSectionTitleText}\"",
            primaryContent);
        Assert.Contains(
            "Style=\"{StaticResource FocusCardTitleText}\"",
            primaryContent);
        Assert.Contains(
            "Style=\"{StaticResource FocusBodyText}\"",
            primaryContent);
        Assert.Contains(
            "Style=\"{StaticResource FocusCaptionText}\"",
            primaryContent);
        Assert.Contains(
            "Style=\"{StaticResource FocusMetaText}\"",
            primaryContent);
        Assert.Contains(
            "Style=\"{StaticResource FocusMetricText}\"",
            primaryContent);
        Assert.Contains(
            "Style=\"{StaticResource FocusDisplayText}\"",
            primaryContent);

        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var directTextSizes = new List<string>();
        foreach (string path in Directory.GetFiles(
                     viewsRoot,
                     "*.xaml"))
        {
            XDocument document = XDocument.Load(path);
            foreach (XElement textBlock in document
                         .Descendants(
                             presentation
                             + "TextBlock")
                         .Where(element =>
                             element.Attribute(
                                 "FontSize") != null))
            {
                bool isIcon =
                    ((string?)textBlock.Attribute(
                        "Style"))?.Contains(
                        "FocusIconText",
                        StringComparison.Ordinal)
                    == true
                    || textBlock
                        .Descendants(
                            presentation
                            + "Style")
                        .Any(style =>
                            ((string?)style.Attribute(
                                "BasedOn"))?.Contains(
                                "FocusIconText",
                                StringComparison.Ordinal)
                            == true);
                if (!isIcon)
                {
                    directTextSizes.Add(
                        $"{Path.GetFileName(path)}:"
                        + $"{(string?)textBlock.Attribute("Text")}"
                        + $"={textBlock.Attribute("FontSize")?.Value}");
                }
            }
        }

        Assert.True(
            directTextSizes.Count <= 3,
            "仍有非图标文字绕过语义字体层级："
            + string.Join(
                ", ",
                directTextSizes));
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
    public void ToggleAndSegmentSelections_UseDynamicAccentStates()
    {
        string root = FindRepositoryRoot();
        string theme = File.ReadAllText(
            Path.Combine(
                root,
                "Themes",
                "FocusTheme.xaml"));
        string themeService = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "ThemeService.cs"));
        string organizer = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "FileOrganizerView.xaml"));

        Assert.Contains(
            "x:Key=\"FocusAccentSoftBrush\"",
            theme);
        Assert.Contains(
            "SetBrush(\"FocusAccentSoftBrush\", accentSoft)",
            themeService);
        Assert.Contains(
            "x:Key=\"FocusToggleButton\"",
            theme);
        Assert.Contains(
            "x:Name=\"ToggleChrome\"",
            theme);
        Assert.Contains(
            "x:Name=\"SegmentChrome\"",
            theme);
        Assert.Contains(
            "x:Name=\"SegmentSelectionIndicator\"",
            theme);
        Assert.Contains(
            "Value=\"{DynamicResource FocusAccentSoftBrush}\"",
            theme);
        Assert.Contains(
            "BasedOn=\"{StaticResource FocusToggleButton}\"",
            organizer);
        Assert.Contains(
            "<Setter Property=\"MinWidth\" Value=\"0\"/>",
            organizer);
        Assert.Contains(
            "<Setter Property=\"MinHeight\" Value=\"0\"/>",
            organizer);
        Assert.Equal(
            5,
            Regex.Matches(
                organizer,
                "Style=\"\\{StaticResource ToolbarToggleButtonStyle\\}\"").Count);
        Assert.DoesNotContain(
            "OrganizerSelectedBrush",
            organizer);
        Assert.DoesNotContain(
            "#FF007AFF",
            organizer);
        Assert.True(
            Regex.Matches(
                organizer,
                "FocusAccentSoftBrush").Count >= 2);
        Assert.True(
            Regex.Matches(
                organizer,
                "FocusAccentBrightBrush").Count >= 2);
    }

    [Fact]
    public void Views_DoNotBypassDynamicThemeWithLiteralColors()
    {
        string root = FindRepositoryRoot();
        string theme = File.ReadAllText(
            Path.Combine(
                root,
                "Themes",
                "FocusTheme.xaml"));
        string themeService = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "ThemeService.cs"));
        string views = string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    Path.Combine(
                        root,
                        "Views"),
                    "*.xaml")
                .Select(File.ReadAllText));

        Assert.Contains(
            "x:Key=\"FocusWarningBrush\"",
            theme);
        Assert.Contains(
            "x:Key=\"FocusWarningSoftBrush\"",
            theme);
        Assert.Contains(
            "x:Key=\"FocusWarningTextBrush\"",
            theme);
        Assert.Contains(
            "x:Key=\"FocusOverlayBrush\"",
            theme);
        Assert.Contains(
            "x:Key=\"FocusEdgeIndicatorBrush\"",
            theme);
        Assert.Contains(
            "SetBrush(\"FocusWarningBrush\"",
            themeService);
        Assert.Contains(
            "SetBrush(\"FocusOverlayBrush\"",
            themeService);
        Assert.Contains(
            "SetBrush(\"FocusEdgeIndicatorBrush\"",
            themeService);
        Assert.Contains(
            "Background=\"{DynamicResource FocusEdgeIndicatorBrush}\"",
            views);
        Assert.Contains(
            "Background=\"{DynamicResource FocusOverlayBrush}\"",
            views);
        Assert.Contains(
            "Background=\"{DynamicResource FocusWarningSoftBrush}\"",
            views);
        Assert.Contains(
            "Foreground=\"{DynamicResource FocusWarningTextBrush}\"",
            views);
        Assert.Contains(
            "BorderBrush=\"{DynamicResource FocusWarningBrush}\"",
            views);
        Assert.Empty(
            Regex.Matches(
                views,
                "#[0-9A-Fa-f]{6,8}")
                .Cast<Match>());
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
            "e.ChangeType == WatcherChangeTypes.Created",
            service);
        Assert.Contains(
            "RefreshChangedPaths(batch.Paths)",
            service);
        Assert.Contains(
            "Func<IReadOnlyList<string>, Task>?",
            service);
        Assert.Contains(
            "NotifyDesktopItemsCreatedAsync(",
            service);
        Assert.Contains(
            "_refreshGate.Release();",
            service);
        Assert.DoesNotContain(
            "private async void FileService_DesktopItemsCreated",
            viewModel);
        Assert.Contains(
            "private Task FileService_DesktopItemsCreated",
            viewModel);
        Assert.Contains(
            "_organizeOperationTracker.TryStart(",
            viewModel);
        Assert.Contains(
            "_organizeOperationTracker",
            viewModel);
        Assert.Contains(
            "SafeDispatcherProgress<",
            viewModel);
        Assert.Contains(
            "NotifyFilesChanged()",
            service);
        Assert.DoesNotContain(
            "FilesChanged?.Invoke()",
            service);
        Assert.Contains(
            "_pendingChanges.RenamePath(",
            service);
        Assert.Contains(
            "_createdPathSuppression.Suppress(",
            service);
        Assert.Contains(
            "_createdPathSuppression.TryConsume(",
            service);
        Assert.Contains(
            "await _fileService.OrganizeFiles(",
            viewModel);
        Assert.Contains(
            "AutoOrganizeStatus",
            viewModel);
        Assert.Contains(
            "NotifyCanExecuteChangedFor(",
            viewModel);
        Assert.Contains(
            "CreateOrganizeProgress(",
            viewModel);
        Assert.Contains(
            "OrganizeProgressMaximum",
            viewModel);
        Assert.Contains(
            "Maximum=\"{Binding OrganizeProgressMaximum}\"",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Views",
                    "FileOrganizerView.xaml")));
        Assert.Contains(
            "Visibility=\"{Binding IsOrganizing",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Views",
                    "FileOrganizerView.xaml")));
        Assert.DoesNotContain(
            "AutoOrganizeIfEnabled",
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
            "CoalescingBackgroundRefresh<",
            viewModel);
        Assert.Contains(
            "CoalescingAsyncSaveQueue<",
            viewModel);
        Assert.Contains(
            "Task.Run(",
            viewModel);
        Assert.Contains(
            "_sessionSaveQueue.CompleteAsync()",
            viewModel);
        Assert.Contains(
            "SessionPersisted?.Invoke",
            viewModel);
        Assert.DoesNotContain(
            "new AppDbContext()",
            viewModel);
        Assert.DoesNotContain(
            "SaveChanges()",
            viewModel);
        Assert.Contains(
            "PomodoroCompleted?.Invoke",
            mainViewModel);
        Assert.Contains(
            "PomodoroViewModel_SessionPersisted",
            mainViewModel);
        Assert.Contains(
            "ViewModel_PomodoroCompleted",
            mainWindow);
        Assert.Contains(
            "SystemSounds.Asterisk.Play()",
            mainWindow);
        Assert.Contains(
            "_toastManager.Enqueue",
            mainWindow);
        Assert.DoesNotContain(
            "ShowBalloonTip",
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
        string workspaceRepository = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "OkrWorkspaceRepository.cs"));
        string dataProvider = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "IOkrDataProvider.cs"));

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
            "stored.Progress = source.Progress",
            workspaceRepository);
        Assert.Contains(
            "CalculateObjectiveProgress",
            viewModel);
        Assert.Contains(
            "!result.IsDeleted",
            workspaceRepository);
        Assert.Contains(
            "DispatchToUi",
            viewModel);
        Assert.Contains(
            "CoalescingBackgroundRefresh",
            viewModel);
        Assert.Contains(
            "OkrWorkspaceApplyPolicy.CanApply",
            viewModel);
        Assert.Contains(
            "Task.Run(",
            viewModel);
        Assert.Contains(
            "_workspaceRefresh.Dispose()",
            viewModel);
        Assert.DoesNotContain(
            "Dispatcher.Invoke(",
            viewModel);
        Assert.DoesNotContain(
            ".Wait()",
            viewModel);
        Assert.DoesNotContain(
            ".Result",
            viewModel);
        Assert.Contains(
            "CreateDraftFromAIAsync",
            dataProvider);
        Assert.Contains(
            "TriggerSyncAsync",
            dataProvider);
        Assert.Contains(
            "IOkrDataProvider, IDisposable",
            viewModel);
        Assert.Contains(
            "_syncService.ProgressChanged -= OnSyncProgress",
            viewModel);
        Assert.DoesNotContain(
            "new AppDbContext",
            viewModel);
        Assert.DoesNotContain(
            "EnsureSchema",
            viewModel);
        Assert.Contains(
            "AddObjectiveAsync",
            workspaceRepository);
        Assert.Contains(
            "AddKeyResultAsync",
            workspaceRepository);
        Assert.Contains(
            "SemaphoreSlim",
            workspaceRepository);
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
        string organizerViewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "FileOrganizerViewModel.cs"));
        string organizerService = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "FileOrganizerService.cs"));

        Assert.Contains("<Grid Background=\"Transparent\">", organizer);
        Assert.Contains("IsAutoOrganizeEnabled", organizer);
        Assert.Contains("AutoOrganizeStatus", organizer);
        Assert.Contains(
            "仅处理开启后新增到桌面根目录的项目",
            organizer);
        Assert.DoesNotContain("DropShadowEffect", organizer);
        Assert.DoesNotContain("OrganizerCardShadow", organizer);
        Assert.DoesNotContain("ToggleDesktopCommand", organizer);
        Assert.DoesNotContain(
            "ToggleDesktop",
            organizerViewModel);
        Assert.DoesNotContain(
            "ToggleDesktopIcons",
            organizerService);
    }

    [Fact]
    public void TaskbarExclusiveMode_SuppressesNativeSurfaceOnceAndGuardRemainsReadOnly()
    {
        string root = FindRepositoryRoot();
        string controller = File.ReadAllText(
            Path.Combine(root, "Services", "TaskbarController.cs"));
        string onboarding = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));

        Assert.Contains("AbsAutoHide", controller);
        Assert.Contains("UsesNativeAutoHide = false", controller);
        Assert.Contains(
            "UsesEmptyWindowRegion = true",
            controller);
        Assert.Contains("SetTaskbarVisible(taskbar, false)", controller);
        Assert.Contains(
            "SetTaskbarSurfaceSuppressed(",
            controller);
        Assert.Contains(
            "CreateRectRgn(",
            controller);
        Assert.Contains(
            "SetWindowRgn(",
            controller);
        Assert.Contains("ValidateReplacement()", controller);
        Assert.Contains(
            "_native.SetWorkArea(_state.PrimaryBounds)",
            controller);
        string guard = controller[
            controller.IndexOf(
                "private void GuardReplacementSafely",
                StringComparison.Ordinal)..];
        Assert.DoesNotContain(
            "_native.SetWorkArea(",
            guard);
        Assert.DoesNotContain(
            "_native.SetTaskbarSurfaceSuppressed(",
            guard);
        Assert.DoesNotContain(
            "ApplyReplacement();",
            guard);
        Assert.Contains(
            "守护器只验证状态，不循环隐藏或反复改写工作区",
            onboarding);
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
        Assert.Contains("private void Partition_Drop", codeBehind);
        Assert.Contains(
            "AsyncInteractionRunner.Start(",
            codeBehind);
        Assert.Contains(
            "BeginTransientSurface();",
            codeBehind);
        Assert.Contains(
            "EndTransientSurface();",
            codeBehind);
        Assert.Contains(
            "shell?.EndDesktopFileDrag();",
            codeBehind);
        Assert.Contains(
            "EndExternalDesktopFileDrag();",
            codeBehind);
        Assert.DoesNotContain(
            "private async void Partition_Drop",
            codeBehind);
        Assert.DoesNotContain(
            "private async void FileCard_MouseMove",
            codeBehind);
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
            "RequestSystemStatusRefresh();",
            viewModel);
        Assert.Contains(
            "CoalescingBackgroundRefresh<",
            viewModel);
        Assert.Contains(
            "_systemStatus.GetStatusSnapshot()",
            viewModel);
        Assert.Contains(
            "DispatcherPriority.Background",
            viewModel);
        Assert.Contains(
            "Interlocked.Increment(",
            viewModel);
        Assert.Contains(
            "_systemStatusRefresh.Dispose();",
            viewModel);
        Assert.Contains(
            "_taskSummaryRefresh.Dispose();",
            viewModel);
        Assert.Contains(
            "RequestTaskSummaryRefresh();",
            viewModel);
        Assert.Contains(
            "_taskSummaryReader.Read(month)",
            viewModel);
        Assert.Contains(
            "TaskSummaryApplyPolicy.GetDecision(",
            viewModel);
        Assert.DoesNotContain(
            "private void RefreshTaskSummary()",
            viewModel);
        Assert.DoesNotContain(
            "context.PomodoroSessions",
            viewModel);
        Assert.DoesNotContain(
            "_systemStatus.GetNetworkStatus()",
            viewModel);
        Assert.Contains("_windowTracker.SetTrackingActive(isVisible)", viewModel);
        Assert.Contains("ShellRefreshActivityPolicy.GetActivity", viewModel);
        Assert.Contains(
            "if (_trackingActive && !_disposed)",
            tracker);
        Assert.Contains(
            "WindowTrackingActivityPolicy.ShouldProcessWindowEvent",
            tracker);
        Assert.Contains(
            "_snapshotStore.TryRefresh(",
            tracker);
        Assert.Contains(
            "keeping the last valid snapshot",
            tracker);
        Assert.Contains(
            "EventSubscriberIsolation.Publish(",
            tracker);
        Assert.Contains(
            "HasShutdownStarted",
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
        string coordinator = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "AppLaunchCoordinator.cs"));
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
            "TryLaunchAppAsync",
            viewModel);
        Assert.True(
            viewModel.Split(
                "[RelayCommand(AllowConcurrentExecutions = true)]",
                StringSplitOptions.None).Length - 1
            >= 3);
        Assert.Contains(
            "Task.Run(",
            coordinator);
        Assert.Contains(
            "CaptureLaunch",
            coordinator);
        Assert.Contains(
            "IsCurrent",
            coordinator);
        Assert.DoesNotContain(
            "_appCatalog.Launch(app)",
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
        string codeBehind = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));

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
            "Task<bool> SetPinnedAsync(",
            appContract);
        Assert.Contains(
            "Task<bool> MovePinnedAsync(",
            appContract);
        Assert.Contains(
            "Task<bool> MovePinnedRelativeAsync(",
            appContract);
        Assert.Contains(
            "Task<bool> MovePinnedByOffsetAsync(",
            appContract);
        Assert.Contains(
            "GetPinnedEntitySnapshot()",
            catalog);
        Assert.Contains(
            "TryReplacePinnedCache(",
            catalog);
        Assert.Contains(
            "ReplacePinnedCache(",
            catalog);
        int getPinnedStart = catalog.IndexOf(
            "public IReadOnlyList<AppLaunchItem> GetPinned()",
            StringComparison.Ordinal);
        int launchStart = catalog.IndexOf(
            "public bool Launch(",
            getPinnedStart,
            StringComparison.Ordinal);
        Assert.True(
            getPinnedStart >= 0
            && launchStart > getPinnedStart);
        Assert.DoesNotContain(
            "_pinnedLoader()",
            catalog[getPinnedStart..launchStart]);
        Assert.Contains(
            "await Task.Run(",
            catalog);
        Assert.Contains(
            "CompleteTaskbarWindowAction(",
            viewModel);
        Assert.Contains(
            "TrySetPinnedAsync(",
            viewModel);
        Assert.Contains(
            "TryMovePinnedRelativeAsync(",
            viewModel);
        Assert.Contains(
            "TryMovePinnedByOffsetAsync(",
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
        Assert.Contains(
            "StartTaskbarAppDrop(",
            codeBehind);
        Assert.Contains(
            "AsyncInteractionRunner.Start(",
            codeBehind);
        Assert.Contains(
            "BeginTransientInteraction();",
            codeBehind);
        Assert.Contains(
            "EndTransientInteraction",
            codeBehind);
    }

    [Fact]
    public void ShellPreferences_UseSnapshotAndBackgroundWriteQueue()
    {
        string root = FindRepositoryRoot();
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));
        string repository = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "ShellPreferenceRepository.cs"));
        string mainXaml = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml"));

        Assert.Contains(
            "ShellPreferenceSnapshot preferenceSnapshot",
            viewModel);
        Assert.Contains(
            "await _shellPreferences.LoadAsync()",
            viewModel);
        Assert.Contains(
            "WaitForShellPreferencesAsync()",
            viewModel);
        Assert.Contains(
            "_shellPreferences.QueueSave(",
            viewModel);
        Assert.Contains(
            "_shellPreferences.Dispose();",
            viewModel);
        Assert.DoesNotContain(
            "new AppDbContext()",
            viewModel);
        Assert.Contains(
            "Task.Run(LoadSafely)",
            repository);
        Assert.Contains(
            "Dictionary<string, string> _pendingValues",
            repository);
        Assert.Contains(
            "Task CompleteAsync()",
            repository);
        Assert.Contains(
            "DisplayTargetModeKey",
            repository);
        Assert.Contains(
            "OnDisplayTargetModeChanged(",
            viewModel);
        Assert.Contains(
            "DisplayTargetChanged?.Invoke();",
            viewModel);
        Assert.Contains(
            "SelectedValue=\"{Binding DisplayTargetMode",
            mainXaml);
        Assert.Contains(
            "ItemsSource=\"{Binding DisplayTargetOptions}\"",
            mainXaml);
        Assert.Contains(
            "ShellDisplayTarget.GetOptions(",
            viewModel);

        string mainWindow = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));
        Assert.Contains(
            "private async void MainWindow_Loaded(",
            mainWindow);
        Assert.Contains(
            "await _viewModel",
            mainWindow);
        Assert.Contains(
            ".WaitForShellPreferencesAsync()",
            mainWindow);
        Assert.Contains(
            "HideShell();",
            mainWindow);
        Assert.Contains(
            "_shellStartupReady = true",
            mainWindow);
        Assert.Contains(
            "if (!_shellStartupReady",
            mainWindow);
    }

    [Fact]
    public void RuntimeNotifications_UseNoActivateNativeFluentToasts()
    {
        string root = FindRepositoryRoot();
        string toastXaml = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "FocusToastWindow.xaml"));
        string toastCode = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "FocusToastWindow.xaml.cs"));
        string manager = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "FocusToastManager.cs"));
        string mainWindow = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));

        Assert.Contains(
            "ShowActivated=\"False\"",
            toastXaml);
        Assert.Contains(
            "ShowInTaskbar=\"False\"",
            toastXaml);
        Assert.Contains(
            "FocusShellTintBrush",
            toastXaml);
        Assert.Contains(
            "FocusCardTitleText",
            toastXaml);
        Assert.DoesNotContain(
            "CornerRadius=",
            toastXaml.Split(
                "<Grid>",
                StringSplitOptions.None)[0]);
        Assert.Contains(
            "WsExNoActivate",
            toastCode);
        Assert.Contains(
            "WindowBackdropService.Apply(this)",
            toastCode);
        Assert.Contains(
            "DispatcherTimer",
            manager);
        Assert.Contains(
            "SystemParameters.ClientAreaAnimation",
            manager);
        Assert.Contains(
            "_toastManager.Enqueue",
            mainWindow);
        Assert.Contains(
            "_toastManager.DismissAll()",
            mainWindow);
        Assert.DoesNotContain(
            "ShowBalloonTip",
            mainWindow);
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
