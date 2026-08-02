using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    CompactOrganizerEntryPresentationTests
{
    [Theory]
    [InlineData(-1, false, "0", "桌面收纳，没有已收纳项目")]
    [InlineData(0, false, "0", "桌面收纳，没有已收纳项目")]
    [InlineData(7, true, "7", "桌面收纳，已收纳 7 个项目")]
    [InlineData(99, true, "99", "桌面收纳，已收纳 99 个项目")]
    [InlineData(100, true, "99+", "桌面收纳，已收纳 100 个项目")]
    public void Compose_ProvidesBoundedBadgeAndExactAccessibleCount(
        int count,
        bool expectedBadge,
        string expectedText,
        string expectedAutomationName)
    {
        CompactOrganizerEntryPresentation result =
            CompactOrganizerEntryPresentationComposer
                .Compose(count);

        Assert.Equal(expectedBadge, result.HasBadge);
        Assert.Equal(expectedText, result.BadgeText);
        Assert.Equal(
            expectedAutomationName,
            result.AutomationName);
    }
}
