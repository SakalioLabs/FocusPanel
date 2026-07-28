using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
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

    public static int Run(string? reportPath)
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
        }
        catch (Exception ex)
        {
            failures.Add($"初始化失败：{ex}");
        }

        WriteReport(reportPath, results, failures);
        return failures.Count == 0 ? 0 : 1;
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
