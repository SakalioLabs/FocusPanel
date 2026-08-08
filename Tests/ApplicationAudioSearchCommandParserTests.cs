using System;
using System.Linq;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    ApplicationAudioSearchCommandParserTests
{
    [Theory]
    [InlineData(
        "Chrome 音量 30",
        "SetVolume",
        30,
        false)]
    [InlineData(
        "Spotify volume up 15%",
        "AdjustVolume",
        15,
        false)]
    [InlineData(
        "微信音量减少 8",
        "AdjustVolume",
        -8,
        false)]
    [InlineData(
        "微信静音",
        "SetMuted",
        0,
        true)]
    [InlineData(
        "取消静音 Spotify",
        "SetMuted",
        0,
        false)]
    [InlineData(
        "mute Chrome",
        "SetMuted",
        0,
        true)]
    public void Parse_RecognizesTargetedCommands(
        string query,
        string expectedKindName,
        int expectedPercent,
        bool expectedMuted)
    {
        ApplicationAudioSearchCommand result =
            Assert.Single(
                ApplicationAudioSearchCommandParser
                    .Parse(
                        query,
                        new[]
                        {
                            Session(
                                "chrome",
                                "Google Chrome"),
                            Session(
                                "spotify",
                                "Spotify"),
                            Session(
                                "wechat",
                                "微信")
                        }));

        Assert.Equal(
            Enum.Parse<AudioSearchCommandKind>(
                expectedKindName),
            result.Command.Kind);
        Assert.Equal(
            expectedPercent,
            result.Command.Percent);
        Assert.Equal(
            expectedMuted,
            result.Command.Muted);
        Assert.StartsWith(
            "application-audio:",
            result.StableKey);
        Assert.Contains(
            result.ApplicationName,
            result.DisplayName);
    }

    [Fact]
    public void Parse_KeepsDuplicateNamedSessionsSeparate()
    {
        ApplicationAudioSearchCommand[] results =
            ApplicationAudioSearchCommandParser
                .Parse(
                    "浏览器 音量 45",
                    new[]
                    {
                        Session(
                            "browser:2",
                            "浏览器"),
                        Session(
                            "browser:1",
                            "浏览器",
                            active: true),
                        Session(
                            "music",
                            "音乐")
                    })
                .ToArray();

        Assert.Equal(2, results.Length);
        Assert.Equal(
            "browser:1",
            results[0].SessionId);
        Assert.Equal(
            "browser:2",
            results[1].SessionId);
        Assert.NotEqual(
            results[0].StableKey,
            results[1].StableKey);
    }

    [Theory]
    [InlineData("音量 30")]
    [InlineData("静音")]
    [InlineData("Chrome 音量 101")]
    [InlineData("Chrome 音量 30 后关机")]
    [InlineData("Chrome muted")]
    [InlineData("")]
    public void Parse_RejectsMasterOrUnsafeCommands(
        string query)
    {
        Assert.Empty(
            ApplicationAudioSearchCommandParser
                .Parse(
                    query,
                    new[]
                    {
                        Session(
                            "chrome",
                            "Chrome")
                    }));
    }

    [Fact]
    public void Parse_RejectsMissingOrUnmatchedSessions()
    {
        Assert.Empty(
            ApplicationAudioSearchCommandParser
                .Parse(
                    "Chrome 音量 30",
                    Array.Empty<
                        ApplicationAudioSessionSnapshot>()));
        Assert.Empty(
            ApplicationAudioSearchCommandParser
                .Parse(
                    "Chrome 音量 30",
                    new[]
                    {
                        Session(
                            "music",
                            "Spotify")
                    }));
    }

    [Theory]
    [InlineData("Chrome 音量 30", true)]
    [InlineData("微信静音", true)]
    [InlineData("unmute Spotify", true)]
    [InlineData("音量 30", false)]
    [InlineData("Chrome", false)]
    public void SyntaxDetection_DoesNotRequireSessionSnapshot(
        string query,
        bool expected)
    {
        Assert.Equal(
            expected,
            ApplicationAudioSearchCommandParser
                .HasTargetedCommandSyntax(query));
    }

    [Fact]
    public void ShellSearch_ProducesExecutablePanelResultOnlyInSystemScopes()
    {
        ApplicationAudioSessionSnapshot[] sessions =
        {
            Session(
                "chrome",
                "Google Chrome",
                active: true)
        };
        ShellSearchResult result =
            Assert.Single(
                ShellSearchPolicy.Compose(
                        Array.Empty<AppLaunchItem>(),
                        Array.Empty<WindowTaskItem>(),
                        "Chrome 音量 30",
                        applicationAudioSessions:
                            sessions)
                    .Where(item =>
                        item.IsApplicationAudioCommand));

        Assert.Equal(
            ShellSearchResultKind
                .ApplicationAudioCommand,
            result.Kind);
        Assert.Equal(
            "chrome",
            result.ApplicationAudioCommand
                ?.SessionId);
        Assert.Contains(
            "应用音量快捷命令",
            result.SecondaryText);
        Assert.True(result.UsesGlyph);

        Assert.DoesNotContain(
            ShellSearchPolicy.Compose(
                Array.Empty<AppLaunchItem>(),
                Array.Empty<WindowTaskItem>(),
                "Chrome 音量 30",
                scope:
                    ShellSearchScope
                        .Applications,
                applicationAudioSessions:
                    sessions),
            item =>
                item.IsApplicationAudioCommand);
    }

    [Fact]
    public void ShellSearch_MasterVolumeKeepsExistingCommand()
    {
        ShellSearchResult result =
            Assert.Single(
                ShellSearchPolicy.Compose(
                        Array.Empty<AppLaunchItem>(),
                        Array.Empty<WindowTaskItem>(),
                        "音量 30",
                        applicationAudioSessions:
                            new[]
                            {
                                Session(
                                    "chrome",
                                    "Chrome")
                            })
                    .Where(item =>
                        item.IsAudioCommand));

        Assert.Equal(
            ShellSearchResultKind.AudioCommand,
            result.Kind);
        Assert.False(
            result.IsApplicationAudioCommand);
    }

    private static
        ApplicationAudioSessionSnapshot Session(
            string id,
            string name,
            bool active = false) =>
        new(
            id,
            name,
            123,
            0.5f,
            false,
            active,
            false);
}
