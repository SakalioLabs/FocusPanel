using System;
using System.Collections.Generic;
using System.IO;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DesktopCrashRecoveryServiceTests
{
    [Fact]
    public void RestoreIfRequested_WithoutMarkerDoesNotTouchDesktopBoundary()
    {
        string directory = CreateDirectory();
        try
        {
            var store = new FakeStore();
            var visibility = new FakeVisibility();
            var service = CreateService(
                store,
                visibility,
                directory);

            DesktopCrashRecoveryResult result =
                service.RestoreIfRequested(
                    force: false);

            Assert.False(result.Attempted);
            Assert.Equal(0, visibility.SetCount);
            Assert.Empty(store.Restored);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ForcedRecovery_RestoresExactOriginalAttributesAndClearsMarker()
    {
        string directory = CreateDirectory();
        try
        {
            string path = Path.Combine(
                directory,
                "desktop",
                "note.txt");
            var store = new FakeStore(
                new DesktopCrashRecoveryItem(
                    7,
                    "note.txt",
                    path,
                    (long)FileAttributes.ReadOnly));
            var visibility = new FakeVisibility();
            visibility.Add(
                path,
                FileAttributes.Hidden
                | FileAttributes.System
                | FileAttributes.ReadOnly);
            var service = CreateService(
                store,
                visibility,
                directory);
            service.Arm();

            DesktopCrashRecoveryResult result =
                service.RestoreIfRequested(
                    force: true);

            Assert.True(result.Attempted);
            Assert.Equal(1, result.Restored);
            Assert.Equal(0, result.Failed);
            Assert.Equal(
                FileAttributes.ReadOnly,
                visibility.Attributes[path]);
            Assert.Equal(new[] { 7 }, store.Restored);
            Assert.False(File.Exists(
                MarkerPath(directory)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MissingOriginalAttributes_ClearsOnlyCollectionFlags()
    {
        string directory = CreateDirectory();
        try
        {
            string path = Path.Combine(
                directory,
                "desktop",
                "folder");
            var store = new FakeStore(
                new DesktopCrashRecoveryItem(
                    9,
                    "folder",
                    path,
                    null));
            var visibility = new FakeVisibility();
            visibility.Add(
                path,
                FileAttributes.Directory
                | FileAttributes.Hidden
                | FileAttributes.System
                | FileAttributes.ReadOnly);
            var service = CreateService(
                store,
                visibility,
                directory);

            DesktopCrashRecoveryResult result =
                service.RestoreIfRequested(
                    force: true);

            Assert.Equal(1, result.Restored);
            Assert.Equal(
                FileAttributes.Directory
                | FileAttributes.ReadOnly,
                visibility.Attributes[path]);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void FailedRecovery_KeepsMarkerAndRecoveryRecord()
    {
        string directory = CreateDirectory();
        try
        {
            var store = new FakeStore(
                new DesktopCrashRecoveryItem(
                    11,
                    "missing.lnk",
                    null,
                    (long)FileAttributes.Normal));
            var visibility = new FakeVisibility();
            var service = CreateService(
                store,
                visibility,
                directory);
            service.Arm();

            DesktopCrashRecoveryResult result =
                service.RestoreIfRequested(
                    force: false);

            Assert.Equal(0, result.Restored);
            Assert.Equal(1, result.Failed);
            Assert.Equal(
                new[] { 11 },
                store.RecoveryRequired);
            Assert.True(File.Exists(
                MarkerPath(directory)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void KnownCrashResidue_IsRestoredAutomaticallyOnlyOnce()
    {
        string directory = CreateDirectory();
        try
        {
            string path = Path.Combine(
                directory,
                "desktop",
                "stale.txt");
            var store = new FakeStore(
                new DesktopCrashRecoveryItem(
                    13,
                    "stale.txt",
                    path,
                    (long)FileAttributes.Normal));
            var visibility = new FakeVisibility();
            visibility.Add(
                path,
                FileAttributes.Hidden
                | FileAttributes.System);
            var service = CreateService(
                store,
                visibility,
                directory);

            DesktopCrashRecoveryResult first =
                service.RestoreKnownCrashResidueOnce(
                    "0.10.78");
            DesktopCrashRecoveryResult second =
                service.RestoreKnownCrashResidueOnce(
                    "0.10.78");

            Assert.True(first.Attempted);
            Assert.Equal(1, first.Restored);
            Assert.False(second.Attempted);
            Assert.Equal(1, visibility.SetCount);
            Assert.True(File.Exists(Path.Combine(
                directory,
                "desktop-recovery-0.10.78-completed")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void EmergencyRecovery_KeepsMarkerWhileParentCanContinue()
    {
        string directory = CreateDirectory();
        try
        {
            string path = Path.Combine(
                directory,
                "desktop",
                "emergency.txt");
            var store = new FakeStore(
                new DesktopCrashRecoveryItem(
                    15,
                    "emergency.txt",
                    path,
                    (long)FileAttributes.Normal));
            var visibility = new FakeVisibility();
            visibility.Add(
                path,
                FileAttributes.Hidden
                | FileAttributes.System);
            var service = CreateService(
                store,
                visibility,
                directory);
            service.Arm();

            DesktopCrashRecoveryResult result =
                service.RestoreIfRequested(
                    force: false,
                    keepMarker: true);

            Assert.Equal(1, result.Restored);
            Assert.True(File.Exists(
                MarkerPath(directory)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static DesktopCrashRecoveryService
        CreateService(
            FakeStore store,
            FakeVisibility visibility,
            string directory) =>
        new(
            store,
            visibility,
            MarkerPath(directory),
            Path.Combine(directory, "desktop"),
            Path.Combine(directory, "public"));

    private static string MarkerPath(
        string directory) =>
        Path.Combine(directory, "recovery.marker");

    private static string CreateDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeStore
        : IDesktopCrashRecoveryStore
    {
        private readonly IReadOnlyList<
            DesktopCrashRecoveryItem> _items;

        internal FakeStore(
            params DesktopCrashRecoveryItem[] items)
        {
            _items = items;
        }

        internal List<int> Restored { get; } = new();
        internal List<int> RecoveryRequired { get; } = new();

        public IReadOnlyList<DesktopCrashRecoveryItem>
            LoadCollectedItems() => _items;

        public void MarkRestored(int preferenceId) =>
            Restored.Add(preferenceId);

        public void MarkRecoveryRequired(
            int preferenceId) =>
            RecoveryRequired.Add(preferenceId);
    }

    private sealed class FakeVisibility
        : IDesktopItemVisibilityService
    {
        internal Dictionary<string, FileAttributes>
            Attributes { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        internal int SetCount { get; private set; }

        internal void Add(
            string path,
            FileAttributes attributes) =>
            Attributes[path] = attributes;

        public bool Exists(string path) =>
            Attributes.ContainsKey(path);

        public FileAttributes GetAttributes(
            string path) => Attributes[path];

        public void SetAttributes(
            string path,
            FileAttributes attributes)
        {
            Attributes[path] = attributes;
            SetCount++;
        }

        public string? TryGetIdentity(string path) =>
            null;

        public void NotifyAttributesChanged(
            string path)
        {
        }

        public bool ShowsProtectedSystemFiles =>
            false;
    }
}
