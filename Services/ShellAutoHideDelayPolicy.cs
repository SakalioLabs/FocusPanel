using System.Collections.Generic;

namespace FocusPanel.Services;

public sealed record ShellAutoHideDelayOption(
    int Value,
    string DisplayName);

internal static class ShellAutoHideDelayPolicy
{
    internal const int DefaultMilliseconds = 500;

    internal static IReadOnlyList<
        ShellAutoHideDelayOption> Options
    {
        get;
    } = new[]
    {
        new ShellAutoHideDelayOption(
            300,
            "快速 · 0.3 秒"),
        new ShellAutoHideDelayOption(
            DefaultMilliseconds,
            "标准 · 0.5 秒"),
        new ShellAutoHideDelayOption(
            800,
            "从容 · 0.8 秒"),
        new ShellAutoHideDelayOption(
            1200,
            "较慢 · 1.2 秒")
    };

    internal static int Normalize(
        int value)
    {
        foreach (ShellAutoHideDelayOption option
                 in Options)
        {
            if (option.Value == value)
                return value;
        }

        return DefaultMilliseconds;
    }
}
