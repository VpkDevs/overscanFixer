using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32;

namespace OverscanFix;

public static class StartupManager
{
    private const string TaskName = "OverscanFixStartup";
    private const string RunKeyName = "OverscanFix";

    public static bool ConfigureStartup(bool enable, bool useScheduledTask)
    {
        bool success = true;
        string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        if (string.IsNullOrEmpty(exePath)) return false;

        if (enable)
        {
            if (useScheduledTask)
            {
                // Register scheduled task with 10 second logon delay for Windows session stabilization
                bool taskRegistered = RegisterLogonTask(exePath, delaySeconds: 10);
                if (!taskRegistered)
                {
                    Logger.Warn("Scheduled task registration failed; falling back to registry Run key.");
                    SetRegistryRun(exePath, true);
                }
                else
                {
                    // Clean up duplicate registry key if task succeeded
                    SetRegistryRun(exePath, false);
                }
            }
            else
            {
                SetRegistryRun(exePath, true);
                RemoveLogonTask();
            }
        }
        else
        {
            SetRegistryRun(exePath, false);
            RemoveLogonTask();
        }

        return success;
    }

    private static void SetRegistryRun(string exePath, bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            if (enable)
            {
                key.SetValue(RunKeyName, $"\"{exePath}\" --minimized");
                Logger.Info("Configured startup via HKCU Run registry key.");
            }
            else
            {
                if (key.GetValue(RunKeyName) != null)
                {
                    key.DeleteValue(RunKeyName);
                    Logger.Info("Removed startup from HKCU Run registry key.");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error configuring registry Run key", ex);
        }
    }

    private static bool RegisterLogonTask(string exePath, int delaySeconds)
    {
        try
        {
            // Use schtasks to create a user-level logon task with delay
            // Format: schtasks /create /tn "OverscanFixStartup" /tr "\"C:\path\OverscanFix.exe\" --minimized" /sc onlogon /delay 0000:10 /f
            string delayStr = $"0000:{delaySeconds:D2}";
            string trArg = $"\\\"{exePath}\\\" --minimized";

            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/create /tn \"{TaskName}\" /tr \"{trArg}\" /sc onlogon /delay {delayStr} /f",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;

            proc.WaitForExit(5000);
            if (proc.ExitCode == 0)
            {
                Logger.Info($"Registered scheduled logon task '{TaskName}' with {delaySeconds}s delay.");
                return true;
            }
            else
            {
                string err = proc.StandardError.ReadToEnd();
                Logger.Warn($"schtasks create returned {proc.ExitCode}: {err}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Exception executing schtasks", ex);
            return false;
        }
    }

    private static void RemoveLogonTask()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/delete /tn \"{TaskName}\" /f",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);
            Logger.Info($"Removed scheduled logon task '{TaskName}' if present.");
        }
        catch (Exception ex)
        {
            Logger.Error("Error removing scheduled task", ex);
        }
    }
}
