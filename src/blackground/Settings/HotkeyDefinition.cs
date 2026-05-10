using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using blackground.Interop;

namespace blackground.Settings;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = NativeMethods.MOD_ALT,
    Control = NativeMethods.MOD_CONTROL,
    Shift = NativeMethods.MOD_SHIFT,
    Win = NativeMethods.MOD_WIN,
}

public sealed class HotkeyDefinition : IEquatable<HotkeyDefinition>
{
    public HotkeyModifiers Modifiers { get; init; }
    public uint VirtualKey { get; init; }

    [JsonIgnore]
    public bool IsValid => Modifiers != HotkeyModifiers.None && VirtualKey != 0;

    public HotkeyDefinition() { }

    public HotkeyDefinition(HotkeyModifiers modifiers, uint vk)
    {
        Modifiers = modifiers;
        VirtualKey = vk;
    }

    public static HotkeyDefinition Default => new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x42 /* B */);

    public override string ToString()
    {
        if (!IsValid) return "(none)";

        var sb = new StringBuilder();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) sb.Append("Ctrl+");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) sb.Append("Alt+");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) sb.Append("Shift+");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) sb.Append("Win+");
        sb.Append(VkToString(VirtualKey));
        return sb.ToString();
    }

    public bool Equals(HotkeyDefinition? other)
        => other is not null && other.Modifiers == Modifiers && other.VirtualKey == VirtualKey;

    public override bool Equals(object? obj) => Equals(obj as HotkeyDefinition);
    public override int GetHashCode() => HashCode.Combine(Modifiers, VirtualKey);

    private static string VkToString(uint vk)
    {
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();    // 0-9
        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();    // A-Z
        if (vk >= 0x70 && vk <= 0x87) return $"F{vk - 0x6F}";          // F1-F24
        return vk switch
        {
            0x20 => "Space",
            0x09 => "Tab",
            0x0D => "Enter",
            0x08 => "Backspace",
            0x2D => "Insert",
            0x2E => "Delete",
            0x24 => "Home",
            0x23 => "End",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x1B => "Esc",
            0xBA => ";",
            0xBB => "=",
            0xBC => ",",
            0xBD => "-",
            0xBE => ".",
            0xBF => "/",
            0xC0 => "`",
            0xDB => "[",
            0xDC => "\\",
            0xDD => "]",
            0xDE => "'",
            _ => $"VK_{vk:X2}",
        };
    }

    public static bool IsModifierVk(int vk) => vk is
        NativeMethods.VK_CONTROL or NativeMethods.VK_LCONTROL or NativeMethods.VK_RCONTROL or
        NativeMethods.VK_MENU or NativeMethods.VK_LMENU or NativeMethods.VK_RMENU or
        NativeMethods.VK_SHIFT or NativeMethods.VK_LSHIFT or NativeMethods.VK_RSHIFT or
        NativeMethods.VK_LWIN or NativeMethods.VK_RWIN;
}
