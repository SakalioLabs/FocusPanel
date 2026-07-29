using System;
using System.IO;
using System.Threading.Tasks;

namespace FocusPanel.Services;

internal interface IDesktopVisibilityIo
{
    Task<FileAttributes> ReadAttributesAsync(
        string path);

    Task ApplyAttributesAsync(
        string path,
        FileAttributes attributes,
        bool requiresElevation);
}

internal sealed class DesktopVisibilityIo
    : IDesktopVisibilityIo
{
    private readonly IDesktopItemVisibilityService
        _visibility;
    private readonly Action<
        string,
        FileAttributes> _setElevatedAttributes;

    internal DesktopVisibilityIo(
        IDesktopItemVisibilityService visibility)
        : this(
            visibility,
            DesktopVisibilityElevatedHelper
                .SetAttributes)
    {
    }

    internal DesktopVisibilityIo(
        IDesktopItemVisibilityService visibility,
        Action<
            string,
            FileAttributes> setElevatedAttributes)
    {
        _visibility =
            visibility
            ?? throw new ArgumentNullException(
                nameof(visibility));
        _setElevatedAttributes =
            setElevatedAttributes
            ?? throw new ArgumentNullException(
                nameof(setElevatedAttributes));
    }

    public Task<FileAttributes>
        ReadAttributesAsync(
            string path)
    {
        string requiredPath =
            RequirePath(path);
        return Task.Run(
            () =>
            {
                if (!_visibility.Exists(
                        requiredPath))
                {
                    throw new FileNotFoundException(
                        "找不到要收纳的桌面项目。",
                        requiredPath);
                }

                return _visibility.GetAttributes(
                    requiredPath);
            });
    }

    public Task ApplyAttributesAsync(
        string path,
        FileAttributes attributes,
        bool requiresElevation)
    {
        string requiredPath =
            RequirePath(path);
        return Task.Run(
            () =>
            {
                if (requiresElevation)
                {
                    _setElevatedAttributes(
                        requiredPath,
                        attributes);
                    return;
                }

                _visibility.SetAttributes(
                    requiredPath,
                    attributes);
                _visibility.NotifyAttributesChanged(
                    requiredPath);
            });
    }

    private static string RequirePath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "Desktop item path is required.",
                nameof(path));
        }

        return path;
    }
}
