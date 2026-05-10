# blackground

A lightweight Windows tray utility that, on a global hotkey, slides a solid-black overlay
behind the currently active window — instantly muting every other window, dialog, and
desktop element until you click outside the active window or press the hotkey again.

## Build

Requires the .NET 8 SDK (or the .NET 9 SDK with the WindowsDesktop 8.0 runtime, which is what
this repo was developed against).

```powershell
dotnet build
```

## Run

```powershell
dotnet run --project src/blackground
```

The app shows no window on launch — only the tray icon. Right-click for the menu, left-click
to toggle the blackout, or double-click to open Settings.

## Publish (single-file self-contained .exe)

```powershell
dotnet publish src/blackground -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Output ends up under `src/blackground/bin/Release/net8.0-windows/win-x64/publish/blackground.exe`.

## Default hotkey

`Ctrl+Alt+B` — change it in Settings → Hotkey (click the field, press the desired combination).

## Settings

- **Hotkey:** any combo with at least one modifier (Ctrl/Alt/Shift/Win) plus a regular key.
- **Monitor scope:** "Active window's monitor" (default) or "All monitors".
- **Opacity:** 50%–100% (default 100%).
- **Start with Windows:** writes to `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

Settings persist to `%APPDATA%\blackground\settings.json`.

## Known limitations

- **Fullscreen-exclusive apps / games** typically render above all overlays — blackground will
  appear inactive against them in v1.
- **Elevated foreground windows** (admin processes) cannot receive input from an unelevated
  hotkey listener, and the overlay cannot slot beneath them. Run blackground elevated if you
  need this — there is no UAC-aware install in v1.
- **Windows that straddle multiple monitors** while in "Active monitor only" mode: the overlay
  covers only the monitor containing the bulk of the window; the rest of the window remains
  on the un-blackouted monitor (PRD §10).
- **DPI / monitor topology changes** while the overlay is active will dismiss the overlay; just
  re-trigger.
- **Tray icon** is generated procedurally at runtime (a simple square). TODO: ship a proper
  `.ico` resource.

## Architecture (one-liner per area)

- `Interop/` — P/Invoke surface (`NativeMethods`), hotkey registration (`HotkeyManager`), and
  the three hooks (`MouseHook`, `KeyboardHook`, `WindowEventHook`).
- `Overlay/` — `OverlayWindow` (transparent borderless WPF window) and `OverlayController`
  which owns the overlay set per-monitor and orchestrates show/hide and z-order.
- `Settings/` — POCO + JSON store at `%APPDATA%\blackground\settings.json`.
- `UI/` — Settings window and the in-process `HotkeyCaptureBox`.
- `Tray/` — `System.Windows.Forms.NotifyIcon` wrapper.
- `Startup/` — single-instance mutex + Run-key registration.
