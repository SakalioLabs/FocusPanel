using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DesktopFileCollectionSynchronizerTests
{
    [Fact]
    public void MetadataRefresh_PreservesInstanceAndSelection()
    {
        DesktopFile current = File(
            "draft.txt",
            @"C:\Desktop\draft.txt");
        current.IsSelected = true;
        var all =
            new ObservableCollection<DesktopFile>
            {
                current
            };
        var visible =
            new ObservableCollection<DesktopFile>
            {
                current
            };
        DesktopFile refreshed = File(
            "draft.txt",
            @"C:\Desktop\draft.txt");
        refreshed.Size = 4096;
        refreshed.FileType = "Document";

        DesktopFileCollectionSynchronizer.Apply(
            all,
            visible,
            new[]
            {
                new DesktopItemRefresh(
                    current.FullPath,
                    refreshed,
                    false)
            });

        Assert.Same(current, Assert.Single(all));
        Assert.Same(
            current,
            Assert.Single(visible));
        Assert.True(current.IsSelected);
        Assert.Equal(4096, current.Size);
        Assert.Equal(
            "Document",
            current.FileType);
    }

    [Fact]
    public void Rename_UpdatesExistingObjectWithoutReplacingIt()
    {
        DesktopFile current = File(
            "old.txt",
            @"C:\Desktop\old.txt");
        current.IsSelected = true;
        var all =
            new ObservableCollection<DesktopFile>
            {
                current
            };
        var visible =
            new ObservableCollection<DesktopFile>
            {
                current
            };

        DesktopFileCollectionSynchronizer.Apply(
            all,
            visible,
            new[]
            {
                new DesktopItemRefresh(
                    current.FullPath,
                    File(
                        "new.txt",
                        @"C:\Desktop\new.txt"),
                    false)
            });

        Assert.Same(current, Assert.Single(all));
        Assert.Same(
            current,
            Assert.Single(visible));
        Assert.Equal("new.txt", current.Name);
        Assert.Equal(
            @"C:\Desktop\new.txt",
            current.FullPath);
        Assert.True(current.IsSelected);
    }

    [Fact]
    public void WatcherRenameBatch_DoesNotRemovePretrackedObject()
    {
        DesktopFile current = File(
            "old.txt",
            @"C:\Desktop\old.txt");
        current.IsSelected = true;
        var all =
            new ObservableCollection<DesktopFile>
            {
                current
            };
        var visible =
            new ObservableCollection<DesktopFile>
            {
                current
            };
        current.Name = "new.txt";
        current.FullPath =
            @"C:\Desktop\new.txt";

        DesktopFileCollectionSynchronizer.Apply(
            all,
            visible,
            new[]
            {
                new DesktopItemRefresh(
                    @"C:\Desktop\old.txt",
                    null,
                    true),
                new DesktopItemRefresh(
                    @"C:\Desktop\new.txt",
                    File(
                        "new.txt",
                        @"C:\Desktop\new.txt"),
                    false)
            });

        Assert.Same(current, Assert.Single(all));
        Assert.Same(
            current,
            Assert.Single(visible));
        Assert.True(current.IsSelected);
    }

    [Fact]
    public void CollectedItem_RemainsInAllButLeavesVisibleFiles()
    {
        DesktopFile current = File(
            "work.txt",
            @"C:\Desktop\work.txt");
        var all =
            new ObservableCollection<DesktopFile>
            {
                current
            };
        var visible =
            new ObservableCollection<DesktopFile>
            {
                current
            };
        DesktopFile collected = File(
            current.Name,
            current.FullPath);
        collected.IsHidden = true;

        DesktopFileCollectionSynchronizer.Apply(
            all,
            visible,
            new[]
            {
                new DesktopItemRefresh(
                    current.FullPath,
                    collected,
                    false)
            });

        Assert.Same(current, Assert.Single(all));
        Assert.True(current.IsHidden);
        Assert.Empty(visible);
    }

    [Fact]
    public void DeletedItem_IsRemovedFromBothCollections()
    {
        DesktopFile current = File(
            "gone.txt",
            @"C:\Desktop\gone.txt");
        var all =
            new ObservableCollection<DesktopFile>
            {
                current
            };
        var visible =
            new ObservableCollection<DesktopFile>
            {
                current
            };

        DesktopFileCollectionSynchronizer.Apply(
            all,
            visible,
            new[]
            {
                new DesktopItemRefresh(
                    current.FullPath,
                    null,
                    true)
            });

        Assert.Empty(all);
        Assert.Empty(visible);
    }

    [Fact]
    public void NewItems_AreSortedWithMovesAndWithoutReset()
    {
        DesktopFile zeta = File(
            "zeta.txt",
            @"C:\Desktop\zeta.txt");
        var all =
            new ObservableCollection<DesktopFile>
            {
                zeta
            };
        var visible =
            new ObservableCollection<DesktopFile>
            {
                zeta
            };
        var actions =
            new List<NotifyCollectionChangedAction>();
        all.CollectionChanged +=
            (_, args) => actions.Add(args.Action);

        DesktopFileCollectionSynchronizer.Apply(
            all,
            visible,
            new[]
            {
                new DesktopItemRefresh(
                    @"C:\Desktop\folder",
                    new DesktopFile
                    {
                        Name = "folder",
                        FullPath =
                            @"C:\Desktop\folder",
                        FileType = "Folder"
                    },
                    false),
                new DesktopItemRefresh(
                    @"C:\Desktop\alpha.txt",
                    File(
                        "alpha.txt",
                        @"C:\Desktop\alpha.txt"),
                    false)
            });

        Assert.Equal(
            new[]
            {
                "folder",
                "alpha.txt",
                "zeta.txt"
            },
            System.Linq.Enumerable.Select(
                all,
                item => item.Name));
        Assert.DoesNotContain(
            NotifyCollectionChangedAction.Reset,
            actions);
    }

    private static DesktopFile File(
        string name,
        string path)
        => new()
        {
            Name = name,
            FullPath = path,
            Extension =
                System.IO.Path.GetExtension(name),
            FileType = "File"
        };
}
