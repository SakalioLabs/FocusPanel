using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class SystemStatusShellEntryTests
{
    [Fact]
    public void TaskView_UsesWinTabShortcutBoundary()
    {
        WindowsShellShortcut? captured = null;
        using var service = new SystemStatusService(
            shortcut =>
            {
                captured = shortcut;
                return true;
            });

        Assert.True(service.OpenTaskView());
        Assert.Equal((ushort)0x09, captured?.Key);
        Assert.True(captured?.UsesWindowsKey);
    }

    [Theory]
    [InlineData(
        "SoundOutput",
        0x56,
        true,
        false)]
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
            "SoundOutput" =>
                service.OpenSoundOutput(),
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
