using System;
using System.Globalization;

namespace FocusPanel.Services;

internal readonly record struct
    CompactTaskEntryPresentation(
        bool HasBadge,
        string BadgeText,
        string AutomationName);

internal static class
    CompactTaskEntryPresentationComposer
{
    internal static CompactTaskEntryPresentation
        Compose(int openTaskCount)
    {
        int count = Math.Max(0, openTaskCount);
        return new CompactTaskEntryPresentation(
            count > 0,
            count > 99
                ? "99+"
                : count.ToString(
                    CultureInfo.InvariantCulture),
            count > 0
                ? $"任务，{count} 个未完成"
                : "任务，没有未完成项目");
    }
}
