namespace FocusPanel.Models;

public sealed record AppUpdateInfo(
    string Version,
    string? ReleaseNotes,
    long DownloadSize);
