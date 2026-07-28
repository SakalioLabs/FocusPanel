using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FocusPanel.Controls;
using FocusPanel.Models;
using FocusPanel.ViewModels;
using FocusPanel.Views;

namespace FocusPanel.Services;

internal static class UiSmokeTestRunner
{
    private static readonly string[] CriticalGlobalResources =
    {
        "BooleanToVisibilityConverter",
        "FocusShellTintBrush",
        "FocusSurfaceBrush",
        "FocusSurfaceSoftBrush",
        "FocusSurfaceStrongBrush",
        "FocusStrokeBrush",
        "FocusKeyboardFocusBrush",
        "FocusAccentSoftBrush",
        "FocusWarningBrush",
        "FocusWarningSoftBrush",
        "FocusWarningTextBrush",
        "FocusDangerSoftBrush",
        "FocusOverlayBrush",
        "FocusEdgeIndicatorBrush",
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
        "FocusDisplayText",
        "FocusContextMenu",
        "FocusMenuItem",
        "FocusMenuSeparator",
        "FocusToolTip",
        "FocusComboBox",
        "FocusComboBoxItem",
        "FocusListBox",
        "FocusListBoxItem",
        "FocusCheckBox",
        "FocusScrollBar",
        "FocusSlider",
        "FocusLinearProgress",
        "FocusTextBox",
        "FocusSearchBox",
        "FocusPasswordBox",
        "FocusToggleButton",
        "FocusSegmentRadioButton",
        "FocusRowButton",
        "FocusDangerButton"
    };

    public static int Run(
        string? reportPath,
        string? dashboardSnapshotPath = null,
        string? calendarSnapshotPath = null)
    {
        var results = new List<string>();
        var failures = new List<string>();

        try
        {
            var application = new App();
            application.InitializeComponent();

            foreach (string key in CriticalGlobalResources)
            {
                if (application.TryFindResource(key) == null)
                    failures.Add($"全局资源缺失：{key}");
                else
                    results.Add($"PASS 资源 {key}");
            }

            CheckSurface("DashboardView", () => new DashboardView(), results, failures);
            CheckSurface("TasksView", () => new TasksView(), results, failures);
            CheckSurface("PomodoroView", () => new PomodoroView(), results, failures);
            CheckSurface("FileOrganizerView", () => new FileOrganizerView(), results, failures);
            CheckSurface("OkrView", () => new OkrView(), results, failures);
            CheckSurface("AIAssistantView", () => new AIAssistantView(), results, failures);
            CheckSurface("TaskDetailWindow", () => new TaskDetailWindow(), results, failures);
            CheckSurface("PomodoroFloatingWindow", () => new PomodoroFloatingWindow(), results, failures);
            CheckSurface("EdgeIndicatorWindow", () => new EdgeIndicatorWindow(), results, failures);
            CheckSurface("CalendarPanelView", () => new CalendarPanelView(), results, failures);
            CheckSurface(
                "AppIconPresenter",
                () => new AppIconPresenter
                {
                    DisplayName = "FocusPanel"
                },
                results,
                failures);
            CheckFluentContextMenu(
                results,
                failures);
            CheckFluentToolTip(
                results,
                failures);
            CheckFluentComboBox(
                results,
                failures);
            CheckFluentListBox(
                results,
                failures);
            CheckFluentTypography(
                results,
                failures);
            CheckFluentActionButtons(
                results,
                failures);
            CheckFluentCheckBox(
                results,
                failures);
            CheckFluentScrollBars(
                results,
                failures);
            CheckFluentSliderAndProgress(
                results,
                failures);
            CheckFluentTextInputs(
                results,
                failures);
            CheckFluentSelectionControls(
                results,
                failures);
            CheckDynamicThemeStateTokens(
                results,
                failures);
            CheckPartitionRefreshScroll(
                results,
                failures);
            CheckDesktopPathRefreshScroll(
                results,
                failures);
            CheckLargeOrganizerVirtualization(
                results,
                failures);
            if (!string.IsNullOrWhiteSpace(
                    dashboardSnapshotPath))
            {
                RenderDashboardSnapshot(
                    application,
                    dashboardSnapshotPath);
                results.Add(
                    "PASS DashboardView 视觉快照");
            }
            if (!string.IsNullOrWhiteSpace(
                    calendarSnapshotPath))
            {
                RenderCalendarSnapshot(
                    application,
                    calendarSnapshotPath);
                results.Add(
                    "PASS CalendarPanelView 视觉快照");
            }
        }
        catch (Exception ex)
        {
            failures.Add($"初始化失败：{ex}");
        }

        WriteReport(reportPath, results, failures);
        return failures.Count == 0 ? 0 : 1;
    }

    private static void RenderDashboardSnapshot(
        Application application,
        string path)
    {
        var viewModel = new DashboardViewModel();
        viewModel.ApplySnapshot(
            new DashboardSnapshot(
                7,
                2,
                50,
                2,
                14,
                new[]
                {
                    new DashboardTaskSummary(
                        1,
                        "完成发布版视觉检查",
                        "FocusPanel 迭代",
                        "进行中"),
                    new DashboardTaskSummary(
                        2,
                        "验证任务栏安全恢复",
                        "稳定性",
                        "待处理"),
                    new DashboardTaskSummary(
                        3,
                        "整理下一轮迭代目标",
                        "产品规划",
                        "待处理")
                },
                new[]
                {
                    new DashboardOkrSummary(
                        1,
                        "让侧边任务栏稳定替代原生体验",
                        72),
                    new DashboardOkrSummary(
                        2,
                        "完成所有核心工作区 Fluent 化",
                        88)
                },
                new DateTime(
                    2026,
                    7,
                    28,
                    15,
                    20,
                    0)));
        var view = new DashboardView
        {
            DataContext = viewModel
        };
        var surface = new Border
        {
            Background =
                (Brush)application.FindResource(
                    "FocusSurfaceStrongBrush"),
            Child = view
        };
        var size = new Size(640, 820);
        surface.Measure(size);
        surface.Arrange(new Rect(size));
        surface.UpdateLayout();

        var bitmap = new RenderTargetBitmap(
            640,
            820,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(surface);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(
            BitmapFrame.Create(bitmap));

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
        viewModel.Dispose();
    }

    private static void CheckSurface(
        string name,
        Func<FrameworkElement> factory,
        ICollection<string> results,
        ICollection<string> failures)
    {
        try
        {
            FrameworkElement surface = factory();
            surface.Measure(new Size(1200, 800));
            surface.Arrange(new Rect(0, 0, 1200, 800));
            surface.UpdateLayout();
            results.Add($"PASS 界面 {name}");
        }
        catch (Exception ex)
        {
            failures.Add($"界面 {name} 加载失败：{ex}");
        }
    }

    private static void CheckFluentContextMenu(
        ICollection<string> results,
        ICollection<string> failures)
    {
        try
        {
            var checkedItem = new MenuItem
            {
                Header = "当前窗口",
                IsCheckable = true,
                IsChecked = true
            };
            var submenu = new MenuItem
            {
                Header = "关机或注销"
            };
            submenu.Items.Add(
                new MenuItem
                {
                    Header = "锁定"
                });
            var menu = new ContextMenu();
            menu.Items.Add(checkedItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(submenu);

            menu.Measure(new Size(420, 800));
            menu.Arrange(
                new Rect(
                    0,
                    0,
                    Math.Max(
                        188,
                        menu.DesiredSize.Width),
                    Math.Max(
                        120,
                        menu.DesiredSize.Height)));
            menu.UpdateLayout();
            menu.ApplyTemplate();
            checkedItem.ApplyTemplate();
            submenu.ApplyTemplate();

            if (menu.Template.FindName(
                    "MenuSurface",
                    menu) is not Border)
            {
                failures.Add(
                    "Fluent 菜单缺少单层圆角表面");
                return;
            }

            if (checkedItem.Template.FindName(
                    "ItemChrome",
                    checkedItem) is not Border
                || submenu.Template.FindName(
                    "PART_Popup",
                    submenu) is not Popup)
            {
                failures.Add(
                    "Fluent 菜单项缺少高亮或子菜单模板");
                return;
            }

            results.Add(
                "PASS Fluent 菜单叶项、勾选、分隔线与子菜单");
        }
        catch (Exception ex)
        {
            failures.Add(
                $"Fluent 菜单加载失败：{ex}");
        }
    }

    private static void CheckPartitionRefreshScroll(
        ICollection<string> results,
        ICollection<string> failures)
    {
        try
        {
            var all =
                new ObservableCollection<
                    PartitionViewModel>();
            var left =
                new ObservableCollection<
                    PartitionViewModel>();
            var right =
                new ObservableCollection<
                    PartitionViewModel>();
            for (int index = 0; index < 30; index++)
            {
                var partition =
                    new PartitionViewModel(
                        $"收纳盒 {index + 1}")
                    {
                        IsCustom = true,
                        ColumnIndex = 0
                    };
                all.Add(partition);
                left.Add(partition);
            }

            var items = new ItemsControl
            {
                ItemsSource = left,
                DisplayMemberPath =
                    nameof(PartitionViewModel.Name)
            };
            var viewer = new ScrollViewer
            {
                Width = 280,
                Height = 90,
                Content = items,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto
            };
            var size = new Size(280, 90);
            viewer.Measure(size);
            viewer.Arrange(new Rect(size));
            viewer.UpdateLayout();
            viewer.ScrollToVerticalOffset(80);
            viewer.UpdateLayout();
            double before =
                viewer.VerticalOffset;
            if (before <= 0)
            {
                failures.Add(
                    "分区刷新滚动验证未建立有效偏移");
                return;
            }

            var desired =
                Enumerable.Range(1, 30)
                    .Select(index =>
                        new PartitionViewModel(
                            $"收纳盒 {index}")
                        {
                            IsCustom = true,
                            ColumnIndex = 0
                        })
                    .ToList();
            PartitionCollectionSynchronizer
                .Synchronize(
                    all,
                    left,
                    right,
                    desired);
            viewer.UpdateLayout();

            if (Math.Abs(
                    viewer.VerticalOffset
                    - before) > 0.1)
            {
                failures.Add(
                    "分区差量刷新改变了滚动偏移");
                return;
            }
            results.Add(
                "PASS 分区差量刷新保持滚动偏移");
        }
        catch (Exception ex)
        {
            failures.Add(
                $"分区滚动稳定性验证失败：{ex}");
        }
    }

    private static void CheckFluentToolTip(
        ICollection<string> results,
        ICollection<string> failures)
    {
        try
        {
            var toolTip = new ToolTip
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "编辑器",
                            FontWeight =
                                FontWeights.SemiBold
                        },
                        new TextBlock
                        {
                            Text =
                                "Shift+左键或中键启动新实例"
                        }
                    }
                }
            };

            toolTip.Measure(
                new Size(
                    360,
                    160));
            toolTip.Arrange(
                new Rect(
                    0,
                    0,
                    Math.Max(
                        120,
                        toolTip.DesiredSize.Width),
                    Math.Max(
                        44,
                        toolTip.DesiredSize.Height)));
            toolTip.UpdateLayout();
            toolTip.ApplyTemplate();

            if (toolTip.Template.FindName(
                    "ToolTipSurface",
                    toolTip) is not Border surface
                || surface.CornerRadius.TopLeft <= 0)
            {
                failures.Add(
                    "Fluent 工具提示缺少单层圆角表面");
                return;
            }

            if (toolTip.HasDropShadow
                || !ReferenceEquals(
                    toolTip.Background,
                    Application.Current.FindResource(
                        "FocusSurfaceStrongBrush"))
                || !ReferenceEquals(
                    toolTip.Foreground,
                    Application.Current.FindResource(
                        "FocusTextBrush")))
            {
                failures.Add(
                    "Fluent 工具提示未跟随动态主题或仍带系统阴影");
                return;
            }

            results.Add(
                "PASS Fluent 工具提示圆角与动态主题");
        }
        catch (Exception ex)
        {
            failures.Add(
                $"Fluent 工具提示加载失败：{ex}");
        }
    }

    private static void CheckDesktopPathRefreshScroll(
        ICollection<string> results,
        ICollection<string> failures)
    {
        try
        {
            var all =
                new ObservableCollection<DesktopFile>();
            var visible =
                new ObservableCollection<DesktopFile>();
            for (int index = 0; index < 40; index++)
            {
                var file = new DesktopFile
                {
                    Name = $"项目 {index + 1:D2}.txt",
                    FullPath =
                        $@"C:\Desktop\项目 {index + 1:D2}.txt",
                    FileType = "Document"
                };
                all.Add(file);
                visible.Add(file);
            }
            DesktopFile selected = visible[18];
            selected.IsSelected = true;

            var items = new ItemsControl
            {
                ItemsSource = visible,
                DisplayMemberPath =
                    nameof(DesktopFile.Name)
            };
            var viewer = new ScrollViewer
            {
                Width = 280,
                Height = 90,
                Content = items,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto
            };
            var size = new Size(280, 90);
            viewer.Measure(size);
            viewer.Arrange(new Rect(size));
            viewer.UpdateLayout();
            viewer.ScrollToVerticalOffset(100);
            viewer.UpdateLayout();
            double before = viewer.VerticalOffset;
            if (before <= 0)
            {
                failures.Add(
                    "路径刷新滚动验证未建立有效偏移");
                return;
            }

            var refreshed = new DesktopFile
            {
                Name = selected.Name,
                FullPath = selected.FullPath,
                FileType = "Document",
                Size = 4096
            };
            DesktopFileCollectionSynchronizer.Apply(
                all,
                visible,
                new[]
                {
                    new DesktopItemRefresh(
                        selected.FullPath,
                        refreshed,
                        false)
                });
            viewer.UpdateLayout();

            if (Math.Abs(
                    viewer.VerticalOffset
                    - before) > 0.1
                || !selected.IsSelected
                || selected.Size != 4096)
            {
                failures.Add(
                    "路径差量刷新未保留滚动或选择状态");
                return;
            }
            results.Add(
                "PASS 路径差量刷新保持卡片与滚动偏移");
        }
        catch (Exception ex)
        {
            failures.Add(
                $"路径刷新滚动稳定性验证失败：{ex}");
        }
    }

    private static void CheckFluentCheckBox(
        ICollection<string> results,
        ICollection<string> failures)
    {
        try
        {
            var checkBox = new CheckBox
            {
                Content = "随 Windows 启动 FocusPanel",
                IsChecked = true,
                Width = 280
            };

            checkBox.Measure(
                new Size(
                    320,
                    100));
            checkBox.Arrange(
                new Rect(
                    0,
                    0,
                    280,
                    Math.Max(
                        44,
                        checkBox.DesiredSize.Height)));
            checkBox.UpdateLayout();
            checkBox.ApplyTemplate();

            if (checkBox.Template.FindName(
                    "InteractionSurface",
                    checkBox) is not Border surface
                || checkBox.Template.FindName(
                    "CheckBoxChrome",
                    checkBox) is not Border chrome
                || chrome.CornerRadius.TopLeft <= 0
                || checkBox.Template.FindName(
                    "CheckGlyph",
                    checkBox) is not TextBlock glyph)
            {
                failures.Add(
                    "Fluent 勾选框缺少点击表面、圆角标记或勾选字形");
                return;
            }

            if (glyph.Visibility != Visibility.Visible
                || !ReferenceEquals(
                    chrome.Background,
                    Application.Current.FindResource(
                        "FocusAccentBrush"))
                || !ReferenceEquals(
                    checkBox.Foreground,
                    Application.Current.FindResource(
                        "FocusTextBrush"))
                || surface.MinHeight < 44)
            {
                failures.Add(
                    "Fluent 勾选框未应用选中状态、动态主题或最小点击区");
                return;
            }

            results.Add(
                "PASS Fluent 勾选框点击区、圆角与选中状态");
        }
        catch (Exception ex)
        {
            failures.Add(
                $"Fluent 勾选框加载失败：{ex}");
        }
    }

    private static void CheckFluentComboBox(
        ICollection<string> results,
        ICollection<string> failures)
    {
        try
        {
            var first = new ComboBoxItem
            {
                Content = "跟随系统",
                IsSelected = true
            };
            var second = new ComboBoxItem
            {
                Content = "深色"
            };
            var comboBox = new ComboBox
            {
                Width = 220,
                Items =
                {
                    first,
                    second
                }
            };

            comboBox.Measure(
                new Size(
                    320,
                    200));
            comboBox.Arrange(
                new Rect(
                    0,
                    0,
                    220,
                    Math.Max(
                        44,
                        comboBox.DesiredSize.Height)));
            comboBox.UpdateLayout();
            comboBox.ApplyTemplate();
            first.ApplyTemplate();

            if (comboBox.Template.FindName(
                    "DropDownToggle",
                    comboBox) is not ToggleButton
                || comboBox.Template.FindName(
                    "PART_Popup",
                    comboBox) is not Popup
                || comboBox.Template.FindName(
                    "DropDownSurface",
                    comboBox) is not Border surface
                || surface.CornerRadius.TopLeft <= 0)
            {
                failures.Add(
                    "Fluent 下拉框缺少封闭按钮、Popup 或单层圆角表面");
                return;
            }

            if (first.Template.FindName(
                    "ItemChrome",
                    first) is not Border
                || first.Template.FindName(
                    "SelectionIndicator",
                    first) is not Border)
            {
                failures.Add(
                    "Fluent 下拉项缺少高亮表面或选中标记");
                return;
            }

            if (!ReferenceEquals(
                    comboBox.Foreground,
                    Application.Current.FindResource(
                        "FocusTextBrush"))
                || !ReferenceEquals(
                    surface.Background,
                    Application.Current.FindResource(
                        "FocusSurfaceStrongBrush")))
            {
                failures.Add(
                    "Fluent 下拉框未使用动态主题资源");
                return;
            }

            results.Add(
                "PASS Fluent 下拉框封闭态、Popup 与选中项");
        }
        catch (Exception ex)
        {
            failures.Add(
                $"Fluent 下拉框加载失败：{ex}");
        }
    }

    private static void CheckFluentScrollBars(
        ICollection<string> results,
        ICollection<string> failures)
    {
        try
        {
            var content = new Border
            {
                Width = 420,
                Height = 640,
                Background = Brushes.Transparent
            };
            var viewer = new ScrollViewer
            {
                Width = 220,
                Height = 160,
                Content = content,
                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto
            };
            var size = new Size(220, 160);
            viewer.Measure(size);
            viewer.Arrange(new Rect(size));
            viewer.UpdateLayout();
            viewer.ApplyTemplate();

            ScrollBar? vertical =
                FindVisualChildren<ScrollBar>(viewer)
                    .FirstOrDefault(
                        bar =>
                            bar.Orientation
                            == Orientation.Vertical);
            ScrollBar? horizontal =
                FindVisualChildren<ScrollBar>(viewer)
                    .FirstOrDefault(
                        bar =>
                            bar.Orientation
                            == Orientation.Horizontal);
            if (vertical == null
                || horizontal == null
                || viewer.ScrollableHeight <= 0
                || viewer.ScrollableWidth <= 0)
            {
                failures.Add(
                    "Fluent 滚动条未覆盖真实纵向和横向滚动容器");
                return;
            }

            vertical.ApplyTemplate();
            horizontal.ApplyTemplate();
            if (vertical.Template.FindName(
                    "ScrollThumb",
                    vertical) is not Thumb verticalThumb
                || horizontal.Template.FindName(
                    "ScrollThumb",
                    horizontal) is not Thumb horizontalThumb)
            {
                failures.Add(
                    "Fluent 滚动条缺少纵向或横向圆角滑块");
                return;
            }

            verticalThumb.ApplyTemplate();
            horizontalThumb.ApplyTemplate();
            if (verticalThumb.Template.FindName(
                    "ThumbSurface",
                    verticalThumb) is not Border verticalSurface
                || horizontalThumb.Template.FindName(
                    "ThumbSurface",
                    horizontalThumb) is not Border horizontalSurface
                || verticalSurface.CornerRadius.TopLeft <= 0
                || horizontalSurface.CornerRadius.TopLeft <= 0)
            {
                failures.Add(
                    "Fluent 滚动滑块未应用圆角表面");
                return;
            }

            if (!ReferenceEquals(
                    vertical.Foreground,
                    Application.Current.FindResource(
                        "FocusMutedTextBrush"))
                || !ReferenceEquals(
                    verticalThumb.Background,
                    Application.Current.FindResource(
                        "FocusMutedTextBrush"))
                || vertical.Width > 10
                || horizontal.Height > 10
                || verticalThumb.MinHeight < 28
                || horizontalThumb.MinWidth < 28)
            {
                failures.Add(
                    "Fluent 滚动条未使用动态主题、紧凑轨道或最小滑块");
                return;
            }

            results.Add(
                "PASS Fluent 纵横滚动条圆角、动态主题与紧凑轨道");
        }
        catch (Exception ex)
        {
            failures.Add(
                $"Fluent 滚动条加载失败：{ex}");
        }
    }

    private static void CheckFluentListBox(
        ICollection<string> results,
        ICollection<string> failures)
    {
        try
        {
            var first = new ListBoxItem
            {
                Content =
                    "release · 文件资源管理器"
            };
            var selected = new ListBoxItem
            {
                Content =
                    "packages · 文件资源管理器",
                IsSelected = true
            };
            var listBox = new ListBox
            {
                Width = 280,
                Height = 120,
                Items =
                {
                    first,
                    selected
                }
            };

            var size = new Size(280, 120);
            listBox.Measure(size);
            listBox.Arrange(new Rect(size));
            listBox.UpdateLayout();
            listBox.ApplyTemplate();
            selected.ApplyTemplate();
            selected.UpdateLayout();
            if (selected.Template.FindName(
                    "ListItemChrome",
                    selected) is not Border chrome
                || chrome.CornerRadius.TopLeft <= 0)
            {
                failures.Add(
                    "Fluent 列表项缺少单层圆角选中表面");
                return;
            }

            if (!ReferenceEquals(
                    selected.Foreground,
                    Application.Current.FindResource(
                        "FocusTextBrush"))
                || !ReferenceEquals(
                    chrome.Background,
                    Application.Current.FindResource(
                        "FocusAccentSoftBrush"))
                || !ReferenceEquals(
                    chrome.BorderBrush,
                    Application.Current.FindResource(
                        "FocusAccentBrush"))
                || selected.FocusVisualStyle != null
                || selected.MinHeight < 44)
            {
                failures.Add(
                    "Fluent 列表选中态未使用动态前景、强调背景或最小点击区");
                return;
            }

            results.Add(
                "PASS Fluent 列表选中态文字、强调色与点击区");
        }
        catch (Exception ex)
        {
            failures.Add(
                $"Fluent 列表加载失败：{ex}");
        }
    }

    private static void CheckFluentTypography(
        ICollection<string> results,
        ICollection<string> failures)
    {
        try
        {
            TextBlock CreateText(
                string key,
                string text)
            {
                return new TextBlock
                {
                    Text = text,
                    Style =
                        (Style)Application.Current
                            .FindResource(key)
                };
            }

            TextBlock page = CreateText(
                "FocusPageTitleText",
                "AI 助手");
            TextBlock section = CreateText(
                "FocusSectionTitleText",
                "状态中心");
            TextBlock card = CreateText(
                "FocusCardTitleText",
                "软件更新");
            TextBlock body = CreateText(
                "FocusBodyText",
                "正文");
            TextBlock secondary = CreateText(
                "FocusSecondaryBodyText",
                "辅助正文");
            TextBlock caption = CreateText(
                "FocusCaptionText",
                "说明");
            TextBlock meta = CreateText(
                "FocusMetaText",
                "元信息");
            TextBlock metric = CreateText(
                "FocusMetricText",
                "28");
            TextBlock display = CreateText(
                "FocusDisplayText",
                "25:00");

            var panel = new StackPanel
            {
                Children =
                {
                    page,
                    section,
                    card,
                    body,
                    secondary,
                    caption,
                    meta,
                    metric,
                    display
                }
            };
            panel.Measure(
                new Size(
                    640,
                    480));
            panel.Arrange(
                new Rect(
                    0,
                    0,
                    640,
                    Math.Max(
                        240,
                        panel.DesiredSize.Height)));
            panel.UpdateLayout();

            Brush muted =
                (Brush)Application.Current.FindResource(
                    "FocusMutedTextBrush");
            if (page.FontSize != 28
                || section.FontSize != 18
                || card.FontSize != 15
                || body.FontSize != 13
                || caption.FontSize != 12
                || meta.FontSize != 11
                || metric.FontSize != 28
                || display.FontSize != 64
                || page.FontWeight
                    != FontWeights.SemiBold
                || !string.Equals(
                    page.FontFamily.Source,
                    "Segoe UI Variable Display",
                    StringComparison.Ordinal)
                || !ReferenceEquals(
                    secondary.Foreground,
                    muted)
                || !ReferenceEquals(
                    caption.Foreground,
                    muted)
                || !ReferenceEquals(
                    meta.Foreground,
                    muted))
            {
                failures.Add(
                    "Fluent 字体层级的字号、字重、字体或辅助文字主题不一致");
                return;
            }

            results.Add(
                "PASS Fluent 页面、章节、正文、说明与指标字体层级");
        }
        catch (Exception ex)
        {
            failures.Add(
                $"Fluent 字体层级加载失败：{ex}");
        }
    }

    private static void CheckFluentActionButtons(
        ICollection<string> results,
        ICollection<string> failures)
    {
        try
        {
            var row = new Button
            {
                Content = "文件资源管理器",
                Width = double.NaN,
                Height = double.NaN,
                Style = (Style)Application.Current.FindResource(
                    "FocusRowButton")
            };
            var danger = new Button
            {
                Content = "删除目标",
                Style = (Style)Application.Current.FindResource(
                    "FocusDangerButton")
            };
            var panel = new StackPanel
            {
                Width = 320,
                Children =
                {
                    row,
                    danger
                }
            };
            panel.Measure(new Size(320, 160));
            panel.Arrange(new Rect(0, 0, 320, 160));
            panel.UpdateLayout();
            row.ApplyTemplate();
            danger.ApplyTemplate();

            Border? rowChrome =
                row.Template.FindName("Chrome", row) as Border;
            Border? dangerChrome =
                danger.Template.FindName("Chrome", danger) as Border;
            if (rowChrome == null
                || dangerChrome == null
                || !double.IsNaN(row.Width)
                || !double.IsNaN(row.Height)
                || row.MinHeight < 44
                || row.HorizontalContentAlignment
                    != HorizontalAlignment.Stretch
                || !ReferenceEquals(
                    row.Foreground,
                    Application.Current.FindResource(
                        "FocusTextBrush"))
                || !ReferenceEquals(
                    danger.Background,
                    Application.Current.FindResource(
                        "FocusDangerSoftBrush"))
                || !ReferenceEquals(
                    danger.Foreground,
                    Application.Current.FindResource(
                        "FocusDangerBrush"))
                || danger.BorderThickness.Left < 1
                || danger.FontWeight != FontWeights.SemiBold
                || rowChrome.CornerRadius.TopLeft != 8
                || dangerChrome.CornerRadius.TopLeft != 8)
            {
                failures.Add(
                    "Fluent 行按钮或危险操作未使用统一圆角、动态画刷与点击区");
                return;
            }

            results.Add(
                "PASS Fluent 行按钮与危险操作动态状态");
        }
        catch (Exception ex)
        {
            failures.Add(
                $"Fluent 操作按钮加载失败：{ex}");
        }
    }

    private static void CheckFluentSliderAndProgress(
        ICollection<string> results,
        ICollection<string> failures)
    {
        try
        {
            var slider = new Slider
            {
                Width = 240,
                Minimum = 0,
                Maximum = 1,
                Value = 0.62,
                Style =
                    (Style)Application.Current.FindResource(
                        "FocusSlider")
            };
            slider.ApplyTemplate();
            slider.Measure(new Size(280, 80));
            slider.Arrange(new Rect(0, 0, 240, 44));
            slider.UpdateLayout();

            if (slider.Template.FindName(
                    "PART_Track",
                    slider) is not Track sliderTrack
                || slider.Template.FindName(
                    "SliderThumb",
                    slider) is not Thumb sliderThumb
                || slider.Template.FindName(
                    "DecreaseTrackButton",
                    slider) is not RepeatButton decreaseTrack
                || slider.Template.FindName(
                    "IncreaseTrackButton",
                    slider) is not RepeatButton increaseTrack)
            {
                failures.Add(
                    "Fluent 滑块缺少标准轨道、圆角滑块或双段进度");
                return;
            }

            sliderThumb.ApplyTemplate();
            if (sliderThumb.Template.FindName(
                    "SliderThumbSurface",
                    sliderThumb) is not Border thumbSurface
                || thumbSurface.CornerRadius.TopLeft <= 0
                || sliderTrack.Orientation !=
                    Orientation.Horizontal
                || Math.Abs(
                    sliderTrack.Value
                    - slider.Value) > 0.001
                || slider.MinHeight < 44)
            {
                failures.Add(
                    "Fluent 滑块未应用圆角滑块、方向或 44px 交互区");
                return;
            }

            if (!ReferenceEquals(
                    decreaseTrack.Background,
                    Application.Current.FindResource(
                        "FocusAccentBrush"))
                || !ReferenceEquals(
                    increaseTrack.Background,
                    Application.Current.FindResource(
                        "FocusSurfaceStrongBrush")))
            {
                failures.Add(
                    "Fluent 滑块已选和未选轨道未使用动态主题");
                return;
            }

            var progress = new ProgressBar
            {
                Width = 240,
                Height = 8,
                Minimum = 0,
                Maximum = 100,
                Value = 64,
                Style =
                    (Style)Application.Current.FindResource(
                        "FocusLinearProgress")
            };
            progress.ApplyTemplate();
            progress.Measure(new Size(240, 20));
            progress.Arrange(new Rect(0, 0, 240, 8));
            progress.UpdateLayout();
            if (progress.Template.FindName(
                    "PART_Track",
                    progress) is not Border progressTrack
                || progress.Template.FindName(
                    "PART_Indicator",
                    progress) is not Border indicator
                || progress.Template.FindName(
                    "IndeterminateIndicator",
                    progress) is not Border indeterminate
                || progressTrack.CornerRadius.TopLeft <= 0
                || indicator.ActualWidth <= 0)
            {
                failures.Add(
                    "Fluent 进度条缺少圆角轨道、有效进度或加载层");
                return;
            }

            progress.IsIndeterminate = true;
            progress.UpdateLayout();
            if (indeterminate.Visibility != Visibility.Visible
                || indicator.Visibility != Visibility.Collapsed
                || !ReferenceEquals(
                    progress.Foreground,
                    Application.Current.FindResource(
                        "FocusAccentBrightBrush"))
                || !ReferenceEquals(
                    progress.Background,
                    Application.Current.FindResource(
                        "FocusSurfaceStrongBrush")))
            {
                failures.Add(
                    "Fluent 进度条未切换不确定状态或动态主题");
                return;
            }

            results.Add(
                "PASS Fluent 音量滑块与确定/加载进度状态");
        }
        catch (Exception ex)
        {
            failures.Add(
                $"Fluent 滑块和进度条加载失败：{ex}");
        }
    }

    private static void CheckFluentTextInputs(
        ICollection<string> results,
        ICollection<string> failures)
    {
        try
        {
            var textBox = new TextBox
            {
                Width = 280,
                Text = "深色主题输入",
                Style =
                    (Style)Application.Current.FindResource(
                        "FocusTextBox")
            };
            textBox.ApplyTemplate();
            textBox.Measure(new Size(320, 80));
            textBox.Arrange(new Rect(0, 0, 280, 44));
            textBox.SelectAll();
            textBox.UpdateLayout();

            if (textBox.Template.FindName(
                    "TextInputChrome",
                    textBox) is not Border textChrome
                || textBox.Template.FindName(
                    "PART_ContentHost",
                    textBox) is not ScrollViewer
                || textChrome.CornerRadius.TopLeft <= 0
                || textBox.SelectionLength !=
                    textBox.Text.Length)
            {
                failures.Add(
                    "Fluent 文本框缺少圆角单表面、内容宿主或文本选择");
                return;
            }

            if (!ReferenceEquals(
                    textBox.CaretBrush,
                    Application.Current.FindResource(
                        "FocusAccentBrightBrush"))
                || !ReferenceEquals(
                    textBox.SelectionBrush,
                    Application.Current.FindResource(
                        "FocusAccentBrush"))
                || !ReferenceEquals(
                    textBox.SelectionTextBrush,
                    Application.Current.FindResource(
                        "FocusTextBrush"))
                || textBox.SelectionOpacity < 0.7
                || textBox.FontFamily.Source !=
                    "Segoe UI Variable Text"
                || textBox.MinHeight < 44)
            {
                failures.Add(
                    "Fluent 文本框未应用动态光标、选择色、字体或点击高度");
                return;
            }

            textBox.IsReadOnly = true;
            textBox.UpdateLayout();
            if (!ReferenceEquals(
                    textChrome.Background,
                    Application.Current.FindResource(
                        "FocusSurfaceBrush"))
                || textChrome.Opacity > 0.8)
            {
                failures.Add(
                    "Fluent 文本框未显示明确只读状态");
                return;
            }

            var passwordBox = new PasswordBox
            {
                Width = 280,
                Password = "focus-panel",
                Style =
                    (Style)Application.Current.FindResource(
                        "FocusPasswordBox")
            };
            passwordBox.ApplyTemplate();
            passwordBox.Measure(new Size(320, 80));
            passwordBox.Arrange(new Rect(0, 0, 280, 44));
            passwordBox.UpdateLayout();
            if (passwordBox.Template.FindName(
                    "PasswordInputChrome",
                    passwordBox) is not Border passwordChrome
                || passwordChrome.CornerRadius.TopLeft <= 0
                || !ReferenceEquals(
                    passwordBox.CaretBrush,
                    Application.Current.FindResource(
                        "FocusAccentBrightBrush"))
                || !ReferenceEquals(
                    passwordBox.SelectionBrush,
                    Application.Current.FindResource(
                        "FocusAccentBrush"))
                || !ReferenceEquals(
                    passwordBox.SelectionTextBrush,
                    Application.Current.FindResource(
                        "FocusTextBrush")))
            {
                failures.Add(
                    "Fluent 密码框缺少圆角表面或动态输入选择主题");
                return;
            }

            passwordBox.IsEnabled = false;
            passwordBox.UpdateLayout();
            if (passwordChrome.Opacity > 0.4)
            {
                failures.Add(
                    "Fluent 密码框未显示明确禁用状态");
                return;
            }

            results.Add(
                "PASS Fluent 文本与密码输入选择、只读和禁用状态");
        }
        catch (Exception ex)
        {
            failures.Add(
                $"Fluent 输入控件加载失败：{ex}");
        }
    }

    private static void CheckFluentSelectionControls(
        ICollection<string> results,
        ICollection<string> failures)
    {
        try
        {
            var toggle = new ToggleButton
            {
                Content = "视图选项",
                IsChecked = true,
                Style =
                    (Style)Application.Current.FindResource(
                        "FocusToggleButton")
            };
            toggle.ApplyTemplate();
            toggle.Measure(new Size(180, 80));
            toggle.Arrange(new Rect(0, 0, 160, 44));
            toggle.UpdateLayout();
            if (toggle.Template.FindName(
                    "ToggleChrome",
                    toggle) is not Border toggleChrome
                || toggleChrome.CornerRadius.TopLeft <= 0
                || !ReferenceEquals(
                    toggleChrome.Background,
                    Application.Current.FindResource(
                        "FocusAccentSoftBrush"))
                || !ReferenceEquals(
                    toggleChrome.BorderBrush,
                    Application.Current.FindResource(
                        "FocusAccentBrightBrush")))
            {
                failures.Add(
                    "Fluent 切换按钮缺少单层圆角或动态选中状态");
                return;
            }

            toggle.IsEnabled = false;
            toggle.UpdateLayout();
            if (toggleChrome.Opacity > 0.4)
            {
                failures.Add(
                    "Fluent 切换按钮未显示明确禁用状态");
                return;
            }

            var segment = new RadioButton
            {
                Content = "看板",
                IsChecked = true,
                Style =
                    (Style)Application.Current.FindResource(
                        "FocusSegmentRadioButton")
            };
            segment.ApplyTemplate();
            segment.Measure(new Size(140, 70));
            segment.Arrange(new Rect(0, 0, 96, 38));
            segment.UpdateLayout();
            if (segment.Template.FindName(
                    "SegmentChrome",
                    segment) is not Border segmentChrome
                || segment.Template.FindName(
                    "SegmentSelectionIndicator",
                    segment) is not Border indicator
                || segmentChrome.CornerRadius.TopLeft <= 0
                || indicator.Visibility != Visibility.Visible
                || !ReferenceEquals(
                    segmentChrome.Background,
                    Application.Current.FindResource(
                        "FocusAccentSoftBrush"))
                || !ReferenceEquals(
                    segment.Foreground,
                    Application.Current.FindResource(
                        "FocusTextBrush")))
            {
                failures.Add(
                    "Fluent 分段选择缺少柔和表面、强调标记或动态文字");
                return;
            }

            results.Add(
                "PASS Fluent 切换与分段选择动态强调状态");
        }
        catch (Exception ex)
        {
            failures.Add(
                $"Fluent 选择控件加载失败：{ex}");
        }
    }

    private static void CheckDynamicThemeStateTokens(
        ICollection<string> results,
        ICollection<string> failures)
    {
        try
        {
            var indicator = new EdgeIndicatorWindow();
            indicator.ApplyTemplate();
            indicator.Measure(new Size(3, 240));
            indicator.Arrange(new Rect(0, 0, 3, 240));
            indicator.UpdateLayout();
            if (indicator.FindName(
                    "IndicatorSurface") is not Border indicatorSurface
                || !ReferenceEquals(
                    indicatorSurface.Background,
                    Application.Current.FindResource(
                        "FocusEdgeIndicatorBrush")))
            {
                failures.Add(
                    "右缘运行指示条未使用动态主题令牌");
                return;
            }

            var organizer = new FileOrganizerView();
            organizer.Measure(new Size(900, 700));
            organizer.Arrange(new Rect(0, 0, 900, 700));
            organizer.UpdateLayout();
            if (organizer.FindName(
                    "RenameOverlay") is not Grid renameOverlay
                || !ReferenceEquals(
                    renameOverlay.Background,
                    Application.Current.FindResource(
                        "FocusOverlayBrush")))
            {
                failures.Add(
                    "桌面重命名遮罩未使用动态主题令牌");
                return;
            }

            if (Application.Current.FindResource(
                    "FocusWarningBrush") is not SolidColorBrush
                || Application.Current.FindResource(
                    "FocusWarningSoftBrush") is not SolidColorBrush
                || Application.Current.FindResource(
                    "FocusWarningTextBrush") is not SolidColorBrush)
            {
                failures.Add(
                    "系统限制警告缺少动态前景或柔和背景令牌");
                return;
            }

            results.Add(
                "PASS 右缘指示、遮罩与警告状态动态主题");
        }
        catch (Exception ex)
        {
            failures.Add(
                $"动态状态主题加载失败：{ex}");
        }
    }

    private static void CheckLargeOrganizerVirtualization(
        ICollection<string> results,
        ICollection<string> failures)
    {
        try
        {
            var panelFactory =
                new FrameworkElementFactory(
                    typeof(
                        ViewportVirtualizingPanel));
            panelFactory.SetValue(
                ViewportVirtualizingPanel
                    .ItemWidthProperty,
                100d);
            panelFactory.SetValue(
                ViewportVirtualizingPanel
                    .ItemHeightProperty,
                120d);
            panelFactory.SetValue(
                ViewportVirtualizingPanel
                    .ItemSpacingProperty,
                10d);
            var source =
                new ObservableCollection<string>(
                    Enumerable.Range(1, 1000)
                        .Select(index =>
                            $"文件 {index:D4}"));
            var items = new ItemsControl
            {
                ItemsPanel =
                    new ItemsPanelTemplate(
                        panelFactory),
                ItemsSource = source
            };
            VirtualizingPanel.SetIsVirtualizing(
                items,
                true);
            VirtualizingPanel
                .SetVirtualizationMode(
                    items,
                    VirtualizationMode.Recycling);
            var viewer = new ScrollViewer
            {
                Width = 350,
                Height = 220,
                Content = items,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto
            };
            var size = new Size(350, 220);
            viewer.Measure(size);
            viewer.Arrange(new Rect(size));
            viewer.UpdateLayout();
            ViewportVirtualizingPanel? panel =
                FindVisualChild<
                    ViewportVirtualizingPanel>(
                    items);
            if (panel == null)
            {
                failures.Add(
                    "大量文件虚拟化验证未找到面板");
                return;
            }

            int initialCount =
                panel.RealizedContainerCount;
            if (initialCount <= 0
                || initialCount >= 100)
            {
                failures.Add(
                    $"首屏生成了 {initialCount} 个文件容器");
                return;
            }

            viewer.ScrollToVerticalOffset(13000);
            viewer.UpdateLayout();
            int scrolledCount =
                panel.RealizedContainerCount;
            if (viewer.VerticalOffset <= 0
                || panel.FirstRealizedIndex <= 0
                || scrolledCount <= 0
                || scrolledCount >= 100)
            {
                failures.Add(
                    "滚动后虚拟化状态异常："
                    + $"offset={viewer.VerticalOffset:F1}, "
                    + $"first={panel.FirstRealizedIndex}, "
                    + $"containers={scrolledCount}");
                return;
            }

            source.Insert(
                0,
                "新增文件");
            source.RemoveAt(
                source.Count - 1);
            viewer.UpdateLayout();
            int changedCount =
                panel.RealizedContainerCount;
            if (changedCount <= 0
                || changedCount >= 100)
            {
                failures.Add(
                    "集合变化后虚拟化容器数量失效："
                    + changedCount);
                return;
            }
            viewer.ScrollToTop();
            viewer.UpdateLayout();
            int returnedCount =
                panel.RealizedContainerCount;
            if (panel.FirstRealizedIndex != 0
                || returnedCount <= 0
                || returnedCount >= 100)
            {
                failures.Add(
                    "返回顶部后虚拟化区间未复位："
                    + $"first={panel.FirstRealizedIndex}, "
                    + $"containers={returnedCount}");
                return;
            }
            results.Add(
                $"PASS 1000 项仅生成 "
                + $"{initialCount}/{scrolledCount}"
                + $"/{changedCount}/{returnedCount} "
                + "个可视容器");
        }
        catch (Exception ex)
        {
            failures.Add(
                $"大量文件虚拟化验证失败：{ex}");
        }
    }

    private static T? FindVisualChild<T>(
        DependencyObject parent)
        where T : DependencyObject
    {
        int count =
            VisualTreeHelper.GetChildrenCount(
                parent);
        for (int index = 0;
             index < count;
             index++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    parent,
                    index);
            if (child is T match)
                return match;
            T? descendant =
                FindVisualChild<T>(child);
            if (descendant != null)
                return descendant;
        }
        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(
        DependencyObject parent)
        where T : DependencyObject
    {
        int count =
            VisualTreeHelper.GetChildrenCount(
                parent);
        for (int index = 0;
             index < count;
             index++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    parent,
                    index);
            if (child is T match)
                yield return match;
            foreach (T descendant in
                     FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void RenderCalendarSnapshot(
        Application application,
        string path)
    {
        var focusByDate =
            new Dictionary<DateTime, CalendarFocusSummary>
            {
                [new DateTime(2026, 7, 8)] =
                    new CalendarFocusSummary(2, 50),
                [new DateTime(2026, 7, 16)] =
                    new CalendarFocusSummary(1, 25),
                [new DateTime(2026, 7, 28)] =
                    new CalendarFocusSummary(3, 75)
            };
        var view = new CalendarPanelView
        {
            DataContext = new CalendarPreviewModel
            {
                DisplayedCalendarMonthTitle =
                    "2026年 7月",
                SelectedCalendarDateTitle =
                    "7月28日 星期二",
                SelectedDayFocusSummary =
                    "完成 3 次专注 · 75 分钟",
                OpenTaskCount = 7,
                CalendarDays =
                    CalendarMonthComposer.Compose(
                        new DateTime(2026, 7, 1),
                        new DateTime(2026, 7, 28),
                        new DateTime(2026, 7, 28),
                        focusByDate)
            }
        };
        var surface = new Border
        {
            Width = 430,
            Height = 560,
            Padding = new Thickness(22),
            CornerRadius = new CornerRadius(16),
            Background =
                (Brush)application.FindResource(
                    "FocusSurfaceStrongBrush"),
            Child = view
        };
        var size = new Size(430, 560);
        surface.Measure(size);
        surface.Arrange(new Rect(size));
        surface.UpdateLayout();

        var bitmap = new RenderTargetBitmap(
            430,
            560,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(surface);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(
            BitmapFrame.Create(bitmap));

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void WriteReport(
        string? reportPath,
        IEnumerable<string> results,
        IReadOnlyCollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(reportPath))
            return;

        string? directory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var lines = new List<string>(results);
        if (failures.Count == 0)
        {
            lines.Add("RESULT PASS");
        }
        else
        {
            foreach (string failure in failures)
                lines.Add($"FAIL {failure}");
            lines.Add("RESULT FAIL");
        }

        File.WriteAllLines(reportPath, lines);
    }

    private sealed class CalendarPreviewModel
    {
        public string DisplayedCalendarMonthTitle
        {
            get;
            init;
        } = string.Empty;

        public string SelectedCalendarDateTitle
        {
            get;
            init;
        } = string.Empty;

        public string SelectedDayFocusSummary
        {
            get;
            init;
        } = string.Empty;

        public int OpenTaskCount { get; init; }

        public IReadOnlyList<CalendarDayItem> CalendarDays
        {
            get;
            init;
        } = Array.Empty<CalendarDayItem>();
    }
}
