using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    CompactTaskEntryPresentationTests
{
    [Theory]
    [InlineData(-1, false, "0", "任务，没有未完成项目")]
    [InlineData(0, false, "0", "任务，没有未完成项目")]
    [InlineData(7, true, "7", "任务，7 个未完成")]
    [InlineData(99, true, "99", "任务，99 个未完成")]
    [InlineData(100, true, "99+", "任务，100 个未完成")]
    public void Compose_ProvidesBoundedBadgeAndExactAccessibleCount(
        int count,
        bool expectedBadge,
        string expectedText,
        string expectedAutomationName)
    {
        CompactTaskEntryPresentation result =
            CompactTaskEntryPresentationComposer
                .Compose(count);

        Assert.Equal(expectedBadge, result.HasBadge);
        Assert.Equal(expectedText, result.BadgeText);
        Assert.Equal(
            expectedAutomationName,
            result.AutomationName);
    }
}
