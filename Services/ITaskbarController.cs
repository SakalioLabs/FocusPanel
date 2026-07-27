using System;

namespace FocusPanel.Services;

public interface ITaskbarController : IDisposable
{
    event Action<string?>? ReplacementStopped;

    bool IsReplacementEnabled { get; }
    bool TryEnableReplacement(out string? error);
    void Restore();
}
