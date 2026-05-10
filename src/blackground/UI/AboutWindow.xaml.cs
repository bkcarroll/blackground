using System.Windows;
using blackground.Settings;

namespace blackground.UI;

public partial class AboutWindow : Window
{
    public AboutWindow(HotkeyDefinition currentHotkey)
    {
        InitializeComponent();
        HotkeyText.Text = currentHotkey?.ToString() ?? "(none)";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
