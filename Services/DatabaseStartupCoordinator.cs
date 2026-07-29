using System;
using System.Threading.Tasks;

namespace FocusPanel.Services;

internal enum DatabaseStartupNoticeKind
{
    Information,
    Warning,
    Error
}

internal sealed record DatabaseStartupNotice(
    string Title,
    string Message,
    DatabaseStartupNoticeKind Kind);

internal sealed record DatabaseStartupCompletion(
    bool Succeeded,
    DatabaseStartupNotice? Notice);

internal sealed class DatabaseStartupCoordinator
{
    private readonly Func<
        bool,
        DatabaseStartupCompletion> _prepare;

    internal DatabaseStartupCoordinator(
        Func<
            bool,
            DatabaseStartupCompletion> prepare)
    {
        _prepare =
            prepare
            ?? throw new ArgumentNullException(
                nameof(prepare));
    }

    internal Task<DatabaseStartupCompletion>
        PrepareAsync(bool restoreRequested) =>
        Task.Run(
            () => _prepare(restoreRequested));
}
