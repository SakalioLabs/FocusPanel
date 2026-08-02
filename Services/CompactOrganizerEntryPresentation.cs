using System;
using System.Globalization;

namespace FocusPanel.Services;

internal readonly record struct
    CompactOrganizerEntryPresentation(
        bool HasBadge,
        string BadgeText,
        string AutomationName);

internal static class
    CompactOrganizerEntryPresentationComposer
{
    internal static CompactOrganizerEntryPresentation
        Compose(int collectedItemCount)
    {
        int count = Math.Max(
            0,
            collectedItemCount);
        return new CompactOrganizerEntryPresentation(
            count > 0,
            count > 99
                ? "99+"
                : count.ToString(
                    CultureInfo.InvariantCulture),
            count > 0
                ? $"桌面收纳，已收纳 {count} 个项目"
                : "桌面收纳，没有已收纳项目");
    }
}
