using FocusPanel.Services;
using FocusPanel.ViewModels;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    ApplicationAudioSessionItemTests
{
    [Fact]
    public void ApplyingSnapshot_DoesNotRequestHardwareWrite()
    {
        var item =
            new ApplicationAudioSessionItem(
                Snapshot(0.4f, false));
        int requests = 0;
        item.VolumeRequested +=
            (_, _) => requests++;

        item.ApplySnapshot(
            Snapshot(0.7f, true));

        Assert.Equal(0, requests);
        Assert.Equal(0.7f, item.Volume);
        Assert.True(item.IsMuted);
        Assert.Equal(0.7f, item.ConfirmedVolume);
        Assert.True(item.ConfirmedMuted);
    }

    [Fact]
    public void UserVolumeChange_IsClampedAndRequested()
    {
        var item =
            new ApplicationAudioSessionItem(
                Snapshot(0.4f, false));
        float requested = -1f;
        item.VolumeRequested +=
            (_, value) => requested = value;

        item.Volume = 3f;

        Assert.Equal(1f, item.Volume);
        Assert.Equal(1f, requested);
    }

    [Fact]
    public void MuteLabel_TracksDisplayedState()
    {
        var item =
            new ApplicationAudioSessionItem(
                Snapshot(0.4f, false));

        Assert.Contains(
            "静音",
            item.MuteActionLabel);
        item.ApplyDisplayedMuted(true);
        Assert.StartsWith(
            "取消静音",
            item.MuteActionLabel);
    }

    private static
        ApplicationAudioSessionSnapshot Snapshot(
            float volume,
            bool muted) =>
        new(
            "session",
            "音乐",
            42,
            volume,
            muted,
            true,
            false);
}
