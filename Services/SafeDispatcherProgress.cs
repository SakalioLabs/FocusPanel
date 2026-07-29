using System;
using System.Windows.Threading;

namespace FocusPanel.Services;

internal sealed class SafeDispatcherProgress<T>
    : IProgress<T>
{
    private readonly Dispatcher _dispatcher;
    private readonly Action<T> _handler;
    private readonly Action<Exception>? _onError;

    internal SafeDispatcherProgress(
        Dispatcher dispatcher,
        Action<T> handler,
        Action<Exception>? onError = null)
    {
        _dispatcher =
            dispatcher
            ?? throw new ArgumentNullException(
                nameof(dispatcher));
        _handler =
            handler
            ?? throw new ArgumentNullException(
                nameof(handler));
        _onError = onError;
    }

    public void Report(T value)
    {
        if (_dispatcher.HasShutdownStarted
            || _dispatcher.HasShutdownFinished)
        {
            return;
        }

        void ApplySafely()
        {
            try
            {
                _handler(value);
            }
            catch (Exception ex)
            {
                ReportError(ex);
            }
        }

        try
        {
            if (_dispatcher.CheckAccess())
            {
                ApplySafely();
                return;
            }

            _dispatcher.BeginInvoke(
                (Action)ApplySafely,
                DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    private void ReportError(Exception error)
    {
        try
        {
            _onError?.Invoke(error);
        }
        catch
        {
            // Diagnostics must never turn progress rendering
            // into an application-fatal exception.
        }
    }
}
