using System;
using System.Collections.Generic;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal enum WindowStateAction
{
    Restore,
    Minimize,
    Maximize
}

internal static class WindowStateActionPolicy
{
    private static readonly IReadOnlyList<
        WindowStateAction> NormalActions =
        new[]
        {
            WindowStateAction.Minimize,
            WindowStateAction.Maximize
        };

    private static readonly IReadOnlyList<
        WindowStateAction> MinimizedActions =
        new[]
        {
            WindowStateAction.Restore,
            WindowStateAction.Maximize
        };

    private static readonly IReadOnlyList<
        WindowStateAction> MaximizedActions =
        new[]
        {
            WindowStateAction.Restore,
            WindowStateAction.Minimize
        };

    internal static IReadOnlyList<
        WindowStateAction> GetActions(
            TrackedWindowState state) =>
        state switch
        {
            TrackedWindowState.Normal =>
                NormalActions,
            TrackedWindowState.Minimized =>
                MinimizedActions,
            TrackedWindowState.Maximized =>
                MaximizedActions,
            _ => throw new
                ArgumentOutOfRangeException(
                    nameof(state))
        };
}
