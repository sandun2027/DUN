using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DUN;

static class Thumbs
{
    static readonly string[] ImageExts = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

    // Loads without keeping the file open, so Paint can overwrite it.
    public static BitmapSource? LoadFull(string path, int decodeHeight = 0)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad; // (IgnoreImageCache would throw: it needs a UriSource)
            if (decodeHeight > 0) bi.DecodePixelHeight = decodeHeight;
            bi.StreamSource = fs;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or UnauthorizedAccessException or ArgumentException or FileFormatException)
        {
            return null;
        }
    }

    // A small picture for a copied file: the image itself, or the file type's icon. Null for folders.
    public static ImageSource? FileIcon(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            if (ImageExts.Contains(Path.GetExtension(path).ToLowerInvariant())) return LoadFull(path, 64);
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon == null) return null;
            var src = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}

// Pixel fingerprint, used to avoid hanging the same image twice.
static class ImageHash
{
    public static string Of(BitmapSource src)
    {
        var bgra = src.Format == PixelFormats.Bgra32 ? src : new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        int stride = bgra.PixelWidth * 4;
        var px = new byte[stride * bgra.PixelHeight];
        bgra.CopyPixels(px, stride, 0);
        for (int i = 3; i < px.Length; i += 4) px[i] = 0; // clipboard DIBs often lose alpha
        return $"i:{bgra.PixelWidth}x{bgra.PixelHeight}:{Convert.ToHexString(SHA1.HashData(px))}";
    }
}
