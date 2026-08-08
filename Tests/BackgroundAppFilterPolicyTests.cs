using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class BackgroundAppFilterPolicyTests
{
    [Fact]
    public void AllScope_PreservesUnifiedApplicationOrder()
    {
        TaskbarAppItem[] source =
        {
            Pinned("编辑器"),
            Windowed("浏览器", @"C:\Apps\Browser.exe"),
            Background("同步助手", @"C:\Apps\SyncAgent.exe")
        };

        IReadOnlyList<TaskbarAppItem> result =
            BackgroundAppFilterPolicy.Apply(
                source,
                null,
                BackgroundAppFilterScope.All);

        Assert.Equal(source, result);
    }

    [Fact]
    public void Scope_SeparatesVisibleWindowsAndPureBackgroundApps()
    {
        TaskbarAppItem pinned = Pinned("编辑器");
        TaskbarAppItem windowed =
            Windowed("浏览器", @"C:\Apps\Browser.exe");
        TaskbarAppItem background =
            Background("同步助手", @"C:\Apps\SyncAgent.exe");
        TaskbarAppItem[] source =
        {
            pinned,
            windowed,
            background
        };

        Assert.Equal(
            new[] { windowed },
            BackgroundAppFilterPolicy.Apply(
                source,
                null,
                BackgroundAppFilterScope.Windows));
        Assert.Equal(
            new[] { background },
            BackgroundAppFilterPolicy.Apply(
                source,
                null,
                BackgroundAppFilterScope.Background));
    }

    [Theory]
    [InlineData("同步", "同步助手")]
    [InlineData("syncagent", "同步助手")]
    [InlineData("browser", "浏览器")]
    public void Query_MatchesDisplayNameOrExecutableWithoutReordering(
        string query,
        string expected)
    {
        TaskbarAppItem[] source =
        {
            Background("同步助手", @"C:\Apps\SyncAgent.exe"),
            Windowed("浏览器", @"C:\Apps\Browser.exe")
        };

        TaskbarAppItem result = Assert.Single(
            BackgroundAppFilterPolicy.Apply(
                source,
                query,
                BackgroundAppFilterScope.All));

        Assert.Equal(expected, result.DisplayName);
        Assert.Equal(2, source.Length);
    }

    [Fact]
    public void ApplicationsDrawer_UsesSearchFiltersAndFocusesSearchEntry()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml"));
        string code = File.ReadAllText(
            Path.Combine(root, "Views", "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"BackgroundAppSearchBox\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding FilteredBackgroundApps}\"", xaml);
        Assert.Contains("Content=\"全部\"", xaml);
        Assert.Contains("Content=\"有窗口\"", xaml);
        Assert.Contains("Content=\"纯后台\"", xaml);
        Assert.Contains("BackgroundAppSearchBox,", code);
        Assert.DoesNotContain(
            "ItemsSource=\"{Binding TaskbarApps}\"\n                                                 MaxHeight=\"420\"",
            xaml);
    }

    private static TaskbarAppItem Pinned(string name) =>
        new()
        {
            IdentityKey = $"pinned:{name}",
            DisplayName = name,
            PinnedLaunches =
                new[]
                {
                    new AppLaunchItem
                    {
                        DisplayName = name,
                        IdentityKey = $"pinned:{name}",
                        LaunchTarget = @"C:\Apps\Pinned.exe"
                    }
                }
        };

    private static TaskbarAppItem Windowed(
        string name,
        string executablePath) =>
        Running(name, executablePath,
            new[]
            {
                new WindowReference(
                    new IntPtr(1),
                    name)
            });

    private static TaskbarAppItem Background(
        string name,
        string executablePath) =>
        Running(
            name,
            executablePath,
            Array.Empty<WindowReference>());

    private static TaskbarAppItem Running(
        string name,
        string executablePath,
        IReadOnlyList<WindowReference> windows) =>
        new()
        {
            IdentityKey = $"exe:{executablePath}",
            DisplayName = name,
            RunningTask = new WindowTaskItem
            {
                IdentityKey = $"exe:{executablePath}",
                DisplayName = name,
                ExecutablePath = executablePath,
                Windows = windows
            }
        };

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory =
            new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(
                    Path.Combine(
                        directory.FullName,
                        "FocusPanel.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "FocusPanel repository root was not found.");
    }
}
