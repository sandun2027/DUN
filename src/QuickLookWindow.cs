using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace DUN;

// Large preview over a dimmed screen: the full image, the full text (selectable), or a big colour swatch.
// Esc, Space or a click outside the content closes it.
sealed class QuickLookWindow : Window
{
    readonly Forms.Screen screen;
    bool closing;

    public QuickLookWindow(ClipItem item, Forms.Screen screen)
    {
        this.screen = screen;
        Title = S.QuickLook;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(0xD8, 0x10, 0x10, 0x10));
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;

        FrameworkElement content = item.Kind switch
        {
            ClipKind.Image => new Image { Source = Thumbs.LoadFull(item.ImagePath!), Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly },
            ClipKind.Color => ColorView(item),
            _ => TextView(item),
        };
        Content = new Grid { Margin = new Thickness(48), Children = { content } };

        SourceInitialized += (_, _) => Place();
        PreviewKeyDown += (_, e) => { if (e.Key is Key.Escape or Key.Space) { e.Handled = true; Close(); } };
        MouseLeftButtonUp += (_, _) => { if (item.Kind == ClipKind.Image || !content.IsMouseOver) Close(); };
        Deactivated += (_, _) => { if (!closing) Close(); };
        Closing += (_, _) => closing = true;
    }

    static FrameworkElement TextView(ClipItem item)
    {
        bool code = item.Kind == ClipKind.Code;
        var box = new TextBox
        {
            Text = item.Kind == ClipKind.Files ? string.Join(Environment.NewLine, item.Files ?? new()) : item.Text ?? "",
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = code ? new SolidColorBrush(Color.FromRgb(0xD5, 0xD9, 0xE0)) : Brushes.Black,
            FontSize = code ? 13 : 15,
            TextWrapping = code ? TextWrapping.NoWrap : TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(18),
        };
        if (code) box.FontFamily = new FontFamily("Consolas");
        return new Border
        {
            Background = code ? new SolidColorBrush(Color.FromRgb(0x23, 0x27, 0x2E)) : new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xF7)),
            CornerRadius = new CornerRadius(6),
            MaxWidth = 980,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = box,
        };
    }

    static FrameworkElement ColorView(ClipItem item)
    {
        var color = Classify.ParseColor(item.Text!) ?? Colors.Transparent;
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new Border
        {
            Width = 320, Height = 240, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(color),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)), BorderThickness = new Thickness(1),
        });
        panel.Children.Add(new TextBox
        {
            Text = $"{item.Text!.Trim()}    #{color.R:X2}{color.G:X2}{color.B:X2}    rgb({color.R}, {color.G}, {color.B})",
            IsReadOnly = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = Brushes.White,
            FontFamily = new FontFamily("Consolas"), FontSize = 16, Margin = new Thickness(0, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Center,
        });
        return panel;
    }

    void Place()
    {
        var b = screen.Bounds;
        Native.SetWindowPos(new WindowInteropHelper(this).Handle, Native.HWND_TOPMOST, b.Left, b.Top, b.Width, b.Height, 0);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dispatcher.BeginInvoke(Place);
    }
}
