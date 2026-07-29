using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using FocusPanel.ViewModels;

namespace FocusPanel.Services;

internal interface IFileOrganizerViewModelFactory
{
    Task<FileOrganizerViewModel> CreateAsync(
        Dispatcher uiDispatcher);
}

internal sealed class FileOrganizerViewModelFactory :
    IFileOrganizerViewModelFactory
{
    private readonly Func<
        Dispatcher,
        FileOrganizerViewModel> _create;

    internal FileOrganizerViewModelFactory()
        : this(CreateCore)
    {
    }

    internal FileOrganizerViewModelFactory(
        Func<Dispatcher, FileOrganizerViewModel>
            create)
    {
        _create =
            create
            ?? throw new ArgumentNullException(
                nameof(create));
    }

    public Task<FileOrganizerViewModel> CreateAsync(
        Dispatcher uiDispatcher)
    {
        if (uiDispatcher == null)
            throw new ArgumentNullException(
                nameof(uiDispatcher));

        return Task.Run(
            () => _create(uiDispatcher));
    }

    private static FileOrganizerViewModel CreateCore(
        Dispatcher uiDispatcher)
    {
        FileOrganizerService? fileService = null;
        try
        {
            var settingsService =
                new SettingsService();
            fileService =
                new FileOrganizerService();
            var viewModel =
                new FileOrganizerViewModel(
                    settingsService,
                    fileService,
                    new OrganizerLayoutRepository(),
                    uiDispatcher);
            fileService = null;
            return viewModel;
        }
        finally
        {
            fileService?.Dispose();
        }
    }
}

internal static class WorkspaceLoadApplyPolicy
{
    internal static bool CanApply(
        long requestedRevision,
        long currentRevision,
        bool isDisposed) =>
        !isDisposed
        && requestedRevision == currentRevision;
}
