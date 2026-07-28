using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DesktopChangeAccumulatorTests
{
    [Fact]
    public void Paths_AreDeduplicatedWithoutCaseSensitivity()
    {
        var accumulator =
            new DesktopChangeAccumulator();

        accumulator.AddPath(
            @"C:\Desktop\Report.txt");
        accumulator.AddPath(
            @"c:\desktop\REPORT.txt");

        DesktopChangeBatch batch =
            accumulator.Take();

        Assert.False(batch.RequiresFullRefresh);
        Assert.Equal(
            new[]
            {
                @"C:\Desktop\Report.txt"
            },
            batch.Paths);
        Assert.Empty(batch.CreatedPaths);
    }

    [Fact]
    public void RenameBatch_KeepsOldAndNewPaths()
    {
        var accumulator =
            new DesktopChangeAccumulator();

        accumulator.AddPath(
            @"C:\Desktop\Old.txt");
        accumulator.AddPath(
            @"C:\Desktop\New.txt");

        DesktopChangeBatch batch =
            accumulator.Take();

        Assert.False(batch.RequiresFullRefresh);
        Assert.Equal(2, batch.Paths.Count);
        Assert.Contains(
            @"C:\Desktop\Old.txt",
            batch.Paths);
        Assert.Contains(
            @"C:\Desktop\New.txt",
            batch.Paths);
        Assert.Empty(batch.CreatedPaths);
    }

    [Fact]
    public void FullRefresh_SupersedesQueuedPaths()
    {
        var accumulator =
            new DesktopChangeAccumulator();
        accumulator.AddPath(
            @"C:\Desktop\One.txt");

        accumulator.RequireFullRefresh();
        accumulator.AddPath(
            @"C:\Desktop\Two.txt");
        DesktopChangeBatch batch =
            accumulator.Take();

        Assert.True(batch.RequiresFullRefresh);
        Assert.Empty(batch.Paths);
        Assert.Empty(batch.CreatedPaths);
    }

    [Fact]
    public void Take_ClearsPreviousBatch()
    {
        var accumulator =
            new DesktopChangeAccumulator();
        accumulator.AddPath(
            @"C:\Desktop\One.txt");

        _ = accumulator.Take();
        DesktopChangeBatch next =
            accumulator.Take();

        Assert.True(next.IsEmpty);
    }

    [Fact]
    public void ChangesAfterFullBatch_AreRetainedSeparately()
    {
        var accumulator =
            new DesktopChangeAccumulator();
        accumulator.RequireFullRefresh();
        DesktopChangeBatch full =
            accumulator.Take();

        accumulator.AddPath(
            @"C:\Desktop\Later.txt");
        DesktopChangeBatch incremental =
            accumulator.Take();

        Assert.True(full.RequiresFullRefresh);
        Assert.False(
            incremental.RequiresFullRefresh);
        Assert.Equal(
            new[]
            {
                @"C:\Desktop\Later.txt"
            },
            incremental.Paths);
    }

    [Fact]
    public void CreatedPath_RemainsMarkedAcrossLaterChanges()
    {
        var accumulator =
            new DesktopChangeAccumulator();

        accumulator.AddPath(
            @"C:\Desktop\New.txt",
            isCreated: true);
        accumulator.AddPath(
            @"c:\desktop\NEW.txt");

        DesktopChangeBatch batch =
            accumulator.Take();

        Assert.Single(batch.Paths);
        Assert.Equal(
            new[] { @"C:\Desktop\New.txt" },
            batch.CreatedPaths);
    }

    [Fact]
    public void Rename_TransfersCreatedIdentityToFinalPath()
    {
        var accumulator =
            new DesktopChangeAccumulator();
        accumulator.AddPath(
            @"C:\Desktop\download.tmp",
            isCreated: true);

        accumulator.RenamePath(
            @"C:\Desktop\download.tmp",
            @"C:\Desktop\report.pdf");
        DesktopChangeBatch batch =
            accumulator.Take();

        Assert.Contains(
            @"C:\Desktop\download.tmp",
            batch.Paths);
        Assert.Contains(
            @"C:\Desktop\report.pdf",
            batch.Paths);
        Assert.Equal(
            new[] { @"C:\Desktop\report.pdf" },
            batch.CreatedPaths);
    }

    [Fact]
    public void RenameOfExistingItem_IsNotMarkedCreated()
    {
        var accumulator =
            new DesktopChangeAccumulator();

        accumulator.RenamePath(
            @"C:\Desktop\old.txt",
            @"C:\Desktop\new.txt");
        DesktopChangeBatch batch =
            accumulator.Take();

        Assert.Equal(2, batch.Paths.Count);
        Assert.Empty(batch.CreatedPaths);
    }
}
