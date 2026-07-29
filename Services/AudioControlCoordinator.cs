using System;
using System.Threading.Tasks;

namespace FocusPanel.Services;

internal readonly record struct AudioControlMutation(
    long VolumeRevision,
    float? Volume,
    long MuteRevision,
    bool? IsMuted);

internal readonly record struct AudioControlOutcome(
    AudioControlMutation Mutation,
    bool? VolumeSucceeded,
    bool? MuteSucceeded);

internal readonly record struct AudioControlConfirmationState(
    float ConfirmedVolume,
    bool ConfirmedMuted,
    long VolumeRevision,
    long MuteRevision,
    bool VolumePending,
    bool MutePending);

internal readonly record struct AudioControlCompletion(
    AudioControlConfirmationState State,
    float? DisplayVolume,
    bool? DisplayMuted,
    bool CurrentSucceeded,
    bool CurrentFailed);

internal static class AudioControlCompletionPolicy
{
    internal static AudioControlCompletion Apply(
        AudioControlConfirmationState state,
        AudioControlOutcome outcome)
    {
        float confirmedVolume =
            state.ConfirmedVolume;
        bool confirmedMuted =
            state.ConfirmedMuted;
        bool volumePending =
            state.VolumePending;
        bool mutePending =
            state.MutePending;
        float? displayVolume = null;
        bool? displayMuted = null;
        bool currentSucceeded = false;
        bool currentFailed = false;

        AudioControlMutation mutation =
            outcome.Mutation;
        if (mutation.Volume is float volume
            && outcome.VolumeSucceeded
                is bool volumeSucceeded)
        {
            if (volumeSucceeded)
                confirmedVolume = volume;

            if (mutation.VolumeRevision
                == state.VolumeRevision)
            {
                volumePending = false;
                displayVolume =
                    volumeSucceeded
                        ? volume
                        : confirmedVolume;
                currentSucceeded |=
                    volumeSucceeded;
                currentFailed |=
                    !volumeSucceeded;
            }
        }

        if (mutation.IsMuted is bool muted
            && outcome.MuteSucceeded
                is bool muteSucceeded)
        {
            if (muteSucceeded)
                confirmedMuted = muted;

            if (mutation.MuteRevision
                == state.MuteRevision)
            {
                mutePending = false;
                displayMuted =
                    muteSucceeded
                        ? muted
                        : confirmedMuted;
                currentSucceeded |=
                    muteSucceeded;
                currentFailed |=
                    !muteSucceeded;
            }
        }

        return new AudioControlCompletion(
            state with
            {
                ConfirmedVolume =
                    confirmedVolume,
                ConfirmedMuted =
                    confirmedMuted,
                VolumePending =
                    volumePending,
                MutePending =
                    mutePending
            },
            displayVolume,
            displayMuted,
            currentSucceeded,
            currentFailed);
    }
}

internal sealed class AudioControlCoordinator
    : IDisposable
{
    private readonly object _sync = new();
    private readonly Func<float, bool> _setVolume;
    private readonly Func<bool, bool> _setMuted;
    private AudioControlMutation? _pending;
    private Task _processor = Task.CompletedTask;
    private bool _isRunning;
    private bool _isAccepting = true;
    private bool _isDisposed;

    internal AudioControlCoordinator(
        Func<float, bool> setVolume,
        Func<bool, bool> setMuted)
    {
        _setVolume =
            setVolume
            ?? throw new ArgumentNullException(
                nameof(setVolume));
        _setMuted =
            setMuted
            ?? throw new ArgumentNullException(
                nameof(setMuted));
    }

    internal event Action<AudioControlOutcome>?
        Completed;

    internal bool QueueVolume(
        long revision,
        float value) =>
        Queue(
            mutation =>
                mutation with
                {
                    VolumeRevision = revision,
                    Volume =
                        Math.Clamp(
                            value,
                            0f,
                            1f)
                });

    internal bool QueueMuted(
        long revision,
        bool value) =>
        Queue(
            mutation =>
                mutation with
                {
                    MuteRevision = revision,
                    IsMuted = value
                });

    internal Task CompleteAsync()
    {
        lock (_sync)
        {
            _isAccepting = false;
            return _processor;
        }
    }

    private bool Queue(
        Func<
            AudioControlMutation,
            AudioControlMutation> merge)
    {
        lock (_sync)
        {
            if (!_isAccepting || _isDisposed)
                return false;

            _pending =
                merge(
                    _pending
                    ?? default);
            if (!_isRunning)
            {
                _isRunning = true;
                _processor = ProcessAsync();
            }

            return true;
        }
    }

    private async Task ProcessAsync()
    {
        while (true)
        {
            AudioControlMutation mutation;
            lock (_sync)
            {
                if (_pending is not
                    AudioControlMutation pending)
                {
                    _isRunning = false;
                    return;
                }

                mutation = pending;
                _pending = null;
            }

            AudioControlOutcome outcome =
                await Task.Run(
                        () => Execute(mutation))
                    .ConfigureAwait(false);
            NotifyCompleted(outcome);
        }
    }

    private AudioControlOutcome Execute(
        AudioControlMutation mutation)
    {
        bool? volumeSucceeded = null;
        if (mutation.Volume is float volume)
        {
            volumeSucceeded =
                AudioControlPolicy.Apply(
                        volume,
                        volume,
                        _setVolume)
                    .Succeeded;
        }

        bool? muteSucceeded = null;
        if (mutation.IsMuted is bool muted)
        {
            muteSucceeded =
                AudioControlPolicy.Apply(
                        muted,
                        muted,
                        _setMuted)
                    .Succeeded;
        }

        return new AudioControlOutcome(
            mutation,
            volumeSucceeded,
            muteSucceeded);
    }

    private void NotifyCompleted(
        AudioControlOutcome outcome)
    {
        Action<AudioControlOutcome>? handlers =
            Completed;
        if (handlers == null)
            return;

        foreach (Delegate handler in
                 handlers.GetInvocationList())
        {
            try
            {
                ((Action<AudioControlOutcome>)handler)(
                    outcome);
            }
            catch
            {
                // A detached UI observer cannot stop later audio writes.
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _isAccepting = false;
        }

        _processor.GetAwaiter()
            .GetResult();
    }
}
