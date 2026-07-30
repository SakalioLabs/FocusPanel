using System;
using System.Threading.Tasks;
using System.Windows;

namespace FocusPanel.Services;

internal sealed class ClipboardTextService
{
    private const int DefaultMaximumAttempts =
        3;
    private static readonly TimeSpan
        RetryDelay =
            TimeSpan.FromMilliseconds(35);

    private readonly Action<string> _setText;
    private readonly Func<
        TimeSpan,
        Task> _delay;
    private readonly int _maximumAttempts;

    internal ClipboardTextService()
        : this(
            text =>
                Clipboard.SetDataObject(
                    text,
                    true))
    {
    }

    internal ClipboardTextService(
        Action<string> setText,
        Func<TimeSpan, Task>? delay = null,
        int maximumAttempts =
            DefaultMaximumAttempts)
    {
        _setText =
            setText
            ?? throw new
                ArgumentNullException(
                    nameof(setText));
        _delay =
            delay
            ?? Task.Delay;
        _maximumAttempts =
            Math.Max(
                1,
                maximumAttempts);
    }

    internal async Task<bool>
        TrySetTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        for (int attempt = 1;
             attempt <= _maximumAttempts;
             attempt++)
        {
            try
            {
                _setText(text);
                return true;
            }
            catch
            {
                if (attempt
                    == _maximumAttempts)
                {
                    return false;
                }
            }

            await _delay(RetryDelay);
        }

        return false;
    }
}
