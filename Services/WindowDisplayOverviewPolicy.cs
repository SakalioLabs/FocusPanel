using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Models;

namespace FocusPanel.Services;

public sealed record WindowDisplayFilterOption(
    string Value,
    string DisplayName,
    int WindowCount);

internal static class WindowDisplayOverviewPolicy
{
    internal const string AllDisplaysValue = "*";

    internal static IReadOnlyList<
        WindowDisplayFilterOption> CreateOptions(
        IEnumerable<WindowTaskItem>?
            applications)
    {
        WindowReference[] windows =
            applications?
                .Where(application =>
                    application != null)
                .SelectMany(application =>
                    application.Windows)
                .GroupBy(window =>
                    window.Handle)
                .Select(group => group.First())
                .ToArray()
            ?? Array.Empty<WindowReference>();
        var options = new List<
            WindowDisplayFilterOption>
        {
            new(
                AllDisplaysValue,
                $"全部屏幕 · {windows.Length}",
                windows.Length)
        };
        options.AddRange(
            windows
                .Where(window =>
                    !string.IsNullOrWhiteSpace(
                        window.DisplayDeviceName)
                    && !string.IsNullOrWhiteSpace(
                        window.DisplayLabel))
                .GroupBy(
                    window =>
                        window.DisplayDeviceName,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    DeviceName = group.Key,
                    DisplayName = group
                        .Select(window =>
                            window.DisplayLabel)
                        .First(),
                    OrderIndex = group.Min(
                        window =>
                            window.DisplayOrder),
                    WindowCount = group.Count()
                })
                .OrderBy(item =>
                    item.OrderIndex)
                .ThenBy(item =>
                    item.DeviceName,
                    StringComparer.OrdinalIgnoreCase)
                .Select(item =>
                    new WindowDisplayFilterOption(
                        item.DeviceName,
                        item.DisplayName + " · "
                        + $"{item.WindowCount} 个窗口",
                        item.WindowCount)));
        return options;
    }

    internal static bool IsUseful(
        IReadOnlyCollection<
            WindowDisplayFilterOption> options) =>
        options.Count > 2;

    internal static string NormalizeSelection(
        string? selectedValue,
        IReadOnlyCollection<
            WindowDisplayFilterOption> options)
    {
        if (!string.IsNullOrWhiteSpace(
                selectedValue))
        {
            WindowDisplayFilterOption? match =
                options.FirstOrDefault(option =>
                    string.Equals(
                        option.Value,
                        selectedValue,
                        StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match.Value;
        }

        return AllDisplaysValue;
    }
}
