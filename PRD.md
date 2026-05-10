# blackground — Product Requirements Document

## 1. Summary

blackground is a lightweight Windows desktop utility that lets the user press a global hotkey to instantly cover everything behind the active foreground window with a solid black overlay. The result is a distraction-free "spotlight" view in which only the active window is visible. Clicking outside the active window dismisses the overlay and returns the desktop to its normal state.

The app runs in the system tray, is configurable via a settings window, and is built in C# / WPF on .NET.

## 2. Problem & Motivation

When working on a single task, surrounding windows, notifications, and desktop clutter pull attention away from the foreground app. Existing tools (Ghoster, Le Dimmer, Focus Dimmer, Dropcloth, SpotlightDimmer) either:

- Auto-dim continuously rather than triggering on-demand,
- Apply a partial transparency rather than a true blackout,
- Lack a click-to-dismiss interaction,
- Or are unmaintained.

blackground fills the gap with an on-demand, hotkey-triggered, configurable blackout that snaps off the moment the user interacts elsewhere.

## 3. Goals

- Hotkey-triggered blackout overlay rendered behind the active window.
- Click-anywhere-outside-active-window to dismiss.
- Sub-100ms perceived activation latency.
- Configurable hotkey, monitor scope, and opacity.
- Runs unobtrusively in the system tray; minimal resource footprint when idle.
- Single self-contained Windows executable.

## 4. Non-Goals

- No per-app rules, schedules, or auto-activation triggers.
- No image, color, or pattern overlays — solid black only (with adjustable opacity).
- No macOS or Linux support.
- No multi-window "spotlight" (only the single active foreground window stays visible).
- No screen recording, screenshot, or presentation features.

## 5. Target User

Knowledge workers on Windows who want a one-keystroke focus mode without committing to an always-on dimmer or a full focus-app suite. Power users comfortable with global hotkeys and tray apps.

## 6. User Stories

1. As a user, I press my configured hotkey and the screen behind my active window goes black so I can focus.
2. As a user, I click outside the active window and the blackout disappears immediately.
3. As a user, I press the hotkey again while the blackout is active and it toggles off.
4. As a user, I open settings from the tray icon and change the hotkey to something that doesn't conflict with my other apps.
5. As a user with multiple monitors, I choose whether the blackout covers only the active window's monitor or all monitors.
6. As a user, I tune the opacity slider to leave a faint hint of the background instead of a full blackout.
7. As a user, I exit the app from the tray menu and the global hotkey is fully released.

## 7. Functional Requirements

### 7.1 Blackout Activation (Hotkey)
- **FR-1.1** Register a global system-wide hotkey via `RegisterHotKey` (user32.dll). Default: `Ctrl + Alt + B`.
- **FR-1.2** On hotkey press while inactive: identify the foreground window via `GetForegroundWindow`, render a black overlay behind it, leave the foreground window fully interactive on top.
- **FR-1.3** On hotkey press while active: dismiss the overlay (toggle behavior).
- **FR-1.4** If hotkey registration fails (already taken by another app), surface a tray balloon notification and a visible warning in the settings UI.

### 7.2 Overlay Rendering
- **FR-2.1** Overlay is a borderless, topmost-relative-to-desktop, click-through-disabled WPF window with a solid black `Background`.
- **FR-2.2** Z-order: overlay sits *between* the active foreground window and everything else. The active window remains the topmost visible window. Implementation: position overlay just below the foreground window in z-order via `SetWindowPos` with `HWND_TOP` then re-raise the foreground window, or use `SetWindowPos(hwndOverlay, hwndForeground, ...)` with `SWP_NOACTIVATE`.
- **FR-2.3** Overlay opacity is user-configurable via a slider (range 50%–100%, default 100%).
- **FR-2.4** Overlay must not appear in the Alt+Tab list or taskbar (`WS_EX_TOOLWINDOW`, `ShowInTaskbar=false`).
- **FR-2.5** Overlay must not steal focus from the active window when shown.

### 7.3 Dismissal
- **FR-3.1** Clicking anywhere outside the active foreground window dismisses the overlay. Detection: install a low-level mouse hook (`WH_MOUSE_LL`) and on click compare the click position to the foreground window's bounds.
- **FR-3.2** If the active foreground window is closed, minimized, or loses foreground status, the overlay dismisses automatically. Detect via `SetWinEventHook` for `EVENT_SYSTEM_FOREGROUND`, `EVENT_OBJECT_DESTROY`, and `EVENT_SYSTEM_MINIMIZESTART`.
- **FR-3.3** Pressing the configured hotkey a second time dismisses the overlay (see FR-1.3).
- **FR-3.4** Pressing `Esc` while the overlay is active dismisses it.

### 7.4 Multi-Monitor Behavior
- **FR-4.1** Default mode: blackout covers only the monitor that contains the majority of the active window's bounding rect (resolved via `MonitorFromWindow`).
- **FR-4.2** Alternate mode (configurable): blackout spans all connected monitors using a separate overlay window per monitor.
- **FR-4.3** When the active window straddles multiple monitors in "active monitor only" mode, the overlay still covers only the primary-containing monitor; the part of the active window on the other monitor remains over the normal desktop. (Document as a known limitation.)

### 7.5 Tracking the Active Window
- **FR-5.1** While the overlay is active, track the foreground window's position and size. If it moves or resizes, the overlay's z-order must be maintained so it stays directly behind the foreground window.
- **FR-5.2** If the overlay covers the active window's monitor only and the user drags the active window to a different monitor, the overlay follows to the new monitor.

### 7.6 Settings Window
- **FR-6.1** Accessible via tray icon double-click or "Settings" tray menu item.
- **FR-6.2** Controls:
  - Hotkey capture field (records key combo on press; validates not empty; warns on registration conflict).
  - Monitor scope: radio "Active window's monitor" / "All monitors".
  - Opacity slider: 50%–100%.
  - "Start with Windows" checkbox (writes to `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).
  - "Reset to defaults" button.
- **FR-6.3** Settings are persisted to `%APPDATA%\blackground\settings.json` and applied immediately on save.

### 7.7 System Tray
- **FR-7.1** Tray icon visible whenever the app is running.
- **FR-7.2** Right-click menu: Toggle Blackout, Settings, About, Exit.
- **FR-7.3** Left-click toggles blackout (same effect as hotkey).
- **FR-7.4** Tray icon visually reflects state (e.g., filled vs outlined) when overlay is active.

### 7.8 Lifecycle
- **FR-8.1** Single-instance enforcement via named mutex; launching a second copy brings the existing settings window forward.
- **FR-8.2** On exit: unregister hotkey, remove mouse hook, dispose tray icon, persist settings.

## 8. Non-Functional Requirements

- **NFR-1 Performance:** Overlay must be visible within 100ms of hotkey press on a typical machine. Idle CPU < 0.5%, idle RAM < 50MB.
- **NFR-2 Reliability:** Mouse hook must not introduce perceptible input lag. If hook latency exceeds 100ms (Windows auto-disables slow hooks), re-register on detection.
- **NFR-3 Compatibility:** Windows 10 (1809+) and Windows 11. .NET 8 LTS or later.
- **NFR-4 Distribution:** Single-file self-contained `.exe` (publish with `PublishSingleFile=true`, `SelfContained=true`).
- **NFR-5 Footprint:** No installer required for v1; portable executable that creates `%APPDATA%\blackground` on first run.
- **NFR-6 Accessibility:** Hotkey works system-wide regardless of focused app, including elevated/admin windows *only if blackground itself runs elevated* (document as a known limitation).

## 9. UX Flow

### Activation flow
1. User presses `Ctrl+Alt+B`.
2. App captures `GetForegroundWindow()` → `hwndFg`.
3. App resolves target monitor(s) per FR-4.
4. App creates (or reuses pooled) overlay window(s), sized to monitor bounds.
5. App calls `SetWindowPos(hwndOverlay, hwndFg, ...)` to slot the overlay just below the foreground window without stealing focus.
6. Mouse hook + WinEvent hooks installed.

### Dismissal flow
- Any of: hotkey re-press, Esc, click outside `hwndFg`, foreground change, `hwndFg` destroyed/minimized.
- App hides overlay window(s), uninstalls mouse hook, uninstalls WinEvent hooks.

### Settings flow
- Tray → Settings → modal window (or non-modal, single instance).
- Hotkey capture: user clicks the field, presses the new combo, value displays as e.g. "Ctrl+Alt+B"; on Save, app re-registers hotkey and surfaces conflict if any.

## 10. Edge Cases & Open Questions

- **Fullscreen apps / games:** Many fullscreen exclusive apps will render above the overlay; document as unsupported in v1.
- **UAC / elevated windows:** Without elevation, hotkey input may not reach an elevated foreground window, and the overlay cannot sit behind it. v1 ships unelevated; note in README.
- **Modal child dialogs of the active window:** Treat the *current* foreground window as the protected window; if a modal child of the original opens and becomes foreground, the overlay reattaches behind the new foreground window. (Restated as expected behavior, not a bug.)
- **DPI / monitor changes:** If a monitor is added/removed or DPI changes while overlay is active, dismiss and let the user re-trigger.
- **Click on taskbar:** A click on the taskbar is "outside the active window" and therefore dismisses (consistent rule).

## 11. Out of Scope (v1)

- Per-app blackout rules
- Image / pattern / color overlays beyond solid black with opacity
- Pomodoro / timed focus sessions
- Auto-dismiss after N seconds
- Multi-language UI (English only)
- Telemetry / analytics
- Auto-update

## 12. Success Criteria

- Hotkey toggles overlay reliably across 100 consecutive presses with no leaked windows or hooks.
- Overlay activation visible within 100ms in manual testing.
- Settings changes persist across app restarts.
- No measurable input lag introduced by mouse hook (qualitative test: typing and mouse use feel unchanged).
- Memory stable over 24h idle run.

## 13. Tech Stack

- **Language:** C# (.NET 8)
- **UI:** WPF
- **Win32 interop:** `RegisterHotKey`, `SetWindowsHookEx (WH_MOUSE_LL)`, `SetWinEventHook`, `GetForegroundWindow`, `MonitorFromWindow`, `SetWindowPos`, `GetWindowRect`
- **Tray:** `H.NotifyIcon.Wpf` or hand-rolled via `Shell_NotifyIcon`
- **Settings persistence:** `System.Text.Json` → `%APPDATA%\blackground\settings.json`
- **Build:** `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`

## 14. Milestones

1. **M1 — Spike:** Prove the z-order trick (overlay behind foreground window without focus theft) on a single monitor. Hardcoded hotkey.
2. **M2 — Core loop:** Hotkey toggle, click-outside dismiss, foreground-change dismiss, single monitor.
3. **M3 — Multi-monitor:** Active-monitor mode and all-monitors mode.
4. **M4 — Settings + persistence:** Settings window, hotkey capture, opacity slider, JSON persistence.
5. **M5 — Tray + lifecycle:** Tray icon, single-instance, start-with-Windows.
6. **M6 — Polish + package:** Single-file publish, README, basic icon set.
