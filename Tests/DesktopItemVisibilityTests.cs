using System;
using System.IO;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DesktopItemVisibilityTests
{
    [Theory]
    [InlineData(FileAttributes.Normal)]
    [InlineData(FileAttributes.Archive)]
    [InlineData(FileAttributes.ReadOnly | FileAttributes.Archive)]
    [InlineData(FileAttributes.Hidden | FileAttributes.Archive)]
    [InlineData(FileAttributes.System | FileAttributes.ReadOnly)]
    public void Collect_AddsHiddenAndSystemWithoutLosingOriginalFlags(FileAttributes original)
    {
        FileAttributes collected = DesktopItemAttributePolicy.Collect(original);

        Assert.True(collected.HasFlag(FileAttributes.Hidden));
        Assert.True(collected.HasFlag(FileAttributes.System));
        Assert.Equal(
            original
                & ~FileAttributes.Normal
                & ~FileAttributes.Hidden
                & ~FileAttributes.System,
            collected & ~FileAttributes.Hidden & ~FileAttributes.System);
    }

    [Theory]
    [InlineData(FileAttributes.Normal)]
    [InlineData(FileAttributes.Hidden | FileAttributes.Archive)]
    [InlineData(FileAttributes.System | FileAttributes.ReadOnly)]
    [InlineData(FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReadOnly)]
    public void Restore_ReturnsExactOriginalAttributes(FileAttributes original)
    {
        Assert.Equal(original, DesktopItemAttributePolicy.Restore((long)original));
    }

    [Fact]
    public void OperationStates_KeepInterruptedWorkRecoverable()
    {
        Assert.NotEqual(
            DesktopVisibilityOperation.Stable,
            DesktopVisibilityOperation.Collecting);
        Assert.NotEqual(
            DesktopVisibilityOperation.Stable,
            DesktopVisibilityOperation.Restoring);
        Assert.NotEqual(
            DesktopVisibilityOperation.Stable,
            DesktopVisibilityOperation.RecoveryRequired);
    }

    [Fact]
    public void DesktopHelper_DoesNotContainExplorerItemDeletionOrMemoryInjection()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "Helpers", "DesktopHelper.cs"));

        Assert.DoesNotContain("LVM_DELETEITEM", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteProcessMemory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadProcessMemory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenProcess", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Restore_RemovesItemFromItsPanelPartition()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "Services", "FileOrganizerService.cs"));

        Assert.Contains("pref.PartitionName = string.Empty;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsBoundary_PreservesIdentityAcrossRenameAndRestoresAttributes()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string originalPath = Path.Combine(directory, "before.txt");
        string renamedPath = Path.Combine(directory, "after.txt");
        File.WriteAllText(originalPath, "FocusPanel");
        var service = new WindowsDesktopItemVisibilityService();

        try
        {
            FileAttributes original = service.GetAttributes(originalPath);
            string? identity = service.TryGetIdentity(originalPath);
            service.SetAttributes(originalPath, DesktopItemAttributePolicy.Collect(original));

            File.Move(originalPath, renamedPath);

            Assert.False(string.IsNullOrWhiteSpace(identity));
            Assert.Equal(identity, service.TryGetIdentity(renamedPath));
            Assert.True(service.GetAttributes(renamedPath).HasFlag(FileAttributes.Hidden));
            Assert.True(service.GetAttributes(renamedPath).HasFlag(FileAttributes.System));

            service.SetAttributes(renamedPath, DesktopItemAttributePolicy.Restore((long)original));
            Assert.Equal(original, service.GetAttributes(renamedPath));
        }
        finally
        {
            if (File.Exists(originalPath))
                File.SetAttributes(originalPath, FileAttributes.Normal);
            if (File.Exists(renamedPath))
                File.SetAttributes(renamedPath, FileAttributes.Normal);
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "FocusPanel.csproj")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("未找到 FocusPanel.csproj。");
    }
}
