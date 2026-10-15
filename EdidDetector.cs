using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;

namespace OverscanFix;

public record EdidInfo(
    string InstanceName,
    string ModelName,
    string SerialNumber,
    string ManufacturerCode
);

public static class EdidDetector
{
    private static readonly Dictionary<string, EdidInfo> _cache = new(StringComparer.OrdinalIgnoreCase);

    public static List<EdidInfo> QueryAllMonitors()
    {
        var results = new List<EdidInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorID");
            foreach (ManagementObject mo in searcher.Get())
            {
                string instanceName = mo["InstanceName"]?.ToString() ?? "";
                string model = ExtractAsciiString(mo["UserFriendlyName"] as ushort[]);
                string serial = ExtractAsciiString(mo["SerialNumberID"] as ushort[]);
                string mfg = ExtractAsciiString(mo["ManufacturerName"] as ushort[]);

                if (string.IsNullOrWhiteSpace(model))
                {
                    model = !string.IsNullOrWhiteSpace(instanceName) ? instanceName.Split('\\').Skip(1).FirstOrDefault() ?? "Display" : "Display";
                }

                var info = new EdidInfo(instanceName, model, serial, mfg);
                results.Add(info);
                _cache[instanceName] = info;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"WmiMonitorID query failed (falling back to standard names): {ex.Message}");
        }

        return results;
    }

    public static string ResolveFriendlyNameForDevice(string deviceName, int index)
    {
        // Query EDID list
        var monitors = QueryAllMonitors();
        if (monitors.Count > 0)
        {
            // If only 1 monitor detected and 1 in Windows
            if (monitors.Count == 1)
            {
                return $"{monitors[0].ModelName} ({deviceName})";
            }

            // Match by index or instance if possible
            if (index - 1 < monitors.Count)
            {
                return $"{monitors[index - 1].ModelName} ({deviceName})";
            }
        }

        return $"{deviceName} (Display {index})";
    }

    private static string ExtractAsciiString(ushort[]? charCodes)
    {
        if (charCodes == null || charCodes.Length == 0) return string.Empty;
        var bytes = charCodes.Where(c => c > 0 && c < 256).Select(c => (byte)c).ToArray();
        return Encoding.ASCII.GetString(bytes).Trim();
    }
}
