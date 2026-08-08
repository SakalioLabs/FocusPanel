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
    string DeviceName = "",
    Rectangle WorkingArea = default);

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
        => GetBounds(
            value,
            ShellPanelEdgePolicy.RightValue);

    internal static Rectangle GetBounds(
        string? value,
        string? panelEdge)
    {
        ShellDisplaySnapshot[] displays =
            CaptureDisplays();
        return Select(
                   displays,
                   value,
                   panelEdge)?.Bounds
            ?? Rectangle.Empty;
    }

    internal static Rectangle GetWorkingArea(
        string? value) =>
        GetWorkingArea(
            CaptureDisplays(),
            value,
            ShellPanelEdgePolicy.RightValue);

    internal static Rectangle GetWorkingArea(
        string? value,
        string? panelEdge) =>
        GetWorkingArea(
            CaptureDisplays(),
            value,
            panelEdge);

    internal static Rectangle GetWorkingArea(
        IReadOnlyCollection<ShellDisplaySnapshot>
            displays,
        string? value,
        string? panelEdge = null)
    {
        ShellDisplaySnapshot? selected =
            Select(
                displays,
                value,
                panelEdge);
        if (selected == null)
            return Rectangle.Empty;

        return selected.Value.WorkingArea.Width > 0
               && selected.Value.WorkingArea.Height > 0
            ? selected.Value.WorkingArea
            : selected.Value.Bounds;
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
        => Select(
            displays,
            value,
            ShellPanelEdgePolicy.RightValue);

    internal static ShellDisplaySnapshot? Select(
        IReadOnlyCollection<ShellDisplaySnapshot> displays,
        string? value,
        string? panelEdge)
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

            return SelectOutermost(
                displays,
                panelEdge);
        }

        if (Parse(normalized)
            == ShellDisplayTargetMode.Primary)
        {
            return Select(
                displays,
                ShellDisplayTargetMode.Primary);
        }

        return SelectOutermost(
            displays,
            panelEdge);
    }

    private static ShellDisplaySnapshot? SelectOutermost(
        IReadOnlyCollection<ShellDisplaySnapshot> displays,
        string? panelEdge)
    {
        if (displays.Count == 0)
            return null;

        return ShellPanelEdgePolicy.IsLeft(panelEdge)
            ? displays
                .OrderBy(display =>
                    display.Bounds.Left)
                .ThenByDescending(display =>
                    display.IsPrimary)
                .ThenBy(display =>
                    display.Bounds.Top)
                .ThenByDescending(display =>
                    display.Bounds.Width)
                .First()
            : Select(
                displays,
                ShellDisplayTargetMode
                    .OutermostRight);
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
        string? selectedValue = null,
        string? panelEdge = null) =>
        CreateOptions(
            CaptureDisplays(),
            selectedValue,
            panelEdge);

    internal static IReadOnlyList<
        ShellDisplayTargetOption> CreateOptions(
        IReadOnlyCollection<ShellDisplaySnapshot>
            displays,
        string? selectedValue = null,
        string? panelEdge = null)
    {
        var options =
            new List<ShellDisplayTargetOption>
            {
                new(
                    OutermostRightValue,
                    ShellPanelEdgePolicy.IsLeft(
                        panelEdge)
                        ? "自动：最左侧屏幕"
                        : "自动：最右侧屏幕"),
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

    internal static ShellDisplaySnapshot[]
        CaptureDisplays() =>
        Forms.Screen.AllScreens
            .Select(screen =>
                new ShellDisplaySnapshot(
                    screen.Bounds,
                    screen.Primary,
                    screen.DeviceName,
                    screen.WorkingArea))
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
