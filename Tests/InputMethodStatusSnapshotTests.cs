using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class InputMethodStatusSnapshotTests
{
    [Theory]
    [InlineData("zh", "微软拼音", "拼")]
    [InlineData("ZH", "Microsoft Pinyin", "拼")]
    [InlineData("zh", "五笔输入法", "五")]
    [InlineData("zh", "Wubi IME", "五")]
    [InlineData("zh", "注音", "注")]
    [InlineData("zh", "Bopomofo", "注")]
    [InlineData("zh", "", "中")]
    [InlineData("zh", "仓颉", "中")]
    public void ChineseMethod_UsesKnownShortLabel(
        string language,
        string description,
        string expectedMethod)
    {
        InputMethodStatusSnapshot snapshot =
            InputMethodStatusSnapshot.FromObservation(
                language,
                description);

        Assert.Equal("中", snapshot.LanguageDisplay);
        Assert.Equal(expectedMethod, snapshot.MethodDisplay);
    }

    [Theory]
    [InlineData("en", "EN")]
    [InlineData("ja", "日")]
    [InlineData("ko", "한")]
    [InlineData("fr", "FR")]
    [InlineData("  de  ", "DE")]
    public void NonChineseLanguage_UsesStableShortLabel(
        string language,
        string expected)
    {
        InputMethodStatusSnapshot snapshot =
            InputMethodStatusSnapshot.FromObservation(
                language,
                "ignored");

        Assert.Equal(expected, snapshot.LanguageDisplay);
        Assert.Equal(expected, snapshot.MethodDisplay);
        Assert.Equal(expected, snapshot.Display);
    }

    [Fact]
    public void ChinesePinyin_BuildsVisibleButtonAndSummary()
    {
        InputMethodStatusSnapshot snapshot =
            InputMethodStatusSnapshot.FromObservation(
                "zh",
                "微软拼音");

        Assert.Equal("中 / 拼", snapshot.Display);
        Assert.Equal(
            "输入法 · 中 / 拼",
            snapshot.ButtonLabel);
        Assert.Equal(
            "当前输入法 中 / 拼",
            snapshot.Summary);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingLanguage_IsUnavailable(
        string? language)
    {
        InputMethodStatusSnapshot snapshot =
            InputMethodStatusSnapshot.FromObservation(
                language,
                "Pinyin");

        Assert.Equal(
            InputMethodStatusSnapshot.Unavailable,
            snapshot);
        Assert.Equal("输入法", snapshot.ButtonLabel);
        Assert.Equal(
            "当前输入法状态不可用",
            snapshot.Summary);
    }
}
