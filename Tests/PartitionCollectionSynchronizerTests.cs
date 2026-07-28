using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using FocusPanel.Models;
using FocusPanel.Services;
using FocusPanel.ViewModels;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PartitionCollectionSynchronizerTests
{
    [Fact]
    public void UnchangedSnapshot_PreservesObjectsAndRaisesNoChanges()
    {
        DesktopFile file = File("one.txt");
        PartitionViewModel current =
            Partition("工作", 0, file);
        current.IsExpanded = false;
        var all =
            new ObservableCollection<PartitionViewModel>
            {
                current
            };
        var left =
            new ObservableCollection<PartitionViewModel>
            {
                current
            };
        var right =
            new ObservableCollection<PartitionViewModel>();
        int allChanges = 0;
        int leftChanges = 0;
        int fileChanges = 0;
        all.CollectionChanged +=
            (_, _) => allChanges++;
        left.CollectionChanged +=
            (_, _) => leftChanges++;
        current.Files.CollectionChanged +=
            (_, _) => fileChanges++;

        PartitionCollectionSynchronizer.Synchronize(
            all,
            left,
            right,
            new[]
            {
                Partition("工作", 0, file)
            });

        Assert.Same(current, all[0]);
        Assert.Same(current, left[0]);
        Assert.Same(file, current.Files[0]);
        Assert.False(current.IsExpanded);
        Assert.Equal(0, allChanges);
        Assert.Equal(0, leftChanges);
        Assert.Equal(0, fileChanges);
    }

    [Fact]
    public void FileMove_ReusesPartitionsAndSelectedFile()
    {
        DesktopFile selected = File("move.txt");
        selected.IsSelected = true;
        PartitionViewModel source =
            Partition("来源", 0, selected);
        PartitionViewModel target =
            Partition("目标", 1);
        var all =
            new ObservableCollection<PartitionViewModel>
            {
                source,
                target
            };
        var left =
            new ObservableCollection<PartitionViewModel>
            {
                source
            };
        var right =
            new ObservableCollection<PartitionViewModel>
            {
                target
            };

        PartitionCollectionSynchronizer.Synchronize(
            all,
            left,
            right,
            new[]
            {
                Partition("来源", 0),
                Partition("目标", 1, selected)
            });

        Assert.Same(source, all[0]);
        Assert.Same(target, all[1]);
        Assert.Empty(source.Files);
        Assert.Same(
            selected,
            Assert.Single(target.Files));
        Assert.True(selected.IsSelected);
    }

    [Fact]
    public void Reorder_UsesMoveInsteadOfReset()
    {
        PartitionViewModel first =
            Partition("一", 0);
        PartitionViewModel second =
            Partition("二", 0);
        var all =
            new ObservableCollection<PartitionViewModel>
            {
                first,
                second
            };
        var left =
            new ObservableCollection<PartitionViewModel>
            {
                first,
                second
            };
        var right =
            new ObservableCollection<PartitionViewModel>();
        var actions =
            new List<NotifyCollectionChangedAction>();
        all.CollectionChanged +=
            (_, args) => actions.Add(args.Action);

        PartitionCollectionSynchronizer.Synchronize(
            all,
            left,
            right,
            new[]
            {
                Partition("二", 0),
                Partition("一", 0)
            });

        Assert.Same(second, all[0]);
        Assert.Same(first, all[1]);
        Assert.Equal(
            new[]
            {
                NotifyCollectionChangedAction.Move
            },
            actions);
    }

    [Fact]
    public void ColumnChange_MovesSamePartitionBetweenColumns()
    {
        PartitionViewModel current =
            Partition("收纳", 0);
        var all =
            new ObservableCollection<PartitionViewModel>
            {
                current
            };
        var left =
            new ObservableCollection<PartitionViewModel>
            {
                current
            };
        var right =
            new ObservableCollection<PartitionViewModel>();

        PartitionCollectionSynchronizer.Synchronize(
            all,
            left,
            right,
            new[]
            {
                Partition("收纳", 1)
            });

        Assert.Same(
            current,
            Assert.Single(all));
        Assert.Empty(left);
        Assert.Same(
            current,
            Assert.Single(right));
        Assert.Equal(1, current.ColumnIndex);
    }

    [Fact]
    public void ReplacedFileWithSamePath_PreservesSelection()
    {
        DesktopFile currentFile =
            File("same.txt");
        currentFile.IsSelected = true;
        DesktopFile refreshedFile =
            File("same.txt");
        PartitionViewModel current =
            Partition("工作", 0, currentFile);
        var all =
            new ObservableCollection<PartitionViewModel>
            {
                current
            };
        var left =
            new ObservableCollection<PartitionViewModel>
            {
                current
            };
        var right =
            new ObservableCollection<PartitionViewModel>();

        PartitionCollectionSynchronizer.Synchronize(
            all,
            left,
            right,
            new[]
            {
                Partition("工作", 0, refreshedFile)
            });

        Assert.Same(
            refreshedFile,
            Assert.Single(current.Files));
        Assert.True(refreshedFile.IsSelected);
    }

    private static PartitionViewModel Partition(
        string name,
        int column,
        params DesktopFile[] files)
    {
        var partition =
            new PartitionViewModel(name)
            {
                IsCustom = true,
                ColumnIndex = column
            };
        foreach (DesktopFile file in files)
            partition.Files.Add(file);
        return partition;
    }

    private static DesktopFile File(string name) => new()
    {
        Name = name,
        FullPath = $@"C:\Desktop\{name}"
    };
}
