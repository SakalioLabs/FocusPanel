using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class BackgroundAppVisibilityPolicyTests
{
    [Theory]
    [InlineData(0, 42, 1, 1, @"C:\Apps\Chat.exe", @"C:\Windows")]
    [InlineData(42, 42, 1, 1, @"C:\Apps\Chat.exe", @"C:\Windows")]
    [InlineData(7, 42, 2, 1, @"C:\Apps\Chat.exe", @"C:\Windows")]
    [InlineData(7, 42, 1, 1, @"C:\Windows\System32\RuntimeBroker.exe", @"C:\Windows")]
    [InlineData(7, 42, 1, 1, @"D:\Browser\crashpad_handler.exe", @"C:\Windows")]
    [InlineData(7, 42, 1, 1, @"D:\Tools\readme.txt", @"C:\Windows")]
    public void IneligibleProcess_IsExcluded(
        uint processId,
        int currentProcessId,
        int processSessionId,
        int currentSessionId,
        string executablePath,
        string windowsDirectory)
    {
        Assert.False(
            BackgroundAppVisibilityPolicy
                .ShouldInclude(
                    processId,
                    currentProcessId,
                    processSessionId,
                    currentSessionId,
                    executablePath,
                    windowsDirectory));
    }

    [Theory]
    [InlineData(@"C:\Program Files\Chat\Chat.exe")]
    [InlineData(@"C:\Users\Me\AppData\Local\Sync\Sync.exe")]
    [InlineData(@"D:\Portable\Player.exe")]
    public void UserApplication_IsIncluded(
        string executablePath)
    {
        Assert.True(
            BackgroundAppVisibilityPolicy
                .ShouldInclude(
                    7,
                    42,
                    1,
                    1,
                    executablePath,
                    @"C:\Windows"));
    }

    [Fact]
    public void DisplayName_PrefersFileDescription()
    {
        Assert.Equal(
            "同步助手",
            BackgroundAppVisibilityPolicy
                .GetDisplayName(
                    "sync",
                    "  同步助手  "));
        Assert.Equal(
            "sync",
            BackgroundAppVisibilityPolicy
                .GetDisplayName(
                    " sync ",
                    null));
    }
}
