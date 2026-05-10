using System;
using System.IO;
using Microsoft.Win32;

namespace blackground.Startup;

public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "blackground";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            if (key is null) return false;
            return key.GetValue(ValueName) is string s && !string.IsNullOrWhiteSpace(s);
        }
        catch
        {
            return false;
        }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null) return;
            if (enabled)
            {
                var path = GetExecutablePath();
                if (string.IsNullOrEmpty(path)) return;
                key.SetValue(ValueName, $"\"{path}\"", RegistryValueKind.String);
            }
            else
            {
                if (key.GetValue(ValueName) is not null)
                {
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                }
            }
        }
        catch
        {
            // Best-effort; Group Policy or permissions can block.
        }
    }

    private static string GetExecutablePath()
    {
        // Prefer the actual host process exe (matters for self-contained single-file).
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path!;
        return AppContext.BaseDirectory;
    }
}
