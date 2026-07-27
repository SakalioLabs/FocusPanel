using System;
using System.IO;
using System.Linq;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AppCatalogSafetyTests
{
    [Fact]
    public void SafeEnumerateShortcuts_FindsNestedLinks()
    {
        string root = Path.Combine(Path.GetTempPath(), "FocusPanel.Tests", Guid.NewGuid().ToString("N"));
        string nested = Path.Combine(root, "Nested");
        Directory.CreateDirectory(nested);
        string shortcut = Path.Combine(nested, "Demo.lnk");
        File.WriteAllText(shortcut, string.Empty);

        try
        {
            string[] results = AppCatalogService.SafeEnumerateShortcuts(root).ToArray();
            Assert.Single(results);
            Assert.Equal(shortcut, results[0]);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SafeEnumerateShortcuts_MissingRootReturnsEmpty()
    {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Assert.Empty(AppCatalogService.SafeEnumerateShortcuts(missing));
    }
}
