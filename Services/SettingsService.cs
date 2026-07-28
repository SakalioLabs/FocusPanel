using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace FocusPanel.Services;

public class SettingsService
{
    private static readonly string SettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
    
    public AppSettings CurrentSettings { get; private set; } = new();

    public SettingsService()
    {
        LoadSettings();
    }

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                CurrentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                CurrentSettings = new AppSettings();
            }
        }
        catch
        {
            CurrentSettings = new AppSettings();
        }
    }

    public void SaveSettings()
    {
        try
        {
            string json = JsonSerializer.Serialize(CurrentSettings);
            File.WriteAllText(SettingsFile, json);
        }
        catch { }
    }
}

public class AppSettings
{
    public string ImageSavePath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
    public string GlobalCustomFieldsJson { get; set; } = string.Empty;
    public Dictionary<string, string> FilePartitions { get; set; } = new Dictionary<string, string>(); // FileName -> PartitionName
    public List<string> CustomPartitionNames { get; set; } = new List<string>(); // Store partition names to persist empty ones
    
    // UI Settings
    public double IconScale { get; set; } = 1.0;
    public bool IsListView { get; set; } = false;
    public bool IsPersonalizedView { get; set; } = true;
}
