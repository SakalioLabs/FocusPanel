using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using Forms = System.Windows.Forms;

namespace FocusPanel.Services;

internal enum ShellDisplayTargetMode
{
    OutermostRight,
    Primary
}

internal readonly record struct ShellDisplaySnapshot(
    Rectangle Bounds,
    bool IsPrimary,
    string DeviceName = "");

public sealed record ShellDisplayTargetOption(
    string Value,
    string DisplayName);

internal static class ShellDisplayTarget
{
    internal const string OutermostRightValue =
        "OutermostRight";
    internal const string PrimaryValue =
        "Primary";
    internal const string DevicePrefix =
        "Device:";

    internal static Rectangle GetBounds(
        ShellDisplayTargetMode mode =
            ShellDisplayTargetMode.OutermostRight)
    {
        ShellDisplaySnapshot[] displays = Forms.Screen.AllScreens
            .Select(screen => new ShellDisplaySnapshot(
                screen.Bounds,
                screen.Primary,
                screen.DeviceName))
            .ToArray();
        return Select(displays, mode)?.Bounds
            ?? Rectangle.Empty;
    }

    internal static Rectangle GetBounds(
        string? value)
    {
        ShellDisplaySnapshot[] displays =
            CaptureDisplays();
        return Select(displays, value)?.Bounds
            ?? Rectangle.Empty;
    }

    internal static ShellDisplaySnapshot? Select(
        IReadOnlyCollection<ShellDisplaySnapshot> displays,
        ShellDisplayTargetMode mode =
            ShellDisplayTargetMode.OutermostRight)
    {
        if (displays.Count == 0)
            return null;

        if (mode == ShellDisplayTargetMode.Primary)
        {
            foreach (ShellDisplaySnapshot display
                     in displays)
            {
                if (display.IsPrimary)
                    return display;
            }
        }

        return displays
            .OrderByDescending(display => display.Bounds.Right)
            .ThenByDescending(display => display.IsPrimary)
            .ThenBy(display => display.Bounds.Top)
            .ThenByDescending(display => display.Bounds.Width)
            .First();
    }

    internal static ShellDisplaySnapshot? Select(
        IReadOnlyCollection<ShellDisplaySnapshot> displays,
        string? value)
    {
        if (displays.Count == 0)
            return null;

        string normalized = NormalizeValue(value);
        if (TryGetDeviceName(
                normalized,
                out string deviceName))
        {
            foreach (ShellDisplaySnapshot display
                     in displays)
            {
                if (string.Equals(
                        display.DeviceName,
                        deviceName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return display;
                }
            }

            foreach (ShellDisplaySnapshot display
                     in displays)
            {
                if (display.IsPrimary)
                    return display;
            }

            return Select(
                displays,
                ShellDisplayTargetMode
                    .OutermostRight);
        }

        return Select(
            displays,
            Parse(normalized));
    }

    internal static ShellDisplayTargetMode Parse(
        string? value) =>
        string.Equals(
            value,
            PrimaryValue,
            System.StringComparison.Ordinal)
            ? ShellDisplayTargetMode.Primary
            : ShellDisplayTargetMode.OutermostRight;

    internal static string NormalizeValue(
        string? value)
    {
        if (TryGetDeviceName(
                value,
                out string deviceName))
        {
            return DevicePrefix + deviceName;
        }

        return Parse(value)
            == ShellDisplayTargetMode.Primary
                ? PrimaryValue
                : OutermostRightValue;
    }

    internal static IReadOnlyList<
        ShellDisplayTargetOption> GetOptions(
        string? selectedValue = null) =>
        CreateOptions(
            CaptureDisplays(),
            selectedValue);

    internal static IReadOnlyList<
        ShellDisplayTargetOption> CreateOptions(
        IReadOnlyCollection<ShellDisplaySnapshot>
            displays,
        string? selectedValue = null)
    {
        var options =
            new List<ShellDisplayTargetOption>
            {
                new(
                    OutermostRightValue,
                    "自动：最右侧屏幕"),
                new(
                    PrimaryValue,
                    "自动：Windows 主屏")
            };

        int index = 1;
        foreach (ShellDisplaySnapshot display
                 in displays
                     .Where(display =>
                         !string.IsNullOrWhiteSpace(
                             display.DeviceName))
                     .OrderBy(display =>
                         display.Bounds.Left)
                     .ThenBy(display =>
                         display.Bounds.Top)
                     .ThenBy(display =>
                         display.DeviceName,
                         StringComparer.OrdinalIgnoreCase))
        {
            string primarySuffix =
                display.IsPrimary
                    ? " · 主屏"
                    : string.Empty;
            options.Add(
                new ShellDisplayTargetOption(
                    DevicePrefix
                    + display.DeviceName,
                    $"显示器 {index++}{primarySuffix} · "
                    + $"{display.Bounds.Width}×"
                    + $"{display.Bounds.Height} · "
                    + $"({display.Bounds.Left},"
                    + $"{display.Bounds.Top})"));
        }

        string normalized =
            NormalizeValue(selectedValue);
        if (TryGetDeviceName(
                normalized,
                out _)
            && options.All(option =>
                !string.Equals(
                    option.Value,
                    normalized,
                    StringComparison.OrdinalIgnoreCase)))
        {
            options.Add(
                new ShellDisplayTargetOption(
                    normalized,
                    "已断开的显示器（当前临时使用主屏）"));
        }

        return new ReadOnlyCollection<
            ShellDisplayTargetOption>(
            options);
    }

    private static ShellDisplaySnapshot[]
        CaptureDisplays() =>
        Forms.Screen.AllScreens
            .Select(screen =>
                new ShellDisplaySnapshot(
                    screen.Bounds,
                    screen.Primary,
                    screen.DeviceName))
            .ToArray();

    private static bool TryGetDeviceName(
        string? value,
        out string deviceName)
    {
        deviceName = string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || !value.StartsWith(
                DevicePrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        deviceName =
            value[DevicePrefix.Length..]
                .Trim();
        return deviceName.Length > 0;
    }
}
