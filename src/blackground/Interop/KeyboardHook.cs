using System;
using System.Runtime.InteropServices;

namespace blackground.Interop;

public sealed class KeyboardHook : IDisposable
{
    public sealed class KeyEventArgs(int vkCode) : EventArgs
    {
        public int VkCode { get; } = vkCode;
    }

    private NativeMethods.HookProc? _proc;
    private IntPtr _hookHandle;

    public event EventHandler<KeyEventArgs>? KeyDown;

    public bool Install()
    {
        if (_hookHandle != IntPtr.Zero) return true;
        _proc = HookCallback;
        var module = NativeMethods.GetModuleHandle(null);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, module, 0);
        return _hookHandle != IntPtr.Zero;
    }

    public void Uninstall()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
            _proc = null;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
            {
                var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                try
                {
                    KeyDown?.Invoke(this, new KeyEventArgs((int)data.vkCode));
                }
                catch
                {
                    // Never throw out of a low-level hook.
                }
            }
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
