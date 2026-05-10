using System;
using System.Runtime.InteropServices;

namespace blackground.Interop;

public sealed class MouseHook : IDisposable
{
    public sealed class GlobalClickEventArgs(int x, int y, int messageId) : EventArgs
    {
        public int X { get; } = x;
        public int Y { get; } = y;
        public int MessageId { get; } = messageId;

        // Set to true to swallow the click (and its matching button-up) so it never
        // reaches any window — prevents activation of whatever was clicked.
        public bool Handled { get; set; }
    }

    private NativeMethods.HookProc? _proc;
    private IntPtr _hookHandle;

    private bool _swallowLeftUp;
    private bool _swallowRightUp;
    private bool _swallowMiddleUp;

    public event EventHandler<GlobalClickEventArgs>? GlobalClick;

    public bool Install()
    {
        if (_hookHandle != IntPtr.Zero) return true;
        _proc = HookCallback;
        var module = NativeMethods.GetModuleHandle(null);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, module, 0);
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
        _swallowLeftUp = _swallowRightUp = _swallowMiddleUp = false;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();

            if (msg == NativeMethods.WM_LBUTTONDOWN ||
                msg == NativeMethods.WM_RBUTTONDOWN ||
                msg == NativeMethods.WM_MBUTTONDOWN)
            {
                var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                var args = new GlobalClickEventArgs(data.pt.X, data.pt.Y, msg);
                try { GlobalClick?.Invoke(this, args); }
                catch { /* never throw out of a low-level hook */ }

                if (args.Handled)
                {
                    if      (msg == NativeMethods.WM_LBUTTONDOWN) _swallowLeftUp = true;
                    else if (msg == NativeMethods.WM_RBUTTONDOWN) _swallowRightUp = true;
                    else if (msg == NativeMethods.WM_MBUTTONDOWN) _swallowMiddleUp = true;
                    return new IntPtr(1);
                }
            }
            else if (msg == NativeMethods.WM_LBUTTONUP && _swallowLeftUp)
            {
                _swallowLeftUp = false;
                return new IntPtr(1);
            }
            else if (msg == NativeMethods.WM_RBUTTONUP && _swallowRightUp)
            {
                _swallowRightUp = false;
                return new IntPtr(1);
            }
            else if (msg == NativeMethods.WM_MBUTTONUP && _swallowMiddleUp)
            {
                _swallowMiddleUp = false;
                return new IntPtr(1);
            }
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
