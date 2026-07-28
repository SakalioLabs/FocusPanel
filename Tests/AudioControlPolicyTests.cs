using System;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AudioControlPolicyTests
{
    [Fact]
    public void SuccessfulWrite_UsesRequestedValue()
    {
        float written = -1;

        AudioControlResult<float> result =
            AudioControlPolicy.Apply(
                0.65f,
                0.25f,
                value =>
                {
                    written = value;
                    return true;
                });

        Assert.True(result.Succeeded);
        Assert.Equal(0.65f, written);
        Assert.Equal(0.65f, result.EffectiveValue);
    }

    [Fact]
    public void RejectedWrite_RestoresConfirmedValue()
    {
        AudioControlResult<float> result =
            AudioControlPolicy.Apply(
                0.9f,
                0.4f,
                _ => false);

        Assert.False(result.Succeeded);
        Assert.Equal(0.4f, result.EffectiveValue);
    }

    [Fact]
    public void ExceptionalWrite_RestoresConfirmedValue()
    {
        AudioControlResult<bool> result =
            AudioControlPolicy.Apply(
                true,
                false,
                _ => throw new InvalidOperationException(
                    "device switched"));

        Assert.False(result.Succeeded);
        Assert.False(result.EffectiveValue);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(int.MaxValue, true)]
    [InlineData(-1, false)]
    [InlineData(int.MinValue, false)]
    public void HResultSuccess_UsesComSignConvention(
        int result,
        bool expected)
    {
        Assert.Equal(
            expected,
            SystemStatusService.HResultSucceeded(result));
    }

    [Fact]
    public void UnavailableSnapshot_DoesNotPretendAudioExists()
    {
        AudioStatusSnapshot snapshot =
            AudioStatusSnapshot.Unavailable;

        Assert.False(snapshot.IsAvailable);
        Assert.Equal(0f, snapshot.MasterVolume);
        Assert.False(snapshot.IsMuted);
    }
}
