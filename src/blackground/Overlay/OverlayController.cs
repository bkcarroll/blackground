using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Application = System.Windows.Application;
using blackground.Interop;
using blackground.Settings;
using blackground.Tracking;

namespace blackground.Overlay;

/// <summary>
/// Orchestrates show/hide of the blackout. Owns OverlayWindow instances (one per active monitor),
/// tracks the foreground window, and rebuilds z-order whenever it changes. Also subscribes to
/// global mouse/keyboard hooks and WinEvents to drive dismissal.
/// </summary>
public sealed class OverlayController : IDisposable
{
    private readonly List<OverlayWindow> _overlays = new();
    private readonly MouseHook _mouseHook = new();
    private readonly KeyboardHook _keyboardHook = new();
    private readonly WindowEventHook _winEvents = new();
    private readonly DispatcherTimer _zOrderTimer;
    private readonly ForegroundTracker _tracker;

    private AppSettings _settings;
    private IntPtr _trackedHwnd;
    private NativeMethods.RECT _lastTrackedRect;

    public bool IsActive { get; private set; }

    public event EventHandler? Dismissed;

    public OverlayController(AppSettings settings, ForegroundTracker tracker)
    {
        _settings = settings;
        _tracker = tracker;

        _mouseHook.GlobalClick += OnGlobalClick;
        _keyboardHook.KeyDown += OnGlobalKeyDown;
        _winEvents.ForegroundChanged += OnForegroundChanged;
        _winEvents.WindowDestroyed += OnTrackedWindowGone;
        _winEvents.WindowMinimized += OnTrackedWindowGone;

        _zOrderTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(33), // ~30 Hz
        };
        _zOrderTimer.Tick += OnZOrderTick;
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        foreach (var w in _overlays)
        {
            w.Opacity = _settings.Opacity;
        }
        // If active and scope changed, rebuild overlays.
        if (IsActive)
        {
            RebuildOverlaysForCurrentForeground();
        }
    }

    public void Toggle()
    {
        if (IsActive) Dismiss();
        else Activate();
    }

    public void Activate()
    {
        if (IsActive) return;

        // Resolve target hwnd via fallback chain:
        //   1. Current foreground if it's a real user window.
        //   2. Last non-shell user-foreground remembered by the tracker.
        //   3. Silent no-op.
        var hwndFg = NativeMethods.GetForegroundWindow();
        if (hwndFg == IntPtr.Zero || ShellWindowFilter.IsShellSurface(hwndFg))
        {
            hwndFg = _tracker.LastUserForegroundHwnd;
        }
        if (!IsAcceptableTarget(hwndFg)) return;

        _trackedHwnd = hwndFg;
        if (!NativeMethods.GetWindowRect(_trackedHwnd, out _lastTrackedRect))
        {
            _trackedHwnd = IntPtr.Zero;
            return;
        }

        BuildOverlaysFor(hwndFg);

        _mouseHook.Install();
        _keyboardHook.Install();
        _winEvents.Install();
        _zOrderTimer.Start();

        IsActive = true;
    }

    private static bool IsAcceptableTarget(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        if (!NativeMethods.IsWindow(hwnd)) return false;
        if (!NativeMethods.IsWindowVisible(hwnd)) return false;
        if (NativeMethods.IsIconic(hwnd)) return false;
        if (ShellWindowFilter.IsShellSurface(hwnd)) return false;
        if (GetProcessId(hwnd) == (uint)Environment.ProcessId) return false;
        return true;
    }

    public void Dismiss()
    {
        if (!IsActive) return;
        IsActive = false;

        _zOrderTimer.Stop();
        _mouseHook.Uninstall();
        _keyboardHook.Uninstall();
        _winEvents.Uninstall();

        foreach (var w in _overlays)
        {
            try { w.HideOverlay(); } catch { /* swallow */ }
        }
        _trackedHwnd = IntPtr.Zero;

        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildOverlaysForCurrentForeground()
    {
        if (_trackedHwnd == IntPtr.Zero) return;
        DisposeOverlays();
        BuildOverlaysFor(_trackedHwnd);
    }

    private void BuildOverlaysFor(IntPtr hwndFg)
    {
        DisposeOverlays();

        var rects = ResolveTargetMonitorRects(hwndFg);
        foreach (var rc in rects)
        {
            var w = new OverlayWindow { Opacity = _settings.Opacity };
            w.ShowAtMonitor(rc);
            w.PlaceBehind(hwndFg);
            _overlays.Add(w);
        }
    }

    private List<NativeMethods.RECT> ResolveTargetMonitorRects(IntPtr hwndFg)
    {
        var list = new List<NativeMethods.RECT>();
        if (_settings.MonitorScope == MonitorScope.AllMonitors)
        {
            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref NativeMethods.RECT rc, IntPtr data) =>
            {
                var mi = new NativeMethods.MONITORINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>() };
                if (NativeMethods.GetMonitorInfo(hMon, ref mi))
                {
                    // rcWork (excludes taskbar) — using rcMonitor would let Windows flag the
                    // overlay as fullscreen and auto-enable Focus Assist / DND. Visually
                    // identical because the taskbar sits above the overlay in z-order anyway.
                    list.Add(mi.rcWork);
                }
                return true;
            }, IntPtr.Zero);
        }
        else
        {
            var hMon = NativeMethods.MonitorFromWindow(hwndFg, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var mi = new NativeMethods.MONITORINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            if (NativeMethods.GetMonitorInfo(hMon, ref mi))
            {
                list.Add(mi.rcMonitor);
            }
        }
        return list;
    }

    private void DisposeOverlays()
    {
        foreach (var w in _overlays)
        {
            try { w.Close(); } catch { /* swallow */ }
        }
        _overlays.Clear();
    }

    private void OnZOrderTick(object? sender, EventArgs e)
    {
        if (!IsActive || _trackedHwnd == IntPtr.Zero) return;
        if (!NativeMethods.IsWindow(_trackedHwnd) || !NativeMethods.IsWindowVisible(_trackedHwnd))
        {
            Dismiss();
            return;
        }

        // Foreground tracking is handled by the WinEvent hook (EVENT_SYSTEM_FOREGROUND);
        // checking here would race the re-target logic in OnForegroundChanged.

        // If the active-window monitor changed (window moved), refresh overlays in active-window scope.
        if (_settings.MonitorScope == MonitorScope.ActiveWindow)
        {
            if (NativeMethods.GetWindowRect(_trackedHwnd, out var rc))
            {
                if (!RectsMatchMonitor(rc, _lastTrackedRect))
                {
                    var oldMon = NativeMethods.MonitorFromPoint(
                        new NativeMethods.POINT { X = (_lastTrackedRect.Left + _lastTrackedRect.Right) / 2,
                                                   Y = (_lastTrackedRect.Top + _lastTrackedRect.Bottom) / 2 },
                        NativeMethods.MONITOR_DEFAULTTONEAREST);
                    var newMon = NativeMethods.MonitorFromPoint(
                        new NativeMethods.POINT { X = (rc.Left + rc.Right) / 2, Y = (rc.Top + rc.Bottom) / 2 },
                        NativeMethods.MONITOR_DEFAULTTONEAREST);
                    if (oldMon != newMon)
                    {
                        _lastTrackedRect = rc;
                        RebuildOverlaysForCurrentForeground();
                        return;
                    }
                }
                _lastTrackedRect = rc;
            }
        }

        // Re-apply z-order to keep overlay just behind the foreground window.
        foreach (var w in _overlays)
        {
            w.PlaceBehind(_trackedHwnd);
        }
    }

    private static bool RectsMatchMonitor(NativeMethods.RECT a, NativeMethods.RECT b)
    {
        // Cheap heuristic: identical rects → same monitor (no recompute).
        return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
    }

    private void OnGlobalClick(object? sender, MouseHook.GlobalClickEventArgs e)
    {
        if (!IsActive || _trackedHwnd == IntPtr.Zero) return;
        if (!TryGetTrackedWindowBounds(out var rc)) { Dismiss(); return; }

        var inside = e.X >= rc.Left && e.X < rc.Right && e.Y >= rc.Top && e.Y < rc.Bottom;
        if (!inside)
        {
            // Swallow the click so the underlying app does not get activated.
            // The hook returns 1 for both this button-down and its matching button-up,
            // so the click is consumed before any window sees it.
            e.Handled = true;

            // Marshal Dismiss back to UI thread.
            Application.Current?.Dispatcher.BeginInvoke(new Action(Dismiss));
        }
    }

    /// <summary>
    /// Returns the bounds we use to test "is the click inside the active window?". Prefers DWM
    /// extended frame bounds (excludes the legacy fat invisible border) and falls back to GetWindowRect.
    /// </summary>
    private bool TryGetTrackedWindowBounds(out NativeMethods.RECT rc)
    {
        var hr = NativeMethods.DwmGetWindowAttribute(
            _trackedHwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out rc, Marshal.SizeOf<NativeMethods.RECT>());
        if (hr == 0) return true;
        return NativeMethods.GetWindowRect(_trackedHwnd, out rc);
    }

    private void OnGlobalKeyDown(object? sender, KeyboardHook.KeyEventArgs e)
    {
        if (!IsActive) return;
        if (e.VkCode == NativeMethods.VK_ESCAPE)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(Dismiss));
        }
    }

    private void OnForegroundChanged(object? sender, WindowEventHook.WinEventArgs e)
    {
        if (!IsActive) return;
        // A brief shell-surface foreground transition (user clicked taskbar / desktop / Start)
        // should not dismiss or re-target — the 30Hz z-order tick keeps placement correct
        // until the next real foreground event lands.
        if (ShellWindowFilter.IsShellSurface(e.Hwnd)) return;
        if (e.Hwnd == _trackedHwnd) return;

        var newFg = e.Hwnd;

        // If the new foreground window is one of our overlays, ignore (shouldn't happen with WS_EX_NOACTIVATE).
        foreach (var w in _overlays)
        {
            if (w.Hwnd == newFg) return;
        }

        // If foreground belongs to our own process (e.g. settings window), ignore.
        if (GetProcessId(newFg) == (uint)Environment.ProcessId) return;

        // PRD §10 modal-child rule: if the new foreground belongs to the same process as the
        // tracked window, treat it as a modal child and re-target instead of dismissing.
        var trackedPid = GetProcessId(_trackedHwnd);
        var newPid = GetProcessId(newFg);
        if (trackedPid != 0 && trackedPid == newPid)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() => RetargetTo(newFg)));
            return;
        }

        Application.Current?.Dispatcher.BeginInvoke(new Action(Dismiss));
    }

    private void RetargetTo(IntPtr newFg)
    {
        if (!IsActive) return;
        if (!NativeMethods.IsWindow(newFg) || !NativeMethods.IsWindowVisible(newFg)) return;
        if (!NativeMethods.GetWindowRect(newFg, out var newRect)) return;

        var oldRect = _lastTrackedRect;
        _trackedHwnd = newFg;
        _lastTrackedRect = newRect;

        // In active-window scope, if the monitor changed, rebuild overlays for the new monitor.
        if (_settings.MonitorScope == MonitorScope.ActiveWindow)
        {
            var oldMon = NativeMethods.MonitorFromPoint(
                new NativeMethods.POINT { X = (oldRect.Left + oldRect.Right) / 2,
                                          Y = (oldRect.Top + oldRect.Bottom) / 2 },
                NativeMethods.MONITOR_DEFAULTTONEAREST);
            var newMon = NativeMethods.MonitorFromPoint(
                new NativeMethods.POINT { X = (newRect.Left + newRect.Right) / 2,
                                          Y = (newRect.Top + newRect.Bottom) / 2 },
                NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (oldMon != newMon)
            {
                RebuildOverlaysForCurrentForeground();
                return;
            }
        }

        // Same monitor (or all-monitors scope): just re-place overlays behind the new window.
        foreach (var w in _overlays)
        {
            w.PlaceBehind(newFg);
        }
    }

    private static uint GetProcessId(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return 0;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    private void OnTrackedWindowGone(object? sender, WindowEventHook.WinEventArgs e)
    {
        if (!IsActive) return;
        if (e.Hwnd == _trackedHwnd)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(Dismiss));
        }
    }

    public void Dispose()
    {
        Dismiss();
        DisposeOverlays();
        _mouseHook.Dispose();
        _keyboardHook.Dispose();
        _winEvents.Dispose();
    }
}
