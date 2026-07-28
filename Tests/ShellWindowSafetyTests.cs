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
}
