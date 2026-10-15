using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    private string _latestReleaseUrl = "https://github.com/VpkDevs/overscanFixer/releases";

    private AppWatcher? _appWatcher;
    private OledOrbitManager? _oledManager;

    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        VersionBadgeText.Text = $"v{UpdateChecker.GetCurrentVersionString()} PRO";

        InitializeSystemTray();
        RegisterGlobalHotkey();
        LoadMonitors();
        LoadProfilesDropdown();

        // Initialize background managers
        _oledManager = new OledOrbitManager(_engine, _settings);
        _oledManager.Start();

        _appWatcher = new AppWatcher(_settings, (profileName, isRevert) =>
        {
            Dispatcher.Invoke(() =>
            {
                var prof = _settings.SavedProfiles.FirstOrDefault(p => p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));
                if (prof != null)
                {
                    _settings.ActiveProfileName = prof.Name;
                    LoadProfileIntoUI(prof);
                    ApplyOverscan();
                    if (_trayIcon != null)
                    {
                        string msg = isRevert ? $"Reverted to '{profileName}'" : $"Auto-applied '{profileName}' for active window";
                        _trayIcon.ShowBalloonTip(1000, "OverscanFix Auto-Trigger", msg, WinForms.ToolTipIcon.Info);
                    }
                }
            });
        });
        _appWatcher.Start();

        // Check if started minimized (boot or CLI flag)
        if (_settings.StartMinimized || Environment.GetCommandLineArgs().Contains("--minimized"))
        {
            Hide();
            if (_settings.CurrentProfile.EnableWorkAreaAdjustment || _settings.CurrentProfile.EnableBlackBorders)
            {
                await _engine.ApplyOverscanWithResilienceAsync(
                    _settings.CurrentProfile.LeftPixels,
                    _settings.CurrentProfile.TopPixels,
                    _settings.CurrentProfile.RightPixels,
                    _settings.CurrentProfile.BottomPixels,
                    _settings.CurrentProfile.EnableWorkAreaAdjustment,
                    _settings.CurrentProfile.EnableBlackBorders,
                    _settings.CurrentProfile.MaskColorHex,
                    _settings.CurrentProfile.MaskOpacity);
                UpdateTrayStatusText(isActive: true);
            }
        }

        // Auto-updater check stub (runs asynchronously in background without blocking)
        if (_settings.CheckForUpdatesOnStartup)
        {
            _ = CheckForUpdatesInBackgroundAsync();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var helper = new WindowInteropHelper(this);
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(WndProcHook);
    }

    private IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            ToggleOverscan();
            handled = true;
        }
        else if (msg == NativeMethods.WM_DISPLAYCHANGE)
        {
            Logger.Info("WM_DISPLAYCHANGE received; re-querying monitors.");
            Dispatcher.Invoke(() =>
            {
                LoadMonitors();
                if (_engine.IsApplied)
                {
                    ApplyOverscan();
                }
            });
        }
        return IntPtr.Zero;
    }

    #region Auto-Updater

    private async Task CheckForUpdatesInBackgroundAsync()
    {
        try
        {
            var updateResult = await UpdateChecker.CheckForUpdatesAsync();
            if (updateResult != null && updateResult.IsUpdateAvailable)
            {
                _latestReleaseUrl = updateResult.HtmlUrl;
                Dispatcher.Invoke(() =>
                {
                    UpdateNotificationText.Text = $"v{updateResult.LatestVersion} is available! (Current: v{updateResult.CurrentVersion})";
                    UpdateNotificationBar.Visibility = Visibility.Visible;
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Background update check error: {ex.Message}");
        }
    }

    private void UpdateReleaseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _latestReleaseUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Could not open release URL", ex);
        }
    }

    private void DismissUpdateBanner_Click(object sender, RoutedEventArgs e)
    {
        UpdateNotificationBar.Visibility = Visibility.Collapsed;
    }

    #endregion

    #region System Tray & Context Menu

    private void InitializeSystemTray()
    {
        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = DrawingIcon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? SystemIcons.Application,
            Visible = true,
            Text = "OverscanFix - Display Overscan Manager"
        };

        RebuildTrayContextMenu();
        _trayIcon.DoubleClick += (s, e) => ShowAndRestore();
    }

    private void RebuildTrayContextMenu()
    {
        if (_trayIcon == null) return;

        var menu = new WinForms.ContextMenuStrip();

        // 1. Open Window
        var openItem = new WinForms.ToolStripMenuItem("Open Controls");
        openItem.Font = new System.Drawing.Font(openItem.Font, System.Drawing.FontStyle.Bold);
        openItem.Click += (s, e) => ShowAndRestore();
        menu.Items.Add(openItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // 2. Toggle Status Item
        string toggleText = _engine.IsApplied ? "Disable Overscan (Reset)" : "Enable Overscan";
        var toggleItem = new WinForms.ToolStripMenuItem(toggleText, null, (s, e) => ToggleOverscan());
        menu.Items.Add(toggleItem);

        // 3. Profiles Submenu
        var profilesSubmenu = new WinForms.ToolStripMenuItem("Switch Profile");
        foreach (var p in _settings.SavedProfiles)
        {
            var pItem = new WinForms.ToolStripMenuItem(p.Name, null, (s, e) =>
            {
                ApplyNamedProfile(p);
            });
            if (p.Name.Equals(_settings.ActiveProfileName, StringComparison.OrdinalIgnoreCase))
            {
                pItem.Checked = true;
            }
            profilesSubmenu.DropDownItems.Add(pItem);
        }
        menu.Items.Add(profilesSubmenu);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // 4. Calibration & Reset Quick Actions
        menu.Items.Add("Run Calibration Overlay", null, (s, e) =>
        {
            ShowAndRestore();
            CalibrationButton_Click(this, new RoutedEventArgs());
        });
        menu.Items.Add("Reset to Fullscreen", null, (s, e) => ResetOverscan());

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // 5. Exit
        menu.Items.Add("Exit", null, (s, e) =>
        {
            _settings.MinimizeToTrayOnClose = false;
            Close();
        });

        _trayIcon.ContextMenuStrip = menu;
    }

    private void UpdateTrayStatusText(bool isActive)
    {
        if (_trayIcon == null) return;

        string profName = _settings.ActiveProfileName;
        int l = _settings.CurrentProfile.LeftPixels;
        int t = _settings.CurrentProfile.TopPixels;
        int r = _settings.CurrentProfile.RightPixels;
        int b = _settings.CurrentProfile.BottomPixels;

        string tooltip = isActive
            ? $"OverscanFix: [{profName}] L{l} T{t} R{r} B{b}"
            : "OverscanFix: Inactive (Full Screen)";

        if (tooltip.Length > 63) tooltip = tooltip.Substring(0, 60) + "...";
        _trayIcon.Text = tooltip;

        RebuildTrayContextMenu();
    }

    private void ShowAndRestore()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    #endregion

    #region Global Hotkey

    private void RegisterGlobalHotkey()
    {
        var helper = new WindowInteropHelper(this);
        // Register Ctrl + Alt + O
        NativeMethods.RegisterHotKey(helper.Handle, HOTKEY_ID,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, 0x4F); // 0x4F = 'O'
    }

    #endregion

    #region Monitors & Profiles Management

    private void LoadMonitors()
    {
        var monitors = _engine.GetAvailableMonitors();
        MonitorSelector.ItemsSource = monitors;

        if (monitors.Count > 0)
        {
            int selectedIndex = 0;
            if (!string.IsNullOrEmpty(_settings.CurrentProfile.TargetMonitorDevice))
            {
                int idx = monitors.FindIndex(m => m.DeviceName.Equals(_settings.CurrentProfile.TargetMonitorDevice, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) selectedIndex = idx;
            }
            MonitorSelector.SelectedIndex = selectedIndex;
        }
    }

    private void LoadProfilesDropdown()
    {
        ProfileSelector.ItemsSource = null;
        ProfileSelector.ItemsSource = _settings.SavedProfiles.Select(p => p.Name).ToList();

        int activeIdx = _settings.SavedProfiles.FindIndex(p => p.Name.Equals(_settings.ActiveProfileName, StringComparison.OrdinalIgnoreCase));
        ProfileSelector.SelectedIndex = activeIdx >= 0 ? activeIdx : 0;
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

            PreviewMonitorDetails.Text = $"Display: {monitor.DisplayName}";

            // Load per-monitor profile if stored
            var monitorProf = _settings.GetProfileForMonitor(monitor.DeviceName);
            LoadProfileIntoUI(monitorProf);
            UpdateDiagramPreview();
        }
    }

    private void ProfileSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;
        if (ProfileSelector.SelectedItem is string profName)
        {
            var prof = _settings.SavedProfiles.FirstOrDefault(p => p.Name.Equals(profName, StringComparison.OrdinalIgnoreCase));
            if (prof != null)
            {
                ApplyNamedProfile(prof);
            }
        }
    }

    private void ApplyNamedProfile(OverscanProfile p)
    {
        _settings.ActiveProfileName = p.Name;
        LoadProfileIntoUI(p);
        ApplyOverscan();
    }

    private void SaveCustomProfileButton_Click(object sender, RoutedEventArgs e)
    {
        string currentMonitor = _engine.ActiveMonitor?.DeviceName ?? "Display";
        string defaultName = $"Custom {_settings.SavedProfiles.Count + 1}";

        var newProfile = new OverscanProfile
        {
            Name = defaultName,
            TargetMonitorDevice = currentMonitor,
            MonitorModel = _engine.ActiveMonitor?.FriendlyModel ?? "",
            LeftPixels = (int)LeftSlider.Value,
            TopPixels = (int)TopSlider.Value,
            RightPixels = (int)RightSlider.Value,
            BottomPixels = (int)BottomSlider.Value,
            UniformLinked = UniformLockCheck.IsChecked == true,
            EnableWorkAreaAdjustment = WorkAreaCheck.IsChecked == true,
            EnableBlackBorders = BlackBordersCheck.IsChecked == true,
            MaskColorHex = _settings.CurrentProfile.MaskColorHex,
            MaskOpacity = _settings.CurrentProfile.MaskOpacity
        };

        _settings.SavedProfiles.Add(newProfile);
        _settings.ActiveProfileName = newProfile.Name;
        _settings.Save();

        LoadProfilesDropdown();
        RebuildTrayContextMenu();

        MessageBox.Show($"Profile '{newProfile.Name}' saved successfully!", "OverscanFix", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void AppRulesButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AppRulesDialog(_settings) { Owner = this };
        dlg.ShowDialog();
    }

    private void OledSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OledSettingsDialog(_settings) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _oledManager?.Start();
        }
    }

    private void MaskSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new MaskSettingsDialog(_settings.CurrentProfile) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _settings.Save();
            if (_engine.IsApplied && BlackBordersCheck.IsChecked == true)
            {
                ApplyOverscan();
            }
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
            CheckUpdatesCheck.IsChecked = _settings.CheckForUpdatesOnStartup;

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
        p.Name = _settings.ActiveProfileName;
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
            p.MonitorModel = _engine.ActiveMonitor.FriendlyModel;
            if (AutoSaveMonitorCheck.IsChecked == true)
            {
                _settings.SetProfileForMonitor(_engine.ActiveMonitor.DeviceName, p.Clone(p.Name));
            }
        }

        var existing = _settings.SavedProfiles.FirstOrDefault(s => s.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.LeftPixels = p.LeftPixels;
            existing.TopPixels = p.TopPixels;
            existing.RightPixels = p.RightPixels;
            existing.BottomPixels = p.BottomPixels;
            existing.UniformLinked = p.UniformLinked;
            existing.EnableWorkAreaAdjustment = p.EnableWorkAreaAdjustment;
            existing.EnableBlackBorders = p.EnableBlackBorders;
            existing.MaskColorHex = p.MaskColorHex;
            existing.MaskOpacity = p.MaskOpacity;
        }

        _settings.Save();
        RebuildTrayContextMenu();
    }

    #endregion

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

    private void ApplyUniformScaleFromPixels(int basePx, bool isHorizontal)
    {
        int monW = _engine.ActiveMonitor?.Width ?? 1920;
        int monH = _engine.ActiveMonitor?.Height ?? 1080;

        double pct = isHorizontal ? (basePx * 100.0 / monW) : (basePx * 100.0 / monH);

        int pxX = (int)Math.Round(pct * monW / 100.0);
        int pxY = (int)Math.Round(pct * monH / 100.0);

        SetLeftPixels(pxX);
        SetRightPixels(pxX);
        SetTopPixels(pxY);
        SetBottomPixels(pxY);
    }

    private void UniformLockCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (UniformLockCheck.IsChecked == true)
        {
            int currentLeft = (int)LeftSlider.Value;
            _isUpdating = true;
            try
            {
                ApplyUniformScaleFromPixels(currentLeft, isHorizontal: true);
            }
            finally
            {
                _isUpdating = false;
                UpdateDiagramPreview();
            }
        }
    }

    private void LeftPxText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(LeftPxText.Text, out int val))
        {
            val = Math.Clamp(val, 0, (int)LeftSlider.Maximum);
            LeftSlider.Value = val;
        }
    }

    private void TopPxText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TopPxText.Text, out int val))
        {
            val = Math.Clamp(val, 0, (int)TopSlider.Maximum);
            TopSlider.Value = val;
        }
    }

    private void RightPxText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(RightPxText.Text, out int val))
        {
            val = Math.Clamp(val, 0, (int)RightSlider.Maximum);
            RightSlider.Value = val;
        }
    }

    private void BottomPxText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(BottomPxText.Text, out int val))
        {
            val = Math.Clamp(val, 0, (int)BottomSlider.Maximum);
            BottomSlider.Value = val;
        }
    }

    private void LeftPctText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(LeftPctText.Text, out double pct))
        {
            int monW = _engine.ActiveMonitor?.Width ?? 1920;
            int px = (int)Math.Round(pct * monW / 100.0);
            LeftSlider.Value = Math.Clamp(px, 0, (int)LeftSlider.Maximum);
        }
    }

    private void TopPctText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(TopPctText.Text, out double pct))
        {
            int monH = _engine.ActiveMonitor?.Height ?? 1080;
            int px = (int)Math.Round(pct * monH / 100.0);
            TopSlider.Value = Math.Clamp(px, 0, (int)TopSlider.Maximum);
        }
    }

    private void RightPctText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(RightPctText.Text, out double pct))
        {
            int monW = _engine.ActiveMonitor?.Width ?? 1920;
            int px = (int)Math.Round(pct * monW / 100.0);
            RightSlider.Value = Math.Clamp(px, 0, (int)RightSlider.Maximum);
        }
    }

    private void BottomPctText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(BottomPctText.Text, out double pct))
        {
            int monH = _engine.ActiveMonitor?.Height ?? 1080;
            int px = (int)Math.Round(pct * monH / 100.0);
            BottomSlider.Value = Math.Clamp(px, 0, (int)BottomSlider.Maximum);
        }
    }

    private void Input_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                tb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }
    }

    #endregion

    #region Visual Diagram Update

    private void UpdateDiagramPreview()
    {
        if (PreviewOuterBorder == null || PreviewInnerBox == null) return;

        int monW = _engine.ActiveMonitor?.Width ?? 1920;
        int monH = _engine.ActiveMonitor?.Height ?? 1080;

        int l = (int)LeftSlider.Value;
        int t = (int)TopSlider.Value;
        int r = (int)RightSlider.Value;
        int b = (int)BottomSlider.Value;

        double outerW = PreviewOuterBorder.Width;
        double outerH = PreviewOuterBorder.Height;

        double scaleX = outerW / monW;
        double scaleY = outerH / monH;

        double marginL = Math.Max(2, l * scaleX);
        double marginT = Math.Max(2, t * scaleY);
        double marginR = Math.Max(2, r * scaleX);
        double marginB = Math.Max(2, b * scaleY);

        PreviewInnerBox.Margin = new Thickness(marginL, marginT, marginR, marginB);

        int safeW = monW - l - r;
        int safeH = monH - t - b;
        PreviewResolutionText.Text = $"{safeW} x {safeH}\n(Safe Area)";

        PreviewLeftBadge.Text = $"{l} px";
        PreviewRightBadge.Text = $"{r} px";
        PreviewTopBadge.Text = $"{t} px";
        PreviewBottomBadge.Text = $"{b} px";

        if (_engine.IsApplied)
        {
            _engine.UpdateCalibrationBounds(l, t, r, b);
        }
    }

    #endregion

    #region Presets and Actions

    private void PresetReset_Click(object sender, RoutedEventArgs e) => ApplyUniformPercentage(0.0);
    private void PresetMild_Click(object sender, RoutedEventArgs e) => ApplyUniformPercentage(2.5);
    private void PresetStandard_Click(object sender, RoutedEventArgs e) => ApplyUniformPercentage(5.0);
    private void PresetAggressive_Click(object sender, RoutedEventArgs e) => ApplyUniformPercentage(7.5);

    private void PresetRetro_Click(object sender, RoutedEventArgs e)
    {
        var retro = _settings.SavedProfiles.FirstOrDefault(p => p.Name.Contains("Retro", StringComparison.OrdinalIgnoreCase));
        if (retro != null) ApplyNamedProfile(retro);
    }

    private void PresetCinema_Click(object sender, RoutedEventArgs e)
    {
        var cinema = _settings.SavedProfiles.FirstOrDefault(p => p.Name.Contains("Cinema", StringComparison.OrdinalIgnoreCase));
        if (cinema != null) ApplyNamedProfile(cinema);
    }

    private void ApplyUniformPercentage(double pct)
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

        _engine.ApplyOverscan(l, t, r, b, workArea, blackBorders,
            _settings.CurrentProfile.MaskColorHex,
            _settings.CurrentProfile.MaskOpacity);
        SaveUIToProfile();

        StatusBadge.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(6, 78, 59));
        StatusBadge.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
        StatusText.Text = $"Active: {l}px L, {t}px T, {r}px R, {b}px B";
        StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(110, 231, 183));

        UpdateTrayStatusText(isActive: true);
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

        UpdateTrayStatusText(isActive: false);
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
        StartupManager.ConfigureStartup(enable, _settings.UseScheduledTaskStartup);
        _settings.Save();
    }

    private void MinimizeToTrayCheck_Changed(object sender, RoutedEventArgs e)
    {
        _settings.MinimizeToTrayOnClose = MinimizeToTrayCheck.IsChecked == true;
        _settings.Save();
    }

    private void CheckUpdatesCheck_Changed(object sender, RoutedEventArgs e)
    {
        _settings.CheckForUpdatesOnStartup = CheckUpdatesCheck.IsChecked == true;
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
            _appWatcher?.Dispose();
            _oledManager?.Dispose();
            _trayIcon?.Dispose();
            _engine.Dispose();
        }
    }
}