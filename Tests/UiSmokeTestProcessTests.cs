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
            Assert.Contains(
                "PASS Fluent 菜单叶项、勾选、分隔线与子菜单",
                report);
            Assert.Contains(
                "PASS Fluent 工具提示圆角与动态主题",
                report);
            Assert.Contains(
                "PASS Fluent 下拉框封闭态、Popup 与选中项",
                report);
            Assert.Contains(
                "PASS Fluent 列表选中态文字、强调色与点击区",
                report);
            Assert.Contains(
                "PASS Fluent 页面、章节、正文、说明与指标字体层级",
                report);
            Assert.Contains(
                "PASS Fluent 行按钮与危险操作动态状态",
                report);
            Assert.Contains(
                "PASS Fluent 勾选框点击区、圆角与选中状态",
                report);
            Assert.Contains(
                "PASS Fluent 纵横滚动条圆角、动态主题与紧凑轨道",
                report);
            Assert.Contains(
                "PASS Fluent 音量滑块与确定/加载进度状态",
                report);
            Assert.Contains(
                "PASS Fluent 文本与密码输入选择、只读和禁用状态",
                report);
            Assert.Contains(
                "PASS Fluent 切换与分段选择动态强调状态",
                report);
            Assert.Contains(
                "PASS 右缘指示、遮罩与警告状态动态主题",
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
