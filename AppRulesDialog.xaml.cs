using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace OverscanFix;

public partial class AppRulesDialog : Window
{
    private readonly AppSettings _settings;
    private readonly ObservableCollection<AppTriggerRule> _rules;

    public AppRulesDialog(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _rules = new ObservableCollection<AppTriggerRule>(_settings.TriggerRules);
        RulesGrid.ItemsSource = _rules;

        NewProfileCombo.ItemsSource = _settings.SavedProfiles.Select(p => p.Name).ToList();
        if (NewProfileCombo.Items.Count > 0)
        {
            NewProfileCombo.SelectedIndex = 0;
        }
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        string proc = NewProcText.Text.Trim();
        if (string.IsNullOrWhiteSpace(proc))
        {
            MessageBox.Show("Please enter an executable process name (e.g. 'RetroArch', 'vlc', 'chrome').", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string prof = NewProfileCombo.SelectedItem?.ToString() ?? "Default";
        var rule = new AppTriggerRule
        {
            ProcessName = proc,
            ProfileName = prof,
            AutoApply = true,
            RevertOnExit = true
        };

        _rules.Add(rule);
        NewProcText.Clear();
    }

    private void RemoveRule_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is AppTriggerRule selected)
        {
            _rules.Remove(selected);
        }
    }

    private void SaveClose_Click(object sender, RoutedEventArgs e)
    {
        _settings.TriggerRules = _rules.ToList();
        _settings.Save();
        DialogResult = true;
        Close();
    }
}
