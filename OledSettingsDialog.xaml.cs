using System;
using System.Windows;

namespace OverscanFix;

public partial class OledSettingsDialog : Window
{
    private readonly AppSettings _settings;

    public OledSettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        EnableOrbitCheck.IsChecked = _settings.EnableOledPixelOrbit;
        OrbitIntervalText.Text = _settings.OledOrbitIntervalMinutes.ToString();
        OrbitMaxShiftText.Text = _settings.OledOrbitMaxShiftPx.ToString();
    }

    private void SaveClose_Click(object sender, RoutedEventArgs e)
    {
        _settings.EnableOledPixelOrbit = EnableOrbitCheck.IsChecked == true;
        if (int.TryParse(OrbitIntervalText.Text, out int interval))
        {
            _settings.OledOrbitIntervalMinutes = Math.Clamp(interval, 1, 120);
        }
        if (int.TryParse(OrbitMaxShiftText.Text, out int shift))
        {
            _settings.OledOrbitMaxShiftPx = Math.Clamp(shift, 1, 16);
        }

        _settings.Save();
        DialogResult = true;
        Close();
    }
}
