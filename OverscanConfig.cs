using System;
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
}

public class AppSettings
{
    public OverscanProfile CurrentProfile { get; set; } = new();
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool StartMinimized { get; set; } = false;

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
                if (loaded != null) return loaded;
            }
        }
        catch { }
        return new AppSettings();
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
        catch { }
    }
}
