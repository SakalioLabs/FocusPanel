using System.Collections.Generic;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    SystemStatusMediaTransportTests
{
    public static IEnumerable<object[]>
        MediaCases()
    {
        yield return new object[]
        {
            MediaTransportAction.PreviousTrack,
            (ushort)0xB1
        };
        yield return new object[]
        {
            MediaTransportAction.PlayPause,
            (ushort)0xB3
        };
        yield return new object[]
        {
            MediaTransportAction.NextTrack,
            (ushort)0xB0
        };
    }

    [Theory]
    [MemberData(nameof(MediaCases))]
    public void SendMediaCommand_UsesInjectableKeyboardBoundary(
        MediaTransportAction action,
        ushort expectedKey)
    {
        WindowsShellShortcut? captured =
            null;
        using var service =
            new SystemStatusService(
                shortcut =>
                {
                    captured = shortcut;
                    return true;
                });

        bool succeeded =
            service.SendMediaCommand(
                action);

        Assert.True(succeeded);
        Assert.Equal(
            expectedKey,
            captured?.Key);
        Assert.False(
            captured?.UsesWindowsKey);
    }

    [Fact]
    public void SendMediaCommand_PropagatesBoundaryFailure()
    {
        using var service =
            new SystemStatusService(
                _ => false);

        Assert.False(
            service.SendMediaCommand(
                MediaTransportAction
                    .PlayPause));
    }

    [Fact]
    public void SendMediaCommand_InvalidActionDoesNotCallBoundary()
    {
        int calls = 0;
        using var service =
            new SystemStatusService(
                _ =>
                {
                    calls++;
                    return true;
                });

        Assert.False(
            service.SendMediaCommand(
                (MediaTransportAction)99));
        Assert.Equal(0, calls);
    }
}
