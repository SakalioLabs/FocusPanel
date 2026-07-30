using System;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusPanel.Services;

namespace FocusPanel.ViewModels;

public partial class ApplicationAudioSessionItem :
    ObservableObject
{
    private bool _applyingSnapshot;

    public ApplicationAudioSessionItem(
        ApplicationAudioSessionSnapshot snapshot)
    {
        SessionId = snapshot.SessionId;
        ApplySnapshot(snapshot);
    }

    public string SessionId { get; }

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private int processId;

    [ObservableProperty]
    private float volume;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(MuteActionLabel))]
    private bool isMuted;

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private bool isSystemSounds;

    internal float ConfirmedVolume
    {
        get;
        private set;
    }

    internal bool ConfirmedMuted
    {
        get;
        private set;
    }

    public string MuteActionLabel =>
        IsMuted
            ? $"取消静音 {DisplayName}"
            : $"静音 {DisplayName}";

    internal event Action<
        ApplicationAudioSessionItem,
        float>? VolumeRequested;

    partial void OnVolumeChanged(float value)
    {
        if (_applyingSnapshot)
            return;

        float normalized =
            Math.Clamp(value, 0f, 1f);
        if (Math.Abs(normalized - value)
            > float.Epsilon)
        {
            ApplyDisplayedVolume(normalized);
        }

        VolumeRequested?.Invoke(
            this,
            normalized);
    }

    internal void ApplySnapshot(
        ApplicationAudioSessionSnapshot snapshot)
    {
        ConfirmedVolume =
            Math.Clamp(snapshot.Volume, 0f, 1f);
        ConfirmedMuted = snapshot.IsMuted;
        _applyingSnapshot = true;
        try
        {
            DisplayName = snapshot.DisplayName;
            ProcessId = snapshot.ProcessId;
            Volume = ConfirmedVolume;
            IsMuted = ConfirmedMuted;
            IsActive = snapshot.IsActive;
            IsSystemSounds =
                snapshot.IsSystemSounds;
        }
        finally
        {
            _applyingSnapshot = false;
        }
    }

    internal void ConfirmVolume(float value)
    {
        ConfirmedVolume =
            Math.Clamp(value, 0f, 1f);
    }

    internal void ConfirmMuted(bool value)
    {
        ConfirmedMuted = value;
    }

    internal void ApplyDisplayedVolume(float value)
    {
        _applyingSnapshot = true;
        try
        {
            Volume =
                Math.Clamp(value, 0f, 1f);
        }
        finally
        {
            _applyingSnapshot = false;
        }
    }

    internal void ApplyDisplayedMuted(bool value)
    {
        _applyingSnapshot = true;
        try
        {
            IsMuted = value;
        }
        finally
        {
            _applyingSnapshot = false;
        }
    }
}
