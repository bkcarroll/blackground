using System;
using System.Windows;
using System.Windows.Interop;
using blackground.Interop;

namespace blackground.Overlay;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        // Manual placement: avoid WPF dpi-scaling — values we set are device pixels.
        WindowStartupLocation = WindowStartupLocation.Manual;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        ex |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));
    }

    public IntPtr Hwnd => new WindowInteropHelper(this).Handle;

    /// <summary>Move/size and show this overlay using device-pixel coordinates without activating it.</summary>
    public void ShowAtMonitor(NativeMethods.RECT rcMonitor)
    {
        if (!IsLoaded)
        {
            // Set initial bounds before WPF auto-sizes; WPF expects DIPs but for SetWindowPos we use pixels.
            // Easiest: just call Show() then SetWindowPos.
            Left = 0; Top = 0; Width = 1; Height = 1;
        }
        if (!IsVisible) Show();

        var hwnd = Hwnd;
        NativeMethods.SetWindowPos(
            hwnd, NativeMethods.HWND_TOP,
            rcMonitor.Left, rcMonitor.Top, rcMonitor.Width, rcMonitor.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOOWNERZORDER);
    }

    /// <summary>Place this overlay just below the given foreground hwnd in z-order.</summary>
    public void PlaceBehind(IntPtr hwndForeground)
    {
        var hwnd = Hwnd;
        if (hwnd == IntPtr.Zero || hwndForeground == IntPtr.Zero) return;
        NativeMethods.SetWindowPos(
            hwnd, hwndForeground, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER);
    }

    public void HideOverlay()
    {
        if (IsVisible) Hide();
    }
}
