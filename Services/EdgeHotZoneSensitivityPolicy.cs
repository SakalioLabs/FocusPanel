using System.Collections.Generic;

namespace FocusPanel.Services;

public sealed record EdgeHotZoneSensitivityOption(
    int DwellMilliseconds,
    string DisplayName);

internal static class EdgeHotZoneSensitivityPolicy
{
    internal const int DefaultDwellMilliseconds = 100;

    internal static IReadOnlyList<
        EdgeHotZoneSensitivityOption> Options
    {
        get;
    } = new[]
    {
        new EdgeHotZoneSensitivityOption(
            40,
            "灵敏 · 约 0.04 秒"),
        new EdgeHotZoneSensitivityOption(
            DefaultDwellMilliseconds,
            "标准 · 约 0.1 秒"),
        new EdgeHotZoneSensitivityOption(
            180,
            "稳妥 · 约 0.18 秒"),
        new EdgeHotZoneSensitivityOption(
            300,
            "刻意 · 约 0.3 秒")
    };

    internal static int NormalizeDwell(
        int value)
    {
        foreach (EdgeHotZoneSensitivityOption option
                 in Options)
        {
            if (option.DwellMilliseconds
                == value)
            {
                return value;
            }
        }

        return DefaultDwellMilliseconds;
    }
}
