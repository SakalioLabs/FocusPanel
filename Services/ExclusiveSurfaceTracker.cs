using System;

namespace FocusPanel.Services;

internal sealed class ExclusiveSurfaceTracker<T>
    where T : class
{
    public T? Active { get; private set; }

    public T? Activate(T surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (ReferenceEquals(Active, surface))
            return null;

        T? previous = Active;
        Active = surface;
        return previous;
    }

    public bool Deactivate(T surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (!ReferenceEquals(Active, surface))
            return false;

        Active = null;
        return true;
    }

    public T? Clear()
    {
        T? previous = Active;
        Active = null;
        return previous;
    }
}
