using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace DUN;

// Wires everything together and owns the tray icon.
sealed class AppController : IDisposable
{
    readonly Application app;
    readonly List<LineWindow> windows = new();
    Settings settings = Settings.Load();
    ClipStore store = null!;
    RevealController reveal = null!;
    Actions actions = null!;
    MessageWindow messages = null!;
    KeyboardHook hook = null!;
    ClipboardWatcher clipboard = null!;
    Forms.NotifyIcon tray = null!;
    Forms.ToolStripMenuItem toggleItem = null!, pauseItem = null!, resumeItem = null!;
    bool settingsOpen;

    public AppController(Application app) => this.app = app;

    public void Start()
    {
        store = new ClipStore(() => settings);
        reveal = new RevealController(() => windows, () => settings.RevealAtTopEdge);
        actions = new Actions(store, reveal, () => settings);
        BuildWindows();
        SystemEvents.DisplaySettingsChanged += OnDisplaysChanged;

        messages = new MessageWindow();
        messages.Hotkey += reveal.Toggle;
        hook = new KeyboardHook { Enabled = settings.ReplaceWinV };
        hook.WinV += reveal.Toggle;
        clipboard = new ClipboardWatcher(() => settings, store.Add);
        messages.ClipboardChanged += clipboard.Notify;

        BuildTray();
        if (!messages.SetHotkey(settings.HotkeyModifiers, settings.HotkeyKey))
            tray.ShowBalloonTip(5000, S.AppName, string.Format(S.HotkeyFailed, CurrentHotkey), Forms.ToolTipIcon.Warning);
        if (!settings.ClipboardWelcomeShown)
        {
            settings.ClipboardWelcomeShown = true;
            settings.Save();
            tray.ShowBalloonTip(5000, S.AppName, string.Format(S.Welcome, CurrentHotkey), Forms.ToolTipIcon.Info);
        }
        reveal.Start();
    }

    string CurrentHotkey => HotkeyText.Describe(settings.HotkeyModifiers, settings.HotkeyKey);

    void OnDisplaysChanged(object? sender, EventArgs e) => app.Dispatcher.BeginInvoke(BuildWindows);

    void BuildWindows()
    {
        foreach (var w in windows) w.Close();
        windows.Clear();
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var w = new LineWindow(screen, store, actions, () => settings);
            w.Dismissed += reveal.RetractAll;
            windows.Add(w);
        }
    }

    void BuildTray()
    {
        var menu = new Forms.ContextMenuStrip();
        toggleItem = new Forms.ToolStripMenuItem(S.ToggleLine, null, (_, _) => reveal.Toggle());
        pauseItem = new Forms.ToolStripMenuItem(S.Pause);
        pauseItem.DropDownItems.Add(S.Pause10, null, (_, _) => PauseFor(TimeSpan.FromMinutes(10)));
        pauseItem.DropDownItems.Add(S.Pause60, null, (_, _) => PauseFor(TimeSpan.FromHours(1)));
        pauseItem.DropDownItems.Add(S.PauseManual, null, (_, _) => { clipboard.PausedUntil = DateTime.MaxValue; UpdatePause(); });
        resumeItem = new Forms.ToolStripMenuItem(S.Resume, null, (_, _) => { clipboard.PausedUntil = DateTime.MinValue; UpdatePause(); });

        menu.Items.Add(toggleItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(pauseItem);
        menu.Items.Add(resumeItem);
        menu.Items.Add(new Forms.ToolStripMenuItem(S.SettingsMenu, null, (_, _) => OpenSettings()));
        menu.Items.Add(new Forms.ToolStripMenuItem(S.OpenFolder, null, (_, _) =>
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Settings.DataDir}\"") { UseShellExecute = true })));
        menu.Items.Add(new Forms.ToolStripMenuItem(S.ClearLine, null, (_, _) => ClearHistory()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem(S.Quit, null, (_, _) => app.Shutdown()));
        menu.Opening += (_, _) => UpdatePause();

        tray = new Forms.NotifyIcon { Icon = Art.TrayIcon(), Text = S.AppName, ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) reveal.Toggle(); };
        UpdateShortcutLabel();
        UpdatePause();
    }

    void PauseFor(TimeSpan span)
    {
        clipboard.PausedUntil = DateTime.Now + span;
        UpdatePause();
    }

    void UpdatePause()
    {
        bool paused = clipboard.Paused;
        pauseItem.Text = !paused ? S.Pause
            : clipboard.PausedUntil == DateTime.MaxValue ? S.PausedForever
            : string.Format(S.PausedUntil, clipboard.PausedUntil);
        pauseItem.Checked = paused;
        resumeItem.Visible = paused;
        tray.Text = paused ? $"{S.AppName} — {pauseItem.Text}" : S.AppName;
    }

    void UpdateShortcutLabel() => toggleItem.ShortcutKeyDisplayString = CurrentHotkey;

    void OpenSettings()
    {
        if (settingsOpen) return;
        settingsOpen = true;
        reveal.RetractAll();
        var w = new SettingsWindow(settings, Autostart.Enabled, messages.ClearHotkey,
            () => messages.SetHotkey(settings.HotkeyModifiers, settings.HotkeyKey));
        bool ok = w.ShowDialog() == true;
        settingsOpen = false;
        if (!ok) return;

        var next = w.Result;
        if (!messages.SetHotkey(next.HotkeyModifiers, next.HotkeyKey))
        {
            MessageBox.Show(string.Format(S.HotkeyFailed, HotkeyText.Describe(next.HotkeyModifiers, next.HotkeyKey)),
                S.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            next.HotkeyModifiers = settings.HotkeyModifiers;
            next.HotkeyKey = settings.HotkeyKey;
            messages.SetHotkey(next.HotkeyModifiers, next.HotkeyKey);
        }
        settings = next;
        settings.Save();
        hook.Enabled = settings.ReplaceWinV;
        try { Autostart.Enabled = w.StartWithWindows; }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException) { }
        store.Sweep();
        UpdateShortcutLabel();
    }

    void ClearHistory()
    {
        int count = 0;
        foreach (var item in store.Items) if (!item.Pinned) count++;
        if (count == 0) return;
        var answer = MessageBox.Show(string.Format(S.ClearConfirm, count), S.AppName, MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.OK) store.ClearUnpinned();
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaysChanged;
        reveal?.Stop();
        if (tray != null) { tray.Visible = false; tray.Dispose(); }
        hook?.Dispose();
        messages?.Dispose();
        store?.Dispose();
    }
}

static class Art
{
    // The app icon, embedded from assets/DUN.ico; the size that suits the tray is picked from it.
    public static Drawing.Icon TrayIcon()
    {
        using var stream = typeof(Art).Assembly.GetManifestResourceStream("DUN.ico")!;
        return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }
}
