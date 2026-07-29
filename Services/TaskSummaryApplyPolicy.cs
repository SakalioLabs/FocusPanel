using System;

namespace FocusPanel.Services;

public readonly record struct TaskSummaryApplyDecision(
    bool ApplyOpenTaskCount,
    bool ApplyCalendar);

public static class TaskSummaryApplyPolicy
{
    public static TaskSummaryApplyDecision GetDecision(
        TaskSummarySnapshot snapshot,
        DateTime currentDisplayedMonth)
    {
        if (!snapshot.IsValid)
            return new(false, false);

        DateTime currentMonth =
            TaskSummarySnapshot.NormalizeMonth(
                currentDisplayedMonth);
        return new(
            true,
            snapshot.DisplayedMonth == currentMonth);
    }
}
