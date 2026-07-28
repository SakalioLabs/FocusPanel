using System;

namespace FocusPanel.Services;

internal readonly record struct AudioControlResult<T>(
    bool Succeeded,
    T EffectiveValue);

internal static class AudioControlPolicy
{
    internal static AudioControlResult<T> Apply<T>(
        T requestedValue,
        T confirmedValue,
        Func<T, bool> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        bool succeeded =
            SystemActionExecution.Try(
                () => write(requestedValue));
        return new AudioControlResult<T>(
            succeeded,
            succeeded ? requestedValue : confirmedValue);
    }
}
