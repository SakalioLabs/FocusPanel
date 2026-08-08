using System;
using System.IO;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PanelIconStoreTests
{
    [Fact]
    public async Task Import_CopiesIconOutsideTheDesktopSource()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string source = Path.Combine(root, "desktop", "custom.ico");
            string store = Path.Combine(root, "panel-icons");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            await File.WriteAllBytesAsync(source, new byte[] { 0, 0, 1, 0, 1, 0 });

            var icons = new PanelIconStore(store);
            string imported = await icons.ImportAsync(source);

            Assert.True(File.Exists(imported));
            Assert.Equal(
                Path.GetFullPath(store),
                Path.GetDirectoryName(imported));
            File.Delete(source);
            Assert.True(File.Exists(imported));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Import_DeduplicatesEqualIconContent()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string first = Path.Combine(root, "first.ico");
            string second = Path.Combine(root, "second.ico");
            byte[] content = { 0, 0, 1, 0, 1, 0, 9, 8, 7 };
            await File.WriteAllBytesAsync(first, content);
            await File.WriteAllBytesAsync(second, content);
            var icons = new PanelIconStore(Path.Combine(root, "icons"));

            string firstImport = await icons.ImportAsync(first);
            string secondImport = await icons.ImportAsync(second);

            Assert.Equal(firstImport, secondImport);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "icons")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.PanelIconStoreTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
