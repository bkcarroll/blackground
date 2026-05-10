using System;
using blackground.Interop;

namespace blackground.Tracking;

/// <summary>
/// App-wide tracker that remembers the most recent non-shell, non-own-process foreground hwnd
/// so the controller has a fallback when the user presses the hotkey while the taskbar (or other
/// shell surface) holds foreground focus.
/// </summary>
public sealed class ForegroundTracker : IDisposable
{
    private NativeMethods.WinEventDelegate? _proc;
    private IntPtr _foregroundHook;
    private IntPtr _destroyHook;
    private readonly uint _ownPid;

    public IntPtr LastUserForegroundHwnd { get; private set; }

    public ForegroundTracker()
    {
        _ownPid = (uint)Environment.ProcessId;
        // Pre-populate so the very first hotkey press has a candidate, even before any
        // foreground transitions have fired.
        var hwnd = NativeMethods.GetForegroundWindow();
        if (IsAcceptable(hwnd)) LastUserForegroundHwnd = hwnd;
    }

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

        return _foregroundHook != IntPtr.Zero;
    }

    public void Uninstall()
    {
        if (_foregroundHook != IntPtr.Zero) { NativeMethods.UnhookWinEvent(_foregroundHook); _foregroundHook = IntPtr.Zero; }
        if (_destroyHook != IntPtr.Zero)    { NativeMethods.UnhookWinEvent(_destroyHook);    _destroyHook = IntPtr.Zero; }
        _proc = null;
    }

    private bool IsAcceptable(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        if (!NativeMethods.IsWindow(hwnd)) return false;
        if (!NativeMethods.IsWindowVisible(hwnd)) return false;
        if (NativeMethods.IsIconic(hwnd)) return false;
        if (ShellWindowFilter.IsShellSurface(hwnd)) return false;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == _ownPid) return false;
        return true;
    }

    private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (idObject != 0) return; // top-level windows only
        try
        {
            if (eventType == NativeMethods.EVENT_SYSTEM_FOREGROUND)
            {
                if (IsAcceptable(hwnd)) LastUserForegroundHwnd = hwnd;
            }
            else if (eventType == NativeMethods.EVENT_OBJECT_DESTROY)
            {
                if (hwnd == LastUserForegroundHwnd) LastUserForegroundHwnd = IntPtr.Zero;
            }
        }
        catch
        {
            // Never throw out of a WinEvent callback.
        }
    }

    public void Dispose() => Uninstall();
}
