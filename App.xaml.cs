using System;
using System.IO;
using System.Linq;
using System.Windows;
using FocusPanel.Data;
using FocusPanel.Helpers;
using FocusPanel.Services;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RestoreNativeDesktopIcons();

        // Set working directory to the application's base directory
        System.IO.Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);

        var backupService = new DatabaseBackupService();

        // Check for restore flag
        if (e.Args.Contains("--restore"))
        {
            if (backupService.RestoreLatestBackup())
            {
                MessageBox.Show("Database restored successfully.", "Restore Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Failed to restore database from backup.", "Restore Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            // Perform Startup Backup (only if not restoring)
            backupService.PerformStartupBackup();
        }

        bool dbInitSuccess = false;

        try
        {
            // Initialize Database
            using (var context = new AppDbContext())
            {
                if (!context.Database.EnsureCreated())
                {
                    // Database exists. Check if schema is valid
                    context.EnsureSchema();
                    var count = context.Todos.Count(); 
                }
                else
                {
                    // New DB created, run any additional setup
                    context.EnsureSchema();
                }
                dbInitSuccess = true;
            }
        }
        catch (Exception ex)
        {
            LogException(ex);
            // Database initialization failed
        }

        if (!dbInitSuccess)
        {
            // Try to recover
            if (backupService.ArchiveCorruptedDatabase() && backupService.RestoreLatestBackup())
            {
                MessageBox.Show("Database corruption detected. Restored from latest backup.", "Database Restored", MessageBoxButton.OK, MessageBoxImage.Information);
                try
                {
                    using (var context = new AppDbContext())
                    {
                         var count = context.Todos.Count();
                         dbInitSuccess = true;
                    }
                }
                catch
                {
                    // Restore failed or backup also corrupted
                }
            }
        }

        if (!dbInitSuccess)
        {
             // If all else fails, recreate
             try
             {
                 using (var context = new AppDbContext())
                 {
                     context.Database.EnsureDeleted();
                     context.Database.EnsureCreated();
                 }
                 MessageBox.Show("Database was corrupted and could not be restored. A new database has been created.", "Database Reset", MessageBoxButton.OK, MessageBoxImage.Warning);
             }
             catch (Exception ex)
             {
                 MessageBox.Show($"Critical Database Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 LogException(ex);
             }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        RestoreNativeDesktopIcons();
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        RestoreNativeDesktopIcons();
        
        // Specific handling for SQLite "no such table" error which might bubble up
        if (e.Exception.Message.Contains("no such table") || e.Exception.InnerException?.Message.Contains("no such table") == true)
        {
             MessageBox.Show("Database schema mismatch detected. The application will restart with a fresh database.", "Database Error", MessageBoxButton.OK, MessageBoxImage.Warning);
             try 
             {
                 File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "focuspanel.db"));
             }
             catch {} // Best effort
             
             // Optionally restart app here, but for now just exit cleanly or let user restart
             System.Diagnostics.Process.Start(ResourceAssembly.Location);
             Current.Shutdown();
             return;
        }

        MessageBox.Show($"An unexpected error occurred: {e.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        RestoreNativeDesktopIcons();
        if (e.ExceptionObject is Exception ex)
        {
            LogException(ex);
            MessageBox.Show($"Critical Error: {ex.Message}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LogException(Exception ex)
    {
        try
        {
            string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            string message = $"[{DateTime.Now}] {ex.Message}\n{ex.StackTrace}\n\n";
            File.AppendAllText(logFile, message);
        }
        catch { }
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
