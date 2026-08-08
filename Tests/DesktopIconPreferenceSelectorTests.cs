using System.Collections.Generic;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DesktopIconPreferenceSelectorTests
{
    [Fact]
    public void Select_PrefersManagedPathIgnoringCase()
    {
        var expected = new DesktopFilePreference
        {
            Id = 2,
            FilePath = "APP.LNK",
            ManagedPath = @"C:\Users\Me\Desktop\APP.LNK"
        };
        var preferences = new List<DesktopFilePreference>
        {
            new() { Id = 1, FilePath = "app.lnk", ManagedPath = @"C:\Users\Public\Desktop\app.lnk" },
            expected
        };

        DesktopFilePreference? selected =
            DesktopIconPreferenceSelector.Select(
                preferences,
                @"c:\users\me\desktop\app.lnk",
                "app.lnk",
                null);

        Assert.Same(expected, selected);
    }

    [Fact]
    public void Select_UsesStableIdentityAfterRename()
    {
        var expected = new DesktopFilePreference
        {
            Id = 3,
            FilePath = "old.lnk",
            ManagedPath = @"C:\Desktop\old.lnk",
            FileIdentity = "volume:file-42"
        };

        DesktopFilePreference? selected =
            DesktopIconPreferenceSelector.Select(
                new[] { expected },
                @"C:\Desktop\new.lnk",
                "new.lnk",
                "VOLUME:FILE-42");

        Assert.Same(expected, selected);
    }

    [Fact]
    public void Select_DoesNotGuessBetweenDuplicateDesktopNames()
    {
        var preferences = new[]
        {
            new DesktopFilePreference { Id = 1, FilePath = "app.lnk" },
            new DesktopFilePreference { Id = 2, FilePath = "APP.LNK" }
        };

        DesktopFilePreference? selected =
            DesktopIconPreferenceSelector.Select(
                preferences,
                @"C:\Desktop\app.lnk",
                "app.lnk",
                null);

        Assert.Null(selected);
    }
}
