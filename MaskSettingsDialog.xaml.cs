using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OverscanFix;

public partial class MaskSettingsDialog : Window
{
    private readonly OverscanProfile _profile;

    public MaskSettingsDialog(OverscanProfile profile)
    {
        InitializeComponent();
        _profile = profile;

        ColorHexText.Text = _profile.MaskColorHex;
        OpacitySlider.Value = _profile.MaskOpacity;
        UpdateColorPreview();
    }

    private void UpdateColorPreview()
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(ColorHexText.Text.Trim());
            ColorSample.Background = new SolidColorBrush(color);
        }
        catch
        {
            // Invalid color syntax
        }
    }

    private void ColorHexText_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateColorPreview();
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValueText != null)
        {
            OpacityValueText.Text = $"{(int)(e.NewValue * 100)}%";
        }
    }

    private void ColorPreset_Black(object sender, RoutedEventArgs e) => ColorHexText.Text = "#000000";
    private void ColorPreset_Slate(object sender, RoutedEventArgs e) => ColorHexText.Text = "#0F172A";
    private void ColorPreset_Blue(object sender, RoutedEventArgs e) => ColorHexText.Text = "#030712";

    private void SaveClose_Click(object sender, RoutedEventArgs e)
    {
        _profile.MaskColorHex = ColorHexText.Text.Trim();
        _profile.MaskOpacity = OpacitySlider.Value;
        DialogResult = true;
        Close();
    }
}
