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
        Assert.Contains("AI 服务配置", view);
        Assert.Contains("DeepSeek", view);
        Assert.Contains("AI 智能分区", view);
        Assert.Contains("ApplyAgentActionCommand", view);
        Assert.Contains("CancelAgentActionCommand", view);
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
    public void Organizer_ExposesSmartPartitionAndPersistentLocks()
    {
        string root = FindRepositoryRoot();
        string view = File.ReadAllText(
            Path.Combine(root, "Views", "FileOrganizerView.xaml"));
        string model = File.ReadAllText(
            Path.Combine(root, "Models", "DesktopPartition.cs"));
        string schema = File.ReadAllText(
            Path.Combine(root, "Data", "AppDbContext.cs"));

        Assert.Contains("Content=\"AI 智能分区\"", view);
        Assert.Contains("SmartPartitionCommand", view);
        Assert.Contains("TogglePartitionLockCommand", view);
        Assert.Contains("已锁定", view);
        Assert.Contains("IsLocked", model);
        Assert.Contains("DesktopPartitions ADD COLUMN IsLocked", schema);
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

        Assert.Contains("Content=\"概览\"", mainWindow);
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
        Assert.DoesNotContain("OKR", view);
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
            3,
            Regex.Matches(
                views,
                "<PasswordBox").Count);
        Assert.Equal(
            3,
            Regex.Matches(
                views,
                "Style=\"\\{StaticResource FocusPasswordBox\\}\"").Count);
    }

    [Fact]
    public void StatusCenter_ExposesSupportedSystemControls()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(Path.Combine(root, "Views", "MainWindow.xaml"));
        string mainWindowCode = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));

        Assert.DoesNotContain("OpenQuickSettingsCommand", mainWindow);
        Assert.Contains("PanelNotificationsDetailsExpander", mainWindow);
        Assert.Contains("PanelNotifications", mainWindow);
        Assert.Contains("UnreadPanelNotificationCount", mainWindow);
        Assert.Contains("MarkAllPanelNotificationsReadCommand", mainWindow);
        Assert.Contains("ClearPanelNotificationsCommand", mainWindow);
        Assert.Contains("InvokePanelNotificationCommand", mainWindow);
        Assert.Contains("FilteredPanelNotifications", mainWindow);
        Assert.Contains("ShowUnreadPanelNotificationsOnly", mainWindow);
        Assert.Contains("MarkPanelNotificationReadCommand", mainWindow);
        Assert.Contains("RemovePanelNotificationCommand", mainWindow);
        Assert.Contains("MaxHeight=\"360\"", mainWindow);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", mainWindow);
        Assert.Contains("不会唤起 Windows 任务栏或通知中心", mainWindow);
        Assert.DoesNotContain("OpenNotificationsCommand", mainWindow);
        Assert.DoesNotContain("Win+N", mainWindow);
        Assert.DoesNotContain("Content=\"Windows 通知中心\"", mainWindow);
        Assert.DoesNotContain("OpenInputSwitcherCommand", mainWindow);
        Assert.Contains(
            "Content=\"输入法\"",
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
        Assert.Contains(
            "Text=\"{Binding StatusCenterSummary}\"",
            mainWindow);
        Assert.Contains(
            "Command=\"{Binding SendMediaCommandCommand}\"",
            mainWindow);
        Assert.Contains(
            "MediaTransportAction.PreviousTrack",
            mainWindow);
        Assert.Contains(
            "MediaTransportAction.PlayPause",
            mainWindow);
        Assert.Contains(
            "MediaTransportAction.NextTrack",
            mainWindow);
        Assert.Contains(
            "PreviewMouseDown=\"StatusCenterButton_PreviewMouseDown\"",
            mainWindow);
        Assert.Contains(
            "MouseEnter=\"StatusCenterButton_MouseEnter\"",
            mainWindow);
        Assert.Contains(
            "按 Shift+Enter 直接打开或收起 Panel 通知",
            mainWindow);
        Assert.Contains(
            "Visibility=\"{Binding HasUnreadPanelNotifications",
            mainWindow);
        Assert.Contains(
            "Text=\"{Binding PanelNotificationBadgeText}\"",
            mainWindow);
        Assert.Contains(
            "TogglePanelNotificationsFromCompactEntry",
            mainWindowCode);
        Assert.DoesNotContain(
            "MarkPanelNotificationsRead",
            mainWindowCode);
        Assert.DoesNotContain(
            "MouseRightButtonUp=\"VolumeButton_MouseRightButtonUp\"",
            mainWindow);
        Assert.Contains(
            "_viewModel.RefreshSystemStatusForInteraction();",
            mainWindowCode);
        Assert.Contains(
            "e.ChangedButton",
            mainWindowCode);
        Assert.Contains(
            "MediaTransportAction",
            mainWindowCode);
        Assert.Contains(
            ".SendMediaCommandCommand",
            mainWindowCode);
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
        Assert.DoesNotContain("Content=\"快捷设置\"", mainWindow);
        Assert.Contains("Visibility=\"{Binding IsCalendarOpen", mainWindow);
        Assert.Contains("Visibility=\"{Binding IsStatusCenterOpen", mainWindow);
        Assert.DoesNotContain("Visibility=\"{Binding IsFocusCenterOpen", mainWindow);
        Assert.Contains("EnableReplacementCommand", mainWindow);
        Assert.Contains(
            "Text=\"后台应用与窗口\"",
            mainWindow);
        Assert.Contains(
            "Tag=\"{x:Static services:StatusCenterDetail.Applications}\"",
            mainWindow);
        Assert.Contains(
            "x:Name=\"ApplicationsDetailsExpander\"",
            mainWindow);
        Assert.DoesNotContain(
            "OpenNotificationOverflow",
            mainWindow);
        Assert.DoesNotContain(
            "显示隐藏图标",
            mainWindow);

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
            "bool SendMediaCommand(",
            statusContract);
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
            "GetInputMethods()",
            statusContract);
        Assert.Contains(
            "bool TryActivateInputMethod(",
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
        Assert.Contains(
            "_shortcutSender(",
            systemStatus);
        Assert.Contains(
            "MediaTransportShortcutMap.Get(",
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
            "RunInlineStatusActionAsync(",
            viewModel);
        Assert.Contains(
            ".SendMediaCommand(",
            viewModel);
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
        Assert.Matches(
            @"ObservableCollection<\s*InputMethodOption>\s*InputMethods",
            viewModel);
        Assert.Contains(
            ".TryActivateInputMethod(",
            viewModel);
        Assert.Contains(
            "_lastActiveExternalWindowHandle",
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
    public void StatusCenter_PutsCommonActionsBeforeScrollableDetails()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml"));
        string mainWindowCode = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));

        int commonStart = mainWindow.IndexOf(
            "AutomationProperties.Name=\"Panel 状态详情\"",
            StringComparison.Ordinal);
        int masterVolume = mainWindow.IndexOf(
            "Value=\"{Binding MasterVolume, Mode=TwoWay",
            StringComparison.Ordinal);
        int brightness = mainWindow.IndexOf(
            "Value=\"{Binding BrightnessPercent, Mode=TwoWay",
            StringComparison.Ordinal);
        int networkStart = mainWindow.IndexOf(
            "Text=\"{Binding NetworkGlyph}\"",
            StringComparison.Ordinal);
        int audioDetails = mainWindow.IndexOf(
            "ItemsSource=\"{Binding ApplicationAudioSessions}\"",
            StringComparison.Ordinal);

        Assert.True(commonStart >= 0);
        Assert.True(masterVolume > commonStart);
        Assert.True(brightness > masterVolume);
        Assert.True(networkStart > brightness);
        Assert.True(audioDetails > networkStart);
        foreach (string entry in new[]
                 {
                     "Content=\"网络\"",
                     "Content=\"应用音量\"",
                     "Content=\"媒体\"",
                     "Content=\"输入法\"",
                     "Content=\"显示桌面\"",
                     "Content=\"锁定\"",
                     "Content=\"电源\"",
                     "AutomationProperties.Name=\"展开 Panel 通知历史\""
                 })
        {
            int position = mainWindow.IndexOf(
                entry,
                commonStart,
                StringComparison.Ordinal);
            Assert.InRange(
                position,
                commonStart + 1,
                networkStart - 1);
        }

        Assert.Single(
            Regex.Matches(
                    mainWindow,
                    "x:Name=\"StatusCenterWindowOverviewButton\"")
                .Cast<Match>());
        Assert.Contains(
            "AutomationProperties.Name=\"Panel 通知历史\"",
            mainWindow);
        Assert.Contains(
            "AutomationProperties.Name=\"设备与会话操作\"",
            mainWindow);
        Assert.DoesNotContain(
            "Content=\"打开 Windows 输入法浮层\"",
            mainWindow);
        Assert.Contains(
            "PanelStatusDetailMenuItem_Click",
            mainWindow);
        Assert.Contains(
            "StatusCenterDetailRequested +=",
            mainWindowCode);
        Assert.Contains(
            "ViewModel_StatusCenterDetailRequested",
            mainWindowCode);
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
        Assert.Contains(
            "SelectInitialDirectory(",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "packaging",
                    "CustomInstallerLauncher.cs")));
        Assert.Contains(
            "CleanupStaleRegistrations()",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "packaging",
                    "CustomInstallerLauncher.cs")));
        Assert.Contains(
            "UnregisterStaleMsi(",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "packaging",
                    "CustomInstallerLauncher.cs")));
        Assert.Contains(
            "已自动撤销错误位置的安装",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "packaging",
                    "CustomInstallerLauncher.cs")));
        Assert.Contains(
            "LauncherVersion = \"0.11.14\"",
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
    public void UpdateAvailability_BelongsToSettingsInsteadOfOrganizer()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string viewModel = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));

        int organizerStart = mainWindow.IndexOf(
            "x:Name=\"OrganizerButton\"",
            StringComparison.Ordinal);
        int tasksStart = mainWindow.IndexOf(
            "x:Name=\"TasksButton\"",
            organizerStart,
            StringComparison.Ordinal);
        Assert.True(
            organizerStart >= 0
            && tasksStart > organizerStart);
        string organizer = mainWindow[
            organizerStart..tasksStart];

        Assert.DoesNotContain(
            "IsUpdateAvailable",
            organizer);
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding OrganizerEntryAutomationName}\"",
            organizer);
        Assert.Contains(
            "Visibility=\"{Binding HasCollectedDesktopItems",
            organizer);
        Assert.Contains(
            "Text=\"{Binding CollectedDesktopItemCountBadgeText}\"",
            organizer);
        Assert.Contains(
            "AutomationProperties.Name=\"FocusPanel 设置\"",
            mainWindow);
        Assert.Contains(
            "Visibility=\"{Binding IsUpdateAvailable",
            mainWindow);
        Assert.Contains("ApplyUpdateAvailability(update)", viewModel);
        Assert.Contains("ApplyUpdateAvailability(null)", viewModel);
        Assert.Contains(
            "CollectedCountChanged +=",
            viewModel);
        Assert.Contains(
            "CollectedCountChanged -=",
            viewModel);
    }

    [Fact]
    public void StatusCenter_UsesProgressiveDisclosureForLowFrequencyDetails()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string theme = File.ReadAllText(
            Path.Combine(root, "Themes", "FocusTheme.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));
        int statusStart = mainWindow.IndexOf(
            "<!-- Status center -->",
            StringComparison.Ordinal);
        int calendarStart = mainWindow.IndexOf(
            "<!-- Calendar and daily overview -->",
            statusStart,
            StringComparison.Ordinal);

        Assert.True(
            statusStart >= 0
            && calendarStart > statusStart);
        string statusCenter = mainWindow[
            statusStart..calendarStart];
        Assert.Equal(
            6,
            statusCenter.Split(
                "Style=\"{StaticResource FocusDetailsExpander}\"",
                StringSplitOptions.None).Length - 1);
        Assert.Contains(
            "AutomationProperties.Name=\"Panel 应用与窗口抽屉\"",
            statusCenter);
        Assert.Contains(
            "AutomationProperties.Name=\"网络与无线详情\"",
            statusCenter);
        Assert.Contains(
            "AutomationProperties.Name=\"声音与应用音量详情\"",
            statusCenter);
        Assert.Contains(
            "AutomationProperties.Name=\"媒体与电池详情\"",
            statusCenter);
        Assert.Contains(
            "AutomationProperties.Name=\"Panel 输入法详情\"",
            statusCenter);
        Assert.Contains(
            "AutomationProperties.Name=\"Panel 通知历史\"",
            statusCenter);
        Assert.Contains(
            "x:Name=\"ApplicationsDetailsExpander\"",
            statusCenter);
        Assert.Contains(
            "x:Name=\"NetworkDetailsExpander\"",
            statusCenter);
        Assert.Contains(
            "x:Name=\"ApplicationAudioDetailsExpander\"",
            statusCenter);
        Assert.Contains(
            "x:Name=\"MediaBatteryDetailsExpander\"",
            statusCenter);
        Assert.Contains(
            "x:Name=\"InputMethodDetailsExpander\"",
            statusCenter);
        Assert.Contains(
            "x:Name=\"PanelNotificationsDetailsExpander\"",
            statusCenter);
        Assert.Equal(
            6,
            statusCenter.Split(
                "Expanded=\"StatusDetailsExpander_Expanded\"",
                StringSplitOptions.None).Length - 1);
        Assert.Equal(
            6,
            statusCenter.Split(
                "Collapsed=\"StatusDetailsExpander_Collapsed\"",
                StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain(
            "IsExpanded=\"True\"",
            statusCenter);
        Assert.True(
            statusCenter.IndexOf(
                "AutomationProperties.Name=\"内置显示器亮度\"",
                StringComparison.Ordinal)
            < statusCenter.IndexOf(
                "AutomationProperties.Name=\"网络与无线详情\"",
                StringComparison.Ordinal));

        Assert.Contains(
            "x:Key=\"FocusDetailsExpander\"",
            theme);
        Assert.Contains(
            "TargetType=\"Expander\"",
            theme);
        Assert.Contains(
            "IsExpanded, RelativeSource={RelativeSource TemplatedParent}",
            theme);
        Assert.Contains(
            "x:Name=\"ExpandSite\"",
            theme);
        Assert.Contains(
            "MinHeight=\"44\"",
            theme);
        Assert.Contains(
            "StatusCenterDetailPolicy.Toggle(",
            codeBehind);
        Assert.Contains(
            "SetOpenStatusCenterDetail(",
            codeBehind);
        Assert.Contains(
            "target.BringIntoView",
            codeBehind);
    }

    [Fact]
    public void CompactDock_AdaptsEightFullEntriesToSixShortScreenEntries()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(Path.Combine(root, "Views", "MainWindow.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));
        int dockStart = mainWindow.IndexOf("<!-- Compact app dock -->", StringComparison.Ordinal);
        int onboardingStart = mainWindow.IndexOf(
            "<!-- First-run safety onboarding -->",
            StringComparison.Ordinal);

        Assert.True(dockStart >= 0 && onboardingStart > dockStart);
        string compactDock = mainWindow[dockStart..onboardingStart];
        Assert.Equal(9, compactDock.Split("Tag=\"CompactFixedEntry\"").Length - 1);
        Assert.Equal(
            1,
            compactDock.Split(
                    "ItemsSource=\"{Binding CompactTaskbarApps}\"")
                .Length - 1);
        int start = compactDock.IndexOf(
            "Click=\"StartButton_Click\"",
            StringComparison.Ordinal);
        int search = compactDock.IndexOf(
            "x:Name=\"SearchButton\"",
            StringComparison.Ordinal);
        int backgroundApps = compactDock.IndexOf(
            "x:Name=\"BackgroundAppsButton\"",
            StringComparison.Ordinal);
        int applications = compactDock.IndexOf(
            "x:Name=\"TaskbarAppsScrollViewer\"",
            StringComparison.Ordinal);
        int organizer = compactDock.IndexOf(
            "Click=\"OrganizerButton_Click\"",
            StringComparison.Ordinal);
        int denseFocus = compactDock.IndexOf(
            "Click=\"DenseFocusCenterButton_Click\"",
            StringComparison.Ordinal);
        int tasks = compactDock.IndexOf(
            "Click=\"TasksButton_Click\"",
            StringComparison.Ordinal);
        int statusCenter = compactDock.IndexOf(
            "Click=\"StatusCenterButton_Click\"",
            StringComparison.Ordinal);
        int time = compactDock.IndexOf(
            "Click=\"CalendarPanelButton_Click\"",
            StringComparison.Ordinal);
        int desktop = compactDock.IndexOf(
            "Click=\"DesktopToggleButton_Click\"",
            StringComparison.Ordinal);
        Assert.True(
            start >= 0
            && start < search
            && search < backgroundApps
            && backgroundApps < applications
            && applications < denseFocus
            && denseFocus < organizer
            && organizer < tasks
            && tasks < statusCenter
            && statusCenter < time
            && time < desktop);
        Assert.Contains("Click=\"OrganizerButton_Click\"", compactDock);
        Assert.Contains("Click=\"DenseFocusCenterButton_Click\"", compactDock);
        Assert.Contains("Click=\"TasksButton_Click\"", compactDock);
        Assert.Contains("Click=\"StatusCenterButton_Click\"", compactDock);
        Assert.DoesNotContain(
            "x:Name=\"TaskViewButton\"",
            compactDock);
        Assert.Contains(
            "PreviewMouseWheel=\"TaskbarApp_PreviewMouseWheel\"",
            compactDock);
        Assert.Contains(
            "x:Name=\"StartButton\"",
            compactDock);
        Assert.Contains(
            "x:Name=\"OrganizerButton\"",
            compactDock);
        Assert.Contains(
            "x:Name=\"TasksButton\"",
            compactDock);
        Assert.Contains(
            "x:Name=\"StatusCenterButton\"",
            compactDock);
        Assert.Contains(
            "x:Name=\"BackgroundAppsButton\"",
            compactDock);
        Assert.Contains(
            "x:Name=\"DesktopToggleButton\"",
            compactDock);
        Assert.Contains(
            "AutomationProperties.Name=\"显示或恢复桌面\"",
            compactDock);
        Assert.Contains(
            "Style=\"{StaticResource CompactDesktopEntryButton}\"",
            compactDock);
        Assert.Contains(
            "Style=\"{StaticResource CompactDenseOnlyEntryButton}\"",
            compactDock);
        Assert.Equal(
            3,
            compactDock.Split(
                    "Style=\"{StaticResource CompactFullOnlyEntryButton}\"")
                .Length - 1);
        Assert.Matches(
            "CompactDockDensityPolicy\\s*"
            + "\\.UsesCombinedFocusEntry\\(Height\\)",
            codeBehind);
        Assert.Matches(
            "DenseFocusCenterButton_Click\\([\\s\\S]*?"
            + "_viewModel\\.LastWorkspace[\\s\\S]*?"
            + "OpenFocusWorkspace\\(destination\\);",
            codeBehind);
        Assert.Contains(
            "Click=\"BackgroundAppsButton_Click\"",
            compactDock);
        Assert.Contains(
            "只显示 Panel 已识别的固定、运行和后台应用，不调用 Windows 任务栏隐藏图标",
            compactDock);
        Assert.Contains(
            "按 Shift+Enter 直接打开或收起 Panel 通知",
            compactDock);
        Assert.Contains(
            "Visibility=\"{Binding HasUnreadPanelNotifications",
            compactDock);
        Assert.Contains(
            "Click=\"PanelVerticalAnchorMenuItem_Click\"",
            compactDock);
        Assert.Contains(
            "x:Name=\"PanelDisplayTargetMenuItem\"",
            compactDock);
        Assert.Contains(
            "Header=\"移动到屏幕\"",
            compactDock);
        Assert.Contains(
            "SubmenuOpened=\"PanelDisplayTargetMenuItem_SubmenuOpened\"",
            compactDock);
        Assert.Contains(
            "Header=\"后台应用与窗口 · Panel\"",
            compactDock);
        Assert.Contains(
            "Tag=\"{x:Static services:StatusCenterDetail.Applications}\"",
            compactDock);
        Assert.DoesNotContain(
            "OpenNotificationOverflow",
            compactDock);
        Assert.Contains(
            "Header=\"Panel 通知\"",
            compactDock);
        Assert.Contains(
            "Header=\"输入法 · Panel\"",
            compactDock);
        Assert.DoesNotContain("ToggleSettingsCommand", compactDock);
        Assert.DoesNotContain("BatteryPercent", compactDock);
        Assert.Contains(
            "Visibility=\"{Binding IsBackgroundOnly, Converter={StaticResource BooleanToVisibilityConverter}}\"",
            compactDock);
        Assert.Contains(
            "AutomationProperties.Name=\"后台运行，无可见窗口\"",
            compactDock);

        string systemStatus = File.ReadAllText(
            Path.Combine(root, "Services", "SystemStatusService.cs"));
        string statusContract = File.ReadAllText(
            Path.Combine(root, "Services", "ISystemStatusService.cs"));
        string project = File.ReadAllText(
            Path.Combine(root, "FocusPanel.csproj"));
        Assert.DoesNotContain("OpenNotificationOverflow", systemStatus);
        Assert.DoesNotContain("OpenNotificationOverflow", statusContract);
        Assert.DoesNotContain("OpenNotifications", systemStatus);
        Assert.DoesNotContain("OpenNotifications", statusContract);
        Assert.DoesNotContain("TaskbarIcon", mainWindow);
        Assert.DoesNotContain("Hardcodet.NotifyIcon", project);
        Assert.DoesNotContain("OpenWidgets", systemStatus);
        Assert.DoesNotContain("OpenWidgets", statusContract);
        Assert.DoesNotContain("System.Windows.Automation", systemStatus);
        Assert.DoesNotContain(
            "DesktopWindowContentBridge",
            systemStatus);
        Assert.DoesNotContain(
            "EnumChildWindows",
            systemStatus);

        string onboarding = mainWindow[onboardingStart..];
        Assert.Contains("<Border Grid.Column=\"0\"", onboarding);
        Assert.DoesNotContain("Grid.ColumnSpan=\"2\"", onboarding);
    }

    [Fact]
    public void CompactTaskEntry_OffersExplicitQuickCaptureWithoutChangingPrimaryClick()
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

        Assert.Contains(
            "Click=\"TasksButton_Click\"",
            mainWindow);
        Assert.Contains(
            "Header=\"快速添加到 Inbox…\"",
            mainWindow);
        Assert.Contains(
            "Click=\"TaskQuickCaptureMenuItem_Click\"",
            mainWindow);
        Assert.Contains(
            "左键打开任务 · 右键快速添加",
            mainWindow);
        Assert.Contains(
            "QuickCapturePrefix",
            codeBehind);
        Assert.Contains(
            "SearchBox.CaretIndex = SearchBox.Text.Length",
            codeBehind);
        Assert.Contains(
            "SearchPanelTitle",
            mainWindow);
        Assert.Contains(
            "SearchPanelInstruction",
            mainWindow);
        Assert.Contains(
            "输入标题后按 Enter，直接保存到 Inbox",
            viewModel);
    }

    [Fact]
    public void CompactDock_FixedEntriesUseLabelsAndOwnedSurfaceIndicators()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml"));
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));
        int dockStart = mainWindow.IndexOf(
            "<!-- Compact app dock -->",
            StringComparison.Ordinal);
        int onboardingStart = mainWindow.IndexOf(
            "<!-- First-run safety onboarding -->",
            StringComparison.Ordinal);

        Assert.True(
            dockStart >= 0
            && onboardingStart > dockStart);
        string compactDock =
            mainWindow[dockStart..onboardingStart];
        Assert.Equal(
            4,
            compactDock.Split(
                "Style=\"{StaticResource CompactLabeledEntryButton}\"",
                StringSplitOptions.None).Length - 1);
        Assert.Equal(
            3,
            compactDock.Split(
                "Style=\"{StaticResource CompactFullOnlyEntryButton}\"",
                StringSplitOptions.None).Length - 1);
        Assert.Single(
            compactDock.Split(
                "Style=\"{StaticResource CompactDenseOnlyEntryButton}\"",
                StringSplitOptions.None)[1..]);
        Assert.Contains("Text=\"开始\"", compactDock);
        Assert.Contains("Text=\"窗口\"", compactDock);
        Assert.Contains("Text=\"收纳\"", compactDock);
        Assert.Contains("Text=\"任务\"", compactDock);
        Assert.Contains("Text=\"Focus\"", compactDock);
        Assert.Contains(
            "Text=\"{Binding AudioCompactValueText}\"",
            compactDock);
        Assert.Contains(
            "MouseEnter=\"StatusCenterButton_MouseEnter\"",
            compactDock);
        Assert.DoesNotContain(
            "IsStartHubOpen",
            compactDock);
        Assert.Contains(
            "Visibility=\"{Binding IsApplicationLauncherOpen, Converter={StaticResource BooleanToVisibilityConverter}}\"",
            compactDock);
        Assert.Contains(
            "Visibility=\"{Binding IsUnifiedSearchEntryActive, Converter={StaticResource BooleanToVisibilityConverter}}\"",
            compactDock);
        Assert.Contains(
            "Visibility=\"{Binding IsOrganizerEntryActive, Converter={StaticResource BooleanToVisibilityConverter}}\"",
            compactDock);
        Assert.Contains(
            "Visibility=\"{Binding IsTasksEntryActive, Converter={StaticResource BooleanToVisibilityConverter}}\"",
            compactDock);
        Assert.Contains(
            "Visibility=\"{Binding IsStatusEntryActive, Converter={StaticResource BooleanToVisibilityConverter}}\"",
            compactDock);
        Assert.Contains(
            "Visibility=\"{Binding IsCalendarOpen, Converter={StaticResource BooleanToVisibilityConverter}}\"",
            compactDock);
        Assert.DoesNotContain(
            "public bool IsFocusEntryActive",
            viewModel);
        Assert.Contains(
            "public bool IsStatusEntryActive",
            viewModel);
        Assert.Contains(
            "public string AudioCompactValueText",
            viewModel);
        Assert.Contains(
            "IsStatusCenterOpen\n        || IsPowerMenuOpen",
            viewModel.Replace("\r\n", "\n"));
    }

    [Fact]
    public void OrganizerAndTasksEntries_OpenPrimaryWorkspacesDirectly()
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

        int organizerStart = mainWindow.IndexOf(
            "x:Name=\"OrganizerButton\"",
            StringComparison.Ordinal);
        int tasksStart = mainWindow.IndexOf(
            "x:Name=\"TasksButton\"",
            organizerStart,
            StringComparison.Ordinal);
        int statusStart = mainWindow.IndexOf(
            "x:Name=\"StatusCenterButton\"",
            tasksStart,
            StringComparison.Ordinal);
        Assert.True(organizerStart >= 0);
        Assert.True(tasksStart > organizerStart);
        Assert.True(statusStart > tasksStart);
        string primaryEntries = mainWindow[
            organizerStart..statusStart];

        Assert.DoesNotContain(
            "<!-- Focus center -->",
            mainWindow);
        Assert.DoesNotContain(
            "IsFocusCenterOpen",
            mainWindow);
        Assert.Contains(
            "Columns=\"6\"",
            mainWindow);
        foreach (string destination in new[]
                 {
                     "Dashboard",
                     "Files",
                     "Tasks",
                     "Pomodoro",
                     "AI"
                 })
        {
            Assert.Contains(
                $"CommandParameter=\"{destination}\"",
                mainWindow);
        }

        Assert.Contains("Text=\"收纳\"", primaryEntries);
        Assert.Contains("Text=\"任务\"", primaryEntries);
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding OrganizerEntryAutomationName}\"",
            primaryEntries);
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding TasksEntryAutomationName}\"",
            primaryEntries);
        Assert.Contains(
            "Visibility=\"{Binding HasOpenTasks, Converter={StaticResource BooleanToVisibilityConverter}}\"",
            primaryEntries);
        Assert.Contains(
            "Text=\"{Binding OpenTaskCountBadgeText}\"",
            primaryEntries);
        Assert.DoesNotContain(
            "Header=\"打开上次使用的工作区\"",
            primaryEntries);
        Assert.Contains(
            "OpenFocusWorkspace(\"Files\")",
            codeBehind.Replace("\r\n", "\n"));
        Assert.Contains(
            "OpenFocusWorkspace(\"Tasks\")",
            codeBehind.Replace("\r\n", "\n"));
        Assert.Contains(
            "_viewModel.NavigateCommand.Execute(",
            codeBehind);
        Assert.DoesNotContain(
            "WorkspaceButton_Click(",
            codeBehind);
        Assert.DoesNotContain(
            "WorkspaceShortcutMenuItem_Click(",
            codeBehind);
        Assert.Contains(
            "public bool HasOpenTasks",
            viewModel);
        Assert.Contains(
            "public bool HasCollectedDesktopItems",
            viewModel);
        Assert.Contains(
            "public string CollectedDesktopItemCountBadgeText",
            viewModel);
        Assert.Contains(
            "public string OrganizerEntryAutomationName",
            viewModel);
        Assert.Contains(
            "public string OpenTaskCountBadgeText",
            viewModel);
        Assert.Contains(
            "public string TasksEntryAutomationName",
            viewModel);
        Assert.Contains(
            "public bool IsOrganizerEntryActive",
            viewModel);
        Assert.Contains(
            "public bool IsTasksEntryActive",
            viewModel);
    }

    [Fact]
    public void WorkspaceNavigation_UsesOneSegmentedSelectionGroup()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml"));
        int start = mainWindow.IndexOf(
            "AutomationProperties.Name=\"主要功能快捷入口\"",
            StringComparison.Ordinal);
        int end = mainWindow.IndexOf(
            "</UniformGrid>",
            start,
            StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start);
        string navigation = mainWindow[start..end];
        Assert.Equal(
            6,
            navigation.Split(
                "<RadioButton",
                StringSplitOptions.None).Length - 1);
        Assert.Equal(
            6,
            navigation.Split(
                "GroupName=\"WorkspaceNavigation\"",
                StringSplitOptions.None).Length - 1);
        Assert.Equal(
            6,
            navigation.Split(
                "Style=\"{StaticResource FocusSegmentRadioButton}\"",
                StringSplitOptions.None).Length - 1);
        foreach (string state in new[]
                 {
                     "IsDashboardWorkspaceActive",
                     "IsFilesWorkspaceActive",
                     "IsTasksWorkspaceActive",
                     "IsPomodoroWorkspaceActive",
                     "IsAiWorkspaceActive",
                     "IsSettingsWorkspaceActive"
                 })
        {
            Assert.Contains(
                $"IsChecked=\"{{Binding {state}, Mode=OneWay}}\"",
                navigation);
        }
        Assert.DoesNotContain(
            "FocusSecondaryButton",
            navigation);
    }

    [Fact]
    public void TaskView_IsRemovedFromCompactDockAndSearch()
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
        string searchCatalog = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "WindowsShellSearchCatalog.cs"));

        Assert.DoesNotContain("TaskViewButton", mainWindow);
        Assert.DoesNotContain("TaskViewButton", codeBehind);
        Assert.DoesNotContain("WindowsShellAction.TaskView", searchCatalog);
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
        Assert.Contains("x:Name=\"TaskbarScrollUpCountText\"", mainWindow);
        Assert.Contains("x:Name=\"TaskbarScrollDownCountText\"", mainWindow);
        Assert.Matches(
            "x:Name=\"TaskbarAppsHost\"[\\s\\S]*?PreviewMouseWheel=\"TaskbarAppsHost_PreviewMouseWheel\"",
            mainWindow);
        Assert.Matches(
            "x:Name=\"TaskbarScrollUpButton\"[\\s\\S]*?Height=\"44\"",
            mainWindow);
        Assert.Matches(
            "x:Name=\"TaskbarScrollDownButton\"[\\s\\S]*?Height=\"44\"",
            mainWindow);
        Assert.Contains(
            "ScrollChanged=\"TaskbarAppsScrollViewer_ScrollChanged\"",
            mainWindow);
        Assert.Contains(
            "CompactTaskbarScrollPolicy.GetState",
            codeBehind);
        Assert.Contains(
            "TaskbarAppsScrollViewer.ExtentHeight",
            codeBehind);
        Assert.Contains(
            "state.HiddenAboveCount",
            codeBehind);
        Assert.Contains(
            "state.HiddenBelowCount",
            codeBehind);
        Assert.Contains(
            "AutomationProperties.SetName(",
            codeBehind);
        Assert.Contains(
            "上方还有 {state.HiddenAboveCount} 个应用",
            codeBehind);
        Assert.Contains(
            "下方还有 {state.HiddenBelowCount} 个应用",
            codeBehind);
        Assert.Contains(
            "private void TaskbarAppsHost_PreviewMouseWheel(",
            codeBehind);
        Assert.Contains(
            "ItemsControl.ContainerFromElement(",
            codeBehind);
        Assert.Contains(
            "TaskbarWheelPolicy.GetAction(",
            codeBehind);
        Assert.Contains(
            "private const double CompactTaskbarOverflowInset = 46;",
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
            "AutomationProperties.HelpText=\"{Binding AccessibleInteractionHint}\"",
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
        Assert.Contains(
            "AutomationProperties.Name=\"窗口总览与统一搜索\"",
            mainWindow);
        Assert.Contains(
            "AutomationProperties.HelpText=\"点击查看全部窗口；顶部可切换应用、任务、系统命令和计算搜索\"",
            mainWindow);
        Assert.Contains("private void FocusCompactDock()", codeBehind);
        Assert.Equal(
            1,
            codeBehind.Split(
                "FocusCompactDock();",
                StringSplitOptions.None).Length - 1);
        Assert.Contains("SearchButton.Focus();", codeBehind);
        Assert.Contains(
            "private void OpenSearchFromSummonHotkey()",
            codeBehind);
        Assert.Contains(
            "OpenSearchFromSummonHotkey();",
            codeBehind);
        Assert.Contains(
            "_viewModel.ToggleSearchCommand",
            codeBehind);
        Assert.Contains(
            "if (!_viewModel.IsSearchOpen)",
            codeBehind);
        Assert.Contains(
            "SearchBox.SelectAll();",
            codeBehind);
        Assert.Contains(
            "Text=\"{Binding SummonShortcutText}\"",
            mainWindow);
        Assert.Contains(
            "Text=\"{Binding WindowOverviewShortcutText}\"",
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
        Assert.Contains(
            "SetWindowOverviewShortcutStatus(",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "ViewModels",
                    "MainViewModel.cs")));
        Assert.Contains(
            "RegisterWindowOverview(",
            codeBehind);
        Assert.Contains(
            "OpenWindowOverviewFromHotkey();",
            codeBehind);
        Assert.Contains(
            "PrepareWindowOverviewFromHotkey()",
            codeBehind);
        Assert.Contains(
            "SearchResultsList.Items.Count > 0",
            codeBehind);
        Assert.Contains(
            "if (e.Key == Key.Enter",
            codeBehind);
        Assert.Contains(
            ".ExecuteSearchResultCommand",
            codeBehind);
        Assert.Contains(
            "WindowOverviewHotkeyId",
            codeBehind);
        Assert.Contains(
            "WindowOverviewHotkeySelectionPolicy",
            codeBehind);
        Assert.Contains(
            "isRepeatedInvocation",
            codeBehind);
        Assert.Contains(
            "RememberActiveWindow(",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "ViewModels",
                    "MainViewModel.cs")));
        Assert.DoesNotContain(
            "Text=\"快速搜索：Ctrl + Alt + Space\"",
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
            "SetShortcutState(",
            viewModel);
        Assert.Contains(
            "Text=\"{Binding ShortcutSlotText}\"",
            mainWindow);
        Assert.Contains(
            "Visibility=\"{Binding HasShortcutGesture, Converter={StaticResource BooleanToVisibilityConverter}}\"",
            mainWindow);
        Assert.Contains(
            ".GetShortcutState(index)",
            viewModel);
        Assert.Contains(
            "ApplyTaskbarShortcutStates();",
            viewModel);
        Assert.DoesNotContain(
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
            "GetDestinationsAsync(",
            mainWindow);
        Assert.Contains(
            "\"最近与常用项目\"",
            mainWindow);
        Assert.Contains(
            "AppJumpListCategory.Frequent",
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
            "Frequent = 1",
            jumpLists);
        Assert.Contains(
            "ComposeGroups(",
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
            "x:Name=\"OrganizerButton\"",
            mainWindow);
        Assert.Contains(
            "x:Name=\"TasksButton\"",
            mainWindow);
        Assert.Contains(
            "x:Name=\"StatusCenterButton\"",
            mainWindow);
        Assert.Contains(
            "x:Name=\"TimeButton\"",
            mainWindow);
        Assert.DoesNotContain(
            "x:Name=\"FocusCenterLastWorkspaceButton\"",
            mainWindow);
        Assert.Contains(
            "x:Name=\"StatusCenterWindowOverviewButton\"",
            mainWindow);
        Assert.Contains(
            "x:Name=\"SettingsEnableReplacementButton\"",
            mainWindow);
        Assert.Contains(
            "x:Name=\"SettingsNavigationButton\"",
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
        Assert.DoesNotContain(
            "FocusCenterLastWorkspaceButton",
            codeBehind);
        Assert.Contains(
            "StatusCenterWindowOverviewButton",
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
    public void SettingsEntry_ReturnsToSettings_AndUpdateToastTargetsTheUpdateAction()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));

        Assert.Matches(
            "x:Name=\"SettingsNavigationButton\"[\\s\\S]*?IsChecked=\"\\{Binding IsSettingsWorkspaceActive",
            mainWindow);
        Assert.Matches(
            "x:Name=\"SettingsUpdateCard\"[\\s\\S]*?Text=\"软件更新\"[\\s\\S]*?x:Name=\"SettingsUpdateActionButton\"",
            mainWindow);

        int settingsHandlerStart = codeBehind.IndexOf(
            "private void SettingsMenuItem_Click",
            StringComparison.Ordinal);
        int powerHandlerStart = codeBehind.IndexOf(
            "private void PowerMenuItem_Click",
            settingsHandlerStart,
            StringComparison.Ordinal);
        string settingsHandler = codeBehind[settingsHandlerStart..powerHandlerStart];

        Assert.Contains(
            "SettingsNavigationButton",
            settingsHandler);
        Assert.DoesNotContain(
            "OrganizerButton",
            settingsHandler);

        int updateHandlerStart = codeBehind.IndexOf(
            "private void OpenUpdateSettings()",
            StringComparison.Ordinal);
        int pomodoroHandlerStart = codeBehind.IndexOf(
            "private void OpenPomodoroWorkspace()",
            updateHandlerStart,
            StringComparison.Ordinal);
        string updateHandler = codeBehind[updateHandlerStart..pomodoroHandlerStart];

        Assert.Contains(
            "SettingsUpdateCard.BringIntoView();",
            updateHandler);
        Assert.Contains(
            "SettingsUpdateActionButton",
            updateHandler);
        Assert.Contains(
            "SettingsNavigationButton",
            updateHandler);
    }

    [Fact]
    public void TimeEntry_OffersDesktopCalendarAndOfficialSettingsActions()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));

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
        Assert.Contains(
            "Header=\"显示桌面\"",
            timeButton);
        Assert.DoesNotContain(
            "InputGestureText=\"Win+D\"",
            timeButton);
        Assert.Contains(
            "ShowDesktopCommand",
            timeButton);
        Assert.Contains(
            "PreviewMouseDown=\"TimeButton_PreviewMouseDown\"",
            timeButton);
        Assert.Contains(
            "Shift+左键或中键显示桌面",
            timeButton);
        Assert.Contains(
            "TimeEntryPolicy.FromLeftClick(",
            codeBehind);
        Assert.Contains(
            "TimeEntryAction.ShowDesktop",
            codeBehind);
        Assert.Contains(
            "e.ChangedButton\n            != MouseButton.Middle",
            codeBehind.Replace("\r\n", "\n"));
        Assert.Contains(
            "_viewModel.ShowDesktopCommand.Execute(null)",
            codeBehind);
        Assert.Contains(
            "x:Name=\"DesktopToggleButton\"",
            mainWindow);
        Assert.Contains(
            "Click=\"DesktopToggleButton_Click\"",
            mainWindow);
        Assert.Contains(
            "private void DesktopToggleButton_Click(",
            codeBehind);
        Assert.Matches(
            "DesktopToggleButton_Click\\([\\s\\S]*?"
            + "ShowDesktopFromCompactEntry\\(\\);",
            codeBehind);
        Assert.DoesNotContain(
            "Win+D",
            mainWindow[mainWindow.IndexOf(
                "x:Name=\"DesktopToggleButton\"",
                StringComparison.Ordinal)..mainWindow.IndexOf(
                "</Button>",
                mainWindow.IndexOf(
                    "x:Name=\"DesktopToggleButton\"",
                    StringComparison.Ordinal),
                StringComparison.Ordinal)]);
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
            "Binding=\"{Binding IsFullyMinimized}\"",
            mainWindow);
        Assert.Contains(
            "<Setter Property=\"Height\" Value=\"7\"/>",
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
            "public bool IsFullyMinimized",
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
            7,
            Regex.Matches(mainWindow, "Opened=\"TransientContextMenu_Opened\"").Count);
        Assert.Equal(
            7,
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

        Assert.Contains("Click=\"StartButton_Click\"", mainWindow);
        Assert.DoesNotContain("OpenWindowsSearchCommand", mainWindow);
        Assert.DoesNotContain("TaskViewButton", mainWindow);
        Assert.Contains(
            "OpenUnifiedSearchMenuItem_Click",
            codeBehind);
        Assert.DoesNotContain(
            "InvokeShellEntryAfterClickAsync(",
            codeBehind);
        Assert.Contains(
            "WorkspaceRequested?.Invoke(\"Status\")",
            viewModel);
        Assert.DoesNotContain("OpenWidgetsCommand", mainWindow);
        Assert.Contains(
            "Click=\"OpenPanelRunMenuItem_Click\"",
            mainWindow);
        Assert.DoesNotContain(
            "OpenRunDialogCommand",
            mainWindow);
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
        Assert.Contains(
            "AutomationProperties.HelpText=\"{Binding AccessibleInteractionHint}\"",
            mainWindow);
        Assert.Equal(
            2,
            Regex.Matches(
                mainWindow,
                "x:Name=\"TaskbarScroll(?:Up|Down)Button\"[\\s\\S]*?IsTabStop=\"False\"").Count);
        Assert.Contains("MouseButton.Middle", codeBehind);
        Assert.Contains("Keyboard.Modifiers", codeBehind);
        Assert.Contains(
            "MoveTaskbarAppUpCommand",
            codeBehind);
        Assert.Contains(
            "MoveTaskbarAppDownCommand",
            codeBehind);
        Assert.Contains(
            "TaskbarKeyboardNavigationPolicy",
            codeBehind);
        Assert.Contains(
            "FocusTaskbarAppAtIndex(",
            codeBehind);
        Assert.Contains(
            "RevealTaskbarAppAtIndex(",
            codeBehind);
        Assert.Contains(
            "InputGestureText = \"Alt+↑\"",
            codeBehind);
        Assert.Contains(
            "InputGestureText = \"Alt+↓\"",
            codeBehind);
        Assert.Contains("LaunchNewTaskbarAppCommand.Execute(task)", codeBehind);
        Assert.Contains(
            "LaunchElevatedTaskbarAppCommand.Execute(task)",
            codeBehind);
        Assert.Contains(
            "Header = \"以管理员身份运行\"",
            codeBehind);
        Assert.Contains(
            "ModifierKeys.Control",
            codeBehind);
        Assert.Contains(
            "TaskbarAppClickAction.CycleWindows",
            codeBehind);
        Assert.Contains(
            "CycleTaskbarWindows(",
            codeBehind);
        Assert.Contains(
            "applyThrottle: false",
            codeBehind);
        Assert.Contains(
            "applyThrottle: true",
            codeBehind);
        Assert.Contains("PopulateTaskbarAppContextMenu", codeBehind);
        Assert.Contains("CloseWindowCommand", codeBehind);
        Assert.Contains("CloseTaskCommand", codeBehind);
        Assert.Contains("VolumeButton_PreviewMouseWheel", mainWindow);
        Assert.Contains(
            "Header=\"{Binding PlacementTarget.DataContext.AudioToggleLabel",
            mainWindow);
        Assert.Contains(
            "Header=\"播放 / 暂停\"",
            mainWindow);
        Assert.Contains(
            "Header=\"网络与无线 · Panel\"",
            mainWindow);
        Assert.Contains(
            "Header=\"应用音量 · Panel\"",
            mainWindow);
        Assert.Contains(
            "Header=\"Panel 通知\"",
            mainWindow);
        Assert.Contains(
            "Header=\"输入法 · Panel\"",
            mainWindow);
        Assert.Contains(
            "Header=\"电源…\"",
            mainWindow);
        Assert.DoesNotContain(
            "VolumeButton_MouseRightButtonUp",
            codeBehind);
        Assert.Contains(
            "WindowBatchActionCoordinator.Execute(",
            viewModel);
        Assert.Contains(
            "_windowTracker.Close(",
            viewModel);
    }

    [Fact]
    public void Shell_UsesOneAuthoritativeApplicationCollectionWithCompactProjection()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string viewModel = File.ReadAllText(
            Path.Combine(root, "ViewModels", "MainViewModel.cs"));
        string codeBehind = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));

        Assert.Single(
            Regex.Matches(
                    mainWindow,
                    "ItemsSource=\"\\{Binding CompactTaskbarApps\\}\"")
                .Cast<Match>());
        Assert.Single(
            Regex.Matches(
                    mainWindow,
                    "ItemsSource=\"\\{Binding FilteredBackgroundApps\\}\"")
                .Cast<Match>());
        Assert.Contains(
            "AutomationProperties.Name=\"Panel 统一应用列表\"",
            mainWindow);
        Assert.DoesNotContain("ItemsSource=\"{Binding PinnedApps}\"", mainWindow);
        Assert.DoesNotContain("ItemsSource=\"{Binding RunningApps}\"", mainWindow);
        Assert.Contains("ObservableCollection<TaskbarAppItem> TaskbarApps", viewModel);
        Assert.Contains(
            "CompactTaskbarApps",
            viewModel);
        Assert.Contains(
            "CompactTaskbarAppPolicy.Select(",
            viewModel);
        Assert.DoesNotContain(
            "_viewModel.TaskbarApps",
            codeBehind);
        Assert.Contains(
            "CompactTaskbarApps.Count",
            codeBehind);
        Assert.Contains(
            "UpdatePanelAnchorMenuChecks(",
            codeBehind);
        Assert.Contains(
            "PanelDisplayTargetMenuItem_SubmenuOpened(",
            codeBehind);
        Assert.Contains(
            "_viewModel.RefreshDisplayTargetOptions();",
            codeBehind);
        Assert.Contains(
            "in _viewModel.DisplayTargetOptions",
            codeBehind);
        Assert.Contains(
            "IsChecked = string.Equals(",
            codeBehind);
        Assert.Contains(
            "PanelDisplayTargetMenuOption_Click",
            codeBehind);
        Assert.Matches(
            "PanelDisplayTargetMenuOption_Click\\([\\s\\S]*?"
            + "_viewModel\\.DisplayTargetMode\\s*=\\s*target;",
            codeBehind);
        Assert.Contains(
            "单击直接选择右上、右中或右下",
            mainWindow);
        Assert.Matches(
            "PanelPositionHandleButton_Click\\([\\s\\S]*?"
            + "CompactDock\\.ContextMenu[\\s\\S]*?"
            + "menu\\.IsOpen\\s*=\\s*true;",
            codeBehind);
        Assert.Contains(
            "x:Name=\"PanelPositionHandleButton\"",
            mainWindow);
        Assert.Contains(
            "PanelVerticalAnchorDragPolicy",
            codeBehind);
        Assert.Contains(
            "CaptureMouse()",
            codeBehind);
        Assert.Contains(
            "LostMouseCapture=\"PanelPositionHandleButton_LostMouseCapture\"",
            mainWindow);
        Assert.Contains(
            "BackgroundAppFilterPolicy.Apply(",
            viewModel);
        Assert.Contains("TaskbarApps,", viewModel);
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
    public void StatusCenter_AppDrawerStaysInsidePanelAndManagesWindows()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));

        Assert.Contains(
            "x:Name=\"ApplicationsDetailsExpander\"",
            mainWindow);
        Assert.Contains(
            "AutomationProperties.Name=\"Panel 统一应用列表\"",
            mainWindow);
        Assert.Contains(
            "MaxHeight=\"420\"",
            mainWindow);
        Assert.Contains(
            "VirtualizingPanel.VirtualizationMode=\"Recycling\"",
            mainWindow);
        Assert.Contains(
            "Click=\"StatusCenterApp_Click\"",
            mainWindow);
        Assert.Contains(
            "ContextMenuOpening=\"TaskbarApp_ContextMenuOpening\"",
            mainWindow);
        Assert.Contains(
            "DataContext.ActivateWindowCommand",
            mainWindow);
        Assert.Contains(
            "DataContext.CloseWindowCommand",
            mainWindow);
        Assert.Contains(
            "StatusCenterAppActionPolicy.Resolve(",
            codeBehind);
        Assert.Contains(
            "IsStatusCenterWindowListExpanded",
            codeBehind);
        Assert.Contains(
            "ActivateTaskbarAppCommand",
            codeBehind);
        Assert.DoesNotContain(
            "StatusCenterWindowOverview_Click",
            mainWindow);
        Assert.DoesNotContain(
            "StatusCenterWindowOverview_Click",
            codeBehind);
    }

    [Fact]
    public void MultiWindowLeftClick_OpensFilteredWindowOverview()
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
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));
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

        int clickStart = codeBehind.IndexOf(
            "private void TaskbarApp_Click(",
            StringComparison.Ordinal);
        int clickEnd = codeBehind.IndexOf(
            "private void TaskbarApp_PreviewMouseDown(",
            clickStart,
            StringComparison.Ordinal);
        Assert.True(clickStart >= 0);
        Assert.True(clickEnd > clickStart);
        string clickHandler = codeBehind[
            clickStart..clickEnd];
        Assert.Contains(
            "OpenTaskbarWindowOverview(",
            clickHandler);
        Assert.Contains(
            "PrepareTaskbarWindowOverview(",
            clickHandler);
        Assert.DoesNotContain(
            "PopulateTaskbarWindowList(",
            clickHandler);
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
            "\"当前窗口\"",
            codeBehind);
        Assert.Contains(
            "\"已最小化\"",
            codeBehind);
        Assert.Contains(
            "\"已最大化\"",
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
            "item.IsActive,\n"
            + "                                item.State,\n"
            + "                                item.IsTopmost,\n"
            + "                                !isActive\n"
            + "                                && item.IsAttentionRequested))",
            tracker.Replace(
                "\r\n",
                "\n"));
        Assert.Contains(
            "left.State == right.State",
            synchronizer);
        Assert.Contains(
            "left.IsTopmost == right.IsTopmost",
            synchronizer);
        Assert.Contains(
            "NativeMethods.IsIconic(hwnd)",
            tracker);
        Assert.Contains(
            "windowIdentityFilter:",
            viewModel);
        Assert.Contains(
            "IsWindowApplicationFilterActive",
            viewModel);
        Assert.Contains(
            "Content=\"查看全部\"",
            xaml);
        Assert.Contains(
            "Command=\"{Binding ClearWindowApplicationFilterCommand}\"",
            xaml);
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
            "x:Name=\"FullOverviewButton\"",
            previewXaml);
        Assert.Contains(
            "Click=\"FullOverviewButton_Click\"",
            previewXaml);
        Assert.Contains(
            "AutomationProperties.Name=\"查看此应用的完整窗口列表\"",
            previewXaml);
        Assert.Contains(
            "x:Name=\"ModeText\"",
            previewXaml);
        Assert.Contains(
            "已固定 · 再点图标收起",
            previewCode);
        Assert.Contains(
            "MinimizeWindowButton_Click",
            previewXaml);
        Assert.Contains(
            "ResizeWindowButton_Click",
            previewXaml);
        Assert.Contains(
            "Click=\"LayoutWindowButton_Click\"",
            previewXaml);
        Assert.Contains(
            "AutomationProperties.Name=\"排列此窗口\"",
            previewXaml);
        Assert.Contains(
            "Handler=\"LayoutContextMenu_Opened\"",
            previewXaml);
        Assert.Contains(
            "Handler=\"LayoutContextMenu_Closed\"",
            previewXaml);
        Assert.Equal(
            6,
            Regex.Matches(
                    previewXaml,
                    "Tag=\"\\{x:Static services:WindowLayoutTarget\\.")
                .Count);
        Assert.Contains(
            "TrackedWindowState.Maximized",
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
            "TaskbarPreviewPinPolicy.Resolve(",
            mainWindow);
        Assert.Contains(
            "pinned: true",
            mainWindow);
        Assert.Contains(
            "_taskbarWindowPreviewPinned",
            mainWindow);
        Assert.Contains(
            "_taskbarWindowPreview?.SetPinned(",
            mainWindow);
        Assert.Contains(
            "TaskbarWindowPreview_FullOverviewRequested",
            mainWindow);
        Assert.Matches(
            "if\\s*\\(_taskbarWindowPreviewPinned\\s*"
            + "&&\\s*_taskbarWindowPreview\\?\\.IsVisible",
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
        Assert.Contains(
            "preview.StateActionRequested +=",
            mainWindow);
        Assert.Contains(
            "preview.StateActionRequested -=",
            mainWindow);
        Assert.Contains(
            "TaskbarWindowPreview_StateActionRequested",
            mainWindow);
        Assert.Contains(
            "preview.LayoutRequested +=",
            mainWindow);
        Assert.Contains(
            "preview.LayoutRequested -=",
            mainWindow);
        Assert.Contains(
            "TaskbarWindowPreview_LayoutRequested",
            mainWindow);
        Assert.Contains(
            "preview.LayoutMenuVisibilityChanged +=",
            mainWindow);
        Assert.Contains(
            "preview.LayoutMenuVisibilityChanged -=",
            mainWindow);
        Assert.Contains(
            "IsLayoutMenuOpen: true",
            mainWindow);
        Assert.Contains(
            "_taskbarHoverCloseTimer.Stop();",
            mainWindow);
        Assert.Contains(
            "_viewModel.ArrangeWindowCommand",
            mainWindow);
    }

    [Fact]
    public void StatusCenterWindowRows_ExposeExplicitLivePreviewCards()
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

        Assert.Contains(
            "Click=\"StatusCenterWindowPreviewButton_Click\"",
            xaml);
        Assert.Contains(
            "ToolTip=\"实时预览窗口\"",
            xaml);
        Assert.Contains(
            "Tag=\"{Binding DataContext, RelativeSource={RelativeSource AncestorType=ListBoxItem}}\"",
            xaml);
        Assert.Contains(
            "private void StatusCenterWindowPreviewButton_Click(",
            codeBehind);
        Assert.Contains(
            "preview.Configure(",
            codeBehind);
        Assert.Contains(
            "_statusWindowPreviewTarget",
            codeBehind);
        Assert.Contains(
            "该窗口或当前桌面环境不允许 DWM 预览",
            codeBehind);
        Assert.DoesNotContain(
            "CopyFromScreen",
            codeBehind);
    }

    [Fact]
    public void TaskbarAppMenu_OffersBatchWindowStateActions()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));

        Assert.Contains(
            "Header = \"最小化此应用全部窗口\"",
            mainWindow);
        Assert.Contains(
            "Header = \"还原此应用已最小化窗口\"",
            mainWindow);
        Assert.Contains(
            "MinimizeTaskWindowsCommand",
            mainWindow);
        Assert.Contains(
            "RestoreTaskWindowsCommand",
            mainWindow);
        Assert.Contains(
            "WindowBatchActionCoordinator.Execute(",
            viewModel);
    }

    [Fact]
    public void TaskbarWindowMenus_UseBoundedQuickListAndFullOverview()
    {
        string root = FindRepositoryRoot();
        string codeBehind = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));

        Assert.True(
            Regex.Matches(
                codeBehind,
                "TaskbarContextWindowPolicy.Select\\(")
                .Count >= 2);
        Assert.Contains(
            "CreateOpenAllWindowsMenuItem(",
            codeBehind);
        Assert.Contains(
            "$\"查看全部 {totalCount} 个窗口…\"",
            codeBehind);
        Assert.Contains(
            "OpenTaskbarWindowOverview(",
            codeBehind);
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
            3,
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
        string taskSearchCoordinator = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "TaskSearchCoordinator.cs"));
        string taskService = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "TaskService.cs"));
        string tasksViewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "TasksViewModel.cs"));
        string tasksViewCode = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "TasksView.xaml.cs"));
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
            "没有找到匹配的应用、窗口、待办、命令或快捷结果",
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
        Assert.DoesNotContain(
            "WindowsShellAction.RunDialog",
            shellSearchCatalog);
        Assert.Contains(
            "WindowsShellAction.ShowDesktop",
            shellSearchCatalog);
        Assert.DoesNotContain(
            "WindowsShellAction.SoundOutput",
            shellSearchCatalog);
        Assert.Contains(
            "WindowsShellAction.ScreenSnipping",
            shellSearchCatalog);
        Assert.Contains(
            "WindowsShellAction.ProjectDisplay",
            shellSearchCatalog);
        Assert.Contains(
            "WindowsShellAction.CastDevices",
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
            "result?.RunCommand",
            viewModel);
        Assert.Contains(
            "ExecutePanelRunCommandAsync(",
            viewModel);
        Assert.DoesNotContain(
            "_systemStatus.OpenRunDialog",
            viewModel);
        Assert.Contains(
            "_systemStatus.ShowDesktop",
            viewModel);
        Assert.DoesNotContain(
            "_systemStatus.OpenSoundOutput",
            viewModel);
        Assert.Contains(
            "_systemStatus.OpenScreenSnipping",
            viewModel);
        Assert.Contains(
            "_systemStatus.OpenProjectDisplay",
            viewModel);
        Assert.Contains(
            "_systemStatus.OpenCastDevices",
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
            "ShellSearchResultKind.Task",
            searchResult);
        Assert.Contains(
            "TaskSearchPolicy.Search(",
            shellSearchPolicy);
        Assert.Contains(
            "taskItems:",
            viewModel);
        Assert.Contains(
            "GetOpenTaskSearchItemsAsync()",
            taskService);
        Assert.Contains(
            "new TaskSearchCoordinator(",
            viewModel);
        Assert.Contains(
            "_taskSearch.CompleteAsync()",
            viewModel);
        Assert.Contains(
            "NavigateToSearchTaskAsync(",
            tasksViewModel);
        Assert.Contains(
            "ApplyExternallyCompletedTask(",
            tasksViewModel);
        Assert.Contains(
            "_subscribedViewModel.SelectedTask",
            tasksViewCode);
        Assert.Contains(
            "OnOpenTaskDetailRequested(",
            tasksViewCode);
        Assert.Contains(
            "CompleteTaskAsync(",
            taskSearchCoordinator);
        Assert.Contains(
            "CompleteSearchTaskCommand",
            mainWindow);
        Assert.Contains(
            "CanCompleteTask",
            mainWindow);
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
            "_viewModel.TaskCompleted +=",
            mainWindowCode);
        Assert.Contains(
            "_viewModel.TaskCompleted -=",
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
            "$\"task-completed:{taskId}\"",
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
            "textBox.SelectAll();",
            codeBehind);
        Assert.Contains(
            "selectAllText: true",
            codeBehind);
        Assert.DoesNotContain(
            "Command=\"{Binding ToggleSearchCommand}\"",
            mainWindow);
        Assert.Contains(
            "ToggleCompactOverlay(",
            codeBehind);
        Assert.Contains(
            "returnTarget.Focus();",
            codeBehind);
        Assert.Contains(
            "SelectedSearchResult?.StableKey",
            viewModel);
        Assert.Contains(
            "AutomationProperties.Name=\"应用、窗口、待办、Panel 运行、系统命令与计算结果\"",
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
    public void AppSearch_EmptyQueryOffersEditableNonExecutingExamples()
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

        Assert.Contains(
            "AutomationProperties.Name=\"搜索示例，点击后可编辑并按回车执行\"",
            mainWindow);
        Assert.Contains(
            "DataTrigger Binding=\"{Binding AreSearchSuggestionsVisible}\" Value=\"True\"",
            mainWindow);
        Assert.Equal(
            6,
            Regex.Matches(
                mainWindow,
                "Click=\"SearchSuggestion_Click\"")
                .Count);
        Assert.Contains("Tag=\"任务管理器\"", mainWindow);
        Assert.Contains("Tag=\">\"", mainWindow);
        Assert.Contains("Tag=\"音量 50\"", mainWindow);
        Assert.Contains("Tag=\"专注 25\"", mainWindow);
        Assert.Contains("Tag=\"任务：\"", mainWindow);
        Assert.Contains(
            "Tag=\"{Binding ApplicationAudioSearchSuggestion}\"",
            mainWindow);
        Assert.Contains(
            "Visibility=\"{Binding HasApplicationAudioSearchSuggestion, Converter={StaticResource BooleanToVisibilityConverter}}\"",
            mainWindow);

        int start = codeBehind.IndexOf(
            "private void SearchSuggestion_Click(",
            StringComparison.Ordinal);
        int end = codeBehind.IndexOf(
            "private void StartButton_Click(",
            start,
            StringComparison.Ordinal);
        Assert.True(start >= 0);
        Assert.True(end > start);
        string handler = codeBehind[start..end];
        Assert.Contains(
            "_viewModel.SearchQuery = suggestion;",
            handler);
        Assert.Contains("SearchBox.Focus();", handler);
        Assert.Contains("SearchBox.Select(", handler);
        Assert.DoesNotContain(
            "ExecuteSearchResult",
            handler);
        Assert.DoesNotContain(".Execute(", handler);
    }

    [Fact]
    public void AppSearch_ExposesVisibleWindowOverviewScope()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml"));
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));

        Assert.Contains(
            "AutomationProperties.Name=\"搜索范围\"",
            mainWindow);
        Assert.Contains(
            "Content=\"{Binding OpenWindowScopeLabel}\"",
            mainWindow);
        Assert.Contains(
            "CommandParameter=\"Windows\"",
            mainWindow);
        Assert.Contains(
            "IsChecked=\"{Binding IsWindowSearchScope, Mode=OneWay}\"",
            mainWindow);
        Assert.Contains(
            "private void SelectSearchScope(",
            viewModel);
        Assert.Contains(
            "ShellSearchEntryPolicy.GetResultLimit(",
            viewModel);
        Assert.True(
            Regex.Matches(
                mainWindow,
                "GroupName=\"ShellSearchScope\"")
                .Count
            == 4);
        Assert.Contains(
            "Text=\"窗口\"",
            mainWindow);
        Assert.Contains(
            "ShellSearchEntryPolicy",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Views",
                    "MainWindow.xaml.cs")));
    }

    [Fact]
    public void WindowOverview_OffersDirectSwitchAndCloseActions()
    {
        string root = FindRepositoryRoot();
        string mainWindow = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml"));

        Assert.Contains(
            "Command=\"{Binding DataContext.ExecuteSearchResultCommand",
            mainWindow);
        Assert.Contains(
            "Command=\"{Binding DataContext.CloseWindowCommand",
            mainWindow);
        Assert.Contains(
            "CommandParameter=\"{Binding Window}\"",
            mainWindow);
        Assert.Contains(
            "StringFormat=关闭窗口：{0}",
            mainWindow);
    }

    [Fact]
    public void WindowOverview_ReusesNoActivateDwmPreviewAndKeyboardClose()
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
        string previewCode = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "TaskbarWindowPreviewWindow.xaml.cs"));

        Assert.Contains(
            "MouseEnter=\"SearchResult_MouseEnter\"",
            mainWindow);
        Assert.Contains(
            "MouseLeave=\"SearchResult_MouseLeave\"",
            mainWindow);
        Assert.Contains(
            "Click=\"SearchWindowPreviewButton_Click\"",
            mainWindow);
        Assert.Contains(
            "ToolTip=\"打开实时窗口缩略图\"",
            mainWindow);
        Assert.Contains(
            "Text=\"{Binding OpenWindowCount}\"",
            mainWindow);
        Assert.Contains(
            "PreviewKeyDown=\"SearchResultsList_PreviewKeyDown\"",
            mainWindow);
        Assert.Contains(
            "private void SearchWindowPreviewButton_Click(",
            codeBehind);
        Assert.Contains(
            "private void SearchWindowHoverOpenTimer_Tick(",
            codeBehind);
        Assert.Contains(
            "TryOpenSearchWindowPreview(",
            codeBehind);
        Assert.Contains(
            "_viewModel.CloseWindowCommand",
            codeBehind);
        Assert.Contains(
            "if (e.Key != Key.Delete",
            codeBehind);
        Assert.Contains(
            "preview.Configure(\n"
            + "                    applicationName,\n"
            + "                    new[] { window },\n"
            + "                    \"点击画面直接切换；右侧按钮正常关闭。\")",
            codeBehind.Replace("\r\n", "\n"));
        Assert.Contains(
            "internal void Configure(\n"
            + "        string displayName,",
            previewCode.Replace("\r\n", "\n"));
        Assert.Contains(
            "completeFooterText",
            previewCode);
        Assert.Contains(
            "CancelTaskbarHoverPreview(\n"
            + "            closeMenu: true);\n"
            + "        _viewModel.IsSearchOpen = false;",
            codeBehind.Replace("\r\n", "\n"));
    }

    [Fact]
    public void WindowOverview_OffersSingleLevelStateContextMenu()
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

        Assert.Contains(
            "ContextMenuOpening=\"SearchWindow_ContextMenuOpening\"",
            mainWindow);
        Assert.Contains(
            "private void PopulateSearchWindowContextMenu(",
            codeBehind);
        Assert.Contains(
            "ContextMenu menu =\n"
            + "            CreateTaskbarContextMenu();",
            codeBehind.Replace("\r\n", "\n"));
        Assert.Contains(
            "AddWindowStateMenuItems(\n"
            + "            menu,\n"
            + "            window);",
            codeBehind.Replace("\r\n", "\n"));
        Assert.Contains(
            "private void AddWindowStateMenuItems(\n"
            + "        ItemsControl windowMenu,",
            codeBehind.Replace("\r\n", "\n"));
        Assert.Contains(
            "Header = \"切换到此窗口\"",
            codeBehind);
        Assert.Contains(
            ".ExecuteSearchResultCommand,\n"
            + "                CommandParameter = result",
            codeBehind.Replace("\r\n", "\n"));
        Assert.Contains(
            "InputGestureText = \"Enter\"",
            codeBehind);
        Assert.Contains(
            "Header = \"关闭窗口\"",
            codeBehind);
        Assert.Contains(
            "InputGestureText = \"Delete\"",
            codeBehind);
        Assert.Contains(
            "Header = \"移到 Panel 所在屏幕\"",
            codeBehind);
        Assert.Contains(
            ".MoveWindowToPanelDisplayCommand,",
            codeBehind);
        Assert.Contains(
            ".CanMoveToDisplay(",
            codeBehind);
        Assert.Contains(
            "ShellDisplayTarget.GetWorkingArea(",
            codeBehind);
        Assert.Contains(
            "MoveWindowToPanelDisplayCommand",
            codeBehind);
        Assert.Contains(
            "_windowTracker.MoveToDisplay(",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "ViewModels",
                    "MainViewModel.cs")));
        Assert.Contains(
            "Header = window.IsTopmost\n"
            + "                    ? \"取消置顶窗口\"\n"
            + "                    : \"置顶窗口\"",
            codeBehind.Replace("\r\n", "\n"));
        Assert.Contains(
            ".ToggleWindowTopmostCommand,",
            codeBehind);
        Assert.Contains(
            "IsChecked = window.IsTopmost",
            codeBehind);
        Assert.Contains(
            "_windowTracker.SetTopmost(",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "ViewModels",
                    "MainViewModel.cs")));
        string windowTracker = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "WindowTracker.cs"));
        Assert.Contains(
            "RequestSnapshotRefresh();\n"
            + "            ScheduleSnapshotRefresh();",
            windowTracker.Replace("\r\n", "\n"));
        Assert.Contains(
            "Header = \"把此应用的窗口移到 Panel 屏幕\"",
            codeBehind);
        Assert.Contains(
            ".MoveTaskWindowsToPanelDisplayCommand,",
            codeBehind);
        Assert.Contains(
            "task.WindowCount > 1",
            codeBehind);
        Assert.Contains(
            "WindowBatchMoveCoordinator.Execute(",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "ViewModels",
                    "MainViewModel.cs")));
        Assert.Contains(
            "AutomationProperties.SetName(\n"
            + "            menu,\n"
            + "            \"窗口操作 \"",
            codeBehind.Replace("\r\n", "\n"));
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
            8,
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
            "contextMenu.PlacementTarget is",
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
            "PopulatePartitionActions(",
            codeBehind);
        Assert.Contains(
            "PartitionActionTarget(",
            codeBehind);
        Assert.Contains(
            "target.ContextMenu.IsOpen = false;",
            codeBehind);
        Assert.DoesNotContain(
            "ItemsSource=\"{Binding Data.AllPartitions, Source={StaticResource VmProxy}}\"",
            organizer);
        Assert.Contains(
            "Tag=\"MoveToPartitionRoot\"",
            organizer);
        Assert.Contains(
            "Tag=\"CollectToPartitionRoot\"",
            organizer);
        Assert.Contains(
            "BeginTransientSurface();",
            codeBehind);
    }

    [Fact]
    public void OrganizerIconView_UsesFullWidthAdaptivePartitions()
    {
        string root = FindRepositoryRoot();
        string organizer = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "FileOrganizerView.xaml"));

        Assert.Contains(
            "ItemsSource=\"{Binding AllPartitions}\"",
            organizer);
        Assert.Contains(
            "AutomationProperties.Name=\"全宽图标收纳盒\"",
            organizer);
        Assert.Contains(
            "Visibility=\"{Binding IsListView, Converter={StaticResource InverseBoolConverter}}\"",
            organizer);
        Assert.Contains(
            "HorizontalScrollBarVisibility=\"Disabled\"",
            organizer);
        Assert.Contains(
            "AutomationProperties.Name=\"全宽图标收纳盒\">",
            organizer);
        Assert.Contains(
            "<controls:AdaptiveIconGridPanel",
            organizer);
        Assert.Contains(
            "MinimumItemWidth=\"{Binding Data.CardWidth, Source={StaticResource VmProxy}}\"",
            organizer);
        Assert.Contains(
            "<Setter Property=\"Margin\" Value=\"6\"/>",
            organizer);
        Assert.Contains(
            "HorizontalContentAlignment=\"Stretch\"",
            organizer);
        Assert.Contains(
            "Property=\"HorizontalAlignment\" Value=\"Stretch\"",
            organizer);
    }

    [Fact]
    public void DesktopIconLookup_PreservesShortcutCustomIcon()
    {
        string root = FindRepositoryRoot();
        string iconHelper = File.ReadAllText(
            Path.Combine(
                root,
                "Helpers",
                "IconHelper.cs"));
        string organizerService = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "FileOrganizerService.cs"));

        Assert.Contains(
            "TryResolveShortcutIcon(",
            iconHelper);
        Assert.DoesNotContain(
            "WScript.Shell",
            iconHelper);
        Assert.Contains(
            "GetIconLocation(",
            iconHelper);
        Assert.Contains(
            "SHDefExtractIcon(",
            iconHelper);
        Assert.Contains(
            "TryGetShellImageListIcon(path)",
            iconHelper);
        Assert.Contains(
            "IconHelper.ClearCache(e.FullPath);",
            organizerService);
        Assert.Contains(
            "IconHelper.ClearCache(fullPath);",
            organizerService);
        Assert.Contains(
            "CustomIconPath",
            organizerService);
        Assert.Contains(
            "preference?.CustomIconPath",
            organizerService);
    }

    [Fact]
    public void DesktopOrganizer_OffersPersistentPanelIcoAndStretchesGrid()
    {
        string root = FindRepositoryRoot();
        string organizer = File.ReadAllText(
            Path.Combine(root, "Views", "FileOrganizerView.xaml"));
        string codeBehind = File.ReadAllText(
            Path.Combine(root, "Views", "FileOrganizerView.xaml.cs"));
        string preference = File.ReadAllText(
            Path.Combine(root, "Models", "DesktopFilePreference.cs"));
        string schema = File.ReadAllText(
            Path.Combine(root, "Data", "AppDbContext.cs"));
        string iconStore = File.ReadAllText(
            Path.Combine(root, "Services", "PanelIconStore.cs"));
        string selector = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "DesktopIconPreferenceSelector.cs"));

        Assert.Contains("Header=\"更换 Panel 图标…\"", organizer);
        Assert.Contains("Header=\"恢复文件默认图标\"", organizer);
        Assert.Contains(
            "HorizontalContentAlignment=\"Stretch\"",
            organizer);
        Assert.Contains("OpenFileDialog", codeBehind);
        Assert.Contains("*.ico", codeBehind);
        Assert.Contains("CustomIconPath", preference);
        Assert.Contains("CustomIconIndex", preference);
        Assert.Contains(
            "ADD COLUMN CustomIconPath TEXT",
            schema);
        Assert.Contains("SHA256.HashDataAsync", iconStore);
        Assert.Contains("FocusPanel", iconStore);
        Assert.Contains("Icons", iconStore);
        Assert.Contains(
            "StringComparison.OrdinalIgnoreCase",
            selector);
        string organizerService = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "FileOrganizerService.cs"));
        Assert.Contains(
            "PreserveStandaloneIconAsync(",
            organizerService);
        Assert.Contains(
            "DesktopIconPreferenceSelector.Select(",
            organizerService);
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
            "CountCommonDesktopCandidates()",
            viewModel);
        Assert.Contains(
            "GetCommonDesktopCandidatePaths()",
            service);
        Assert.Contains(
            "授权完成前不会隐藏任何图标",
            viewModel);
        Assert.Contains(
            "IsProtectedPanelLauncher",
            service);
        Assert.Contains(
            "Restore poisoned desktop batch error",
            service);
        Assert.Contains(
            "AutoOrganizeStatus",
            viewModel);
        Assert.Contains(
            "CollectPendingCommonDesktopCommand",
            viewModel);
        Assert.Contains(
            "AuthorizationRequiredPaths",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Services",
                    "DesktopAutoOrganizePolicy.cs")));
        Assert.Contains(
            "授权收纳公共桌面项目",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Views",
                    "FileOrganizerView.xaml")));
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
    public void DesktopOrganizer_UsesOneElevatedSessionPerBatchWithoutElevatingShell()
    {
        string root = FindRepositoryRoot();
        string service = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "FileOrganizerService.cs"));
        string visibilityIo = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "DesktopVisibilityIo.cs"));
        string helper = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "DesktopVisibilityElevatedHelper.cs"));
        string manifest = File.ReadAllText(
            Path.Combine(root, "app.manifest"));

        Assert.Contains(
            "BeginElevatedBatchAsync()",
            service);
        Assert.Contains(
            "catch (OperationCanceledException)",
            service);
        Assert.Contains(
            "IDesktopVisibilityElevatedBatch",
            visibilityIo);
        Assert.Contains(
            "SessionCommand",
            helper);
        Assert.Contains(
            "NamedPipeServerStream",
            helper);
        Assert.Contains(
            "PipeOptions.CurrentUserOnly",
            helper);
        Assert.Contains(
            "level=\"asInvoker\"",
            manifest);
        Assert.DoesNotContain(
            "level=\"requireAdministrator\"",
            manifest);
    }

    [Fact]
    public void DesktopOrganizer_UsesDenseWrapForIconsAndVirtualizationForLists()
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
            1,
            CountOccurrences(
                xaml,
                "<controls:ViewportVirtualizingPanel"));
        Assert.Equal(
            1,
            CountOccurrences(
                xaml,
                "VirtualizingPanel.IsVirtualizing=\"True\""));
        Assert.Equal(
            1,
            CountOccurrences(
                xaml,
                "VirtualizingPanel.VirtualizationMode=\"Recycling\""));
        Assert.Contains(
            "<controls:AdaptiveIconGridPanel",
            xaml);
        Assert.Contains(
            "<Setter Property=\"Margin\" Value=\"6\"/>",
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
        Assert.DoesNotContain(
            "_okrViewModel",
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
    public void TaskbarExclusiveMode_RequiresPersistentSurfaceAndAllowsOnlyOneRepair()
    {
        string root = FindRepositoryRoot();
        string controller = File.ReadAllText(
            Path.Combine(root, "Services", "TaskbarController.cs"));
        string onboarding = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));

        Assert.Contains("AbsAutoHide", controller);
        Assert.Contains("UsesNativeAutoHide = false", controller);
        Assert.Contains(
            "UsesEmptyWindowRegion = false",
            controller);
        Assert.Contains(
            "UsesDwmCloak = false",
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
        Assert.Contains(
            "DwmSetWindowAttribute(",
            controller);
        Assert.Contains("ValidateReplacement()", controller);
        Assert.Contains(
            "_native.SetWorkArea(_state.PrimaryBounds)",
            controller);
        int guardStart = controller.IndexOf(
            "private void GuardReplacementSafely",
            StringComparison.Ordinal);
        int guardEnd = controller.IndexOf(
            "private bool IsCurrentReplacement",
            guardStart,
            StringComparison.Ordinal);
        string guard = controller[
            guardStart..guardEnd];
        Assert.DoesNotContain(
            "_native.SetWorkArea(",
            guard);
        Assert.DoesNotContain(
            "_native.SetTaskbarSurfaceSuppressed(",
            guard);
        Assert.DoesNotContain(
            "_native.SetTaskbarAppCloaked(",
            guard);
        Assert.DoesNotContain(
            "ApplyReplacement();",
            guard);
        Assert.Contains(
            "TryRepairReplacementOnce()",
            guard);
        Assert.Contains(
            "ref _repairAttempted",
            controller);
        Assert.Contains(
            "Interlocked.CompareExchange(",
            controller);
        Assert.Contains(
            "当前 Windows 环境没有接受任何持久任务栏抑制层",
            controller);
        Assert.Contains(
            "整次会话最多自动修复一次",
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
        Assert.Contains("_windowTracker.SetTrackingActive(true)", viewModel);
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
            "WindowTrackingEventPolicy.EventSystemAlert",
            tracker);
        Assert.Contains(
            "_attention.Observe(",
            tracker);
        Assert.Contains(
            "_attention.Retain(",
            tracker);
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
            "AttentionTaskbarIdentity =",
            viewModel);
        Assert.Contains(
            "RevealAttentionTaskbarApp",
            codeBehind);
    }

    [Fact]
    public void CompactTaskbar_ShowsAttentionWithoutNestedBorder()
    {
        string root = FindRepositoryRoot();
        string view = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml"));

        Assert.Contains(
            "Binding IsAttentionRequested",
            view);
        Assert.Contains(
            "AutomationProperties.Name=\"此应用需要注意\"",
            view);
        Assert.Contains(
            "AutomationProperties.Name=\"此窗口需要注意\"",
            view);
        Assert.Contains(
            "Value=\"{DynamicResource FocusWarningSoftBrush}\"",
            view);
        Assert.Contains(
            "Fill=\"{DynamicResource FocusWarningBrush}\"",
            view);
        Assert.DoesNotContain(
            "CornerRadius=\"7\"",
            view);
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
            "bool Minimize(IntPtr handle)",
            windowContract);
        Assert.Contains(
            "bool Maximize(IntPtr handle)",
            windowContract);
        Assert.Contains(
            "bool Restore(IntPtr handle)",
            windowContract);
        Assert.Contains(
            "bool Close(IntPtr handle)",
            windowContract);
        Assert.Contains(
            "MinimizeWindowCommand",
            codeBehind);
        Assert.Contains(
            "MaximizeWindowCommand",
            codeBehind);
        Assert.Contains(
            "RestoreWindowCommand",
            codeBehind);
        Assert.Contains(
            "AddWindowStateMenuItems(",
            codeBehind);
        Assert.Contains(
            "Header = \"还原窗口\"",
            codeBehind);
        Assert.Contains(
            "Header = \"最小化窗口\"",
            codeBehind);
        Assert.Contains(
            "Header = \"最大化窗口\"",
            codeBehind);
        Assert.Contains(
            "WindowStateActionPolicy",
            codeBehind);
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
        string mainWindowCode = File.ReadAllText(
            Path.Combine(
                root,
                "Views",
                "MainWindow.xaml.cs"));

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
        Assert.Contains(
            "PanelVerticalAnchorKey",
            repository);
        Assert.Contains(
            "OnPanelVerticalAnchorChanged(",
            viewModel);
        Assert.Contains(
            "SelectedValue=\"{Binding PanelVerticalAnchor",
            mainXaml);
        Assert.Contains(
            "CalculateAnchoredPanel(",
            mainWindowCode);
        Assert.Contains(
            "x:Name=\"TaskbarAppsHost\"",
            mainXaml);
        Assert.Contains(
            "MinHeight=\"98\"",
            mainXaml);
        Assert.Contains(
            "AutoHideDelayKey",
            repository);
        Assert.Contains(
            "OnAutoHideDelayMillisecondsChanged(",
            viewModel);
        Assert.Contains(
            "ItemsSource=\"{Binding AutoHideDelayOptions}\"",
            mainXaml);
        Assert.Contains(
            "SelectedValue=\"{Binding AutoHideDelayMilliseconds",
            mainXaml);
        Assert.Contains(
            "AutomationProperties.Name=\"鼠标离开后自动收起速度\"",
            mainXaml);
        Assert.Contains(
            "HotZoneDwellKey",
            repository);
        Assert.Contains(
            "OnHotZoneDwellMillisecondsChanged(",
            viewModel);
        Assert.Contains(
            "ItemsSource=\"{Binding HotZoneSensitivityOptions}\"",
            mainXaml);
        Assert.Contains(
            "SelectedValue=\"{Binding HotZoneDwellMilliseconds",
            mainXaml);
        Assert.Contains(
            "AutomationProperties.Name=\"右缘悬停呼出灵敏度\"",
            mainXaml);
        Assert.Contains(
            "KeepCompactDockVisibleKey",
            repository);
        Assert.Contains(
            "OnKeepCompactDockVisibleChanged(",
            viewModel);
        Assert.Contains(
            "IsChecked=\"{Binding KeepCompactDockVisible}\"",
            mainXaml);
        Assert.Contains(
            "AutomationProperties.Name=\"始终显示紧凑任务栏\"",
            mainXaml);
        Assert.Contains(
            "AutomationProperties.Name=\"首次启动始终显示紧凑任务栏\"",
            mainXaml);
        Assert.Contains(
            "PersistCompactDockPreference();",
            viewModel);
        Assert.Contains(
            "PersistentCompactDockDefaultPolicy.Resolve(",
            repository);
        Assert.Contains(
            "FirstRunOnboardingPolicy.ShouldShow(",
            viewModel);
        Assert.DoesNotContain(
            "|| !IsReplacementEnabled",
            viewModel);
        Assert.Contains(
            "Content=\"开始使用（保留任务栏）\"",
            mainXaml);
        Assert.Contains(
            "Content=\"立即接管任务栏\"",
            mainXaml);

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
        Assert.Contains(
            "int? delayMilliseconds = null",
            mainWindow);
        Assert.Contains(
            "?? _viewModel\n                .AutoHideDelayMilliseconds",
            mainWindow.Replace("\r\n", "\n"));
        Assert.Contains(
            ".HotZoneDwellMilliseconds);",
            mainWindow);
        Assert.Contains(
            ".SetDwellMilliseconds(",
            mainWindow);
        Assert.Contains(
            "ShellAutoHidePolicy.Decide(",
            mainWindow);
        Assert.Contains(
            "ShellAutoHideAction.CollapseToCompact",
            mainWindow);
        Assert.Contains(
            "ApplyCompactDockVisibilityPreference()",
            mainWindow);
        Assert.Contains(
            "DecideAvailabilityChange(",
            mainWindow);
        Assert.Contains(
            "PersistentCompactDockAvailabilityAction",
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
        Assert.Contains(
            "FocusNotificationCenter",
            manager);
        Assert.Contains(
            "PanelNotificationsDetailsExpander",
            File.ReadAllText(
                Path.Combine(
                    root,
                    "Views",
                    "MainWindow.xaml")));
        Assert.Contains(
            "ResolveNotificationAction",
            mainWindow);
        Assert.Contains(
            "FocusNotificationActionKind.OpenUpdates",
            mainWindow);
        Assert.Contains(
            "FocusNotificationActionKind.OpenPomodoro",
            mainWindow);
        Assert.Contains(
            "FocusNotificationActionKind.OpenTasks",
            mainWindow);
        Assert.Contains(
            "FocusNotificationActionKind\n                        .OpenDesktopOrganizer",
            mainWindow.Replace("\r\n", "\n"));
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
