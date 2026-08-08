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
        return ShellDisplayPresentationPolicy
            .Create(displays)
            .Select(presentation =>
                new WindowDisplayMoveOption(
                    presentation.DeviceName,
                    presentation.DisplayName,
                    presentation.WorkArea))
            .ToArray();
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
