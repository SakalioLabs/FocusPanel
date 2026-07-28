using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class UiSmokeTestProcessTests
{
    [Fact]
    public void PackagedUiSurfaces_LoadAndResolveRuntimeResources()
    {
        string executablePath = Path.Combine(AppContext.BaseDirectory, "FocusPanel.exe");
        string reportPath = Path.Combine(
            Path.GetTempPath(),
            $"FocusPanel-ui-smoke-{Guid.NewGuid():N}.txt");

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = $"--ui-smoke-test \"{reportPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            Assert.NotNull(process);
            Assert.True(process!.WaitForExit(30_000), "UI 冒烟测试进程未在 30 秒内退出。");

            string report = File.Exists(reportPath)
                ? File.ReadAllText(reportPath)
                : "未生成 UI 冒烟测试报告。";
            Assert.True(process.ExitCode == 0, report);
            Assert.Contains("PASS 界面 TaskDetailWindow", report);
            Assert.Contains("PASS 界面 FileOrganizerView", report);
            Assert.Contains(
                "PASS 1000 项仅生成",
                report);
            Assert.Contains("RESULT PASS", report);
        }
        finally
        {
            if (File.Exists(reportPath))
                File.Delete(reportPath);
        }
    }
}
