using System;
using System.IO;
using System.Windows;
using System.Threading;
using FocusPanel.Services;
using FocusPanel.Views;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellWindowSafetyTests
{
    [Fact]
    public void DesktopOverlayWindow_IsNotPartOfApplicationAssembly()
    {
        Assert.Null(typeof(EdgeHotZoneMonitor).Assembly.GetType("FocusPanel.Views.DesktopOverlayWindow"));
        Assert.Null(typeof(EdgeHotZoneMonitor).Assembly.GetType("FocusPanel.ViewModels.DesktopOverlayViewModel"));
        Assert.Null(typeof(EdgeHotZoneMonitor).Assembly.GetType("FocusPanel.ViewModels.DesktopDropRequest"));
    }

    [Fact]
    public void EdgeHotZoneMonitor_DoesNotCreateAWindowSurface()
    {
        Assert.False(typeof(Window).IsAssignableFrom(typeof(EdgeHotZoneMonitor)));
    }

    [Fact]
    public void FileOrganizer_DoesNotExposeRemovedSmartRescueAction()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", ".."));
        string view = File.ReadAllText(Path.Combine(
            projectRoot,
            "Views",
            "FileOrganizerView.xaml"));

        Assert.DoesNotContain("SmartRescueCommand", view);
    }

    [Fact]
    public void EdgeIndicator_LoadsAsAThreePixelNonInteractiveWindow()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var indicator = new EdgeIndicatorWindow();
                Assert.Equal(3, indicator.Width);
                Assert.False(indicator.ShowActivated);
                Assert.False(indicator.IsHitTestVisible);
                Assert.False(indicator.IsStarting);
                indicator.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    [Fact]
    public void StartupIndicator_IsReusedByMainShell()
    {
        string projectRoot = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", ".."));
        string app = File.ReadAllText(
            Path.Combine(
                projectRoot,
                "App.xaml.cs"));
        string mainWindow = File.ReadAllText(
            Path.Combine(
                projectRoot,
                "Views",
                "MainWindow.xaml.cs"));
        string indicator = File.ReadAllText(
            Path.Combine(
                projectRoot,
                "Views",
                "EdgeIndicatorWindow.xaml.cs"));

        int showIndicator = app.IndexOf(
            "TryShowStartupIndicator();",
            StringComparison.Ordinal);
        int prepareDatabase = app.IndexOf(
            "await _databaseStartup.PrepareAsync(",
            StringComparison.Ordinal);
        int transferIndicator = app.IndexOf(
            "new MainWindow(startupIndicator);",
            StringComparison.Ordinal);

        Assert.True(showIndicator >= 0);
        Assert.True(prepareDatabase > showIndicator);
        Assert.True(transferIndicator
            > prepareDatabase);
        Assert.Contains(
            "_edgeIndicator =\n            startupIndicator;",
            mainWindow);
        Assert.Contains(
            "_edgeIndicator ??= new EdgeIndicatorWindow();",
            mainWindow);
        Assert.Contains(
            "ShowStartingIndicator()",
            indicator);
        Assert.Contains(
            "RepeatBehavior.Forever",
            indicator);
        Assert.Contains(
            "SystemParameters.HighContrast",
            indicator);
        Assert.Contains(
            "ClientAreaAnimation",
            indicator);
        Assert.Contains(
            "ShowWindow(hwnd, SwShowNoActivate)",
            indicator);
        Assert.DoesNotContain(
            "Activate()",
            indicator);
        Assert.Contains(
            "startupIndicator?.Close();",
            app);
    }
}
