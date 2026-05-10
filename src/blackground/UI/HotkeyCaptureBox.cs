using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TextBox = System.Windows.Controls.TextBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextCompositionEventArgs = System.Windows.Input.TextCompositionEventArgs;
using blackground.Settings;
using blackground.Interop;

namespace blackground.UI;

/// <summary>
/// A TextBox that captures global-style key combos. Click into it, press a combo, the value updates.
/// Lone modifier presses are rejected.
/// </summary>
public class HotkeyCaptureBox : TextBox
{
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey), typeof(HotkeyDefinition), typeof(HotkeyCaptureBox),
        new FrameworkPropertyMetadata(HotkeyDefinition.Default,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyChanged));

    public HotkeyDefinition Hotkey
    {
        get => (HotkeyDefinition)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    private const string PlaceholderText = "Press Hotkey";
    private static readonly System.Windows.Media.Brush PlaceholderBrush =
        new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x99, 0x99, 0x99));

    public HotkeyCaptureBox()
    {
        IsReadOnly = true;
        Cursor = System.Windows.Input.Cursors.IBeam;
        ToolTip = "Click here, then press the desired key combination.";
        UpdateText();
    }

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((HotkeyCaptureBox)d).UpdateText();
    }

    private void UpdateText()
    {
        Text = Hotkey?.ToString() ?? "(none)";
        ClearValue(ForegroundProperty);
        IsReadOnlyCaretVisible = false;
    }

    private void ShowPlaceholder()
    {
        Foreground = PlaceholderBrush;
        Text = PlaceholderText;
        CaretIndex = 0;
        IsReadOnlyCaretVisible = true;
    }

    protected override void OnGotKeyboardFocus(System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        ShowPlaceholder();
    }

    protected override void OnLostKeyboardFocus(System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        UpdateText();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore lone modifier presses.
        if (key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin)
        {
            return;
        }

        var mods = HotkeyModifiers.None;
        var k = Keyboard.Modifiers;
        if ((k & ModifierKeys.Control) != 0) mods |= HotkeyModifiers.Control;
        if ((k & ModifierKeys.Alt) != 0) mods |= HotkeyModifiers.Alt;
        if ((k & ModifierKeys.Shift) != 0) mods |= HotkeyModifiers.Shift;
        if ((k & ModifierKeys.Windows) != 0) mods |= HotkeyModifiers.Win;

        if (mods == HotkeyModifiers.None) return; // Require at least one modifier.

        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0) return;

        Hotkey = new HotkeyDefinition(mods, vk);
        // The DP setter is a no-op when the value is equal (value-based Equals),
        // so OnHotkeyChanged won't fire and the placeholder would remain. Force a refresh.
        UpdateText();
    }

    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        e.Handled = true;
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        // Lock the caret at index 0. Without this, the base TextBox places the caret
        // wherever the user clicks. We still want the click to move keyboard focus to us,
        // so handle that explicitly before swallowing the event.
        if (!IsKeyboardFocused) Focus();
        CaretIndex = 0;
        e.Handled = true;
    }

    protected override void OnSelectionChanged(RoutedEventArgs e)
    {
        base.OnSelectionChanged(e);
        if (SelectionStart != 0 || SelectionLength != 0) Select(0, 0);
    }
}
