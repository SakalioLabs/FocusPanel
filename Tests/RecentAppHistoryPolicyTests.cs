using System;
using System.Linq;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class RecentAppHistoryPolicyTests
{
    [Fact]
    public void Record_MovesExactIdentityToFrontAndCapsHistory()
    {
        string[] existing = Enumerable
            .Range(0, 10)
            .Select(index => $"exe:c:\\apps\\{index}.exe")
            .ToArray();

        var updated = RecentAppHistoryPolicy.Record(
            existing,
            " EXE:C:\\APPS\\4.EXE ");

        Assert.Equal(
            RecentAppHistoryPolicy.MaximumEntries,
            updated.Count);
        Assert.Equal(
            "EXE:C:\\APPS\\4.EXE",
            updated[0]);
        Assert.Single(
            updated.Where(identity =>
                string.Equals(
                    identity,
                    "exe:c:\\apps\\4.exe",
                    StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Parse_RejectsCorruptJsonAndNormalizesDuplicates()
    {
        Assert.Empty(
            RecentAppHistoryPolicy.Parse("not-json"));

        var parsed = RecentAppHistoryPolicy.Parse(
            "[\" exe:c:\\\\one.exe \",\"EXE:C:\\\\ONE.EXE\",\"\"]");

        Assert.Equal(
            new[] { "exe:c:\\one.exe" },
            parsed);
    }

    [Fact]
    public void Serialize_RoundTripsBoundedHistory()
    {
        string json = RecentAppHistoryPolicy.Serialize(
            new[]
            {
                "aumid:one",
                "aumid:two",
                "AUMID:ONE"
            });

        Assert.Equal(
            new[]
            {
                "aumid:one",
                "aumid:two"
            },
            RecentAppHistoryPolicy.Parse(json));
    }

    [Fact]
    public void LauncherOrder_IsPinnedThenRecentThenCatalogOrder()
    {
        AppLaunchItem sameNameOne = App(
            "编辑器",
            "exe:c:\\one.exe");
        AppLaunchItem fixedApp = App(
            "固定应用",
            "exe:c:\\fixed.exe",
            pinned: true);
        AppLaunchItem remaining = App(
            "浏览器",
            "exe:c:\\browser.exe");
        AppLaunchItem sameNameTwo = App(
            "编辑器",
            "exe:c:\\two.exe");

        var ordered = RecentAppHistoryPolicy
            .OrderForLauncher(
                new[]
                {
                    sameNameOne,
                    fixedApp,
                    remaining,
                    sameNameTwo
                },
                new[]
                {
                    "exe:c:\\two.exe",
                    "exe:c:\\one.exe"
                });

        Assert.Same(fixedApp, ordered[0]);
        Assert.Same(sameNameTwo, ordered[1]);
        Assert.Same(sameNameOne, ordered[2]);
        Assert.Same(remaining, ordered[3]);
    }

    [Fact]
    public void RecentMarker_UsesIdentityAndDoesNotOverridePinnedLabel()
    {
        AppLaunchItem recent = App(
            "绘图",
            "exe:c:\\paint.exe");
        AppLaunchItem pinned = App(
            "终端",
            "exe:c:\\terminal.exe",
            pinned: true);

        Assert.Equal(
            "应用 · 最近启动",
            ShellSearchResult
                .FromApplication(
                    recent,
                    isRecentlyLaunched: true)
                .SecondaryText);
        Assert.Equal(
            "应用 · 已固定",
            ShellSearchResult
                .FromApplication(
                    pinned,
                    isRecentlyLaunched: true)
                .SecondaryText);
        Assert.False(
            RecentAppHistoryPolicy.Contains(
                new[] { recent.IdentityKey },
                "exe:c:\\different.exe"));
    }

    private static AppLaunchItem App(
        string displayName,
        string identity,
        bool pinned = false) =>
        new()
        {
            DisplayName = displayName,
            LaunchKind = AppLaunchKind.Executable,
            LaunchTarget = identity[4..],
            IdentityKey = identity,
            IsPinned = pinned
        };
}
