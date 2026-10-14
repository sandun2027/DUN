using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace DUN;

// Everything you can do with a clip.
sealed class Actions
{
    readonly ClipStore store;
    readonly RevealController reveal;
    readonly Func<Settings> settings;

    public Actions(ClipStore store, RevealController reveal, Func<Settings> settings)
    {
        this.store = store;
        this.reveal = reveal;
        this.settings = settings;
    }

    // Click / Enter. Copies by default; Shift does the other thing. Settings can swap the two.
    public void Primary(ClipItem item, bool alternate, CardView? view)
    {
        if (settings().PasteOnClick != alternate) Paste(item, plain: false);
        else if (Copy(item, plain: false)) view?.Flash(S.Copied);
    }

    public bool Copy(ClipItem item, bool plain)
    {
        // WinForms' clipboard, not WPF's: WPF's SetDataObject fails on some machines (CLIPBRD_E_CANT_OPEN / E_NOTIMPL).
        var data = new Forms.DataObject();
        data.SetData(ClipboardWatcher.SelfFormat, new MemoryStream(new byte[] { 1 })); // so we don't record it again
        switch (item.Kind)
        {
            case ClipKind.Image:
                byte[] bytes;
                try { bytes = File.ReadAllBytes(item.ImagePath!); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
                var bmp = new System.Drawing.Bitmap(new MemoryStream(bytes));
                data.SetImage(bmp);
                data.SetData("PNG", false, new MemoryStream(bytes));
                break;
            case ClipKind.Files:
                var files = new StringCollection();
                foreach (var f in item.Files ?? new())
                    if (File.Exists(f) || Directory.Exists(f)) files.Add(f);
                if (files.Count == 0) return false;
                data.SetFileDropList(files);
                break;
            default:
                data.SetText(string.IsNullOrEmpty(item.Text) ? " " : item.Text, Forms.TextDataFormat.UnicodeText);
                if (!plain && !string.IsNullOrEmpty(item.Html)) data.SetText(item.Html, Forms.TextDataFormat.Html);
                if (!plain && !string.IsNullOrEmpty(item.Rtf)) data.SetText(item.Rtf, Forms.TextDataFormat.Rtf);
                break;
        }
        try
        {
            Forms.Clipboard.SetDataObject(data, true, 10, 50);
            return true;
        }
        catch (ExternalException) { return false; }
    }

    // Puts the clip on the clipboard, returns focus to the app you were in and presses Ctrl+V there.
    public async void Paste(ClipItem item, bool plain)
    {
        var target = reveal.PasteTarget != IntPtr.Zero ? reveal.PasteTarget : Native.GetForegroundWindow();
        if (!Copy(item, plain)) return;
        reveal.RetractAll();
        if (target == IntPtr.Zero || Native.IsOwn(target)) return;
        Native.ForceForeground(target);
        await Task.Delay(90);
        // Shift is usually still held (Shift+click, Shift+Enter); Ctrl+Shift+V would mean something else.
        foreach (ushort vk in new ushort[] { 0x10, 0x11 })
            if (Native.KeyDown(vk)) Native.SendKeys((vk, true));
        Native.SendKeys((0x11, false), (0x56, false), (0x56, true), (0x11, true));
    }

    // Double click.
    public void Open(ClipItem item)
    {
        switch (item.Kind)
        {
            case ClipKind.Image:
                reveal.RetractAll();
                Shell(item.ImagePath!);
                break;
            case ClipKind.Link:
                reveal.RetractAll();
                Shell(item.Text!.Trim());
                break;
            case ClipKind.Files:
                if (item.Files?.FirstOrDefault(p => File.Exists(p) || Directory.Exists(p)) is { } f)
                {
                    reveal.RetractAll();
                    Shell(f);
                }
                break;
            default:
                Edit(item);
                break;
        }
    }

    // Press and hold: Paint for images, the text editor for text.
    public void Edit(ClipItem item)
    {
        reveal.RetractAll();
        if (item.Kind == ClipKind.Image)
        {
            try { Process.Start(new ProcessStartInfo("mspaint.exe", $"\"{item.ImagePath}\"") { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception) { Shell(item.ImagePath!, "edit"); }
        }
        else if (item.Kind == ClipKind.Files) ShowInFolder(item);
        else Front(new TextEditWindow(item, store, this));
    }

    public void QuickLook(ClipItem item)
    {
        reveal.RetractAll();
        Front(new QuickLookWindow(item, Forms.Screen.FromPoint(Forms.Cursor.Position)));
    }

    public void Remove(ClipItem item) => store.Remove(item);

    public void TogglePin(ClipItem item) => store.SetPinned(item, !item.Pinned);

    public void ShowInFolder(ClipItem item)
    {
        var path = item.Kind == ClipKind.Image ? item.ImagePath : item.Files?.FirstOrDefault(p => File.Exists(p) || Directory.Exists(p));
        if (path == null) return;
        reveal.RetractAll();
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    public void SaveAs(ClipItem item)
    {
        reveal.RetractAll();
        bool image = item.Kind == ClipKind.Image;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"Clip {item.Time:yyyy-MM-dd HH.mm.ss}",
            DefaultExt = image ? ".png" : ".txt",
            Filter = image ? "PNG|*.png" : "Text|*.txt",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            if (image) File.Copy(item.ImagePath!, dlg.FileName, true);
            else File.WriteAllText(dlg.FileName, item.Text ?? "");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    // Text drags as text; images drag as a file with a readable name (plus the bitmap); files as themselves.
    // Always a copy: the clip stays on the line.
    public void DragOut(ClipItem item, CardView view)
    {
        var data = new DataObject();
        switch (item.Kind)
        {
            case ClipKind.Image:
                if (DragCopy(item) is { } file) data.SetFileDropList(new StringCollection { file });
                if (Thumbs.LoadFull(item.ImagePath!) is { } bmp) data.SetImage(bmp);
                break;
            case ClipKind.Files:
                var files = new StringCollection();
                foreach (var f in item.Files ?? new())
                    if (File.Exists(f) || Directory.Exists(f)) files.Add(f);
                if (files.Count == 0) return;
                data.SetFileDropList(files);
                break;
            default:
                data.SetText(item.Text ?? "");
                break;
        }
        reveal.HoldOpen++;
        try { DragDrop.DoDragDrop(view, data, DragDropEffects.Copy); }
        finally { reveal.HoldOpen--; }
    }

    static string? DragCopy(ClipItem item)
    {
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "DUN");
            Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, $"Image {item.Time:yyyy-MM-dd HH.mm.ss}{Path.GetExtension(item.ImagePath)}");
            File.Copy(item.ImagePath!, dest, true);
            return dest;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    public void ShowMenu(ClipItem item, CardView view)
    {
        var menu = new ContextMenu { PlacementTarget = view, Placement = PlacementMode.MousePoint };
        void Add(string text, Action action)
        {
            var mi = new MenuItem { Header = text };
            mi.Click += (_, _) => action();
            menu.Items.Add(mi);
        }

        Add(S.Copy, () => { if (Copy(item, false)) view.Flash(S.Copied); });
        Add(S.Paste, () => Paste(item, false));
        if (item.IsRich) Add(S.PastePlain, () => Paste(item, true));
        menu.Items.Add(new Separator());
        Add(item.Pinned ? S.Unpin : S.Pin, () => TogglePin(item));
        if (item.Kind == ClipKind.Image) Add(S.EditImage, () => Edit(item));
        else if (item.IsText) Add(S.Edit, () => Edit(item));
        if (item.Kind == ClipKind.Link) Add(S.OpenLink, () => Open(item));
        else if (item.Kind is ClipKind.Image or ClipKind.Files) Add(S.Open, () => Open(item));
        Add(S.QuickLook, () => QuickLook(item));
        if (item.Kind is ClipKind.Image or ClipKind.Files) Add(S.ShowInFolder, () => ShowInFolder(item));
        if (item.Kind != ClipKind.Files) Add(S.SaveAs, () => SaveAs(item));
        menu.Items.Add(new Separator());
        Add(S.Remove, () => Remove(item));

        reveal.HoldOpen++;
        menu.Closed += (_, _) => reveal.HoldOpen--;
        menu.IsOpen = true;
    }

    static void Front(Window w)
    {
        w.Show();
        Native.ForceForeground(new WindowInteropHelper(w).Handle);
        w.Activate();
    }

    static void Shell(string path, string? verb = null)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = verb ?? "" }); }
        catch (System.ComponentModel.Win32Exception) { }
    }
}
