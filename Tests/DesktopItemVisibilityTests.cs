using System;
using System.IO;
using System.Threading.Tasks;
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
    public async Task VisibilityIo_ReadsAttributesOffCallingThread()
    {
        int callerThread =
            Environment.CurrentManagedThreadId;
        var visibility =
            new RecordingVisibilityService
            {
                Attributes =
                    FileAttributes.Archive
            };
        var io =
            new DesktopVisibilityIo(
                visibility);

        FileAttributes attributes =
            await io.ReadAttributesAsync(
                @"C:\Desktop\item.txt");

        Assert.Equal(
            FileAttributes.Archive,
            attributes);
        Assert.NotEqual(
            callerThread,
            visibility.LastThreadId);
        Assert.Equal(
            1,
            visibility.ExistsCalls);
        Assert.Equal(
            1,
            visibility.GetAttributesCalls);
    }

    [Fact]
    public async Task VisibilityIo_LocalWriteSetsAndNotifiesOffCallingThread()
    {
        int callerThread =
            Environment.CurrentManagedThreadId;
        var visibility =
            new RecordingVisibilityService();
        var io =
            new DesktopVisibilityIo(
                visibility);

        await io.ApplyAttributesAsync(
            @"C:\Desktop\item.txt",
            FileAttributes.Hidden
            | FileAttributes.System,
            false);

        Assert.NotEqual(
            callerThread,
            visibility.LastThreadId);
        Assert.Equal(
            1,
            visibility.SetAttributesCalls);
        Assert.Equal(
            1,
            visibility.NotifyCalls);
    }

    [Fact]
    public async Task VisibilityIo_ElevatedWriteUsesSameBackgroundBoundary()
    {
        int callerThread =
            Environment.CurrentManagedThreadId;
        int elevatedThread = callerThread;
        int elevatedCalls = 0;
        var visibility =
            new RecordingVisibilityService();
        var io =
            new DesktopVisibilityIo(
                visibility,
                (_, _) =>
                {
                    elevatedThread =
                        Environment
                            .CurrentManagedThreadId;
                    elevatedCalls++;
                });

        await io.ApplyAttributesAsync(
            @"C:\Users\Public\Desktop\item.txt",
            FileAttributes.Normal,
            true);

        Assert.Equal(
            1,
            elevatedCalls);
        Assert.NotEqual(
            callerThread,
            elevatedThread);
        Assert.Equal(
            0,
            visibility.SetAttributesCalls);
        Assert.Equal(
            0,
            visibility.NotifyCalls);
    }

    [Fact]
    public async Task VisibilityIo_ElevatedBatchReusesOneSessionForEveryWrite()
    {
        int legacyCalls = 0;
        int batchStarts = 0;
        var batch = new RecordingElevatedBatch();
        var io = new DesktopVisibilityIo(
            new RecordingVisibilityService(),
            (_, _) => legacyCalls++,
            () =>
            {
                batchStarts++;
                return batch;
            });

        using (await io.BeginElevatedBatchAsync())
        {
            await io.ApplyAttributesAsync(
                @"C:\Users\Public\Desktop\one.lnk",
                FileAttributes.Hidden
                | FileAttributes.System,
                true);
            await io.ApplyAttributesAsync(
                @"C:\Users\Public\Desktop\two.lnk",
                FileAttributes.Hidden
                | FileAttributes.System,
                true);
        }

        Assert.Equal(1, batchStarts);
        Assert.Equal(2, batch.SetCalls);
        Assert.Equal(1, batch.DisposeCalls);
        Assert.Equal(0, legacyCalls);
    }

    [Fact]
    public async Task VisibilityIo_AfterBatchUsesSingleItemFallbackAgain()
    {
        int legacyCalls = 0;
        var batch = new RecordingElevatedBatch();
        var io = new DesktopVisibilityIo(
            new RecordingVisibilityService(),
            (_, _) => legacyCalls++,
            () => batch);

        using (await io.BeginElevatedBatchAsync())
        {
            await io.ApplyAttributesAsync(
                @"C:\Users\Public\Desktop\during.lnk",
                FileAttributes.Hidden,
                true);
        }
        await io.ApplyAttributesAsync(
            @"C:\Users\Public\Desktop\after.lnk",
            FileAttributes.Normal,
            true);

        Assert.Equal(1, batch.SetCalls);
        Assert.Equal(1, legacyCalls);
    }

    [Fact]
    public async Task VisibilityIo_MissingItemFailsWithoutAttributeRead()
    {
        var visibility =
            new RecordingVisibilityService
            {
                ItemExists = false
            };
        var io =
            new DesktopVisibilityIo(
                visibility);

        await Assert.ThrowsAsync<
            FileNotFoundException>(
            () =>
                io.ReadAttributesAsync(
                    @"C:\Desktop\missing.txt"));

        Assert.Equal(
            0,
            visibility.GetAttributesCalls);
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

    private sealed class RecordingVisibilityService
        : IDesktopItemVisibilityService
    {
        internal bool ItemExists { get; set; } =
            true;

        internal FileAttributes Attributes
        {
            get;
            set;
        } = FileAttributes.Normal;

        internal int LastThreadId { get; private set; }

        internal int ExistsCalls { get; private set; }

        internal int GetAttributesCalls
        {
            get;
            private set;
        }

        internal int SetAttributesCalls
        {
            get;
            private set;
        }

        internal int NotifyCalls { get; private set; }

        public bool ShowsProtectedSystemFiles =>
            false;

        public bool Exists(string path)
        {
            RecordThread();
            ExistsCalls++;
            return ItemExists;
        }

        public FileAttributes GetAttributes(
            string path)
        {
            RecordThread();
            GetAttributesCalls++;
            return Attributes;
        }

        public void SetAttributes(
            string path,
            FileAttributes attributes)
        {
            RecordThread();
            SetAttributesCalls++;
            Attributes = attributes;
        }

        public string? TryGetIdentity(
            string path) =>
            null;

        public void NotifyAttributesChanged(
            string path)
        {
            RecordThread();
            NotifyCalls++;
        }

        private void RecordThread() =>
            LastThreadId =
                Environment.CurrentManagedThreadId;
    }

    private sealed class RecordingElevatedBatch
        : IDesktopVisibilityElevatedBatch
    {
        internal int SetCalls { get; private set; }

        internal int DisposeCalls
        {
            get;
            private set;
        }

        public void SetAttributes(
            string path,
            FileAttributes attributes)
        {
            _ = path;
            _ = attributes;
            SetCalls++;
        }

        public void Dispose() => DisposeCalls++;
    }
}
