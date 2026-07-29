using System;

namespace FocusPanel.Services;

internal enum FatalExceptionCategory
{
    Unexpected,
    DatabaseSchemaMismatch
}

internal readonly record struct FatalExceptionNotice(
    string Title,
    string Message,
    bool IsWarning);

internal static class UnhandledExceptionRecoveryPolicy
{
    internal static FatalExceptionCategory Classify(
        Exception exception)
    {
        for (Exception? current = exception;
             current != null;
             current = current.InnerException)
        {
            if (current.Message.Contains(
                    "no such table",
                    StringComparison.OrdinalIgnoreCase))
            {
                return FatalExceptionCategory
                    .DatabaseSchemaMismatch;
            }
        }

        return FatalExceptionCategory.Unexpected;
    }

    internal static FatalExceptionNotice CreateNotice(
        Exception exception,
        string logPath)
    {
        if (Classify(exception)
            == FatalExceptionCategory.DatabaseSchemaMismatch)
        {
            return new FatalExceptionNotice(
                "数据库结构异常",
                "检测到数据库结构异常。FocusPanel 已恢复系统任务栏和桌面图标，"
                + "并将安全退出。\n\n"
                + "现有数据库不会被删除或覆盖；下次启动时会重新检查结构并尝试安全恢复。"
                + $"\n\n崩溃日志：{logPath}",
                IsWarning: true);
        }

        return new FatalExceptionNotice(
            "FocusPanel 已安全停止",
            "FocusPanel 遇到未处理错误，已恢复系统任务栏和桌面图标。"
            + "为避免继续运行不完整状态，应用将安全退出。\n\n"
            + $"错误：{exception.Message}"
            + $"\n\n崩溃日志：{logPath}",
            IsWarning: false);
    }
}
