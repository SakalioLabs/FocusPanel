using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class CompactTypeToSearchPolicyTests
{
    [Theory]
    [InlineData("p")]
    [InlineData("Paint.NET")]
    [InlineData("任务")]
    [InlineData("音量 50")]
    public void FocusedCompactDock_OpensWithCommittedText(
        string text)
    {
        Assert.Equal(
            text,
            CompactTypeToSearchPolicy
                .GetInitialQuery(
                    text,
                    ShellKeyboardFocusKind.Command,
                    isCompactDockFocused: true,
                    hasCommandModifier: false,
                    hasTransientSurface: false));
    }

    [Theory]
    [InlineData(false, false, false, ShellKeyboardFocusKind.Command)]
    [InlineData(true, true, false, ShellKeyboardFocusKind.Command)]
    [InlineData(true, false, true, ShellKeyboardFocusKind.Command)]
    [InlineData(true, false, false, ShellKeyboardFocusKind.TextInput)]
    [InlineData(true, false, false, ShellKeyboardFocusKind.SelectionInput)]
    public void EditingModifiersAndTransientSurfaces_DoNotRerouteText(
        bool compactFocused,
        bool hasModifier,
        bool transient,
        ShellKeyboardFocusKind focusKind)
    {
        Assert.Null(
            CompactTypeToSearchPolicy
                .GetInitialQuery(
                    "a",
                    focusKind,
                    compactFocused,
                    hasModifier,
                    transient));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\r")]
    public void EmptyWhitespaceAndControlText_AreIgnored(
        string text)
    {
        Assert.Null(
            CompactTypeToSearchPolicy
                .GetInitialQuery(
                    text,
                    ShellKeyboardFocusKind.Command,
                    isCompactDockFocused: true,
                    hasCommandModifier: false,
                    hasTransientSurface: false));
    }
}
