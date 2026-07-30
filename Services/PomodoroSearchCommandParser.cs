using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FocusPanel.Services;

internal readonly record struct
    PomodoroSearchCommand(
        int DurationMinutes)
{
    internal string StableKey =>
        $"focus:start:{DurationMinutes}";

    internal string DisplayName =>
        $"开始 {DurationMinutes} 分钟专注";
}

internal static class PomodoroSearchCommandParser
{
    private static readonly Regex CommandPattern =
        new(
            @"^(?:开始\s*)?(?:专注|番茄钟?|focus|pomodoro)\s*(\d{1,3})\s*(?:分钟|分|min|mins|minutes?)?$",
            RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant);

    internal static bool TryParse(
        string? query,
        out PomodoroSearchCommand command)
    {
        command = default;
        if (string.IsNullOrWhiteSpace(query))
            return false;

        string normalized =
            query.Trim()
                .Replace('０', '0')
                .Replace('１', '1')
                .Replace('２', '2')
                .Replace('３', '3')
                .Replace('４', '4')
                .Replace('５', '5')
                .Replace('６', '6')
                .Replace('７', '7')
                .Replace('８', '8')
                .Replace('９', '9');
        Match match =
            CommandPattern.Match(
                normalized);
        if (!match.Success
            || !int.TryParse(
                match.Groups[1].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int minutes)
            || minutes is < 1 or > 180)
        {
            return false;
        }

        command =
            new PomodoroSearchCommand(
                minutes);
        return true;
    }
}
