using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FocusPanel.Services;

internal readonly record struct
    ApplicationAudioControlMutation(
        string SessionId,
        long Revision,
        float? Volume,
        bool? IsMuted);

internal readonly record struct
    ApplicationAudioControlOutcome(
        ApplicationAudioControlMutation Mutation,
        bool? VolumeSucceeded,
        bool? MuteSucceeded);

internal sealed class
    ApplicationAudioControlCoordinator :
    IDisposable
{
    private readonly object _sync = new();
    private readonly Func<
        string,
        float,
        bool> _setVolume;
    private readonly Func<
        string,
        bool,
        bool> _setMuted;
    private readonly Dictionary<
        string,
        ApplicationAudioControlMutation>
        _pending =
            new(StringComparer.Ordinal);
    private Task _processor = Task.CompletedTask;
    private bool _isRunning;
    private bool _isAccepting = true;
    private bool _isDisposed;

    internal ApplicationAudioControlCoordinator(
        Func<string, float, bool> setVolume,
        Func<string, bool, bool> setMuted)
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

    internal event Action<
        ApplicationAudioControlOutcome>?
        Completed;

    internal bool QueueVolume(
        string sessionId,
        long revision,
        float value) =>
        Queue(
            sessionId,
            current =>
                current with
                {
                    Revision = revision,
                    Volume =
                        Math.Clamp(
                            value,
                            0f,
                            1f)
                });

    internal bool QueueMuted(
        string sessionId,
        long revision,
        bool value) =>
        Queue(
            sessionId,
            current =>
                current with
                {
                    Revision = revision,
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
        string sessionId,
        Func<
            ApplicationAudioControlMutation,
            ApplicationAudioControlMutation>
            merge)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return false;

        lock (_sync)
        {
            if (!_isAccepting || _isDisposed)
                return false;

            _pending.TryGetValue(
                sessionId,
                out ApplicationAudioControlMutation
                    current);
            if (string.IsNullOrEmpty(
                    current.SessionId))
            {
                current =
                    new ApplicationAudioControlMutation(
                        sessionId,
                        0,
                        null,
                        null);
            }

            _pending[sessionId] =
                merge(current);
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
            ApplicationAudioControlMutation
                mutation;
            lock (_sync)
            {
                if (_pending.Count == 0)
                {
                    _isRunning = false;
                    return;
                }

                KeyValuePair<
                    string,
                    ApplicationAudioControlMutation>
                    next =
                        _pending.First();
                mutation = next.Value;
                _pending.Remove(next.Key);
            }

            ApplicationAudioControlOutcome outcome =
                await Task.Run(
                        () => Execute(mutation))
                    .ConfigureAwait(false);
            NotifyCompleted(outcome);
        }
    }

    private ApplicationAudioControlOutcome Execute(
        ApplicationAudioControlMutation mutation)
    {
        bool? volumeSucceeded = null;
        if (mutation.Volume is float volume)
        {
            try
            {
                volumeSucceeded =
                    _setVolume(
                        mutation.SessionId,
                        volume);
            }
            catch
            {
                volumeSucceeded = false;
            }
        }

        bool? muteSucceeded = null;
        if (mutation.IsMuted is bool muted)
        {
            try
            {
                muteSucceeded =
                    _setMuted(
                        mutation.SessionId,
                        muted);
            }
            catch
            {
                muteSucceeded = false;
            }
        }

        return new ApplicationAudioControlOutcome(
            mutation,
            volumeSucceeded,
            muteSucceeded);
    }

    private void NotifyCompleted(
        ApplicationAudioControlOutcome outcome)
    {
        Action<ApplicationAudioControlOutcome>?
            handlers = Completed;
        if (handlers == null)
            return;

        foreach (Delegate handler in
                 handlers.GetInvocationList())
        {
            try
            {
                ((Action<
                    ApplicationAudioControlOutcome>)
                    handler)(outcome);
            }
            catch
            {
                // A detached UI observer cannot stop later writes.
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
