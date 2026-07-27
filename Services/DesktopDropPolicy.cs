using System;
using System.IO;

namespace FocusPanel.Services;

public static class DesktopDropPolicy
{
    public static DesktopDropLocation Classify(
        string path,
        string userDesktopPath,
        string? commonDesktopPath = null)
    {
        if (IsDirectChild(path, userDesktopPath))
            return DesktopDropLocation.UserDesktop;
        if (IsDirectChild(path, commonDesktopPath))
            return DesktopDropLocation.CommonDesktop;
        return DesktopDropLocation.OutsideDesktop;
    }

    public static bool IsDesktopRootItem(string path, string desktopPath)
        => Classify(path, desktopPath) == DesktopDropLocation.UserDesktop;

    private static bool IsDirectChild(string path, string? desktopPath)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(desktopPath))
            return false;

        try
        {
            string normalizedDesktop = Path.GetFullPath(desktopPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string? parent = Path.GetDirectoryName(Path.GetFullPath(path))?
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(parent, normalizedDesktop, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

public enum DesktopDropLocation
{
    OutsideDesktop,
    UserDesktop,
    CommonDesktop
}
