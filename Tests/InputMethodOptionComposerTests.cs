using System.Linq;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class InputMethodOptionComposerTests
{
    [Fact]
    public void Compose_KeepsDistinctMethodsForSameLanguage()
    {
        InputMethodOption[] options =
            InputMethodOptionComposer.Compose(
                    new[]
                    {
                        new InputMethodObservation(
                            101,
                            "zh",
                            "中文(简体，中国)",
                            "微软拼音"),
                        new InputMethodObservation(
                            102,
                            "zh",
                            "中文(简体，中国)",
                            "微软五笔")
                    },
                    activeLayoutHandle: 102)
                .ToArray();

        Assert.Equal(2, options.Length);
        Assert.Equal(
            new[] { "微软拼音", "微软五笔" },
            options.Select(item =>
                item.DisplayName));
        Assert.False(options[0].IsActive);
        Assert.True(options[1].IsActive);
        Assert.Equal("中 / 拼", options[0].ShortLabel);
        Assert.Equal("中 / 五", options[1].ShortLabel);
    }

    [Fact]
    public void Compose_DeduplicatesSameLayoutHandleInStableOrder()
    {
        InputMethodOption[] options =
            InputMethodOptionComposer.Compose(
                    new[]
                    {
                        new InputMethodObservation(
                            20,
                            "en",
                            "English",
                            string.Empty),
                        new InputMethodObservation(
                            10,
                            "fr",
                            "français",
                            string.Empty),
                        new InputMethodObservation(
                            20,
                            "en",
                            "duplicate",
                            string.Empty)
                    },
                    activeLayoutHandle: 20)
                .ToArray();

        Assert.Equal(
            new long[] { 20, 10 },
            options.Select(item =>
                item.LayoutHandle));
        Assert.Equal("English", options[0].DisplayName);
        Assert.Equal("EN", options[0].ShortLabel);
    }

    [Fact]
    public void Compose_DistinguishesLayoutsWithSameVisibleName()
    {
        InputMethodOption[] options =
            InputMethodOptionComposer.Compose(
                    new[]
                    {
                        new InputMethodObservation(
                            0x00000409,
                            "en",
                            "English (United States)",
                            string.Empty),
                        new InputMethodObservation(
                            0x00010409,
                            "en",
                            "English (United States)",
                            string.Empty)
                    },
                    activeLayoutHandle:
                        0x00000409)
                .ToArray();

        Assert.Contains(
            "布局 00000409",
            options[0].Detail);
        Assert.Contains(
            "布局 00010409",
            options[1].Detail);
    }
}
