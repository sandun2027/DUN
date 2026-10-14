using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DUN;

enum ClipKind { Text, Link, Code, Color, Image, Files }

// One thing that was copied. Text lives in the index; images are PNG files in the Images folder.
sealed class ClipItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ClipKind Kind { get; set; }
    public string? Text { get; set; }
    public string? Html { get; set; }
    public string? Rtf { get; set; }
    public string? Image { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public List<string>? Files { get; set; }
    public bool Pinned { get; set; }
    public DateTime Time { get; set; }
    public string Hash { get; set; } = "";

    [JsonIgnore] public bool IsRich => Html != null || Rtf != null;
    [JsonIgnore] public bool IsText => Kind is ClipKind.Text or ClipKind.Link or ClipKind.Code or ClipKind.Color;
    [JsonIgnore] public string? ImagePath => Image == null ? null : Path.Combine(ClipStore.ImageDir, Image);

    public string SearchText() =>
        Kind == ClipKind.Files ? string.Join(" ", (Files ?? new()).Select(f => Path.GetFileName(f))) : Text ?? "";
}

static class Classify
{
    public static ClipKind Text(string text)
    {
        var t = text.Trim();
        if (t.Length <= 40 && ParseColor(t) != null) return ClipKind.Color;
        if (t.IndexOfAny(new[] { ' ', '\n', '\t' }) < 0 && Uri.TryCreate(t, UriKind.Absolute, out var u)
            && u.Scheme is "http" or "https" or "ftp" or "mailto") return ClipKind.Link;
        if (LooksLikeCode(text)) return ClipKind.Code;
        return ClipKind.Text;
    }

    static bool LooksLikeCode(string s)
    {
        var lines = s.Replace("\r\n", "\n").Split('\n').Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count < 2) return false;
        int score = 0;
        foreach (var l in lines)
        {
            if (l.StartsWith("    ") || l.StartsWith('\t')) score++;
            var end = l.TrimEnd();
            if (end.EndsWith(';') || end.EndsWith('{') || end.EndsWith('}') || end.EndsWith(')')) score++;
            if (l.Contains("=>") || l.Contains("==") || l.Contains("()") || l.Contains("</") || l.Contains("//")) score++;
        }
        return score >= Math.Max(3, lines.Count * 0.6);
    }

    // CSS-style colours: #rgb, #rgba, #rrggbb, #rrggbbaa, rgb(), rgba().
    public static Color? ParseColor(string s)
    {
        s = s.Trim();
        try
        {
            if (Regex.IsMatch(s, "^#([0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"))
            {
                var h = s[1..];
                if (h.Length <= 4) h = string.Concat(h.Select(c => new string(c, 2)));
                byte B(int i) => Convert.ToByte(h.Substring(i, 2), 16);
                return Color.FromArgb(h.Length == 8 ? B(6) : (byte)255, B(0), B(2), B(4));
            }
            var m = Regex.Match(s, @"^rgba?\(\s*([\d.]+)[\s,]+([\d.]+)[\s,]+([\d.]+)(?:[\s,/]+([\d.]+%?))?\s*\)$", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            byte C(int g) => (byte)Math.Clamp(double.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture), 0, 255);
            double a = 1;
            if (m.Groups[4].Success)
            {
                var v = m.Groups[4].Value;
                a = v.EndsWith('%') ? double.Parse(v[..^1], CultureInfo.InvariantCulture) / 100 : double.Parse(v, CultureInfo.InvariantCulture);
            }
            return Color.FromArgb((byte)(Math.Clamp(a, 0, 1) * 255), C(1), C(2), C(3));
        }
        catch (Exception e) when (e is FormatException or OverflowException) { return null; }
    }

    public static string HashText(string s) => "t:" + Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(s)));
    public static string HashFiles(IEnumerable<string> files) =>
        "f:" + Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(string.Join("\n", files).ToLowerInvariant())));
}

// The clipboard history. Items[0] is the most recently copied.
sealed class ClipStore : IDisposable
{
    public static string Root { get; set; } = Settings.DataDir; // --shots points this at sample data
    public static string ImageDir => Path.Combine(Root, "Images");
    static string IndexPath => Path.Combine(Root, "clips.json");

    public ObservableCollection<ClipItem> Items { get; } = new();
    public event Action<ClipItem>? Bumped;  // copied again: moved back to the front
    public event Action<ClipItem>? Updated; // pinned or edited: its card needs redrawing

    readonly Func<Settings> settings;
    readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    readonly DispatcherTimer sweepTimer = new() { Interval = TimeSpan.FromHours(1) };
    readonly Dictionary<string, DispatcherTimer> pending = new(StringComparer.OrdinalIgnoreCase);
    readonly FileSystemWatcher watcher;

    public ClipStore(Func<Settings> settings)
    {
        this.settings = settings;
        Directory.CreateDirectory(ImageDir);
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Save(); };
        sweepTimer.Tick += (_, _) => Sweep();
        Load();
        ImportOldLine();
        Sweep();
        sweepTimer.Start();

        // Notices edits saved by Paint and images deleted by hand.
        watcher = new FileSystemWatcher(ImageDir) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size };
        watcher.Changed += (_, e) => OnDisk(e.Name);
        watcher.Created += (_, e) => OnDisk(e.Name);
        watcher.Deleted += (_, e) => OnDisk(e.Name);
        watcher.Renamed += (_, e) => { OnDisk(e.OldName); OnDisk(e.Name); };
        watcher.EnableRaisingEvents = true;
    }

    public void Add(ClipItem item, BitmapSource? bitmap = null)
    {
        var same = Items.FirstOrDefault(i => i.Hash == item.Hash);
        if (same != null)
        {
            same.Time = DateTime.Now;
            int at = Items.IndexOf(same);
            if (at > 0) Items.Move(at, 0);
            Bumped?.Invoke(same);
            ScheduleSave();
            return;
        }
        if (bitmap != null)
        {
            item.Image = item.Id + ".png";
            try
            {
                using var fs = File.Create(item.ImagePath!);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(fs);
            }
            catch (IOException) { return; }
            item.Width = bitmap.PixelWidth;
            item.Height = bitmap.PixelHeight;
        }
        item.Time = DateTime.Now;
        Items.Insert(0, item);
        Sweep();
        ScheduleSave();
    }

    public void Remove(ClipItem item)
    {
        Items.Remove(item);
        if (item.ImagePath is { } path)
        {
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        ScheduleSave();
    }

    public void SetPinned(ClipItem item, bool pinned)
    {
        item.Pinned = pinned;
        Updated?.Invoke(item);
        ScheduleSave();
    }

    public void UpdateText(ClipItem item, string text)
    {
        if (text == item.Text) return;
        item.Text = text;
        item.Html = item.Rtf = null;
        item.Kind = Classify.Text(text);
        item.Hash = Classify.HashText(text);
        Updated?.Invoke(item);
        ScheduleSave();
    }

    public void ClearUnpinned()
    {
        foreach (var item in Items.Where(i => !i.Pinned).ToList()) Remove(item);
    }

    // Enforces the item limit and the retention period. Pinned items are never dropped.
    public void Sweep()
    {
        var s = settings();
        var cutoff = s.RetentionDays > 0 ? DateTime.Now.AddDays(-s.RetentionDays) : DateTime.MinValue;
        int kept = 0;
        foreach (var item in Items.ToList())
        {
            if (item.Pinned) continue;
            if (item.Time < cutoff || ++kept > s.MaxItems) Remove(item);
        }
    }

    void OnDisk(string? name) => dispatcher.BeginInvoke(() =>
    {
        if (name == null || Items.FirstOrDefault(i => string.Equals(i.Image, name, StringComparison.OrdinalIgnoreCase)) is not { } item) return;
        // Editors often save by delete + rename, so wait for the dust to settle before deciding.
        if (!pending.TryGetValue(name, out var timer))
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            timer.Tick += (_, _) => { timer.Stop(); pending.Remove(name); Settle(item); };
            pending[name] = timer;
        }
        timer.Stop();
        timer.Start();
    });

    void Settle(ClipItem item)
    {
        if (!Items.Contains(item)) return;
        if (!File.Exists(item.ImagePath)) { Items.Remove(item); ScheduleSave(); return; }
        if (Thumbs.LoadFull(item.ImagePath!) is not { } bmp) return;
        var hash = ImageHash.Of(bmp);
        if (hash == item.Hash) return; // our own write
        item.Hash = hash;
        item.Width = bmp.PixelWidth;
        item.Height = bmp.PixelHeight;
        Updated?.Invoke(item);
        ScheduleSave();
    }

    void ScheduleSave()
    {
        saveTimer.Stop();
        saveTimer.Start();
    }

    void Load()
    {
        try
        {
            var list = JsonSerializer.Deserialize<List<ClipItem>>(File.ReadAllText(IndexPath));
            foreach (var item in list ?? new())
                if (item.Kind != ClipKind.Image || File.Exists(item.ImagePath)) Items.Add(item);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
    }

    void Save()
    {
        try
        {
            Directory.CreateDirectory(Root);
            var tmp = IndexPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Items.ToList()));
            File.Move(tmp, IndexPath, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    // The screenshot version kept its photos in Line\; bring them over once.
    void ImportOldLine()
    {
        var old = Path.Combine(Root, "Line");
        if (!Directory.Exists(old)) return;
        foreach (var f in new DirectoryInfo(old).GetFiles().OrderBy(f => f.CreationTimeUtc))
        {
            if (Thumbs.LoadFull(f.FullName) is not { } bmp) continue;
            var item = new ClipItem
            {
                Kind = ClipKind.Image, Time = f.CreationTime, Width = bmp.PixelWidth, Height = bmp.PixelHeight, Hash = ImageHash.Of(bmp),
            };
            item.Image = item.Id + f.Extension.ToLowerInvariant();
            try { File.Move(f.FullName, item.ImagePath!); Items.Insert(0, item); }
            catch (IOException) { }
        }
        try { Directory.Delete(old); } catch (IOException) { } // only when nothing is left
        ScheduleSave();
    }

    public void Dispose()
    {
        watcher.Dispose();
        sweepTimer.Stop();
        saveTimer.Stop();
        Save();
    }
}
