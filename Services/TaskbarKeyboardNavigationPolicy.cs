using System;
using System.Windows.Input;

namespace FocusPanel.Services;

internal enum TaskbarKeyboardNavigationAction
{
    None,
    Previous,
    Next,
    First,
    Last,
    PreviousPage,
    NextPage
}

internal static class TaskbarKeyboardNavigationPolicy
{
    internal static TaskbarKeyboardNavigationAction
        GetAction(
            Key key,
            ModifierKeys modifiers)
    {
        if (modifiers != ModifierKeys.None)
            return TaskbarKeyboardNavigationAction.None;

        return key switch
        {
            Key.Up =>
                TaskbarKeyboardNavigationAction.Previous,
            Key.Down =>
                TaskbarKeyboardNavigationAction.Next,
            Key.Home =>
                TaskbarKeyboardNavigationAction.First,
            Key.End =>
                TaskbarKeyboardNavigationAction.Last,
            Key.PageUp =>
                TaskbarKeyboardNavigationAction.PreviousPage,
            Key.PageDown =>
                TaskbarKeyboardNavigationAction.NextPage,
            _ => TaskbarKeyboardNavigationAction.None
        };
    }

    internal static int GetTargetIndex(
        int currentIndex,
        int itemCount,
        TaskbarKeyboardNavigationAction action,
        int pageSize)
    {
        if (itemCount <= 0
            || currentIndex < 0
            || currentIndex >= itemCount
            || action
                == TaskbarKeyboardNavigationAction.None)
        {
            return -1;
        }

        int lastIndex = itemCount - 1;
        int safePageSize = Math.Max(1, pageSize);
        return action switch
        {
            TaskbarKeyboardNavigationAction.Previous =>
                Math.Max(0, currentIndex - 1),
            TaskbarKeyboardNavigationAction.Next =>
                Math.Min(lastIndex, currentIndex + 1),
            TaskbarKeyboardNavigationAction.First => 0,
            TaskbarKeyboardNavigationAction.Last => lastIndex,
            TaskbarKeyboardNavigationAction.PreviousPage =>
                Math.Max(0, currentIndex - safePageSize),
            TaskbarKeyboardNavigationAction.NextPage =>
                Math.Min(lastIndex, currentIndex + safePageSize),
            _ => -1
        };
    }

    internal static int GetPageSize(
        double viewportHeight,
        double itemExtent,
        double leadingInset,
        double trailingInset)
    {
        if (!double.IsFinite(viewportHeight)
            || !double.IsFinite(itemExtent)
            || itemExtent <= 0)
        {
            return 1;
        }

        double usableHeight = Math.Max(
            0,
            viewportHeight
            - NormalizeInset(leadingInset)
            - NormalizeInset(trailingInset));
        return Math.Max(
            1,
            (int)Math.Floor(
                usableHeight / itemExtent));
    }

    private static double NormalizeInset(
        double value) =>
        double.IsFinite(value)
            ? Math.Max(0, value)
            : 0;
}
