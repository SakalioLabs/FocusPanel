using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public void Save_WritesAppDataFileAtomicallyAndReloads()
    {
        using var workspace = new TemporarySettingsWorkspace();
        var service = new SettingsService(
            workspace.SettingsFile,
            workspace.LegacyFile);
        service.CurrentSettings.IconScale = 1.25;
        service.CurrentSettings.IsListView = true;
        service.CurrentSettings.CustomPartitionNames.Add(
            "项目资料");

        bool saved = service.SaveSettings();
        var reloaded = new SettingsService(
            workspace.SettingsFile,
            workspace.LegacyFile);

        Assert.True(saved);
        Assert.Null(service.LastError);
        Assert.Equal(
            1.25,
            reloaded.CurrentSettings.IconScale);
        Assert.True(
            reloaded.CurrentSettings.IsListView);
        Assert.Contains(
            "项目资料",
            reloaded.CurrentSettings
                .CustomPartitionNames);
        Assert.Empty(
            Directory.GetFiles(
                workspace.DirectoryPath,
                ".settings-*.tmp"));
    }

    [Fact]
    public void Load_MigratesLegacyFileWithoutDeletingIt()
    {
        using var workspace = new TemporarySettingsWorkspace();
        var legacy = new AppSettings
        {
            ImageSavePath = @"D:\MyImages",
            GlobalCustomFieldsJson = "[{\"Name\":\"优先级\"}]",
            IconScale = 1.4
        };
        File.WriteAllText(
            workspace.LegacyFile,
            JsonSerializer.Serialize(legacy));

        var service = new SettingsService(
            workspace.SettingsFile,
            workspace.LegacyFile);

        Assert.True(service.MigratedLegacySettings);
        Assert.True(File.Exists(workspace.SettingsFile));
        Assert.True(File.Exists(workspace.LegacyFile));
        Assert.Equal(
            @"D:\MyImages",
            service.CurrentSettings.ImageSavePath);
        Assert.Equal(
            legacy.GlobalCustomFieldsJson,
            service.CurrentSettings
                .GlobalCustomFieldsJson);
    }

    [Fact]
    public void Load_MigratesOldDefaultImageFolderToAppData()
    {
        using var workspace = new TemporarySettingsWorkspace();
        var legacy = new AppSettings
        {
            ImageSavePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Images")
        };
        File.WriteAllText(
            workspace.LegacyFile,
            JsonSerializer.Serialize(legacy));

        var service = new SettingsService(
            workspace.SettingsFile,
            workspace.LegacyFile);

        Assert.Equal(
            AppSettings.DefaultImageSavePath,
            service.CurrentSettings.ImageSavePath);
    }

    [Fact]
    public void Load_CorruptFileFallsBackAndReportsError()
    {
        using var workspace = new TemporarySettingsWorkspace();
        File.WriteAllText(
            workspace.SettingsFile,
            "{not-json");

        var service = new SettingsService(
            workspace.SettingsFile,
            workspace.LegacyFile);

        Assert.NotNull(service.LastError);
        Assert.Contains(
            "无法读取",
            service.LastError);
        Assert.Equal(
            1.0,
            service.CurrentSettings.IconScale);
        Assert.NotNull(
            service.CurrentSettings.FilePartitions);
        Assert.NotNull(
            service.CurrentSettings
                .CustomPartitionNames);
    }

    [Fact]
    public void Save_FailureIsObservableAndCleansTemporaryFile()
    {
        using var workspace = new TemporarySettingsWorkspace();
        var service = new SettingsService(
            workspace.DirectoryPath,
            workspace.LegacyFile);

        bool saved = service.SaveSettings();

        Assert.False(saved);
        Assert.NotNull(service.LastError);
        Assert.Contains(
            "无法保存",
            service.LastError);
        Assert.Empty(
            Directory.GetFiles(
                Path.GetDirectoryName(
                    workspace.DirectoryPath)!,
                ".settings-*.tmp"));
    }

    [Fact]
    public void DefaultLocation_IsRoamingAppDataNotInstallFolder()
    {
        string settingsDirectory =
            SettingsService.GetSettingsDirectory();

        Assert.StartsWith(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData),
            settingsDirectory,
            StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(
            Path.GetFullPath(
                AppDomain.CurrentDomain.BaseDirectory),
            Path.GetFullPath(settingsDirectory));
    }

    private sealed class TemporarySettingsWorkspace :
        IDisposable
    {
        internal TemporarySettingsWorkspace()
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                "FocusPanel.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            SettingsFile = Path.Combine(
                DirectoryPath,
                "settings.json");
            LegacyFile = Path.Combine(
                DirectoryPath,
                "legacy-settings.json");
        }

        internal string DirectoryPath { get; }
        internal string SettingsFile { get; }
        internal string LegacyFile { get; }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(
                    DirectoryPath,
                    recursive: true);
            }
        }
    }
}
