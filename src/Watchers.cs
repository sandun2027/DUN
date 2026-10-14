using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DUN;

// Hidden message-only window: receives the global hotkey and clipboard notifications.
sealed class MessageWindow : IDisposable
{
    const int HotkeyId = 1;
    readonly HwndSource source;

    public event Action? Hotkey;
    public event Action? ClipboardChanged;

    public MessageWindow()
    {
        source = new HwndSource(new HwndSourceParameters("DUNMessages") { ParentWindow = new IntPtr(-3) });
        source.AddHook(Hook);
        Native.AddClipboardFormatListener(source.Handle);
    }

    IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && wParam == HotkeyId) { Hotkey?.Invoke(); handled = true; }
        else if (msg == Native.WM_CLIPBOARDUPDATE) ClipboardChanged?.Invoke();
        return IntPtr.Zero;
    }

    public bool SetHotkey(uint mods, uint vk)
    {
        ClearHotkey();
        return Native.RegisterHotKey(source.Handle, HotkeyId, mods | 0x4000 /* MOD_NOREPEAT */, vk);
    }

    public void ClearHotkey() => Native.UnregisterHotKey(source.Handle, HotkeyId);

    public void Dispose()
    {
        ClearHotkey();
        Native.RemoveClipboardFormatListener(source.Handle);
        source.Dispose();
    }
}

// Win+V is reserved by Windows, so RegisterHotKey can't have it; a low-level hook takes it instead.
// Only installed while the setting is on.
sealed class KeyboardHook : IDisposable
{
    const int VK_V = 0x56;
    readonly Native.HookProc proc;
    readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    IntPtr hook;
    bool swallowing;

    public event Action? WinV;

    public KeyboardHook() => proc = Callback;

    public bool Enabled
    {
        get => hook != IntPtr.Zero;
        set
        {
            if (value == Enabled) return;
            if (value) hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, proc, Native.GetModuleHandle(null), 0);
            else
            {
                Native.UnhookWindowsHookEx(hook);
                hook = IntPtr.Zero;
            }
        }
    }

    IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var k = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
            bool injected = (k.flags & 0x10) != 0;
            if (k.vkCode == VK_V && !injected)
            {
                bool down = (int)wParam is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;
                if (down && (swallowing || WinOnly()))
                {
                    if (!swallowing)
                    {
                        swallowing = true;
                        dispatcher.BeginInvoke(Fire);
                    }
                    return new IntPtr(1);
                }
                if (!down && swallowing)
                {
                    swallowing = false;
                    return new IntPtr(1);
                }
            }
        }
        return Native.CallNextHookEx(hook, code, wParam, lParam);
    }

    static bool WinOnly() => (Native.KeyDown(0x5B) || Native.KeyDown(0x5C))
        && !Native.KeyDown(0x10) && !Native.KeyDown(0x11) && !Native.KeyDown(0x12);

    void Fire()
    {
        // A stray key between Win down and Win up keeps the Start menu from opening on release.
        Native.SendKeys((0xE8, false), (0xE8, true));
        WinV?.Invoke();
    }

    public void Dispose() => Enabled = false;
}

// Records everything copied: text (with its formatting), links, colours, images and files.
sealed class ClipboardWatcher
{
    public const string SelfFormat = "DUN.Self";
    static readonly uint SelfId = Native.RegisterClipboardFormat(SelfFormat);
    static readonly uint ExcludeFormat = Native.RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
    static readonly uint ViewerIgnoreFormat = Native.RegisterClipboardFormat("Clipboard Viewer Ignore");
    static readonly uint HistoryFormat = Native.RegisterClipboardFormat("CanIncludeInClipboardHistory");
    const int MaxText = 1_000_000, MaxRich = 4_000_000;

    readonly Func<Settings> settings;
    readonly Action<ClipItem, BitmapSource?> onClip;
    readonly DispatcherTimer settle = new() { Interval = TimeSpan.FromMilliseconds(150) };
    string? owner;

    public DateTime PausedUntil { get; set; } = DateTime.MinValue;
    public bool Paused => DateTime.Now < PausedUntil;

    public ClipboardWatcher(Func<Settings> settings, Action<ClipItem, BitmapSource?> onClip)
    {
        this.settings = settings;
        this.onClip = onClip;
        settle.Tick += (_, _) => { settle.Stop(); Read(); };
    }

    // Wait a moment so the owner finishes putting all its formats on the clipboard.
    public void Notify()
    {
        if (Paused) return;
        owner = Native.ProcessName(Native.GetClipboardOwner()) ?? Native.ProcessName(Native.GetForegroundWindow());
        settle.Stop();
        settle.Start();
    }

    bool Excluded(string? app)
    {
        if (app == null) return false;
        foreach (var raw in settings().ExcludedApps)
        {
            var name = raw.Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
            if (name.Length > 0 && string.Equals(name, app, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    void Read()
    {
        // Our own copies, and entries that password managers mark private.
        if (Native.IsClipboardFormatAvailable(SelfId) || Native.IsClipboardFormatAvailable(ExcludeFormat)
            || Native.IsClipboardFormatAvailable(ViewerIgnoreFormat) || Excluded(owner)) return;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try { Capture(); return; }
            catch (Exception e) when (e is ExternalException or IOException)
            {
                Thread.Sleep(60); // someone else has the clipboard open
            }
        }
    }

    void Capture()
    {
        if (Native.IsClipboardFormatAvailable(HistoryFormat)
            && Clipboard.GetData("CanIncludeInClipboardHistory") is MemoryStream h
            && h.Length >= 4 && new BinaryReader(h).ReadInt32() == 0) return;

        if (Clipboard.ContainsFileDropList())
        {
            var files = Clipboard.GetFileDropList().Cast<string>().Where(f => !string.IsNullOrEmpty(f)).ToList();
            if (files.Count > 0) onClip(new ClipItem { Kind = ClipKind.Files, Files = files, Hash = Classify.HashFiles(files) }, null);
            return;
        }
        if (Clipboard.ContainsText())
        {
            var text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text) || text.Length > MaxText) return;
            onClip(new ClipItem
            {
                Kind = Classify.Text(text),
                Text = text,
                Html = Rich(TextDataFormat.Html),
                Rtf = Rich(TextDataFormat.Rtf),
                Hash = Classify.HashText(text),
            }, null);
            return;
        }
        if (Clipboard.ContainsImage() && ReadImage() is { } bmp)
            onClip(new ClipItem { Kind = ClipKind.Image, Hash = ImageHash.Of(bmp) }, bmp);
    }

    static string? Rich(TextDataFormat format)
    {
        if (!Clipboard.ContainsText(format)) return null;
        var s = Clipboard.GetText(format);
        return string.IsNullOrEmpty(s) || s.Length > MaxRich ? null : s;
    }

    static BitmapSource? ReadImage()
    {
        if (Clipboard.ContainsData("PNG") && Clipboard.GetData("PNG") is MemoryStream png)
            return Decode(png);
        using var img = Forms.Clipboard.GetImage();
        if (img == null) return null;
        var ms = new MemoryStream();
        img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return Decode(ms);
    }

    static BitmapSource Decode(Stream s)
    {
        s.Position = 0;
        var frame = BitmapDecoder.Create(s, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        frame.Freeze();
        return frame;
    }
}
