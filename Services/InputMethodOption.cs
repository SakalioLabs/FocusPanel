using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusPanel.Services;

public readonly record struct InputMethodOption(
    long LayoutHandle,
    string DisplayName,
    string Detail,
    string ShortLabel,
    bool IsActive);

internal readonly record struct InputMethodObservation(
    long LayoutHandle,
    string? TwoLetterLanguage,
    string? NativeLanguageName,
    string? Description);

internal static class InputMethodOptionComposer
{
    internal static IReadOnlyList<
        InputMethodOption> Compose(
        IEnumerable<InputMethodObservation>
            observations,
        long activeLayoutHandle)
    {
        ArgumentNullException.ThrowIfNull(
            observations);

        InputMethodOption[] options = observations
            .Where(item =>
                item.LayoutHandle != 0)
            .GroupBy(
                item => item.LayoutHandle)
            .Select(group =>
                ComposeOption(
                    group.First(),
                    group.Key
                    == activeLayoutHandle))
            .ToArray();

        HashSet<string> duplicateNames =
            options
                .GroupBy(
                    item => item.DisplayName,
                    StringComparer
                        .CurrentCultureIgnoreCase)
                .Where(group =>
                    group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(
                    StringComparer
                        .CurrentCultureIgnoreCase);
        return options
            .Select(option =>
                duplicateNames.Contains(
                    option.DisplayName)
                    ? option with
                    {
                        Detail = option.Detail
                            + " · 布局 "
                            + FormatLayoutId(
                                option
                                    .LayoutHandle)
                    }
                    : option)
            .ToArray();
    }

    private static InputMethodOption
        ComposeOption(
        InputMethodObservation observation,
        bool isActive)
    {
        InputMethodStatusSnapshot status =
            InputMethodStatusSnapshot
                .FromObservation(
                    observation
                        .TwoLetterLanguage,
                    observation.Description);
        string nativeLanguageName =
            observation.NativeLanguageName
                ?.Trim()
            ?? string.Empty;
        string description =
            observation.Description
                ?.Trim()
            ?? string.Empty;
        string displayName =
            description.Length > 0
                ? description
                : nativeLanguageName.Length > 0
                    ? nativeLanguageName
                    : status.Display;
        string detail =
            description.Length > 0
            && nativeLanguageName.Length > 0
            && !string.Equals(
                description,
                nativeLanguageName,
                StringComparison
                    .CurrentCultureIgnoreCase)
                ? nativeLanguageName
                : $"输入语言 · {status.Display}";

        return new InputMethodOption(
            observation.LayoutHandle,
            displayName,
            detail,
            status.Display,
            isActive);
    }

    private static string FormatLayoutId(
        long layoutHandle)
    {
        string value = unchecked(
                (ulong)layoutHandle)
            .ToString("X16");
        return value[^8..];
    }
}
