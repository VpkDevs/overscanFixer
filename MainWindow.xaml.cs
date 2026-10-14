using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DrawingColor = System.Drawing.Color;
using DrawingIcon = System.Drawing.Icon;
using WinForms = System.Windows.Forms;

namespace OverscanFix;

public partial class MainWindow : Window
{
    private readonly OverscanEngine _engine = new();
    private readonly AppSettings _settings;
    private WinForms.NotifyIcon? _trayIcon;
    private bool _isUpdating;
    private const int HOTKEY_ID = 9001;
    private HwndSource? _hwndSource;

    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeSystemTray();
        RegisterGlobalHotkey();
        LoadMonitors();
        LoadProfileIntoUI(_settings.CurrentProfile);

        // Check if started minimized
        if (_settings.StartMinimized || Environment.GetCommandLineArgs().Contains("--minimized"))
        {
            Hide();
            if (_settings.CurrentProfile.EnableWorkAreaAdjustment || _settings.CurrentProfile.EnableBlackBorders)
            {
                ApplyOverscan();
            }
        }
    }

    private void InitializeSystemTray()
    {
        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = DrawingIcon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? SystemIcons.Application,
            Visible = true,
            Text = "OverscanFix - Display Overscan Manager"
        };

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Open Controls", null, (s, e) => ShowAndRestore());
        menu.Items.Add("Toggle Overscan (Ctrl+Alt+O)", null, (s, e) => ToggleOverscan());
        menu.Items.Add("Reset to Fullscreen", null, (s, e) => ResetOverscan());
        menu.Items.Add("-");
        menu.Items.Add("Exit", null, (s, e) =>
        {
            _settings.MinimizeToTrayOnClose = false;
            Close();
        });

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (s, e) => ShowAndRestore();
    }

    private void ShowAndRestore()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void RegisterGlobalHotkey()
    {
        var helper = new WindowInteropHelper(this);
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(HwndHook);

        // Register Ctrl + Alt + O
        NativeMethods.RegisterHotKey(helper.Handle, HOTKEY_ID,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, 0x4F); // 0x4F = 'O'
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            ToggleOverscan();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void LoadMonitors()
    {
        var monitors = _engine.GetAvailableMonitors();
        MonitorSelector.ItemsSource = monitors;

        if (monitors.Count > 0)
        {
            // Select saved monitor or primary
            int selectedIndex = 0;
            if (!string.IsNullOrEmpty(_settings.CurrentProfile.TargetMonitorDevice))
            {
                int idx = monitors.FindIndex(m => m.DeviceName == _settings.CurrentProfile.TargetMonitorDevice);
                if (idx >= 0) selectedIndex = idx;
            }
            MonitorSelector.SelectedIndex = selectedIndex;
        }
    }

    private void LoadProfileIntoUI(OverscanProfile p)
    {
        _isUpdating = true;
        try
        {
            UniformLockCheck.IsChecked = p.UniformLinked;
            WorkAreaCheck.IsChecked = p.EnableWorkAreaAdjustment;
            BlackBordersCheck.IsChecked = p.EnableBlackBorders;
            StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
            MinimizeToTrayCheck.IsChecked = _settings.MinimizeToTrayOnClose;

            SetLeftPixels(p.LeftPixels);
            SetTopPixels(p.TopPixels);
            SetRightPixels(p.RightPixels);
            SetBottomPixels(p.BottomPixels);
        }
        finally
        {
            _isUpdating = false;
            UpdateDiagramPreview();
        }
    }

    private void SaveUIToProfile()
    {
        var p = _settings.CurrentProfile;
        p.LeftPixels = (int)LeftSlider.Value;
        p.TopPixels = (int)TopSlider.Value;
        p.RightPixels = (int)RightSlider.Value;
        p.BottomPixels = (int)BottomSlider.Value;
        p.UniformLinked = UniformLockCheck.IsChecked == true;
        p.EnableWorkAreaAdjustment = WorkAreaCheck.IsChecked == true;
        p.EnableBlackBorders = BlackBordersCheck.IsChecked == true;

        if (_engine.ActiveMonitor != null)
        {
            p.TargetMonitorDevice = _engine.ActiveMonitor.DeviceName;
        }

        _settings.Save();
    }

    private void MonitorSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MonitorSelector.SelectedItem is MonitorItem monitor)
        {
            _engine.SelectMonitor(monitor);

            // Update max slider bounds
            LeftSlider.Maximum = Math.Max(100, monitor.Width / 3);
            RightSlider.Maximum = Math.Max(100, monitor.Width / 3);
            TopSlider.Maximum = Math.Max(80, monitor.Height / 3);
            BottomSlider.Maximum = Math.Max(80, monitor.Height / 3);

            PreviewMonitorDetails.Text = $"Display: {monitor.Width} x {monitor.Height} ({(double)monitor.Width / monitor.Height:F2}:1)";
            UpdateDiagramPreview();
        }
    }

    #region Two-Way Sync (Pixels <-> Percentage)

    private void SetLeftPixels(int px)
    {
        int monitorW = _engine.ActiveMonitor?.Width ?? 1920;
        LeftSlider.Value = px;
        LeftPxText.Text = px.ToString();
        double pct = (px * 100.0) / monitorW;
        LeftPctText.Text = pct.ToString("F1");
    }

    private void SetTopPixels(int px)
    {
        int monitorH = _engine.ActiveMonitor?.Height ?? 1080;
        TopSlider.Value = px;
        TopPxText.Text = px.ToString();
        double pct = (px * 100.0) / monitorH;
        TopPctText.Text = pct.ToString("F1");
    }

    private void SetRightPixels(int px)
    {
        int monitorW = _engine.ActiveMonitor?.Width ?? 1920;
        RightSlider.Value = px;
        RightPxText.Text = px.ToString();
        double pct = (px * 100.0) / monitorW;
        RightPctText.Text = pct.ToString("F1");
    }

    private void SetBottomPixels(int px)
    {
        int monitorH = _engine.ActiveMonitor?.Height ?? 1080;
        BottomSlider.Value = px;
        BottomPxText.Text = px.ToString();
        double pct = (px * 100.0) / monitorH;
        BottomPctText.Text = pct.ToString("F1");
    }

    private void LeftSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;
        _isUpdating = true;
        try
        {
            int val = (int)Math.Round(e.NewValue);
            SetLeftPixels(val);
            if (UniformLockCheck.IsChecked == true)
            {
                ApplyUniformScaleFromPixels(val, isHorizontal: true);
            }
        }
        finally
        {
            _isUpdating = false;
            UpdateDiagramPreview();
        }
    }

    private void TopSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;
        _isUpdating = true;
        try
        {
            int val = (int)Math.Round(e.NewValue);
            SetTopPixels(val);
            if (UniformLockCheck.IsChecked == true)
            {
                ApplyUniformScaleFromPixels(val, isHorizontal: false);
            }
        }
        finally
        {
            _isUpdating = false;
            UpdateDiagramPreview();
        }
    }

    private void RightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;
        _isUpdating = true;
        try
        {
            int val = (int)Math.Round(e.NewValue);
            SetRightPixels(val);
            if (UniformLockCheck.IsChecked == true)
            {
                ApplyUniformScaleFromPixels(val, isHorizontal: true);
            }
        }
        finally
        {
            _isUpdating = false;
            UpdateDiagramPreview();
        }
    }

    private void BottomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;
        _isUpdating = true;
        try
        {
            int val = (int)Math.Round(e.NewValue);
            SetBottomPixels(val);
            if (UniformLockCheck.IsChecked == true)
            {
                ApplyUniformScaleFromPixels(val, isHorizontal: false);
            }
        }
        finally
        {
            _isUpdating = false;
            UpdateDiagramPreview();
        }
    }

    private void ApplyUniformScaleFromPixels(int px, bool isHorizontal)
    {
        int monW = _engine.ActiveMonitor?.Width ?? 1920;
        int monH = _engine.ActiveMonitor?.Height ?? 1080;
        double pct = isHorizontal ? (px * 100.0 / monW) : (px * 100.0 / monH);

        int pxX = (int)Math.Round(pct * monW / 100.0);
        int pxY = (int)Math.Round(pct * monH / 100.0);

        SetLeftPixels(pxX);
        SetRightPixels(pxX);
        SetTopPixels(pxY);
        SetBottomPixels(pxY);
    }

    private void LeftPxText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(LeftPxText.Text, out int v)) LeftSlider.Value = v;
    }

    private void TopPxText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TopPxText.Text, out int v)) TopSlider.Value = v;
    }

    private void RightPxText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(RightPxText.Text, out int v)) RightSlider.Value = v;
    }

    private void BottomPxText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(BottomPxText.Text, out int v)) BottomSlider.Value = v;
    }

    private void LeftPctText_LostFocus(object sender, RoutedEventArgs e)
    {
        int monW = _engine.ActiveMonitor?.Width ?? 1920;
        if (double.TryParse(LeftPctText.Text, out double pct)) LeftSlider.Value = (pct * monW) / 100.0;
    }

    private void TopPctText_LostFocus(object sender, RoutedEventArgs e)
    {
        int monH = _engine.ActiveMonitor?.Height ?? 1080;
        if (double.TryParse(TopPctText.Text, out double pct)) TopSlider.Value = (pct * monH) / 100.0;
    }

    private void RightPctText_LostFocus(object sender, RoutedEventArgs e)
    {
        int monW = _engine.ActiveMonitor?.Width ?? 1920;
        if (double.TryParse(RightPctText.Text, out double pct)) RightSlider.Value = (pct * monW) / 100.0;
    }

    private void BottomPctText_LostFocus(object sender, RoutedEventArgs e)
    {
        int monH = _engine.ActiveMonitor?.Height ?? 1080;
        if (double.TryParse(BottomPctText.Text, out double pct)) BottomSlider.Value = (pct * monH) / 100.0;
    }

    private void Input_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb)
        {
            var binding = tb.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();
            Keyboard.ClearFocus();
        }
    }

    private void UniformLockCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (UniformLockCheck.IsChecked == true)
        {
            ApplyUniformScaleFromPixels((int)LeftSlider.Value, isHorizontal: true);
        }
    }

    #endregion

    #region Visual Diagram Preview

    private void UpdateDiagramPreview()
    {
        int monW = _engine.ActiveMonitor?.Width ?? 1920;
        int monH = _engine.ActiveMonitor?.Height ?? 1080;

        int l = (int)LeftSlider.Value;
        int t = (int)TopSlider.Value;
        int r = (int)RightSlider.Value;
        int b = (int)BottomSlider.Value;

        int safeW = Math.Max(100, monW - l - r);
        int safeH = Math.Max(100, monH - t - b);

        PreviewResolutionText.Text = $"{safeW} x {safeH}\n(Safe Area)";

        // Badges
        PreviewLeftBadge.Text = $"{l}px ({(l * 100.0 / monW):F1}%)";
        PreviewRightBadge.Text = $"{r}px ({(r * 100.0 / monW):F1}%)";
        PreviewTopBadge.Text = $"{t}px ({(t * 100.0 / monH):F1}%)";
        PreviewBottomBadge.Text = $"{b}px ({(b * 100.0 / monH):F1}%)";

        // Map monitor dimensions to preview box (360 x 220)
        double scaleX = 360.0 / monW;
        double scaleY = 220.0 / monH;

        double previewMarginL = Math.Max(2, l * scaleX);
        double previewMarginT = Math.Max(2, t * scaleY);
        double previewMarginR = Math.Max(2, r * scaleX);
        double previewMarginB = Math.Max(2, b * scaleY);

        PreviewInnerBox.Margin = new Thickness(previewMarginL, previewMarginT, previewMarginR, previewMarginB);

        // Update live calibration window if open
        _engine.UpdateCalibrationBounds(l, t, r, b);
    }

    #endregion

    #region Presets & Actions

    private void PresetReset_Click(object sender, RoutedEventArgs e)
    {
        _isUpdating = true;
        try
        {
            SetLeftPixels(0);
            SetTopPixels(0);
            SetRightPixels(0);
            SetBottomPixels(0);
        }
        finally
        {
            _isUpdating = false;
            UpdateDiagramPreview();
        }
    }

    private void PresetMild_Click(object sender, RoutedEventArgs e) => ApplyPresetPercentage(2.5);
    private void PresetStandard_Click(object sender, RoutedEventArgs e) => ApplyPresetPercentage(5.0);
    private void PresetAggressive_Click(object sender, RoutedEventArgs e) => ApplyPresetPercentage(7.5);

    private void ApplyPresetPercentage(double pct)
    {
        int monW = _engine.ActiveMonitor?.Width ?? 1920;
        int monH = _engine.ActiveMonitor?.Height ?? 1080;

        _isUpdating = true;
        try
        {
            int pxX = (int)Math.Round(pct * monW / 100.0);
            int pxY = (int)Math.Round(pct * monH / 100.0);

            SetLeftPixels(pxX);
            SetRightPixels(pxX);
            SetTopPixels(pxY);
            SetBottomPixels(pxY);
        }
        finally
        {
            _isUpdating = false;
            UpdateDiagramPreview();
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyOverscan();
    }

    private void ApplyOverscan()
    {
        int l = (int)LeftSlider.Value;
        int t = (int)TopSlider.Value;
        int r = (int)RightSlider.Value;
        int b = (int)BottomSlider.Value;

        bool workArea = WorkAreaCheck.IsChecked == true;
        bool blackBorders = BlackBordersCheck.IsChecked == true;

        _engine.ApplyOverscan(l, t, r, b, workArea, blackBorders);
        SaveUIToProfile();

        StatusBadge.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(6, 78, 59));
        StatusBadge.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
        StatusText.Text = $"Active: {l}px L, {t}px T, {r}px R, {b}px B";
        StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(110, 231, 183));

        if (_trayIcon != null)
        {
            _trayIcon.Text = $"OverscanFix: Active ({l}px / {t}px)";
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ResetOverscan();
    }

    private void ResetOverscan()
    {
        _engine.RestoreDefaultWorkArea();

        StatusBadge.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 65, 85));
        StatusBadge.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139));
        StatusText.Text = "Status: Inactive (Full Screen)";
        StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225));

        if (_trayIcon != null)
        {
            _trayIcon.Text = "OverscanFix: Inactive";
        }
    }

    private void ToggleOverscan()
    {
        if (_engine.IsApplied)
        {
            ResetOverscan();
        }
        else
        {
            ApplyOverscan();
        }
    }

    private void CalibrationButton_Click(object sender, RoutedEventArgs e)
    {
        int l = (int)LeftSlider.Value;
        int t = (int)TopSlider.Value;
        int r = (int)RightSlider.Value;
        int b = (int)BottomSlider.Value;
        _engine.ShowCalibration(l, t, r, b);
    }

    private void FitWindowsButton_Click(object sender, RoutedEventArgs e)
    {
        int l = (int)LeftSlider.Value;
        int t = (int)TopSlider.Value;
        int r = (int)RightSlider.Value;
        int b = (int)BottomSlider.Value;
        _engine.FitAllWindows(l, t, r, b);
    }

    private void StartWithWindowsCheck_Changed(object sender, RoutedEventArgs e)
    {
        bool enable = StartWithWindowsCheck.IsChecked == true;
        _settings.StartWithWindows = enable;
        _engine.SetStartupRun(enable);
        _settings.Save();
    }

    private void MinimizeToTrayCheck_Changed(object sender, RoutedEventArgs e)
    {
        _settings.MinimizeToTrayOnClose = MinimizeToTrayCheck.IsChecked == true;
        _settings.Save();
    }

    #endregion

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_settings.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            Hide();
            _trayIcon?.ShowBalloonTip(1500, "OverscanFix", "Running in system tray. Double-click to open or press Ctrl+Alt+O.", WinForms.ToolTipIcon.Info);
        }
        else
        {
            _trayIcon?.Dispose();
            _engine.Dispose();
        }
    }
}