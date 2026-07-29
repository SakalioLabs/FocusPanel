using CommunityToolkit.Mvvm.ComponentModel;

namespace FocusPanel.ViewModels;

public partial class WorkspaceLoadingViewModel :
    ObservableObject
{
    internal WorkspaceLoadingViewModel(
        string message)
    {
        Message = message;
    }

    [ObservableProperty]
    private string message;

    [ObservableProperty]
    private bool hasError;

    internal void ShowError(string message)
    {
        Message = message;
        HasError = true;
    }
}
