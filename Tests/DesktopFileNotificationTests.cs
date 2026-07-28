using FocusPanel.Models;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DesktopFileNotificationTests
{
    [Fact]
    public void RefreshedMetadata_NotifiesDerivedLabels()
    {
        var file = new DesktopFile
        {
            Name = "draft.txt",
            Extension = ".txt",
            Size = 1,
            FileType = "Document"
        };
        var properties =
            new System.Collections.Generic.HashSet<string>();
        file.PropertyChanged +=
            (_, args) =>
            {
                if (args.PropertyName != null)
                    properties.Add(args.PropertyName);
            };

        file.Name = "photo.png";
        file.Extension = ".png";
        file.Size = 2048;
        file.CreatedAt = System.DateTime.Now;
        file.FileType = "Image";

        Assert.Contains(
            nameof(DesktopFile.DisplayName),
            properties);
        Assert.Contains(
            nameof(DesktopFile.SizeDisplay),
            properties);
        Assert.Contains(
            nameof(DesktopFile.DateGroup),
            properties);
        Assert.Contains(
            nameof(DesktopFile.Category),
            properties);
    }
}
