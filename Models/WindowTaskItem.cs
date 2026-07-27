using System;
using System.Collections.Generic;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FocusPanel.Models;

public partial class WindowTaskItem : ObservableObject
{
    public string AppKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? ExecutablePath { get; init; }
    public ImageSource? Icon { get; init; }
    public IReadOnlyList<WindowReference> Windows { get; init; } = Array.Empty<WindowReference>();
    public int WindowCount => Windows.Count;
    public IntPtr PrimaryHandle => Windows.Count == 0 ? IntPtr.Zero : Windows[0].Handle;

    [ObservableProperty]
    private bool isActive;
}

public sealed record WindowReference(IntPtr Handle, string Title);
