namespace FocusPanel.Services;

internal sealed class TaskbarGuardConfirmation
{
    private readonly int _requiredObservations;
    private readonly object _sync = new();
    private TaskbarReplacementStopReason? _pendingReason;
    private int _observationCount;

    internal TaskbarGuardConfirmation(
        int requiredObservations = 2)
    {
        if (requiredObservations < 1)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(requiredObservations));
        }

        _requiredObservations =
            requiredObservations;
    }

    internal bool ObserveInvalid(
        TaskbarReplacementStopReason reason)
    {
        lock (_sync)
        {
            if (_pendingReason != reason)
            {
                _pendingReason = reason;
                _observationCount = 1;
            }
            else
            {
                _observationCount++;
            }

            return _observationCount
                >= _requiredObservations;
        }
    }

    internal void ObserveValid()
    {
        lock (_sync)
        {
            _pendingReason = null;
            _observationCount = 0;
        }
    }
}
