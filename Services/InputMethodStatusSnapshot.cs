using System;

namespace FocusPanel.Services;

public readonly record struct InputMethodStatusSnapshot(
    string LanguageDisplay,
    string MethodDisplay)
{
    public static InputMethodStatusSnapshot Unavailable { get; } =
        new("—", "—");

    public string Display =>
        LanguageDisplay == "—"
            ? "—"
            : string.Equals(
                LanguageDisplay,
                MethodDisplay,
                StringComparison.Ordinal)
                ? LanguageDisplay
                : $"{LanguageDisplay} / {MethodDisplay}";

    public string ButtonLabel =>
        Display == "—"
            ? "输入法"
            : $"输入法 · {Display}";

    public string Summary =>
        Display == "—"
            ? "当前输入法状态不可用"
            : $"当前输入法 {Display}";

    internal static InputMethodStatusSnapshot FromObservation(
        string? twoLetterLanguage,
        string? inputMethodDescription)
    {
        string language = NormalizeLanguage(
            twoLetterLanguage);
        if (language == "—")
            return Unavailable;
        if (language != "中")
        {
            return new InputMethodStatusSnapshot(
                language,
                language);
        }

        string description =
            inputMethodDescription?.Trim()
            ?? string.Empty;
        string method =
            ContainsAny(
                description,
                "拼音",
                "Pinyin")
                ? "拼"
                : ContainsAny(
                    description,
                    "五笔",
                    "Wubi")
                    ? "五"
                    : ContainsAny(
                        description,
                        "注音",
                        "Bopomofo")
                        ? "注"
                        : "中";
        return new InputMethodStatusSnapshot(
            language,
            method);
    }

    private static string NormalizeLanguage(
        string? twoLetterLanguage)
    {
        string language =
            twoLetterLanguage?.Trim().ToLowerInvariant()
            ?? string.Empty;
        return language switch
        {
            "zh" => "中",
            "ja" => "日",
            "ko" => "한",
            "en" => "EN",
            "" => "—",
            _ => language.ToUpperInvariant()
        };
    }

    private static bool ContainsAny(
        string source,
        string first,
        string second)
        => source.Contains(
                first,
                StringComparison.OrdinalIgnoreCase)
            || source.Contains(
                second,
                StringComparison.OrdinalIgnoreCase);
}
