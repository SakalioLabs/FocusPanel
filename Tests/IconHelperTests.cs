using System;
using System.IO;
using FocusPanel.Helpers;
using Xunit;

namespace FocusPanel.Tests;

public sealed class IconHelperTests
{
    [Fact]
    public void InternetShortcut_ResolvesExplicitRelativeIco()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string shortcut = Path.Combine(root, "Custom.url");
            File.WriteAllText(
                shortcut,
                "[InternetShortcut]\r\n"
                + "URL=https://example.test\r\n"
                + "IconFile=icons\\custom.ico\r\n"
                + "IconIndex=2\r\n");

            bool resolved = IconHelper.TryResolveCustomIconLocation(
                shortcut,
                out string iconPath,
                out int iconIndex);

            Assert.True(resolved);
            Assert.Equal(
                Path.GetFullPath(
                    Path.Combine(root, "icons", "custom.ico")),
                iconPath);
            Assert.Equal(2, iconIndex);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CustomizedFolder_ResolvesDesktopIniIconResource()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string folder = Path.Combine(root, "Folder");
            Directory.CreateDirectory(folder);
            File.WriteAllText(
                Path.Combine(folder, "desktop.ini"),
                "[.ShellClassInfo]\r\nIconResource=folder.ico,4\r\n");

            bool resolved = IconHelper.TryResolveCustomIconLocation(
                folder,
                out string iconPath,
                out int iconIndex);

            Assert.True(resolved);
            Assert.Equal(
                Path.Combine(folder, "folder.ico"),
                iconPath);
            Assert.Equal(4, iconIndex);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void IconPath_ExpandsEnvironmentVariablesAndQuotes()
    {
        string root = CreateTemporaryDirectory();
        const string variable = "FOCUSPANEL_ICON_TEST_ROOT";
        string? previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, root);
            string normalized = IconHelper.NormalizeIconPath(
                $"\"%{variable}%\\custom.ico\"",
                Path.Combine(root, "shortcut.lnk"));

            Assert.Equal(
                Path.Combine(root, "custom.ico"),
                normalized);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ExplicitPanelIcon_InvalidFileFallsBackToItemIcon()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string item = Path.Combine(root, "note.txt");
            File.WriteAllText(item, "test");

            var fallback = IconHelper.GetIcon(
                item,
                Path.Combine(root, "missing.ico"),
                0,
                true);

            Assert.NotNull(fallback);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.IconTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
