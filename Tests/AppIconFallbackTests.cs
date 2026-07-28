using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AppIconFallbackTests
{
    [Theory]
    [InlineData("focus panel", "F")]
    [InlineData("7-Zip", "7")]
    [InlineData("微信", "微")]
    [InlineData("  obs studio", "O")]
    [InlineData("--- PowerShell", "P")]
    public void GetText_UsesFirstIdentifyingCharacter(
        string displayName,
        string expected)
    {
        Assert.Equal(
            expected,
            AppIconFallback.GetText(displayName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("🎯")]
    public void GetText_UsesStableFallbackWhenNameIsNotIdentifying(
        string? displayName)
    {
        Assert.Equal("A", AppIconFallback.GetText(displayName));
    }
}
