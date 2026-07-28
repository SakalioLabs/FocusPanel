using System;

namespace FocusPanel.Services;

public interface IFocusDialogInteractionHost
{
    void BeginTransientInteraction();
    void EndTransientInteraction();
}

internal sealed class FocusDialogInteractionLease
    : IDisposable
{
    private IFocusDialogInteractionHost? _host;

    private FocusDialogInteractionLease(
        IFocusDialogInteractionHost? host)
    {
        _host = host;
        _host?.BeginTransientInteraction();
    }

    internal static FocusDialogInteractionLease Enter(
        IFocusDialogInteractionHost? host) =>
        new(host);

    public void Dispose()
    {
        IFocusDialogInteractionHost? host =
            _host;
        _host = null;
        host?.EndTransientInteraction();
    }
}
