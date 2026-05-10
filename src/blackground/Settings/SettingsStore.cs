using System;
using System.IO;
using System.Text.Json;

namespace blackground.Settings;

public static class SettingsStore
{
    public static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "blackground");

    public static string SettingsPath => Path.Combine(AppDataDir, "settings.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                var defaults = AppSettings.Defaults();
                Save(defaults);
                return defaults;
            }
            var json = File.ReadAllText(SettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
            return Sanitize(loaded ?? AppSettings.Defaults());
        }
        catch
        {
            return AppSettings.Defaults();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            var json = JsonSerializer.Serialize(Sanitize(settings), Options);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Best-effort persistence; ignore I/O errors.
        }
    }

    private static AppSettings Sanitize(AppSettings s)
    {
        if (s.Hotkey is null || !s.Hotkey.IsValid) s.Hotkey = HotkeyDefinition.Default;
        if (s.Opacity < 0.5) s.Opacity = 0.5;
        if (s.Opacity > 1.0) s.Opacity = 1.0;
        return s;
    }
}
