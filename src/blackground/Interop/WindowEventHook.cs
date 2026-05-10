using System;

namespace blackground.Interop;

public sealed class WindowEventHook : IDisposable
{
    public sealed class WinEventArgs(uint eventType, IntPtr hwnd) : EventArgs
    {
        public uint EventType { get; } = eventType;
        public IntPtr Hwnd { get; } = hwnd;
    }

    private NativeMethods.WinEventDelegate? _proc;
    private IntPtr _foregroundHook;
    private IntPtr _destroyHook;
    private IntPtr _minimizeHook;

    public event EventHandler<WinEventArgs>? ForegroundChanged;
    public event EventHandler<WinEventArgs>? WindowDestroyed;
    public event EventHandler<WinEventArgs>? WindowMinimized;

    public bool Install()
    {
        if (_proc != null) return true;
        _proc = WinEventProc;

        _foregroundHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _proc, 0, 0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        _destroyHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_DESTROY, NativeMethods.EVENT_OBJECT_DESTROY,
            IntPtr.Zero, _proc, 0, 0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        _minimizeHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_MINIMIZESTART, NativeMethods.EVENT_SYSTEM_MINIMIZESTART,
            IntPtr.Zero, _proc, 0, 0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        return _foregroundHook != IntPtr.Zero;
    }

    public void Uninstall()
    {
        if (_foregroundHook != IntPtr.Zero) { NativeMethods.UnhookWinEvent(_foregroundHook); _foregroundHook = IntPtr.Zero; }
        if (_destroyHook != IntPtr.Zero) { NativeMethods.UnhookWinEvent(_destroyHook); _destroyHook = IntPtr.Zero; }
        if (_minimizeHook != IntPtr.Zero) { NativeMethods.UnhookWinEvent(_minimizeHook); _minimizeHook = IntPtr.Zero; }
        _proc = null;
    }

    private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // We only care about top-level window events (idObject == OBJID_WINDOW = 0).
        if (idObject != 0) return;

        var args = new WinEventArgs(eventType, hwnd);
        try
        {
            switch (eventType)
            {
                case NativeMethods.EVENT_SYSTEM_FOREGROUND: ForegroundChanged?.Invoke(this, args); break;
                case NativeMethods.EVENT_OBJECT_DESTROY: WindowDestroyed?.Invoke(this, args); break;
                case NativeMethods.EVENT_SYSTEM_MINIMIZESTART: WindowMinimized?.Invoke(this, args); break;
            }
        }
        catch
        {
            // Never throw out of a WinEvent callback.
        }
    }

    public void Dispose() => Uninstall();
}
