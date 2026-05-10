using System;
using System.IO;
using System.Text;

namespace blackground.Interop;

/// <summary>
/// Recognizes top-level windows that are part of the Windows shell (taskbar, desktop,
/// Start menu, search, notification center) so we don't try to spotlight them.
/// </summary>
public static class ShellWindowFilter
{
    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Progman",
        "WorkerW",
        "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland",
    };

    // Process image leaf-names that host shell UI on top of Windows.UI.Core.CoreWindow.
    // Class alone is ambiguous (real UWP apps share the class), so we additionally
    // require the owning process to be one of these.
    private static readonly HashSet<string> ShellHostProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "shellexperiencehost.exe",
        "searchhost.exe",
        "searchui.exe",
        "startmenuexperiencehost.exe",
        "textinputhost.exe",
        "lockapp.exe",
    };

    public static bool IsShellSurface(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        var cls = GetWindowClass(hwnd);
        if (cls.Length == 0) return false;
        if (ShellClasses.Contains(cls)) return true;
        if (cls == "Windows.UI.Core.CoreWindow")
        {
            var image = GetProcessImageLeafName(hwnd);
            return image.Length > 0 && ShellHostProcesses.Contains(image);
        }
        return false;
    }

    private static string GetWindowClass(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        int len = NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return len > 0 ? sb.ToString(0, len) : string.Empty;
    }

    private static string GetProcessImageLeafName(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0) return string.Empty;

        var hProc = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProc == IntPtr.Zero) return string.Empty;

        try
        {
            var sb = new StringBuilder(1024);
            uint cap = (uint)sb.Capacity;
            if (!NativeMethods.QueryFullProcessImageName(hProc, 0, sb, ref cap)) return string.Empty;
            return Path.GetFileName(sb.ToString(0, (int)cap));
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            NativeMethods.CloseHandle(hProc);
        }
    }
}
