using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OverscanFix;

public class AppWatcher : IDisposable
{
    private readonly AppSettings _settings;
    private readonly Action<string, bool> _onTriggerMatched;
    private CancellationTokenSource? _cts;
    private Task? _watcherTask;
    private string _lastActiveProcess = string.Empty;
    private string? _previousProfileBeforeTrigger;

    public AppWatcher(AppSettings settings, Action<string, bool> onTriggerMatched)
    {
        _settings = settings;
        _onTriggerMatched = onTriggerMatched;
    }

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        _watcherTask = Task.Run(() => WatchLoopAsync(_cts.Token));
        Logger.Info("AppWatcher started background foreground window polling.");
    }

    public void Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    private async Task WatchLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                IntPtr hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid > 0)
                    {
                        string procName = string.Empty;
                        try
                        {
                            using var proc = Process.GetProcessById((int)pid);
                            procName = proc.ProcessName;
                        }
                        catch { }

                        if (!string.IsNullOrEmpty(procName) && !procName.Equals(_lastActiveProcess, StringComparison.OrdinalIgnoreCase))
                        {
                            _lastActiveProcess = procName;
                            CheckProcessRules(procName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"AppWatcher polling exception: {ex.Message}");
            }

            await Task.Delay(1000, token);
        }
    }

    private void CheckProcessRules(string currentProc)
    {
        var rule = _settings.TriggerRules.Find(r =>
            r.AutoApply &&
            !string.IsNullOrWhiteSpace(r.ProcessName) &&
            currentProc.Contains(r.ProcessName, StringComparison.OrdinalIgnoreCase));

        if (rule != null)
        {
            Logger.Info($"App trigger matched: {currentProc} -> Profile '{rule.ProfileName}'");
            if (_previousProfileBeforeTrigger == null)
            {
                _previousProfileBeforeTrigger = _settings.ActiveProfileName;
            }
            _onTriggerMatched(rule.ProfileName, false);
        }
        else
        {
            // If we previously switched due to a rule with RevertOnExit, check if reverted
            if (_previousProfileBeforeTrigger != null)
            {
                Logger.Info($"Foreground app changed away from trigger; reverting to '{_previousProfileBeforeTrigger}'");
                string prev = _previousProfileBeforeTrigger;
                _previousProfileBeforeTrigger = null;
                _onTriggerMatched(prev, true);
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
