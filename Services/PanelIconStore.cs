using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FocusPanel.Services;

internal interface IPanelIconStore
{
    Task<string> ImportAsync(
        string sourcePath,
        CancellationToken cancellationToken = default);
}

internal sealed class PanelIconStore : IPanelIconStore
{
    private readonly string _rootPath;

    internal PanelIconStore()
        : this(
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "FocusPanel",
                "Icons"))
    {
    }

    internal PanelIconStore(string rootPath)
    {
        _rootPath = string.IsNullOrWhiteSpace(rootPath)
            ? throw new ArgumentException(
                "图标存储目录不能为空。",
                nameof(rootPath))
            : Path.GetFullPath(rootPath);
    }

    public async Task<string> ImportAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        string normalized = Path.GetFullPath(
            sourcePath
                ?? throw new ArgumentNullException(
                    nameof(sourcePath)));
        if (!File.Exists(normalized))
        {
            throw new FileNotFoundException(
                "找不到选择的图标文件。",
                normalized);
        }

        await using FileStream source = new(
            normalized,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        byte[] hash = await SHA256.HashDataAsync(
            source,
            cancellationToken).ConfigureAwait(false);
        string fileName =
            Convert.ToHexString(hash).ToLowerInvariant()
            + ".ico";
        Directory.CreateDirectory(_rootPath);
        string destination = Path.Combine(
            _rootPath,
            fileName);
        if (File.Exists(destination))
            return destination;

        source.Position = 0;
        string temporary = destination
            + "."
            + Guid.NewGuid().ToString("N")
            + ".tmp";
        try
        {
            await using (FileStream target = new(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                await source.CopyToAsync(
                    target,
                    cancellationToken).ConfigureAwait(false);
                await target.FlushAsync(
                    cancellationToken).ConfigureAwait(false);
            }

            try
            {
                File.Move(
                    temporary,
                    destination);
            }
            catch (IOException) when (File.Exists(destination))
            {
                File.Delete(temporary);
            }
            return destination;
        }
        catch
        {
            try
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            catch
            {
                // A failed cleanup must not hide the original import error.
            }
            throw;
        }
    }
}
