using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    ApplicationAudioSessionServiceTests
{
    [Fact]
    public void GetSessions_SortsActiveAndRemovesDuplicateIds()
    {
        var native =
            new FakeAudioSessionNativeApi
            {
                Sessions =
                {
                    Session("b", "浏览器", 0.7f),
                    Session(
                        "a",
                        "音乐",
                        0.4f,
                        active: true),
                    Session(
                        "a",
                        "重复音乐",
                        0.9f,
                        active: true)
                }
            };
        using var service =
            new ApplicationAudioSessionService(
                native);

        ApplicationAudioSessionSnapshot[]
            result =
                service.GetSessions().ToArray();

        Assert.Equal(2, result.Length);
        Assert.Equal("a", result[0].SessionId);
        Assert.Equal("音乐", result[0].DisplayName);
        Assert.Equal("b", result[1].SessionId);
    }

    [Fact]
    public void GetSessions_ClampsAndCapsVisibleSessions()
    {
        var native =
            new FakeAudioSessionNativeApi();
        for (int index = 0; index < 15; index++)
        {
            native.Sessions.Add(
                Session(
                    index.ToString(),
                    $"应用 {index:00}",
                    index == 0 ? -1f : 2f));
        }
        using var service =
            new ApplicationAudioSessionService(
                native);

        ApplicationAudioSessionSnapshot[]
            result =
                service.GetSessions().ToArray();

        Assert.Equal(12, result.Length);
        Assert.All(
            result,
            session =>
                Assert.InRange(
                    session.Volume,
                    0f,
                    1f));
    }

    [Fact]
    public void Writes_ValidateIdAndClampVolume()
    {
        var native =
            new FakeAudioSessionNativeApi();
        using var service =
            new ApplicationAudioSessionService(
                native);

        Assert.False(
            service.TrySetVolume("", 0.5f));
        Assert.True(
            service.TrySetVolume("music", 4f));
        Assert.True(
            service.TrySetMuted("music", true));

        Assert.Equal(
            ("music", 1f),
            native.VolumeWrites.Single());
        Assert.Equal(
            ("music", true),
            native.MuteWrites.Single());
    }

    [Fact]
    public void NativeFailures_AreContained()
    {
        var native =
            new FakeAudioSessionNativeApi
            {
                Throw = true
            };
        using var service =
            new ApplicationAudioSessionService(
                native);

        Assert.Empty(service.GetSessions());
        Assert.False(
            service.TrySetVolume("a", 0.5f));
        Assert.False(
            service.TrySetMuted("a", true));
    }

    private static
        ApplicationAudioSessionSnapshot Session(
            string id,
            string name,
            float volume,
            bool active = false) =>
        new(
            id,
            name,
            123,
            volume,
            false,
            active,
            false);

    private sealed class FakeAudioSessionNativeApi :
        IAudioSessionNativeApi
    {
        internal List<
            ApplicationAudioSessionSnapshot>
            Sessions
        {
            get;
        } = new();

        internal List<(string, float)>
            VolumeWrites
        {
            get;
        } = new();

        internal List<(string, bool)>
            MuteWrites
        {
            get;
        } = new();

        internal bool Throw
        {
            get;
            init;
        }

        public IReadOnlyList<
            ApplicationAudioSessionSnapshot>
            GetSessions()
        {
            if (Throw)
                throw new InvalidOperationException();
            return Sessions;
        }

        public bool TrySetVolume(
            string sessionId,
            float volume)
        {
            if (Throw)
                throw new InvalidOperationException();
            VolumeWrites.Add(
                (sessionId, volume));
            return true;
        }

        public bool TrySetMuted(
            string sessionId,
            bool muted)
        {
            if (Throw)
                throw new InvalidOperationException();
            MuteWrites.Add(
                (sessionId, muted));
            return true;
        }
    }
}
