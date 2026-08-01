using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class SystemStatusShellEntryTests
{
    [Fact]
    public void StartMenu_PrefersDocumentedNativeShellCommand()
    {
        int shortcutCalls = 0;
        int nativeCalls = 0;
        using var service = new SystemStatusService(
            _ =>
            {
                shortcutCalls++;
                return true;
            },
            () =>
            {
                nativeCalls++;
                return true;
            });

        Assert.True(service.OpenStartMenu());
        Assert.Equal(1, nativeCalls);
        Assert.Equal(0, shortcutCalls);
    }

    [Fact]
    public void StartMenu_FallsBackToWindowsKeyWhenShellCommandFails()
    {
        WindowsShellShortcut? captured = null;
        using var service = new SystemStatusService(
            shortcut =>
            {
                captured = shortcut;
                return true;
            },
            () => false);

        Assert.True(service.OpenStartMenu());
        Assert.Equal((ushort)0x5B, captured?.Key);
        Assert.False(captured?.UsesWindowsKey);
    }

    [Fact]
    public void TaskView_UsesWinTabShortcutBoundary()
    {
        WindowsShellShortcut? captured = null;
        using var service = new SystemStatusService(
            shortcut =>
            {
                captured = shortcut;
                return true;
            },
            () => true);

        Assert.True(service.OpenTaskView());
        Assert.Equal((ushort)0x09, captured?.Key);
        Assert.True(captured?.UsesWindowsKey);
    }
}
