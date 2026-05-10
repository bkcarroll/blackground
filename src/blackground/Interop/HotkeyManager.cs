using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using blackground.Settings;

namespace blackground.Interop;

public sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 0x0BB1; // arbitrary

    private readonly HwndSource _source;
    private bool _registered;
    private HotkeyDefinition? _current;

    public event EventHandler? HotkeyPressed;

    public HotkeyManager()
    {
        // Create a message-only window to receive WM_HOTKEY.
        var parameters = new HwndSourceParameters("blackgroundHotkeySink")
        {
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
            Width = 0,
            Height = 0,
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public IntPtr Handle => _source.Handle;
    public HotkeyDefinition? CurrentHotkey => _current;

    /// <summary>Register the given hotkey. Returns true on success; on failure, error contains a user-facing message.</summary>
    public bool TryRegister(HotkeyDefinition hotkey, out string? error)
    {
        error = null;
        Unregister();
        if (!hotkey.IsValid)
        {
            error = "Hotkey is not valid.";
            return false;
        }

        var ok = NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, (uint)hotkey.Modifiers | NativeMethods.MOD_NOREPEAT, hotkey.VirtualKey);
        if (!ok)
        {
            var err = Marshal.GetLastWin32Error();
            error = $"Failed to register hotkey '{hotkey}'. It may be in use by another application (Win32 error {err}).";
            _registered = false;
            _current = null;
            return false;
        }
        _registered = true;
        _current = hotkey;
        return true;
    }

    public void Unregister()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
            _current = null;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
