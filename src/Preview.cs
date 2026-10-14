using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DUN;

// `DUN.exe --preview out.png` renders one card of each kind, on a light and a dark backdrop,
// for checking the look without touching the clipboard history (and for README screenshots).
static class Preview
{
    public static void Render(string output, Action done)
    {
        var sample = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dun-preview.png");
        SaveSampleImage(sample);
        var items = new List<ClipItem>
        {
            new() { Kind = ClipKind.Text, Text = "会议纪要：下周一上午十点在三楼会议室，讨论第四季度的发布计划和人员安排。", Pinned = true },
            new() { Kind = ClipKind.Image, Image = sample, Width = 1600, Height = 1000 },
            new() { Kind = ClipKind.Link, Text = "https://github.com/alejandrobujan/tendedero" },
            new() { Kind = ClipKind.Code, Text = "function hang(clip) {\n    if (clip.pinned) return;\n    line.unshift(clip);\n}" },
            new() { Kind = ClipKind.Color, Text = "#3B82F6" },
            new() { Kind = ClipKind.Files, Files = new() { @"C:\Windows\notepad.exe", @"C:\Windows\regedit.exe" } },
            new() { Kind = ClipKind.Text, Text = "Rich text keeps its formatting when pasted.", Html = "<b>x</b>" },
        };

        const double W = 1240, Row = 190;
        var root = new Canvas { Width = W, Height = Row * 2 };
        for (int row = 0; row < 2; row++)
        {
            bool dark = row == 1;
            var band = new Rectangle { Width = W, Height = Row, Fill = dark ? Brush(0x1F, 0x29, 0x37) : Brush(0xF3, 0xF4, 0xF6) };
            Canvas.SetTop(band, row * Row);
            root.Children.Add(band);
            var rope = new System.Windows.Shapes.Path
            {
                Stroke = Brush(0x8A, 0x76, 0x5E),
                StrokeThickness = 1.8,
                Data = Geometry.Parse($"M 0,{row * Row + 14} Q {W / 2},{row * Row + 22} {W},{row * Row + 14}"),
            };
            root.Children.Add(rope);
            double x = 30;
            for (int i = 0; i < items.Count; i++)
            {
                var v = new CardView(items[i], null!) { Number = dark ? i + 1 : 0, Selected = dark && i == 2 };
                Canvas.SetLeft(v, x);
                Canvas.SetTop(v, row * Row + 8);
                root.Children.Add(v);
                x += v.Width + 22;
            }
        }

        // Animations (selection ring, lift) need a live window to run; render once they've settled.
        var host = new Window
        {
            WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false,
            Left = -10000, Top = -10000, SizeToContent = SizeToContent.WidthAndHeight, Content = root,
        };
        host.Show();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var rtb = new RenderTargetBitmap((int)(W * 2), (int)(Row * 4), 192, 192, PixelFormats.Pbgra32);
            rtb.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(output)) encoder.Save(fs);
            host.Close();
            done();
        };
        timer.Start();
    }

    static void SaveSampleImage(string path)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x60, 0xA5, 0xFA), Color.FromRgb(0x81, 0x8C, 0xF8), 30), null, new Rect(0, 0, 320, 200));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0xC0, 0xFD, 0xE6, 0x8A)), null, new Point(250, 60), 30, 30);
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x8A)), null, Geometry.Parse("M 0,200 L 90,110 L 160,170 L 230,100 L 320,200 Z"));
        }
        var bmp = new RenderTargetBitmap(320, 200, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(dv);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }

    static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
