using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using Microsoft.Win32;

namespace DUN;

sealed class Settings
{
    public static readonly string DataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DUN");
    static string FilePath => Path.Combine(DataDir, "settings.json");

    public bool ClipboardWelcomeShown { get; set; }
    public uint HotkeyModifiers { get; set; } = 0x1 | 0x2; // Alt + Ctrl
    public uint HotkeyKey { get; set; } = 0x56;            // V
    public bool ReplaceWinV { get; set; }
    public bool PasteOnClick { get; set; }
    public bool RevealAtTopEdge { get; set; } = true;
    public int MaxItems { get; set; } = 200;
    public int RetentionDays { get; set; }                 // 0 = keep forever
    public List<string> ExcludedApps { get; set; } = new() { "1Password", "Bitwarden", "KeePass", "KeePassXC" };

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(DataDir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public Settings Clone() => JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(this))!;
}

static class HotkeyText
{
    // RegisterHotKey modifier bits match WPF's ModifierKeys: Alt=1, Ctrl=2, Shift=4, Win=8.
    public static string Modifiers(uint mods)
    {
        var s = "";
        if ((mods & 0x2) != 0) s += "Ctrl+";
        if ((mods & 0x1) != 0) s += "Alt+";
        if ((mods & 0x4) != 0) s += "Shift+";
        if ((mods & 0x8) != 0) s += "Win+";
        return s;
    }

    public static string Describe(uint mods, uint vk)
    {
        var key = KeyInterop.KeyFromVirtualKey((int)vk);
        return Modifiers(mods) + (key >= Key.D0 && key <= Key.D9 ? ((int)(key - Key.D0)).ToString() : key.ToString());
    }
}

static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(S.AppName) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) key.SetValue(S.AppName, $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue(S.AppName, false);
        }
    }
}
