using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellFocusRetentionPolicyTests
{
    [Theory]
    [InlineData(ShellKeyboardFocusKind.None, false)]
    [InlineData(ShellKeyboardFocusKind.Command, false)]
    [InlineData(ShellKeyboardFocusKind.TextInput, true)]
    [InlineData(ShellKeyboardFocusKind.SelectionInput, true)]
    public void OnlyInputControlsRetainExpandedShell(
        ShellKeyboardFocusKind focusKind,
        bool expected)
    {
        Assert.Equal(
            expected,
            ShellFocusRetentionPolicy.ShouldRetainShell(
                focusKind));
    }
}
