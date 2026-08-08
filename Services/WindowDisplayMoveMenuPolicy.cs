using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal sealed record WindowDisplayMoveOption(
    string DeviceName,
    string DisplayName,
    Rectangle WorkArea);

internal sealed record WindowDisplayMoveMenuState(
    WindowDisplayMoveOption Option,
    bool IsCurrent,
    bool CanMove);

public sealed record WindowDisplayMoveRequest(
    WindowReference Window,
    Rectangle TargetWorkArea,
    string TargetDisplayName);

public sealed record TaskbarDisplayMoveRequest(
    TaskbarAppItem Task,
    Rectangle TargetWorkArea,
    string TargetDisplayName);

internal static class WindowDisplayMoveMenuPolicy
{
    internal static IReadOnlyList<
        WindowDisplayMoveOption> CreateOptions(
        IReadOnlyCollection<ShellDisplaySnapshot>
            displays)
    {
        ShellDisplaySnapshot[] ordered = displays
            .Where(display =>
                display.Bounds.Width > 0
                && display.Bounds.Height > 0
                && !string.IsNullOrWhiteSpace(
                    display.DeviceName))
            .OrderBy(display =>
                display.Bounds.Left)
            .ThenBy(display =>
                display.Bounds.Top)
            .ThenBy(display =>
                display.DeviceName,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var options = new List<
            WindowDisplayMoveOption>(
            ordered.Length);
        for (int index = 0;
             index < ordered.Length;
             index++)
        {
            ShellDisplaySnapshot display =
                ordered[index];
            Rectangle workArea =
                display.WorkingArea.Width > 0
                && display.WorkingArea.Height > 0
                    ? display.WorkingArea
                    : display.Bounds;
            string primary = display.IsPrimary
                ? " · 主屏"
                : string.Empty;
            options.Add(
                new WindowDisplayMoveOption(
                    display.DeviceName,
                    $"显示器 {index + 1}{primary} · "
                    + $"{display.Bounds.Width}×"
                    + $"{display.Bounds.Height} · "
                    + $"({display.Bounds.Left},"
                    + $"{display.Bounds.Top})",
                    workArea));
        }

        return options;
    }

    internal static IReadOnlyList<
        WindowDisplayMoveMenuState> ResolveWindow(
        IReadOnlyList<WindowDisplayMoveOption>
            options,
        Func<Rectangle, bool> canMove)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(canMove);
        bool[] movable = options
            .Select(option =>
                SystemActionExecution.Try(
                    () => canMove(
                        option.WorkArea)))
            .ToArray();
        int currentIndex = movable.Count(value =>
            !value) == 1
                ? Array.FindIndex(
                    movable,
                    value => !value)
                : -1;
        return options
            .Select((option, index) =>
                new WindowDisplayMoveMenuState(
                    option,
                    index == currentIndex,
                    movable[index]))
            .ToArray();
    }
}
