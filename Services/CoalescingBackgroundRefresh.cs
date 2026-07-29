using System;
using System.Threading;
using System.Threading.Tasks;

namespace FocusPanel.Services;

public sealed class CoalescingBackgroundRefresh<T> : IDisposable
{
    private readonly object _sync = new();
    private readonly Func<T> _capture;
    private readonly Func<T, CancellationToken, Task> _applyAsync;
    private readonly Action<Exception>? _reportFailure;
    private readonly CancellationTokenSource _disposeCancellation = new();
    private Task _currentRun = Task.CompletedTask;
    private bool _isRunning;
    private bool _isPending;
    private bool _isDisposed;

    public CoalescingBackgroundRefresh(
        Func<T> capture,
        Func<T, CancellationToken, Task> applyAsync,
        Action<Exception>? reportFailure = null)
    {
        _capture = capture
            ?? throw new ArgumentNullException(nameof(capture));
        _applyAsync = applyAsync
            ?? throw new ArgumentNullException(nameof(applyAsync));
        _reportFailure = reportFailure;
    }

    public void Request()
    {
        lock (_sync)
        {
            if (_isDisposed)
                return;

            if (_isRunning)
            {
                _isPending = true;
                return;
            }

            _isRunning = true;
            _currentRun = RunAsync(
                _disposeCancellation.Token);
        }
    }

    public Task WhenIdleAsync()
    {
        lock (_sync)
            return _currentRun;
    }

    private async Task RunAsync(
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                T snapshot = await Task.Run(
                        _capture,
                        cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                await _applyAsync(
                        snapshot,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                Finish();
                return;
            }
            catch (Exception ex)
            {
                try
                {
                    _reportFailure?.Invoke(ex);
                }
                catch
                {
                    // Diagnostics must not terminate future refreshes.
                }
            }

            lock (_sync)
            {
                if (_isDisposed)
                {
                    _isPending = false;
                    _isRunning = false;
                    return;
                }

                if (_isPending)
                {
                    _isPending = false;
                    continue;
                }

                _isRunning = false;
                return;
            }
        }
    }

    private void Finish()
    {
        lock (_sync)
        {
            _isPending = false;
            _isRunning = false;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _isPending = false;
        }

        _disposeCancellation.Cancel();
    }
}
