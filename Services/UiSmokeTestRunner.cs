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
        "FocusContextMenu",
        "FocusMenuItem",
        "FocusMenuSeparator",
        "FocusToolTip",
        "FocusComboBox",
        "FocusComboBoxItem",
        "FocusCheckBox"
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
            CheckFluentCheckBox(
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
