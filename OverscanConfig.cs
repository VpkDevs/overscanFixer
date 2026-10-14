using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OverscanFix;

public class OverscanProfile
{
    public string Name { get; set; } = "Default";
    public string TargetMonitorDevice { get; set; } = string.Empty;

    // Insets in Pixels
    public int LeftPixels { get; set; } = 40;
    public int TopPixels { get; set; } = 25;
    public int RightPixels { get; set; } = 40;
    public int BottomPixels { get; set; } = 25;

    // Linked uniform adjustment
    public bool UniformLinked { get; set; } = false;

    // Feature Toggles
    public bool EnableWorkAreaAdjustment { get; set; } = true;
    public bool EnableBlackBorders { get; set; } = false;
    public bool AutoFitWindows { get; set; } = true;

    public OverscanProfile Clone(string newName)
    {
        return new OverscanProfile
        {
            Name = newName,
            TargetMonitorDevice = TargetMonitorDevice,
            LeftPixels = LeftPixels,
            TopPixels = TopPixels,
            RightPixels = RightPixels,
            BottomPixels = BottomPixels,
            UniformLinked = UniformLinked,
            EnableWorkAreaAdjustment = EnableWorkAreaAdjustment,
            EnableBlackBorders = EnableBlackBorders,
            AutoFitWindows = AutoFitWindows
        };
    }
}

public class AppSettings
{
    public string ActiveProfileName { get; set; } = "Default";
    public OverscanProfile CurrentProfile { get; set; } = new();
    
    // Per-monitor profiles dictionary (key: DeviceName, value: Profile)
    public Dictionary<string, OverscanProfile> MonitorProfiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // List of saved named profiles
    public List<OverscanProfile> SavedProfiles { get; set; } = new();

    public bool StartWithWindows { get; set; } = false;
    public bool UseScheduledTaskStartup { get; set; } = true;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public bool CheckForUpdatesOnStartup { get; set; } = true;

    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OverscanFix");
    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    loaded.EnsureDefaultProfiles();
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load settings file, reverting to defaults", ex);
        }

        var defaults = new AppSettings();
        defaults.EnsureDefaultProfiles();
        return defaults;
    }

    public void EnsureDefaultProfiles()
    {
        if (SavedProfiles.Count == 0)
        {
            SavedProfiles.Add(CurrentProfile ?? new OverscanProfile { Name = "Default" });
            SavedProfiles.Add(new OverscanProfile { Name = "Mild TV (2.5%)", LeftPixels = 24, RightPixels = 24, TopPixels = 14, BottomPixels = 14 });
            SavedProfiles.Add(new OverscanProfile { Name = "Standard TV (5%)", LeftPixels = 48, RightPixels = 48, TopPixels = 27, BottomPixels = 27 });
            SavedProfiles.Add(new OverscanProfile { Name = "Aggressive (7.5%)", LeftPixels = 72, RightPixels = 72, TopPixels = 40, BottomPixels = 40 });
        }
    }

    public OverscanProfile GetProfileForMonitor(string deviceName)
    {
        if (!string.IsNullOrEmpty(deviceName) && MonitorProfiles.TryGetValue(deviceName, out var prof))
        {
            return prof;
        }
        return CurrentProfile;
    }

    public void SetProfileForMonitor(string deviceName, OverscanProfile prof)
    {
        if (!string.IsNullOrEmpty(deviceName))
        {
            MonitorProfiles[deviceName] = prof;
        }
        CurrentProfile = prof;
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
            }
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to save settings.json", ex);
        }
    }
}
