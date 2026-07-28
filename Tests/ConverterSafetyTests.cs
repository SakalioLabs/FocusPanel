using System;
using System.Globalization;
using System.Windows.Data;
using FocusPanel.Converters;
using FocusPanel.Models;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ConverterSafetyTests
{
    [Fact]
    public void ReadOnlyConverters_DoNotAttemptToWriteBack()
    {
        object expected = Binding.DoNothing;
        object? value = null;
        Type targetType = typeof(object);
        object? parameter = null;
        CultureInfo culture = CultureInfo.InvariantCulture;

        Assert.Same(expected, new BooleanToStrikeThroughConverter()
            .ConvertBack(value!, targetType, parameter!, culture));
        Assert.Same(expected, new ProgressToColorConverter()
            .ConvertBack(value!, targetType, parameter!, culture));
        Assert.Same(expected, new SyncStatusToIconConverter()
            .ConvertBack(value!, targetType, parameter!, culture));
        Assert.Same(expected, new SyncStatusToTooltipConverter()
            .ConvertBack(value!, targetType, parameter!, culture));
    }

    [Theory]
    [InlineData(0, "\u4ECA\u5929")]
    [InlineData(1, "\u6628\u5929")]
    [InlineData(3, "\u672C\u5468")]
    [InlineData(10, "\u672C\u6708")]
    [InlineData(40, "\u66F4\u65E9")]
    public void DesktopFile_DateGroupUsesChineseLabels(
        int daysAgo,
        string expected)
    {
        var file = new DesktopFile
        {
            CreatedAt = DateTime.Now.Date.AddDays(-daysAgo)
        };

        Assert.Equal(expected, file.DateGroup);
    }
}
