using System;
using System.Threading.Tasks;

namespace FocusPanel.Services;

internal readonly record struct
    BrightnessControlOutcome(
        long Revision,
        int Percent,
        bool Succeeded);

internal sealed class BrightnessControlCoordinator :
    IDisposable
{
    private readonly object _sync = new();
    private readonly Func<int, bool> _setBrightness;
    private (long Revision, int Percent)? _pending;
    private Task _processor = Task.CompletedTask;
    private bool _isRunning;
    private bool _isAccepting = true;
    private bool _isDisposed;

    internal BrightnessControlCoordinator(
        Func<int, bool> setBrightness)
    {
        _setBrightness =
            setBrightness
            ?? throw new ArgumentNullException(
                nameof(setBrightness));
    }

    internal event Action<BrightnessControlOutcome>?
        Completed;

    internal bool Queue(
        long revision,
        int percent)
    {
        lock (_sync)
        {
            if (!_isAccepting || _isDisposed)
                return false;

            _pending =
                (revision,
                    Math.Clamp(percent, 0, 100));
            if (!_isRunning)
            {
                _isRunning = true;
                _processor = ProcessAsync();
            }

            return true;
        }
    }

    internal Task CompleteAsync()
    {
        lock (_sync)
        {
            _isAccepting = false;
            return _processor;
        }
    }

    private async Task ProcessAsync()
    {
        while (true)
        {
            (long Revision, int Percent) mutation;
            lock (_sync)
            {
                if (_pending is not { } pending)
                {
                    _isRunning = false;
                    return;
                }

                mutation = pending;
                _pending = null;
            }

            bool succeeded =
                await Task.Run(
                        () =>
                        {
                            try
                            {
                                return _setBrightness(
                                    mutation.Percent);
                            }
                            catch
                            {
                                return false;
                            }
                        })
                    .ConfigureAwait(false);
            NotifyCompleted(
                new BrightnessControlOutcome(
                    mutation.Revision,
                    mutation.Percent,
                    succeeded));
        }
    }

    private void NotifyCompleted(
        BrightnessControlOutcome outcome)
    {
        Action<BrightnessControlOutcome>? handlers =
            Completed;
        if (handlers == null)
            return;

        foreach (Delegate handler in
                 handlers.GetInvocationList())
        {
            try
            {
                ((Action<BrightnessControlOutcome>)handler)(
                    outcome);
            }
            catch
            {
                // A detached observer cannot stop later display writes.
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
