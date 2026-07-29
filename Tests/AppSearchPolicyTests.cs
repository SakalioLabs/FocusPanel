using System.Collections.Generic;
using System.Linq;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AppSearchPolicyTests
{
    [Fact]
    public void ExactName_BeatsPinnedSubstring()
    {
        AppLaunchItem[] results = Search(
            "code",
            App(
                "Barcode Utility",
                "barcode.exe",
                pinned: true),
            App(
                "Code",
                "code.exe"));

        Assert.Equal(
            new[] { "Code", "Barcode Utility" },
            results.Select(
                app => app.DisplayName));
    }

    [Fact]
    public void ExactExecutableName_BeatsDisplayPrefix()
    {
        AppLaunchItem[] results = Search(
            "code",
            App(
                "Code Helper",
                "helper.exe"),
            App(
                "Visual Studio Code",
                "code.exe"));

        Assert.Equal(
            "Visual Studio Code",
            results[0].DisplayName);
    }

    [Theory]
    [InlineData(
        "vsc",
        "Visual Studio Code")]
    [InlineData(
        "vs",
        "Visual Studio Code")]
    [InlineData(
        "fpm",
        "FocusPanel Manager")]
    [InlineData(
        "fpm",
        "FocusPanelManager")]
    public void WordInitialsAndCamelCase_AreSearchable(
        string query,
        string displayName)
    {
        AppLaunchItem result =
            Assert.Single(
                Search(
                    query,
                    App(
                        displayName,
                        "app.exe")));

        Assert.Equal(
            displayName,
            result.DisplayName);
    }

    [Fact]
    public void MultipleWords_MatchPrefixesRegardlessOfSpacing()
    {
        AppLaunchItem result =
            Assert.Single(
                Search(
                    "  studio   co  ",
                    App(
                        "Visual Studio Code",
                        "code.exe")));

        Assert.Equal(
            "Visual Studio Code",
            result.DisplayName);
    }

    [Theory]
    [InlineData(
        "cafe",
        "Café Tools")]
    [InlineData(
        "桌面",
        "桌面整理")]
    [InlineData(
        "focuspanel",
        "Focus-Panel")]
    public void CultureAndPunctuation_AreNormalized(
        string query,
        string displayName)
    {
        Assert.Single(
            Search(
                query,
                App(
                    displayName,
                    "app.exe")));
    }

    [Fact]
    public void EmptyQuery_PreservesPinnedFirstBehavior()
    {
        AppLaunchItem[] results = Search(
            "",
            App(
                "Alpha",
                "alpha.exe"),
            App(
                "Zulu",
                "zulu.exe",
                pinned: true));

        Assert.Equal(
            new[] { "Zulu", "Alpha" },
            results.Select(
                app => app.DisplayName));
    }

    [Fact]
    public void TypoDoesNotCreateUnboundedFuzzyMatch()
    {
        Assert.Empty(
            Search(
                "vixual",
                App(
                    "Visual Studio Code",
                    "code.exe")));
    }

    [Fact]
    public void LimitAndStableIdentityBreakTies()
    {
        AppLaunchItem[] results =
            AppSearchPolicy.Search(
                    new[]
                    {
                        App(
                            "Editor",
                            "b.exe",
                            identity: "b"),
                        App(
                            "Editor",
                            "a.exe",
                            identity: "a"),
                        App(
                            "Editor",
                            "c.exe",
                            identity: "c")
                    },
                    "editor",
                    2)
                .ToArray();

        Assert.Equal(
            new[] { "a", "b" },
            results.Select(
                app => app.IdentityKey));
    }

    [Fact]
    public void NonPositiveLimit_ReturnsNoResults()
    {
        Assert.Empty(
            AppSearchPolicy.Search(
                new[]
                {
                    App(
                        "Editor",
                        "editor.exe")
                },
                "editor",
                0));
    }

    private static AppLaunchItem[] Search(
        string query,
        params AppLaunchItem[] apps) =>
        AppSearchPolicy.Search(
                apps,
                query,
                24)
            .ToArray();

    private static AppLaunchItem App(
        string displayName,
        string executable,
        bool pinned = false,
        string? identity = null) =>
        new()
        {
            DisplayName = displayName,
            LaunchKind =
                AppLaunchKind.Executable,
            LaunchTarget =
                $@"C:\Apps\{executable}",
            IconKey = executable,
            IdentityKey =
                identity ?? executable,
            IsPinned = pinned
        };
}
