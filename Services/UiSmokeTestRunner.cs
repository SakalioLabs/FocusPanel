using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        string? dashboardSnapshotPath = null)
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
            if (!string.IsNullOrWhiteSpace(
                    dashboardSnapshotPath))
            {
                RenderDashboardSnapshot(
                    application,
                    dashboardSnapshotPath);
                results.Add(
                    "PASS DashboardView 视觉快照");
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
}
