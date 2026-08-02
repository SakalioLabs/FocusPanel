using System;
using System.Text.RegularExpressions;

namespace FocusPanel.Services;

internal readonly record struct
    TaskCaptureCommand(
        string Title)
{
    internal string StableKey =>
        "task:capture:"
        + Title;

    internal string DisplayName =>
        $"收集任务：{Title}";
}

internal static class TaskCaptureCommandParser
{
    internal const string QuickCapturePrefix =
        "任务 ";

    internal const int MaximumTitleLength =
        120;

    private static readonly Regex
        ChineseOrTodoPattern =
            new(
                @"^(?:任务|待办|todo)(?:(?:\s*[:：]\s*)|(?:\s+))(.+)$",
                RegexOptions.IgnoreCase
                | RegexOptions.CultureInvariant);

    private static readonly Regex TaskPattern =
        new(
            @"^task\s*[:：]\s*(.+)$",
            RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant);

    internal static bool TryParse(
        string? query,
        out TaskCaptureCommand command)
    {
        command = default;
        if (string.IsNullOrWhiteSpace(query))
            return false;

        string value = query.Trim();
        Match match =
            ChineseOrTodoPattern.Match(
                value);
        if (!match.Success)
        {
            match =
                TaskPattern.Match(value);
        }

        if (!match.Success)
            return false;

        string title =
            match.Groups[1].Value.Trim();
        if (title.Length == 0
            || title.Length
                > MaximumTitleLength
            || title.IndexOfAny(
                new[] { '\r', '\n', '\t' })
                >= 0)
        {
            return false;
        }

        foreach (char character in title)
        {
            if (char.IsControl(character))
                return false;
        }

        command =
            new TaskCaptureCommand(title);
        return true;
    }
}
