using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace DUN;

// The look shared by DUN's own windows (settings, text editor) and the installer:
// white rounded card, quiet greys, one dark accent.
static class Theme
{
    public static readonly FontFamily Font = new("Microsoft YaHei UI, Segoe UI");
    public static readonly Color Ink = Color.FromRgb(0x25, 0x28, 0x2D);
    public static readonly Color Body = Color.FromRgb(0x5B, 0x66, 0x75);
    public static readonly Color Muted = Color.FromRgb(0x92, 0x9C, 0xAB);
    public static readonly Color Faint = Color.FromRgb(0xAD, 0xB5, 0xC0);
    public static readonly Color Line = Color.FromRgb(0xE9, 0xED, 0xF2);
    public static readonly Color Divider = Color.FromRgb(0xF1, 0xF3, 0xF6);
    public static readonly Color Surface = Color.FromRgb(0xF7, 0xF8, 0xFA);
    public static readonly Color Focus = Color.FromRgb(0x8C, 0xAB, 0xC8);

    public static SolidColorBrush B(Color c) => new(c);

    public static TextBlock Text(string text, double size, Color color, bool bold = false) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = B(color),
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
    };
}

// Borderless window: rounded white card with a soft shadow, a title bar you can drag, and a close button.
// Content is laid out at a fixed size and scaled down if the screen is too small for it.
class ChromeWindow : Window
{
    protected readonly Grid Body = new();

    public ChromeWindow(string title, double width, double height)
    {
        Title = "DUN " + title;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = Theme.Font;
        ShowInTaskbar = true;

        const double margin = 16;
        var work = SystemParameters.WorkArea;
        double scale = Math.Min(1, Math.Min((work.Height - 12) / (height + 2 * margin), (work.Width - 12) / (width + 2 * margin)));
        Width = (width + 2 * margin) * scale;
        Height = (height + 2 * margin) * scale;

        var bar = new Grid { Height = 60, Background = Brushes.Transparent };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(30, 0, 0, 0) };
        brand.Children.Add(new TextBlock { Text = "DUN", FontFamily = new FontFamily("Segoe UI"), FontWeight = FontWeights.Bold, FontSize = 19, Foreground = Theme.B(Theme.Ink) });
        brand.Children.Add(new TextBlock { Text = title, FontSize = 11, Foreground = Theme.B(Theme.Muted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 3, 0, 0) });
        bar.Children.Add(brand);
        var close = new Pill(null, false)
        {
            Width = 34, Height = 34, Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 20, 0),
            Child = new Path { Data = Geometry.Parse("M 0,0 L 10,10 M 10,0 L 0,10"), Stroke = Theme.B(Color.FromRgb(0x90, 0x99, 0xA7)), StrokeThickness = 1.3, Width = 10, Height = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        close.Click += Cancel;
        bar.Children.Add(close);
        bar.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource == bar || e.OriginalSource is TextBlock || e.OriginalSource is StackPanel) DragMove(); };

        var layout = new Grid { Width = width, Height = height };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition());
        layout.Children.Add(bar);
        Grid.SetRow(Body, 1);
        layout.Children.Add(Body);

        var card = new Border
        {
            Margin = new Thickness(margin),
            CornerRadius = new CornerRadius(22),
            Background = Brushes.White,
            BorderBrush = Theme.B(Theme.Line),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { BlurRadius = 25, ShadowDepth = 5, Opacity = 0.14, Color = Color.FromRgb(0x27, 0x34, 0x49) },
            Child = layout,
        };
        Content = new Viewbox { Stretch = Stretch.Uniform, Child = new Grid { Width = width + 2 * margin, Height = height + 2 * margin, Children = { card } } };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && !e.Handled) { e.Handled = true; Cancel(); } };
    }

    protected virtual void Cancel() => Close();

    // A white panel with a title, for grouping rows.
    public static Border Card(string title, params UIElement[] rows)
    {
        var stack = new StackPanel();
        stack.Children.Add(Theme.Text(title, 15, Theme.Ink, bold: true));
        for (int i = 0; i < rows.Length; i++)
        {
            if (i > 0) stack.Children.Add(new Border { Height = 1, Background = Theme.B(Theme.Divider) });
            stack.Children.Add(rows[i]);
        }
        return new Border
        {
            CornerRadius = new CornerRadius(16),
            Background = Brushes.White,
            BorderBrush = Theme.B(Theme.Line),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(20, 16, 20, 6),
            Child = stack,
        };
    }

    // Title and description on the left, the control on the right.
    public static Grid Row(string title, string? note, UIElement control)
    {
        var g = new Grid { Margin = new Thickness(0, 12, 0, 12) };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        left.Children.Add(Theme.Text(title, 13, Theme.Ink));
        if (note != null)
        {
            var n = Theme.Text(note, 11, Theme.Muted);
            n.Margin = new Thickness(0, 4, 0, 0);
            left.Children.Add(n);
        }
        g.Children.Add(left);
        if (control is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        g.Children.Add(control);
        return g;
    }

    // Title and description above a full-width control.
    public static StackPanel Stacked(string title, string? note, UIElement control)
    {
        var s = new StackPanel { Margin = new Thickness(0, 12, 0, 14) };
        s.Children.Add(Theme.Text(title, 13, Theme.Ink));
        if (note != null)
        {
            var n = Theme.Text(note, 11, Theme.Muted);
            n.Margin = new Thickness(0, 4, 0, 0);
            s.Children.Add(n);
        }
        if (control is FrameworkElement fe) fe.Margin = new Thickness(0, 10, 0, 0);
        s.Children.Add(control);
        return s;
    }
}

// Rounded button: dark and filled for the main action, plain grey text otherwise.
sealed class Pill : Border
{
    readonly TextBlock? label;
    public event Action? Click;

    public Pill(string? text, bool primary)
    {
        CornerRadius = new CornerRadius(12);
        Padding = new Thickness(18, 10, 18, 10);
        Background = primary ? Theme.B(Theme.Ink) : Brushes.Transparent;
        Cursor = Cursors.Hand;
        Focusable = true;
        if (text != null)
        {
            label = new TextBlock
            {
                Text = text,
                FontSize = 13,
                Foreground = primary ? Brushes.White : Theme.B(Color.FromRgb(0x69, 0x71, 0x7D)),
                FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Child = label;
        }
        if (!primary)
        {
            MouseEnter += (_, _) => Background = Theme.B(Theme.Surface);
            MouseLeave += (_, _) => Background = Brushes.Transparent;
        }
        else
        {
            MouseEnter += (_, _) => Opacity = 0.86;
            MouseLeave += (_, _) => Opacity = 1;
        }
        MouseLeftButtonUp += (_, e) => { e.Handled = true; Click?.Invoke(); };
        KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { e.Handled = true; Click?.Invoke(); } };
    }

    public string Text { set { if (label != null) label.Text = value; } }
}

// iOS-style switch.
sealed class Toggle : Border
{
    readonly TranslateTransform knob = new();
    readonly SolidColorBrush track = new();
    bool on;
    public event Action? Changed;

    public Toggle(bool value)
    {
        Width = 40;
        Height = 24;
        CornerRadius = new CornerRadius(12);
        Padding = new Thickness(3);
        Background = track;
        Cursor = Cursors.Hand;
        Focusable = true;
        Child = new Ellipse
        {
            Width = 18, Height = 18, Fill = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, RenderTransform = knob,
            Effect = new DropShadowEffect { BlurRadius = 4, ShadowDepth = 1, Opacity = 0.18 },
        };
        on = value;
        Render(false);
        MouseLeftButtonUp += (_, e) => { e.Handled = true; Flip(); };
        KeyDown += (_, e) => { if (e.Key is Key.Space or Key.Enter) { e.Handled = true; Flip(); } };
    }

    public bool IsOn => on;

    void Flip()
    {
        on = !on;
        Render(true);
        Changed?.Invoke();
    }

    void Render(bool animate)
    {
        var color = on ? Theme.Ink : Color.FromRgb(0xDC, 0xE1, 0xE8);
        double x = on ? 16 : 0;
        if (!animate) { track.Color = color; knob.X = x; return; }
        var d = TimeSpan.FromMilliseconds(160);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        track.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color, d));
        knob.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, d) { EasingFunction = ease });
    }
}

// A row of mutually exclusive choices in one rounded track; the chosen one is a raised white chip.
sealed class Segmented : Border
{
    readonly Border[] chips;
    int selected = -1;
    public event Action? Changed;

    public Segmented(int value, params string[] options)
    {
        CornerRadius = new CornerRadius(11);
        Background = Theme.B(Color.FromRgb(0xF0, 0xF2, 0xF5));
        Padding = new Thickness(3);
        HorizontalAlignment = HorizontalAlignment.Left;
        var grid = new UniformGrid { Rows = 1 };
        chips = new Border[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            chips[i] = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                Child = new TextBlock { Text = options[i], FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center },
            };
            chips[i].MouseLeftButtonUp += (_, e) => { e.Handled = true; Selected = index; Changed?.Invoke(); };
            grid.Children.Add(chips[i]);
        }
        Child = grid;
        Selected = value;
    }

    public int Selected
    {
        get => selected;
        set
        {
            selected = Math.Clamp(value, 0, chips.Length - 1);
            for (int i = 0; i < chips.Length; i++)
            {
                bool on = i == selected;
                chips[i].Background = on ? Brushes.White : Brushes.Transparent;
                chips[i].Effect = on ? new DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.10 } : null;
                var t = (TextBlock)chips[i].Child;
                t.Foreground = Theme.B(on ? Theme.Ink : Color.FromRgb(0x6B, 0x74, 0x80));
                t.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }
    }
}

static class Fields
{
    // A borderless TextBox inside a soft grey rounded field that outlines itself when focused.
    public static Border Wrap(TextBox box)
    {
        box.BorderThickness = new Thickness(0);
        box.Background = Brushes.Transparent;
        box.Foreground = Theme.B(Color.FromRgb(0x44, 0x52, 0x66));
        box.FontSize = 12.5;
        box.CaretBrush = Theme.B(Theme.Ink);
        var field = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = Theme.B(Theme.Surface),
            BorderBrush = Theme.B(Theme.Line),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 7, 10, 7),
            Child = box,
        };
        box.GotKeyboardFocus += (_, _) => { field.BorderBrush = Theme.B(Theme.Focus); field.Background = Brushes.White; };
        box.LostKeyboardFocus += (_, _) => { field.BorderBrush = Theme.B(Theme.Line); field.Background = Theme.B(Theme.Surface); };
        return field;
    }

    // − value + in one field.
    public static Border Stepper(int value, int min, int max, int step, Action<int> changed)
    {
        var text = new TextBlock { Text = value.ToString(), FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Theme.B(Theme.Ink), MinWidth = 44, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Border Key(string glyph, int delta)
        {
            var k = new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(8), Cursor = Cursors.Hand, Background = Brushes.Transparent,
                Child = new TextBlock { Text = glyph, FontSize = 15, Foreground = Theme.B(Theme.Body), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -2, 0, 0) },
            };
            k.MouseEnter += (_, _) => k.Background = Brushes.White;
            k.MouseLeave += (_, _) => k.Background = Brushes.Transparent;
            k.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                value = Math.Clamp(value + delta, min, max);
                text.Text = value.ToString();
                changed(value);
            };
            return k;
        }
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(Key("−", -step));
        row.Children.Add(text);
        row.Children.Add(Key("+", step));
        return new Border
        {
            CornerRadius = new CornerRadius(11),
            Background = Theme.B(Theme.Surface),
            BorderBrush = Theme.B(Theme.Line),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(3),
            Child = row,
        };
    }

    // Shortcut shown as keycaps: [Ctrl] [Alt] [V].
    public static StackPanel Keycaps(string combo, bool recording)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var part in combo.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            row.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(7),
                Background = Brushes.White,
                BorderBrush = Theme.B(recording ? Theme.Focus : Color.FromRgb(0xDD, 0xE2, 0xE9)),
                BorderThickness = new Thickness(1, 1, 1, 2),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 5, 0),
                Child = new TextBlock { Text = part, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Theme.B(Theme.Ink) },
            });
        }
        return row;
    }
}
