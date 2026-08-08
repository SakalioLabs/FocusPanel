namespace FocusPanel.Services;

public sealed record TaskbarReplacementDiagnostics(
    bool IsEnabled,
    bool IsHealthy,
    bool IsExplorerHostCurrent,
    bool IsNativeRevealDisabled,
    bool IsWorkAreaReleased,
    bool IsWindowHidden,
    bool IsSurfaceSuppressed,
    bool IsDwmCloaked,
    string Summary);

internal static class
    TaskbarReplacementDiagnosticsComposer
{
    internal static TaskbarReplacementDiagnostics
        Compose(
            bool isEnabled,
            bool isExplorerHostCurrent,
            bool isNativeRevealDisabled,
            bool isWorkAreaReleased,
            bool isWindowHidden,
            bool isSurfaceSuppressed,
            bool isDwmCloaked)
    {
        if (!isEnabled)
        {
            return new TaskbarReplacementDiagnostics(
                false,
                false,
                isExplorerHostCurrent,
                isNativeRevealDisabled,
                isWorkAreaReleased,
                isWindowHidden,
                isSurfaceSuppressed,
                isDwmCloaked,
                "替代模式未启用，Windows 任务栏保持原设置。");
        }

        bool hasPresentationSuppression =
            isWindowHidden
            || isSurfaceSuppressed
            || isDwmCloaked;
        bool isHealthy =
            isExplorerHostCurrent
            && isNativeRevealDisabled
            && isWorkAreaReleased
            && hasPresentationSuppression;
        string suppression =
            GetSuppressionSummary(
                isWindowHidden,
                isSurfaceSuppressed,
                isDwmCloaked);
        if (isHealthy)
        {
            return new TaskbarReplacementDiagnostics(
                true,
                true,
                true,
                true,
                true,
                isWindowHidden,
                isSurfaceSuppressed,
                isDwmCloaked,
                "接管检查正常：原生边缘呼出已关闭，"
                + "主屏工作区已释放，呈现抑制为"
                + suppression
                + "。");
        }

        string issue =
            !isExplorerHostCurrent
                ? "Explorer 任务栏宿主已经变化"
                : !isNativeRevealDisabled
                    ? "Windows 又启用了原生边缘呼出"
                    : !isWorkAreaReleased
                        ? "主屏工作区仍被原任务栏占用"
                        : "原任务栏呈现抑制层已经失效";
        return new TaskbarReplacementDiagnostics(
            true,
            false,
            isExplorerHostCurrent,
            isNativeRevealDisabled,
            isWorkAreaReleased,
            isWindowHidden,
            isSurfaceSuppressed,
            isDwmCloaked,
            "接管检查发现问题："
            + issue
            + "；当前呈现状态为"
            + suppression
            + "。守护进程会按安全策略恢复，不会循环隐藏。");
    }

    internal static TaskbarReplacementDiagnostics
        Failure(
            bool isEnabled,
            string message) =>
        new(
            isEnabled,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            "暂时无法完成接管检查："
            + message);

    private static string GetSuppressionSummary(
        bool isWindowHidden,
        bool isSurfaceSuppressed,
        bool isDwmCloaked)
    {
        if (isSurfaceSuppressed
            && isDwmCloaked)
        {
            return "窗口区域 + DWM 双层保护";
        }

        if (isDwmCloaked)
            return "DWM 隐藏保护";
        if (isSurfaceSuppressed)
            return "窗口区域隐藏保护";
        if (isWindowHidden)
            return "任务栏窗口隐藏";
        return "无有效保护";
    }
}
