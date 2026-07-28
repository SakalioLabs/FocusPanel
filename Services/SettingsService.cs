using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace FocusPanel.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

    private readonly string _settingsFile;
    private readonly string _legacySettingsFile;

    public SettingsService()
        : this(
            Path.Combine(
                GetSettingsDirectory(),
                "settings.json"),
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "settings.json"))
    {
    }

    internal SettingsService(
        string settingsFile,
        string legacySettingsFile)
    {
        _settingsFile = settingsFile;
        _legacySettingsFile = legacySettingsFile;
        LoadSettings();
    }

    public AppSettings CurrentSettings
    {
        get;
        private set;
    } = AppSettings.CreateDefault();

    public string SettingsFile => _settingsFile;
    public string? LastError { get; private set; }
    public bool MigratedLegacySettings { get; private set; }

    public void LoadSettings()
    {
        LastError = null;
        MigratedLegacySettings = false;
        try
        {
            if (File.Exists(_settingsFile))
            {
                CurrentSettings =
                    ReadSettings(_settingsFile);
                return;
            }

            if (File.Exists(_legacySettingsFile))
            {
                CurrentSettings =
                    ReadSettings(_legacySettingsFile);
                MigrateLegacyDefaultImagePath();
                MigratedLegacySettings = SaveSettings();
                return;
            }

            CurrentSettings =
                AppSettings.CreateDefault();
        }
        catch (Exception ex)
        {
            CurrentSettings =
                AppSettings.CreateDefault();
            LastError =
                $"无法读取 FocusPanel 设置：{ex.Message}";
            Debug.WriteLine(LastError);
        }
    }

    public bool SaveSettings()
    {
        string? directory =
            Path.GetDirectoryName(_settingsFile);
        if (string.IsNullOrWhiteSpace(directory))
        {
            LastError = "设置文件路径无效。";
            return false;
        }

        string temporaryFile = Path.Combine(
            directory,
            $".settings-{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
            Normalize(CurrentSettings);
            string json = JsonSerializer.Serialize(
                CurrentSettings,
                JsonOptions);
            File.WriteAllText(
                temporaryFile,
                json,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));
            File.Move(
                temporaryFile,
                _settingsFile,
                overwrite: true);
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastError =
                $"无法保存 FocusPanel 设置：{ex.Message}";
            Debug.WriteLine(LastError);
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryFile))
                    File.Delete(temporaryFile);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"清理设置临时文件失败：{ex.Message}");
            }
        }
    }

    internal static string GetSettingsDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData),
            "FocusPanel");

    private static AppSettings ReadSettings(string path)
    {
        string json = File.ReadAllText(
            path,
            Encoding.UTF8);
        AppSettings settings =
            JsonSerializer.Deserialize<AppSettings>(
                json,
                JsonOptions)
            ?? AppSettings.CreateDefault();
        Normalize(settings);
        return settings;
    }

    private void MigrateLegacyDefaultImagePath()
    {
        string legacyDefault = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Images");
        string? currentPath = TryNormalizePath(
            CurrentSettings.ImageSavePath);
        string? legacyPath = TryNormalizePath(
            legacyDefault);
        if (currentPath != null
            && legacyPath != null
            && string.Equals(
                currentPath,
                legacyPath,
                StringComparison.OrdinalIgnoreCase))
        {
            CurrentSettings.ImageSavePath =
                AppSettings.DefaultImageSavePath;
        }
    }

    private static string? TryNormalizePath(
        string path)
    {
        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return null;
        }
    }

    private static void Normalize(
        AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(
                settings.ImageSavePath))
        {
            settings.ImageSavePath =
                AppSettings.DefaultImageSavePath;
        }
        settings.GlobalCustomFieldsJson ??=
            string.Empty;
        settings.FilePartitions ??=
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
        settings.CustomPartitionNames ??=
            new List<string>();
        settings.IconScale = Math.Clamp(
            settings.IconScale <= 0
                ? 1.0
                : settings.IconScale,
            0.5,
            2.0);
    }
}

public sealed class AppSettings
{
    public static string DefaultImageSavePath =>
        Path.Combine(
            SettingsService.GetSettingsDirectory(),
            "Images");

    public string ImageSavePath { get; set; } =
        DefaultImageSavePath;
    public string GlobalCustomFieldsJson { get; set; } =
        string.Empty;
    public Dictionary<string, string> FilePartitions
    {
        get;
        set;
    } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> CustomPartitionNames
    {
        get;
        set;
    } = new();

    public double IconScale { get; set; } = 1.0;
    public bool IsListView { get; set; }
    public bool IsPersonalizedView { get; set; } = true;

    internal static AppSettings CreateDefault() =>
        new();
}
