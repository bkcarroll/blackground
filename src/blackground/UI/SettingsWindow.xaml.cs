using System;
using System.Windows;
using blackground.Settings;
using blackground.Startup;

namespace blackground.UI;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _working;

    /// <summary>
    /// Invoked when the user clicks Save with valid settings. Receives the new settings.
    /// Return null on success, or an error string to display inline (e.g. hotkey conflict).
    /// </summary>
    public Func<AppSettings, string?>? Apply { get; set; }

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
        _working = current.Clone();
        LoadIntoUi(_working);
        OpacitySlider.ValueChanged += (_, _) => UpdateOpacityLabel();
    }

    private void LoadIntoUi(AppSettings s)
    {
        HotkeyBox.Hotkey = s.Hotkey;
        ScopeActive.IsChecked = s.MonitorScope == MonitorScope.ActiveWindow;
        ScopeAll.IsChecked = s.MonitorScope == MonitorScope.AllMonitors;
        OpacitySlider.Value = s.Opacity;
        StartWithWindowsCheck.IsChecked = s.StartWithWindows;
        UpdateOpacityLabel();
        HotkeyError.Visibility = Visibility.Collapsed;
        StatusLabel.Text = string.Empty;
    }

    private void UpdateOpacityLabel()
    {
        OpacityLabel.Text = $"{(int)Math.Round(OpacitySlider.Value * 100)}%";
    }

    private AppSettings BuildFromUi()
    {
        return new AppSettings
        {
            Hotkey = HotkeyBox.Hotkey ?? HotkeyDefinition.Default,
            MonitorScope = ScopeAll.IsChecked == true ? MonitorScope.AllMonitors : MonitorScope.ActiveWindow,
            Opacity = OpacitySlider.Value,
            StartWithWindows = StartWithWindowsCheck.IsChecked == true,
        };
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var built = BuildFromUi();
        if (built.Hotkey is null || !built.Hotkey.IsValid)
        {
            HotkeyError.Text = "Please choose a hotkey with at least one modifier and a regular key.";
            HotkeyError.Visibility = Visibility.Visible;
            return;
        }
        HotkeyError.Visibility = Visibility.Collapsed;

        var error = Apply?.Invoke(built);
        if (!string.IsNullOrEmpty(error))
        {
            HotkeyError.Text = error;
            HotkeyError.Visibility = Visibility.Visible;
            StatusLabel.Text = string.Empty;
            return;
        }

        // Apply Start-with-Windows last so a hotkey failure doesn't leave a stale registry entry.
        StartupRegistration.Set(built.StartWithWindows);

        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        LoadIntoUi(AppSettings.Defaults());
        StatusLabel.Text = "Defaults loaded — click Save to apply.";
    }
}
