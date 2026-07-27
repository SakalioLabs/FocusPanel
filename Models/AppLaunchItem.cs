using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FocusPanel.Models;

public partial class AppLaunchItem : ObservableObject
{
    public string DisplayName { get; init; } = string.Empty;
    public AppLaunchKind LaunchKind { get; init; }
    public string LaunchTarget { get; init; } = string.Empty;
    public string? Arguments { get; init; }
    public string? IconKey { get; init; }
    public string IdentityKey { get; set; } = string.Empty;
    [ObservableProperty]
    private ImageSource? icon;

    [ObservableProperty]
    private bool isPinned;
}
