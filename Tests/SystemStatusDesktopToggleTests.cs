using System;
using System.IO;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class SystemStatusDesktopToggleTests
{
    [Fact]
    public void ShowDesktop_UsesShellAutomationWithoutSendingAKey()
    {
        int shortcutCalls = 0;
        var desktop = new FakeDesktopToggleNative
        {
            Result = true
        };
        using var service = new SystemStatusService(
            _ =>
            {
                shortcutCalls++;
                return true;
            },
            desktopToggle: desktop);

        bool succeeded = service.ShowDesktop();

        Assert.True(succeeded);
        Assert.Equal(1, desktop.Calls);
        Assert.Equal(0, shortcutCalls);
    }

    [Fact]
    public void ShowDesktop_PropagatesShellRefusalWithoutKeyboardFallback()
    {
        int shortcutCalls = 0;
        var desktop = new FakeDesktopToggleNative();
        using var service = new SystemStatusService(
            _ =>
            {
                shortcutCalls++;
                return true;
            },
            desktopToggle: desktop);

        Assert.False(service.ShowDesktop());
        Assert.Equal(1, desktop.Calls);
        Assert.Equal(0, shortcutCalls);
    }

    [Fact]
    public void ProductionBoundary_UsesShellToggleDesktopNotSendInput()
    {
        string root = FindRepositoryRoot();
        string boundary = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "DesktopToggleService.cs"));
        string shortcuts = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "WindowsShellShortcut.cs"));

        Assert.Contains("Shell.Application", boundary);
        Assert.Contains("ToggleDesktop", boundary);
        Assert.DoesNotContain("SendInput", boundary);
        Assert.DoesNotContain(
            "WindowsShellAction.ShowDesktop =>",
            shortcuts);
    }

    private sealed class FakeDesktopToggleNative :
        IDesktopToggleNative
    {
        internal bool Result { get; init; }

        internal int Calls { get; private set; }

        public bool ToggleDesktop()
        {
            Calls++;
            return Result;
        }
    }

    private static string FindRepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(
                    Path.Combine(
                        directory,
                        "FocusPanel.csproj")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException(
            "FocusPanel repository root not found.");
    }
}
