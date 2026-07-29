using System;
using System.IO;
using System.Threading.Tasks;

namespace FocusPanel.Services;

internal readonly record struct TaskImageImportResult(
    bool Succeeded,
    string SavedPath,
    string Error);

internal sealed class TaskImageImportCoordinator
{
    private readonly Func<string, string, string>
        _import;

    internal TaskImageImportCoordinator(
        Func<string, string, string>? import = null)
    {
        _import =
            import
            ?? ImportImage;
    }

    internal Task<TaskImageImportResult> ImportAsync(
        string sourcePath,
        string destinationDirectory)
    {
        string detachedSource =
            sourcePath?.Trim()
            ?? string.Empty;
        string detachedDestination =
            destinationDirectory?.Trim()
            ?? string.Empty;
        if (detachedSource.Length == 0)
        {
            return Task.FromResult(
                new TaskImageImportResult(
                    false,
                    string.Empty,
                    "没有可导入的图片路径。"));
        }
        if (detachedDestination.Length == 0)
        {
            return Task.FromResult(
                new TaskImageImportResult(
                    false,
                    string.Empty,
                    "请先在任务设置中选择图片保存位置。"));
        }

        return Task.Run(
            () =>
            {
                try
                {
                    string savedPath =
                        _import(
                            detachedSource,
                            detachedDestination);
                    if (string.IsNullOrWhiteSpace(
                            savedPath))
                    {
                        return new TaskImageImportResult(
                            false,
                            string.Empty,
                            "Windows 没有返回有效的图片保存路径。");
                    }

                    return new TaskImageImportResult(
                        true,
                        savedPath,
                        string.Empty);
                }
                catch (Exception ex)
                {
                    return new TaskImageImportResult(
                        false,
                        string.Empty,
                        ex.Message);
                }
            });
    }

    private static string ImportImage(
        string sourcePath,
        string destinationDirectory)
    {
        Directory.CreateDirectory(
            destinationDirectory);
        string fileName =
            Path.GetFileName(sourcePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new IOException(
                "无法识别所选图片的文件名。");
        }

        string destinationPath =
            Path.Combine(
                destinationDirectory,
                $"{Guid.NewGuid():N}_{fileName}");
        File.Copy(
            sourcePath,
            destinationPath);
        return destinationPath;
    }
}
