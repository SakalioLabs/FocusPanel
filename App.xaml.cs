using System;
using System.Linq;
using System.Windows;
using FocusPanel.Data;
using FocusPanel.Helpers;
using FocusPanel.Services;
using FocusPanel.Views;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel;

public partial class App : Application
{
    private readonly CrashLogService _crashLog =
        new();
    private readonly DatabaseStartupCoordinator
        _databaseStartup;
    private readonly DesktopCrashRecoveryService
        _desktopCrashRecovery = new();
    private bool _handlingFatalException;
    private bool _fatalShutdown;

    public App()
    {
        _databaseStartup =
            new DatabaseStartupCoordinator(
                PrepareDatabase);
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    protected override async void OnStartup(
        StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length >= 3
            && string.Equals(e.Args[0], "--taskbar-watchdog", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(e.Args[1], out int parentProcessId))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            int exitCode = TaskbarWatchdog.Run(parentProcessId, e.Args[2]);
            Shutdown(exitCode);
            return;
        }

        // A stale taskbar session proves that the previous shell did not
        // complete its normal shutdown path. Keep that signal so desktop
        // item attributes can be repaired after the database is available.
        bool hadOrphanedTaskbarSession =
            TaskbarController.HasOrphanedSession();
        // Always repair a stale replacement session before normal startup.
        TaskbarController.RestoreOrphanedSession();
        RestoreNativeDesktopIcons();
        ThemeService.ApplyCurrentTheme();

        // Set working directory to the application's base directory
        System.IO.Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);

        EdgeIndicatorWindow? startupIndicator =
            TryShowStartupIndicator();
        DatabaseStartupCompletion completion;
        try
        {
            completion =
                await _databaseStartup.PrepareAsync(
                    e.Args.Contains("--restore"));
        }
        catch (Exception ex)
        {
            LogException(ex);
            completion =
                new DatabaseStartupCompletion(
                    false,
                    new DatabaseStartupNotice(
                        "数据库启动失败",
                        "FocusPanel 无法完成数据库启动检查。"
                        + "\n\n当前数据没有被继续修改，请保留数据库与日志后再进行人工恢复。"
                        + $"\n日志：{_crashLog.LogPath}",
                        DatabaseStartupNoticeKind.Error));
        }

        if (completion.Notice != null)
        {
            startupIndicator?.HideIndicator();
            ShowDatabaseStartupNotice(
                completion.Notice);
        }
        if (!completion.Succeeded)
        {
            startupIndicator?.Close();
            Shutdown(-1);
            return;
        }

        DesktopCrashRecoveryResult desktopRecovery =
            _desktopCrashRecovery.RestoreIfRequested(
                hadOrphanedTaskbarSession);
        DesktopCrashRecoveryResult upgradeRecovery =
            _desktopCrashRecovery
                .RestoreKnownCrashResidueOnce(
                    "0.10.78");
        desktopRecovery = new DesktopCrashRecoveryResult(
            desktopRecovery.Attempted
                || upgradeRecovery.Attempted,
            desktopRecovery.Restored
                + upgradeRecovery.Restored,
            desktopRecovery.Failed
                + upgradeRecovery.Failed);
        _desktopCrashRecovery.Arm();

        var mainWindow =
            new MainWindow(startupIndicator);
        MainWindow = mainWindow;
        mainWindow.Show();
        mainWindow.ShowDesktopRecoveryNotice(
            desktopRecovery);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (!_fatalShutdown)
            _desktopCrashRecovery.Disarm();
        TaskbarController.RestoreOrphanedSession();
        RestoreNativeDesktopIcons();
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        _fatalShutdown = true;
        LogException(e.Exception);
        _desktopCrashRecovery
            .RestoreCollectedItems();
        TaskbarController.RestoreOrphanedSession();
        RestoreNativeDesktopIcons();

        if (_handlingFatalException)
        {
            Current.Shutdown(-1);
            return;
        }

        _handlingFatalException = true;
        FatalExceptionNotice notice =
            UnhandledExceptionRecoveryPolicy.CreateNotice(
                e.Exception,
                _crashLog.LogPath);
        try
        {
            MessageBox.Show(
                notice.Message,
                notice.Title,
                MessageBoxButton.OK,
                notice.IsWarning
                    ? MessageBoxImage.Warning
                    : MessageBoxImage.Error);
        }
        catch
        {
            // The dispatcher is already failing. Recovery must not depend on another UI operation.
        }

        Current.Shutdown(-1);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _fatalShutdown = true;
        _desktopCrashRecovery
            .RestoreCollectedItems();
        TaskbarController.RestoreOrphanedSession();
        RestoreNativeDesktopIcons();
        if (e.ExceptionObject is Exception ex)
            LogException(ex);
    }

    private bool TryInitializeDatabase()
    {
        try
        {
            using var context =
                new AppDbContext();
            context.Database.EnsureCreated();
            context.EnsureSchema();
            _ = context.Todos.Count();
            return true;
        }
        catch (Exception ex)
        {
            LogException(ex);
            return false;
        }
    }

    private void LogException(Exception ex)
        => _crashLog.TryAppend(ex);

    private DatabaseStartupCompletion PrepareDatabase(
        bool restoreRequested)
    {
        var backupService =
            new DatabaseBackupService();
        DatabaseStartupNotice? notice = null;

        if (restoreRequested)
        {
            bool restored =
                backupService.TryRestoreLatestBackup(
                    out string restoreMessage);
            notice =
                new DatabaseStartupNotice(
                    restored
                        ? "数据库恢复完成"
                        : "数据库恢复失败",
                    restoreMessage,
                    restored
                        ? DatabaseStartupNoticeKind
                            .Information
                        : DatabaseStartupNoticeKind
                            .Error);
        }
        else
        {
            backupService.PerformStartupBackup();
        }

        bool dbInitSuccess =
            TryInitializeDatabase();
        if (dbInitSuccess)
        {
            return new DatabaseStartupCompletion(
                true,
                notice);
        }

        bool archiveSucceeded =
            backupService.ArchiveCorruptedDatabase();
        string recoveryMessage =
            "没有找到可安全恢复的数据库备份。";
        bool backupRestored =
            archiveSucceeded
            && backupService.TryRestoreLatestBackup(
                out recoveryMessage);
        DatabaseStartupRecoveryAction action =
            DatabaseStartupRecoveryPolicy.Decide(
                archiveSucceeded,
                backupRestored);

        if (action
            == DatabaseStartupRecoveryAction
                .ValidateRestoredDatabase)
        {
            dbInitSuccess =
                TryInitializeDatabase();
            if (dbInitSuccess)
            {
                notice =
                    new DatabaseStartupNotice(
                        "数据库已恢复",
                        $"检测到数据库异常。{recoveryMessage}",
                        DatabaseStartupNoticeKind
                            .Information);
            }
        }
        else if (action
                 == DatabaseStartupRecoveryAction
                     .CreateFreshDatabase)
        {
            dbInitSuccess =
                TryInitializeDatabase();
            if (dbInitSuccess)
            {
                notice =
                    new DatabaseStartupNotice(
                        "已保留异常数据库",
                        "原数据库无法使用且没有有效备份。"
                        + "原文件已归档保留，FocusPanel 已创建新的业务数据库。",
                        DatabaseStartupNoticeKind
                            .Warning);
            }
        }

        if (dbInitSuccess)
        {
            return new DatabaseStartupCompletion(
                true,
                notice);
        }

        string detail = action
            == DatabaseStartupRecoveryAction
                .StopWithoutChanges
            ? "FocusPanel 无法安全归档当前数据库，因此没有删除、覆盖或重建任何数据。"
            : "数据库恢复后仍未通过应用结构检查，因此没有继续覆盖或重建数据。";
        return new DatabaseStartupCompletion(
            false,
            new DatabaseStartupNotice(
                "数据库安全保护",
                $"{detail}\n\n请保留数据库与日志后再进行人工恢复。"
                + $"\n日志：{_crashLog.LogPath}",
                DatabaseStartupNoticeKind.Error));
    }

    private static void ShowDatabaseStartupNotice(
        DatabaseStartupNotice notice)
    {
        MessageBoxImage image = notice.Kind switch
        {
            DatabaseStartupNoticeKind
                .Information =>
                MessageBoxImage.Information,
            DatabaseStartupNoticeKind.Warning =>
                MessageBoxImage.Warning,
            _ => MessageBoxImage.Error
        };
        MessageBox.Show(
            notice.Message,
            notice.Title,
            MessageBoxButton.OK,
            image);
    }

    private static EdgeIndicatorWindow?
        TryShowStartupIndicator()
    {
        EdgeIndicatorWindow? indicator = null;
        try
        {
            indicator =
                new EdgeIndicatorWindow();
            indicator.ShowStartingIndicator();
            return indicator;
        }
        catch
        {
            try
            {
                indicator?.Close();
            }
            catch
            {
            }

            return null;
        }
    }

    private static void RestoreNativeDesktopIcons()
    {
        try
        {
            DesktopHelper.ToggleDesktopIcons(true);
            DesktopHelper.RefreshDesktop();
        }
        catch
        {
            // Best-effort recovery path. Startup and crash handling must never fail because Explorer is unavailable.
        }
    }
}
