using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        "FocusKeyboardFocusBrush"
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
            CheckPartitionRefreshScroll(
                results,
                failures);
            CheckDesktopPathRefreshScroll(
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
