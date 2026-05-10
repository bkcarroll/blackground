namespace blackground.Settings;

public enum MonitorScope
{
    ActiveWindow = 0,
    AllMonitors = 1,
}

public sealed class AppSettings
{
    public HotkeyDefinition Hotkey { get; set; } = HotkeyDefinition.Default;
    public MonitorScope MonitorScope { get; set; } = MonitorScope.ActiveWindow;

    /// <summary>0.5–1.0; default 1.0.</summary>
    public double Opacity { get; set; } = 1.0;

    public bool StartWithWindows { get; set; } = false;

    public AppSettings Clone() => new()
    {
        Hotkey = new HotkeyDefinition(Hotkey.Modifiers, Hotkey.VirtualKey),
        MonitorScope = MonitorScope,
        Opacity = Opacity,
        StartWithWindows = StartWithWindows,
    };

    public static AppSettings Defaults() => new();
}
