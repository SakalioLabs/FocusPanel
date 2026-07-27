using System;

namespace FocusPanel.Services;

public interface ITaskbarController : IDisposable
{
    bool IsReplacementEnabled { get; }
    bool TryEnableReplacement(out string? error);
    void Restore();
}
