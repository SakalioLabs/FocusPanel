using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FocusPanel.Services;

internal readonly record struct
    ApplicationAudioSearchCommand(
        string SessionId,
        string ApplicationName,
        AudioSearchCommand Command)
{
    internal string StableKey =>
        $"application-audio:{SessionId}:{Command.StableKey}";

    internal string DisplayName =>
        Command.Kind switch
        {
            AudioSearchCommandKind.SetVolume =>
                $"将 {ApplicationName} 音量设为 {Command.Percent}%",
            AudioSearchCommandKind.AdjustVolume =>
                Command.Percent > 0
                    ? $"将 {ApplicationName} 音量提高 {Command.Percent}%"
                    : $"将 {ApplicationName} 音量降低 {-Command.Percent}%",
            AudioSearchCommandKind.SetMuted =>
                Command.Muted
                    ? $"静音 {ApplicationName}"
                    : $"取消静音 {ApplicationName}",
            _ => $"调整 {ApplicationName} 音量"
        };
}

internal static class
    ApplicationAudioSearchCommandParser
{
    private const int DefaultLimit = 6;

    private static readonly Regex[] Patterns =
    {
        new(
            @"^(?<application>.+?)\s*(?<command>音量\s*(?:(?:[+＋\-－−]\s*)?\d{1,3}\s*[%％]?|(?:增加|提高|加|降低|减少|减)\s*\d{1,3}\s*[%％]?))$",
            RegexOptions.CultureInvariant),
        new(
            @"^(?<application>.+?)\s*(?<command>取消静音|解除静音|静音)$",
            RegexOptions.CultureInvariant),
        new(
            @"^(?<command>取消静音|解除静音|静音)\s*(?<application>.+)$",
            RegexOptions.CultureInvariant),
        new(
            @"^(?<application>.+?)\s+(?<command>(?:volume|vol)\s*(?:(?:[+\-]\s*)?\d{1,3}\s*%?|(?:up|down)\s*\d{1,3}\s*%?))$",
            RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant),
        new(
            @"^(?<application>.+?)\s+(?<command>unmute|mute)$",
            RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant),
        new(
            @"^(?<command>unmute|mute)\s+(?<application>.+)$",
            RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant)
    };

    internal static IReadOnlyList<
        ApplicationAudioSearchCommand> Parse(
            string? query,
            IEnumerable<
                ApplicationAudioSessionSnapshot>?
                sessions,
            int limit = DefaultLimit)
    {
        if (limit <= 0
            || !TrySplit(
                query,
                out string applicationQuery,
                out AudioSearchCommand command))
        {
            return Array.Empty<
                ApplicationAudioSearchCommand>();
        }

        return (sessions
                ?? Array.Empty<
                    ApplicationAudioSessionSnapshot>())
            .Where(session =>
                !string.IsNullOrWhiteSpace(
                    session.SessionId)
                && !string.IsNullOrWhiteSpace(
                    session.DisplayName))
            .GroupBy(
                session => session.SessionId,
                StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(session => new
            {
                Session = session,
                Rank = AppSearchPolicy.GetTextRank(
                    session.DisplayName,
                    string.Empty,
                    applicationQuery)
            })
            .Where(candidate =>
                candidate.Rank.HasValue)
            .OrderBy(candidate =>
                candidate.Rank!.Value)
            .ThenByDescending(candidate =>
                candidate.Session.IsActive)
            .ThenBy(candidate =>
                candidate.Session.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .Take(limit)
            .Select(candidate =>
                new ApplicationAudioSearchCommand(
                    candidate.Session.SessionId,
                    candidate.Session.DisplayName,
                    command))
            .ToArray();
    }

    internal static bool HasTargetedCommandSyntax(
        string? query) =>
        TrySplit(
            query,
            out _,
            out _);

    private static bool TrySplit(
        string? query,
        out string applicationQuery,
        out AudioSearchCommand command)
    {
        applicationQuery = string.Empty;
        command = default;
        if (string.IsNullOrWhiteSpace(query))
            return false;

        string normalized = query.Trim();
        foreach (Regex pattern in Patterns)
        {
            Match match = pattern.Match(normalized);
            if (!match.Success)
                continue;

            string candidateApplication =
                match.Groups["application"]
                    .Value.Trim();
            string candidateCommand =
                match.Groups["command"]
                    .Value.Trim();
            if (candidateApplication.Length == 0
                || !AudioSearchCommandParser
                    .TryParse(
                        candidateCommand,
                        out command))
            {
                continue;
            }

            applicationQuery =
                candidateApplication;
            return true;
        }

        return false;
    }
}
