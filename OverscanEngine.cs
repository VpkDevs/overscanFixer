using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OverscanFix;

public class MonitorItem
{
    public string DeviceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsPrimary { get; set; }
    public NativeMethods.RECT OriginalWorkArea { get; set; }

    public override string ToString() => DisplayName;
}

public class OverscanEngine : IDisposable
{
    private readonly Dictionary<string, NativeMethods.RECT> _savedWorkAreas = new(StringComparer.OrdinalIgnoreCase);
    private BorderMaskWindow? _borderWindow;
    private CalibrationWindow? _calibrationWindow;
    private bool _isApplied;

    public bool IsApplied => _isApplied;
    public MonitorItem? ActiveMonitor { get; private set; }

    public List<MonitorItem> GetAvailableMonitors()
    {
        var list = new List<MonitorItem>();
        int index = 1;
        foreach (var s in Screen.AllScreens)
        {
            var item = new MonitorItem
            {
                DeviceName = s.DeviceName,
                DisplayName = $"Monitor {index++}: {s.DeviceName} ({s.Bounds.Width}x{s.Bounds.Height}){(s.Primary ? " [Primary]" : "")}",
                X = s.Bounds.X,
                Y = s.Bounds.Y,
                Width = s.Bounds.Width,
                Height = s.Bounds.Height,
                IsPrimary = s.Primary,
                OriginalWorkArea = new NativeMethods.RECT(s.WorkingArea.Left, s.WorkingArea.Top, s.WorkingArea.Right, s.WorkingArea.Bottom)
            };

            if (!_savedWorkAreas.ContainsKey(s.DeviceName))
            {
                _savedWorkAreas[s.DeviceName] = item.OriginalWorkArea;
            }

            list.Add(item);
        }
        return list;
    }

    public void SelectMonitor(MonitorItem monitor)
    {
        ActiveMonitor = monitor;
    }

    public void ApplyOverscan(int leftPx, int topPx, int rightPx, int bottomPx, bool applyWorkArea, bool showBorders)
    {
        if (ActiveMonitor == null) return;

        int safeLeft = ActiveMonitor.X + leftPx;
        int safeTop = ActiveMonitor.Y + topPx;
        int safeRight = ActiveMonitor.X + ActiveMonitor.Width - rightPx;
        int safeBottom = ActiveMonitor.Y + ActiveMonitor.Height - bottomPx;

        // Ensure minimum safe dimensions (at least 200x200 remaining)
        if (safeRight <= safeLeft + 200 || safeBottom <= safeTop + 200)
        {
            Logger.Warn($"Requested margins too large for {ActiveMonitor.Width}x{ActiveMonitor.Height}: Left={leftPx}, Right={rightPx}, Top={topPx}, Bottom={bottomPx}");
            return;
        }

        if (applyWorkArea)
        {
            var safeRect = new NativeMethods.RECT(safeLeft, safeTop, safeRight, safeBottom);
            NativeMethods.SetWorkArea(safeRect);
        }

        if (showBorders)
        {
            if (_borderWindow == null)
            {
                _borderWindow = new BorderMaskWindow(ActiveMonitor.X, ActiveMonitor.Y, ActiveMonitor.Width, ActiveMonitor.Height);
                _borderWindow.Show();
            }
            _borderWindow.UpdateBorders(leftPx, topPx, rightPx, bottomPx);
        }
        else
        {
            HideBorders();
        }

        _isApplied = true;
    }

    /// <summary>
    /// Applies overscan with startup resilience retries.
    /// During Windows logon, DWM and shell startup can clobber or reset the work area multiple times.
    /// This method enforces the target work area across 3 retry intervals (immediate, +1.5s, +4s).
    /// </summary>
    public async Task ApplyOverscanWithResilienceAsync(int leftPx, int topPx, int rightPx, int bottomPx, bool applyWorkArea, bool showBorders)
    {
        ApplyOverscan(leftPx, topPx, rightPx, bottomPx, applyWorkArea, showBorders);

        if (applyWorkArea && ActiveMonitor != null)
        {
            int safeLeft = ActiveMonitor.X + leftPx;
            int safeTop = ActiveMonitor.Y + topPx;
            int safeRight = ActiveMonitor.X + ActiveMonitor.Width - rightPx;
            int safeBottom = ActiveMonitor.Y + ActiveMonitor.Height - bottomPx;
            var safeRect = new NativeMethods.RECT(safeLeft, safeTop, safeRight, safeBottom);

            int[] retryDelaysMs = { 1500, 2500, 4000 };
            foreach (int delay in retryDelaysMs)
            {
                await Task.Delay(delay);
                var current = NativeMethods.GetCurrentWorkArea();
                if (current.Left != safeLeft || current.Top != safeTop || current.Right != safeRight || current.Bottom != safeBottom)
                {
                    Logger.Info($"Work area drift detected during boot sequence (Expected: {safeRect}, Actual: {current}). Re-applying...");
                    NativeMethods.SetWorkArea(safeRect);
                }
            }
        }
    }

    public void RestoreDefaultWorkArea()
    {
        if (ActiveMonitor != null && _savedWorkAreas.TryGetValue(ActiveMonitor.DeviceName, out var orig))
        {
            NativeMethods.SetWorkArea(orig);
        }
        else
        {
            if (ActiveMonitor != null)
            {
                var r = new NativeMethods.RECT(ActiveMonitor.X, ActiveMonitor.Y, ActiveMonitor.X + ActiveMonitor.Width, ActiveMonitor.Y + ActiveMonitor.Height - 48);
                NativeMethods.SetWorkArea(r);
            }
        }

        HideBorders();
        _isApplied = false;
    }

    public void ShowCalibration(int leftPx, int topPx, int rightPx, int bottomPx)
    {
        if (ActiveMonitor == null) return;

        if (_calibrationWindow != null)
        {
            _calibrationWindow.Close();
            _calibrationWindow = null;
        }

        _calibrationWindow = new CalibrationWindow(
            ActiveMonitor.X, ActiveMonitor.Y, ActiveMonitor.Width, ActiveMonitor.Height,
            leftPx, topPx, rightPx, bottomPx);
        _calibrationWindow.Closed += (s, e) => _calibrationWindow = null;
        _calibrationWindow.Show();
    }

    public void UpdateCalibrationBounds(int leftPx, int topPx, int rightPx, int bottomPx)
    {
        _calibrationWindow?.UpdateSafeBounds(leftPx, topPx, rightPx, bottomPx);
    }

    public void HideCalibration()
    {
        _calibrationWindow?.Close();
        _calibrationWindow = null;
    }

    public void HideBorders()
    {
        if (_borderWindow != null)
        {
            _borderWindow.Close();
            _borderWindow = null;
        }
    }

    public void FitAllWindows(int leftPx, int topPx, int rightPx, int bottomPx)
    {
        if (ActiveMonitor == null) return;

        int safeLeft = ActiveMonitor.X + leftPx;
        int safeTop = ActiveMonitor.Y + topPx;
        int safeRight = ActiveMonitor.X + ActiveMonitor.Width - rightPx;
        int safeBottom = ActiveMonitor.Y + ActiveMonitor.Height - bottomPx;
        int safeW = safeRight - safeLeft;
        int safeH = safeBottom - safeTop;

        NativeMethods.EnumWindows((hWnd, lParam) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd) || NativeMethods.IsIconic(hWnd))
                return true;

            var sb = new StringBuilder(256);
            NativeMethods.GetWindowText(hWnd, sb, 256);
            string title = sb.ToString();

            // Filter out system utility and desktop windows
            if (string.IsNullOrWhiteSpace(title) ||
                title == "Program Manager" ||
                title == "Windows Input Experience" ||
                title.StartsWith("Overscan"))
                return true;

            if (NativeMethods.GetWindowRect(hWnd, out var r))
            {
                bool needsAdjust = (r.Left < safeLeft || r.Top < safeTop || r.Right > safeRight || r.Bottom > safeBottom);
                if (needsAdjust && r.Width > 150 && r.Height > 100)
                {
                    int newX = Math.Max(safeLeft, r.Left);
                    int newY = Math.Max(safeTop, r.Top);
                    int newW = Math.Min(r.Width, safeW);
                    int newH = Math.Min(r.Height, safeH);

                    if (newX + newW > safeRight) newX = safeRight - newW;
                    if (newY + newH > safeBottom) newY = safeBottom - newH;

                    NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, newX, newY, newW, newH,
                        NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
                }
            }
            return true;
        }, IntPtr.Zero);
    }

    public void Dispose()
    {
        RestoreDefaultWorkArea();
        HideBorders();
        HideCalibration();
    }
}
