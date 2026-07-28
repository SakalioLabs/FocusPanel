using System;
using System.IO;

namespace FocusPanel.Services;

public enum AppUpdateSourceKind
{
    GitHub,
    Lan
}

public sealed record AppUpdateSourceConfiguration(
    AppUpdateSourceKind Kind,
    string Location);

public static class AppUpdateSourcePolicy
{
    public static bool TryNormalize(
        AppUpdateSourceConfiguration configuration,
        out AppUpdateSourceConfiguration normalized,
        out string? error)
    {
        if (configuration.Kind == AppUpdateSourceKind.GitHub)
        {
            normalized = new AppUpdateSourceConfiguration(
                AppUpdateSourceKind.GitHub,
                VelopackUpdateService.RepositoryUrl);
            error = null;
            return true;
        }

        string location = configuration.Location?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(location))
        {
            normalized = configuration;
            error = "请输入局域网 HTTP 地址或 Windows 共享目录。";
            return false;
        }

        if (Uri.TryCreate(location, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            string normalizedUri = uri.AbsoluteUri.TrimEnd('/') + "/";
            normalized = new AppUpdateSourceConfiguration(AppUpdateSourceKind.Lan, normalizedUri);
            error = null;
            return true;
        }

        if (location.StartsWith(@"\\", StringComparison.Ordinal)
            || Path.IsPathFullyQualified(location))
        {
            try
            {
                normalized = new AppUpdateSourceConfiguration(
                    AppUpdateSourceKind.Lan,
                    Path.GetFullPath(location).TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar));
                error = null;
                return true;
            }
            catch (Exception ex) when (
                ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                normalized = configuration;
                error = $"更新目录格式无效：{ex.Message}";
                return false;
            }
        }

        normalized = configuration;
        error = "更新源必须是 http(s) 地址、UNC 共享目录或绝对文件夹路径。";
        return false;
    }
}
