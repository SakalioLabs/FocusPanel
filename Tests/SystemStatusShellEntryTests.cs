using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class SystemStatusShellEntryTests
{
    [Theory]
    [InlineData(
        "ScreenSnipping",
        0x53,
        false,
        true)]
    [InlineData(
        "ProjectDisplay",
        0x50,
        false,
        false)]
    [InlineData(
        "CastDevices",
        0x4B,
        false,
        false)]
    public void ProductivityEntries_UsePublicWindowsShortcutBoundary(
        string action,
        int expectedKey,
        bool expectedControl,
        bool expectedShift)
    {
        WindowsShellShortcut? captured = null;
        using var service = new SystemStatusService(
            shortcut =>
            {
                captured = shortcut;
                return true;
            });

        bool succeeded = action switch
        {
            "ScreenSnipping" =>
                service.OpenScreenSnipping(),
            "ProjectDisplay" =>
                service.OpenProjectDisplay(),
            "CastDevices" =>
                service.OpenCastDevices(),
            _ => false
        };

        Assert.True(succeeded);
        Assert.True(captured?.UsesWindowsKey);
        Assert.Equal(expectedControl, captured?.UsesControl);
        Assert.Equal(expectedShift, captured?.UsesShift);
        Assert.Equal((ushort)expectedKey, captured?.Key);
    }
}
