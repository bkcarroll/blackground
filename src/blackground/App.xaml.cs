using System;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using StartupEventArgs = System.Windows.StartupEventArgs;
using ExitEventArgs = System.Windows.ExitEventArgs;
using blackground.Interop;
using blackground.Overlay;
using blackground.Settings;
using blackground.Startup;
using blackground.Tracking;
using blackground.Tray;
using blackground.UI;
using WinForms = System.Windows.Forms;

namespace blackground;

public partial class App : Application
{
    private SingleInstance? _singleInstance;
    private TrayIcon? _tray;
    private HotkeyManager? _hotkey;
    private OverlayController? _overlay;
    private ForegroundTracker? _foregroundTracker;
    private AppSettings _settings = AppSettings.Defaults();
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstance();
        if (!_singleInstance.TryAcquire())
        {
            // Existing instance has been signaled — exit silently.
            Shutdown();
            return;
        }
        _singleInstance.AnotherInstanceLaunched += (_, _) =>
            Dispatcher.BeginInvoke(new Action(ShowSettingsWindow));

        _settings = SettingsStore.Load();
        // Reconcile registry with persisted setting on startup (best-effort).
        try { _settings.StartWithWindows = StartupRegistration.IsEnabled(); } catch { }

        _foregroundTracker = new ForegroundTracker();
        _foregroundTracker.Install();

        _tray = new TrayIcon();
        _tray.ToggleRequested += (_, _) => HandleToggleRequest();
        _tray.SettingsRequested += (_, _) => ShowSettingsWindow();
        _tray.AboutRequested += (_, _) => ShowAbout();
        _tray.ExitRequested += (_, _) => Shutdown();

        _overlay = new OverlayController(_settings, _foregroundTracker);
        _overlay.Dismissed += (_, _) => _tray?.SetActive(false);

        _hotkey = new HotkeyManager();
        _hotkey.HotkeyPressed += (_, _) => HandleToggleRequest();

        if (!_hotkey.TryRegister(_settings.Hotkey, out var hkErr))
        {
            _tray.ShowBalloon("blackground — Hotkey Conflict",
                hkErr ?? "Failed to register hotkey.",
                WinForms.ToolTipIcon.Warning);
        }
    }

    private void HandleToggleRequest()
    {
        _overlay?.Toggle();
        _tray?.SetActive(_overlay?.IsActive == true);
    }

    private void ShowSettingsWindow()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings)
        {
            Apply = ApplySettings,
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private string? ApplySettings(AppSettings newSettings)
    {
        // Try to register hotkey first; if it fails, abort so the user can pick another.
        if (_hotkey is not null)
        {
            if (!_hotkey.TryRegister(newSettings.Hotkey, out var error))
            {
                // Re-register the previous one so we're not left without any hotkey.
                _hotkey.TryRegister(_settings.Hotkey, out _);
                return error ?? "Hotkey registration failed.";
            }
        }

        _settings = newSettings;
        SettingsStore.Save(_settings);
        _overlay?.UpdateSettings(_settings);
        return null;
    }

    private void ShowAbout()
    {
        var about = new AboutWindow(_settings.Hotkey);
        about.ShowDialog();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _hotkey?.Dispose(); } catch { }
        try { _overlay?.Dispose(); } catch { }
        try { _foregroundTracker?.Dispose(); } catch { }
        try { _tray?.Dispose(); } catch { }
        try { SettingsStore.Save(_settings); } catch { }
        try { _singleInstance?.Dispose(); } catch { }
        base.OnExit(e);
    }
}
