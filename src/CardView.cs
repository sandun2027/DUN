using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace DUN;

// One clip on the line: a photo, a sticky note, a code card, a link card, a colour chip or a file
// card, held by a peg (red when pinned). Handles swing physics, hover/selection and the mouse gestures.
sealed class CardView : Grid
{
    public const double PegDrop = 10; // card top sits this far below the peg top
    const double ImageHeight = 92, Radius = 10, TallestCard = 104;
    static readonly Color Accent = Color.FromRgb(0x3B, 0x82, 0xF6);

    public ClipItem Item { get; }
    public double Angle, Velocity; // degrees, degrees per second

    readonly Actions actions;
    readonly Border frame, close, badge, number, ring;
    readonly TextBlock badgeText = new() { Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.SemiBold };
    readonly TextBlock numberText = new() { FontSize = 10.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly ScaleTransform scale = new();
    readonly RotateTransform rotate = new();
    readonly DropShadowEffect shadow = new() { BlurRadius = 16, ShadowDepth = 4, Direction = 270, Opacity = 0.26 };
    readonly DispatcherTimer holdTimer = new() { Interval = TimeSpan.FromMilliseconds(550) };
    Point downAt;
    bool pressed, hover, selected;
    int shownNumber = -1;

    // Card size (without the peg), known from the item alone so the line can lay out unrealized cards.
    public static Size SizeOf(ClipItem item) => item.Kind switch
    {
        ClipKind.Image => new Size(Math.Clamp(ImageHeight * Aspect(item), 64, 200) + 10, ImageHeight + 10),
        ClipKind.Text => new Size(150, 104),
        ClipKind.Code => new Size(184, 104),
        ClipKind.Link => new Size(180, 76),
        ClipKind.Color => new Size(88, 94),
        _ => new Size(170, 62),
    };

    static double Aspect(ClipItem i) => i.Width > 0 && i.Height > 0 ? (double)i.Width / i.Height : 1.6;

    public CardView(ClipItem item, Actions actions)
    {
        Item = item;
        this.actions = actions;
        var size = SizeOf(item);
        Width = size.Width;
        Height = PegDrop + size.Height;
        RenderTransformOrigin = new Point(0.5, 0);
        RenderTransform = new TransformGroup { Children = { scale, rotate } };

        // Focus ring: an accent outline with a soft glow, a few pixels clear of the card.
        ring = new Border
        {
            CornerRadius = new CornerRadius(Radius + 4),
            BorderThickness = new Thickness(2),
            BorderBrush = new SolidColorBrush(Accent),
            Background = new SolidColorBrush(Color.FromArgb(0x24, Accent.R, Accent.G, Accent.B)),
            Margin = new Thickness(-5, PegDrop - 5, -5, -5),
            Effect = new DropShadowEffect { Color = Accent, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.85 },
            Opacity = 0,
            IsHitTestVisible = false,
        };

        frame = new Border
        {
            Width = size.Width,
            Height = size.Height,
            Margin = new Thickness(0, PegDrop, 0, 0),
            VerticalAlignment = VerticalAlignment.Top,
            CornerRadius = new CornerRadius(Radius),
            BorderThickness = new Thickness(1),
            BorderBrush = B(0, 0, 0, 0x1C),
            Effect = shadow,
            Cursor = Cursors.Hand,
        };
        badge = new Border
        {
            Background = B(0x11, 0x18, 0x27, 0xE6),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0,
            IsHitTestVisible = false,
            Child = badgeText,
        };
        var inner = new Grid();
        inner.Children.Add(Dress(item));
        inner.Children.Add(badge);
        Round(inner, Math.Max(3, Radius - 1 - frame.Padding.Left));
        frame.Child = inner;

        var peg = new Border
        {
            Width = 10,
            Height = 22,
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Background = item.Pinned
                ? Horizontal(Color.FromRgb(0xFC, 0x8A, 0x8A), Color.FromRgb(0xDC, 0x26, 0x26), Color.FromRgb(0xB9, 0x1C, 0x1C))
                : Horizontal(Color.FromRgb(0xEE, 0xD9, 0xB6), Color.FromRgb(0xCB, 0xA4, 0x74), Color.FromRgb(0xA9, 0x82, 0x55)),
            BorderBrush = B(0, 0, 0, 0x40),
            BorderThickness = new Thickness(0.8),
            IsHitTestVisible = false,
            Child = new Border // the spring
            {
                Width = 4,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = B(0, 0, 0, 0x55),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2),
            },
        };

        // Ctrl+number keycap under the card in keyboard mode.
        number = new Border
        {
            MinWidth = 20,
            Height = 18,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(5, 0, 5, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = B(0, 0, 0, 0x22),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, -(TallestCard - size.Height) - 27), // one baseline for every card, below the tallest
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            Child = numberText,
        };

        close = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = B(0x11, 0x18, 0x27, 0xEE),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1.5),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, PegDrop - 7, -7, 0),
            Opacity = 0,
            IsHitTestVisible = false,
            Cursor = Cursors.Hand,
            Child = new TextBlock
            {
                Text = "",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Foreground = Brushes.White,
                FontSize = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        close.MouseLeftButtonDown += (_, e) => { e.Handled = true; actions.Remove(Item); };

        Children.Add(ring);
        Children.Add(frame);
        Children.Add(peg);
        Children.Add(number);
        Children.Add(close);
        Restyle();

        MouseEnter += (_, _) =>
        {
            hover = true;
            close.IsHitTestVisible = true;
            Fade(close, 1, 120);
            Wobble();
            Restyle();
        };
        MouseLeave += (_, _) =>
        {
            hover = false;
            close.IsHitTestVisible = false;
            Fade(close, 0, 160);
            Restyle();
        };
        frame.MouseLeftButtonDown += OnDown;
        frame.MouseMove += OnMove;
        frame.MouseLeftButtonUp += OnUp;
        frame.MouseDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Middle) return;
            e.Handled = true;
            actions.QuickLook(Item);
        };
        frame.MouseRightButtonUp += (_, e) => { e.Handled = true; actions.ShowMenu(Item, this); };
        holdTimer.Tick += OnHold;
    }

    public void Detach() => holdTimer.Stop();

    public bool Selected
    {
        set
        {
            if (value == selected) return;
            selected = value;
            Fade(ring, value ? 1 : 0, 160);
            Restyle();
        }
    }

    // Ctrl+1…9 shortcut shown in keyboard mode; 0 hides it.
    public int Number
    {
        set
        {
            if (value == shownNumber) return;
            shownNumber = value;
            numberText.Text = value.ToString();
            number.Visibility = value > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // Selected cards and the one under the pointer lift a little: bigger, deeper shadow, on top.
    void Restyle()
    {
        double s = selected ? 1.07 : hover ? 1.035 : 1;
        var a = new DoubleAnimation(s, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        bool lifted = selected || hover;
        shadow.BlurRadius = lifted ? 24 : 16;
        shadow.ShadowDepth = lifted ? 7 : 4;
        shadow.Opacity = lifted ? 0.34 : 0.26;
        Panel.SetZIndex(this, selected ? 20 : hover ? 10 : 0);
        number.Background = selected ? new SolidColorBrush(Accent) : B(0xFF, 0xFF, 0xFF, 0xF2);
        numberText.Foreground = selected ? Brushes.White : B(0x33, 0x41, 0x55);
    }

    public void Wobble() => Velocity += (Random.Shared.Next(2) == 0 ? -1 : 1) * 40;

    public void Flash(string text)
    {
        badgeText.Text = "✓  " + text;
        var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1100) };
        a.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
        a.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(800))));
        a.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1100))));
        badge.BeginAnimation(OpacityProperty, a);
    }

    // Damped pendulum around the peg, with the occasional gust of breeze.
    public void Step(double dt, Random rng)
    {
        if (rng.NextDouble() < dt * 0.12) Velocity += (rng.NextDouble() - 0.5) * 10;
        Velocity += (-40 * Angle - 1.6 * Velocity) * dt;
        Angle += Velocity * dt;
        rotate.Angle = Angle;
    }

    // Styles the frame for the item's kind and returns what goes inside it.
    UIElement Dress(ClipItem item)
    {
        switch (item.Kind)
        {
            case ClipKind.Image:
            {
                frame.Background = Brushes.White;
                frame.Padding = new Thickness(4);
                var g = new Grid { Background = B(0x1F, 0x29, 0x37) };
                g.Children.Add(new Image { Source = Thumbs.LoadFull(item.ImagePath!, 200), Stretch = Stretch.UniformToFill });
                return g;
            }
            case ClipKind.Code:
            {
                frame.Background = B(0x1F, 0x24, 0x30);
                frame.BorderBrush = B(0xFF, 0xFF, 0xFF, 0x14);
                frame.Padding = new Thickness(10, 8, 10, 6);
                var lines = (item.Text ?? "").Replace("\r\n", "\n").Split('\n');
                var preview = string.Join("\n", lines.SkipWhile(l => l.Trim().Length == 0).Take(8)).Replace("\t", "    ");
                var dots = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                foreach (var c in new[] { Color.FromRgb(0xF8, 0x71, 0x71), Color.FromRgb(0xFB, 0xBF, 0x24), Color.FromRgb(0x34, 0xD3, 0x99) })
                    dots.Children.Add(new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(c), Margin = new Thickness(0, 0, 4, 0) });
                return Note(preview, string.Format(S.Lines, lines.Length), B(0xE2, 0xE8, 0xF0), B(0x64, 0x74, 0x8B),
                    wrap: false, mono: true, rich: false, header: dots);
            }
            case ClipKind.Link:
            {
                frame.Background = Brushes.White;
                frame.Padding = new Thickness(10, 9, 10, 8);
                bool mail = item.Text!.Trim().StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
                var (domain, rest) = SplitLink(item.Text!.Trim());
                var head = new DockPanel();
                var icon = new Border
                {
                    Width = 20,
                    Height = 20,
                    CornerRadius = new CornerRadius(10),
                    Background = B(0xEF, 0xF6, 0xFF),
                    Margin = new Thickness(0, 0, 7, 0),
                    Child = new TextBlock
                    {
                        Text = mail ? "" : "",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Accent),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                };
                DockPanel.SetDock(icon, Dock.Left);
                head.Children.Add(icon);
                head.Children.Add(new TextBlock
                {
                    Text = domain, FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis, Foreground = B(0x1D, 0x4E, 0xD8),
                });
                var stack = new StackPanel();
                stack.Children.Add(head);
                stack.Children.Add(new TextBlock
                {
                    Text = rest, FontSize = 11, Margin = new Thickness(0, 5, 0, 0), MaxHeight = 30,
                    TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = B(0x64, 0x74, 0x8B),
                });
                return stack;
            }
            case ClipKind.Color:
            {
                frame.Background = Brushes.White;
                frame.Padding = new Thickness(5);
                var dock = new DockPanel();
                var label = new TextBlock
                {
                    Text = item.Text!.Trim(), FontFamily = new FontFamily("Consolas"), FontSize = 10, Margin = new Thickness(0, 4, 0, 1),
                    HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = B(0x47, 0x55, 0x69),
                };
                DockPanel.SetDock(label, Dock.Bottom);
                dock.Children.Add(label);
                var color = Classify.ParseColor(item.Text!) ?? Colors.Transparent;
                dock.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(color),
                    BorderBrush = B(0, 0, 0, 0x1A),
                    BorderThickness = new Thickness(1),
                });
                return dock;
            }
            case ClipKind.Files:
            {
                frame.Background = Vertical(Color.FromRgb(0xF8, 0xFA, 0xFC), Color.FromRgb(0xEC, 0xF0, 0xF5));
                frame.Padding = new Thickness(9, 8, 10, 8);
                var files = item.Files ?? new();
                var first = files.FirstOrDefault() ?? "";
                UIElement icon = Thumbs.FileIcon(first) is { } src
                    ? new Image { Source = src, Width = 24, Height = 24 }
                    : new TextBlock
                    {
                        Text = Directory.Exists(first) ? "" : "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 18,
                        Foreground = B(0x64, 0x74, 0x8B), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    };
                var tile = new Border
                {
                    Width = 40,
                    Height = 40,
                    CornerRadius = new CornerRadius(9),
                    Background = Brushes.White,
                    BorderBrush = B(0, 0, 0, 0x14),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 9, 0),
                    Child = icon,
                };
                var dock = new DockPanel();
                DockPanel.SetDock(tile, Dock.Left);
                dock.Children.Add(tile);
                var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                stack.Children.Add(new TextBlock
                {
                    Text = Path.GetFileName(first.TrimEnd('\\')), FontSize = 12, FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis, Foreground = B(0x0F, 0x17, 0x2A),
                });
                stack.Children.Add(new TextBlock
                {
                    Text = files.Count > 1 ? string.Format(S.MoreFiles, files.Count - 1) : Path.GetFileName(Path.GetDirectoryName(first.TrimEnd('\\')) ?? ""),
                    FontSize = 10.5, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, Foreground = B(0x64, 0x74, 0x8B),
                });
                dock.Children.Add(stack);
                return dock;
            }
            default:
            {
                frame.Background = Vertical(Color.FromRgb(0xFF, 0xFB, 0xEB), Color.FromRgb(0xFE, 0xF0, 0xBE));
                frame.BorderBrush = B(0xD9, 0xA4, 0x41, 0x40);
                frame.Padding = new Thickness(11, 9, 11, 6);
                var text = (item.Text ?? "").Trim().Replace("\r\n", "\n").Replace('\t', ' ');
                if (text.Length > 300) text = text[..300];
                return Note(text, string.Format(S.Chars, (item.Text ?? "").Length), B(0x44, 0x40, 0x3C), B(0xA8, 0x94, 0x6A),
                    wrap: true, mono: false, rich: item.IsRich, header: null);
            }
        }
    }

    static UIElement Note(string text, string footer, Brush fg, Brush dim, bool wrap, bool mono, bool rich, UIElement? header)
    {
        var dock = new DockPanel();
        if (header != null)
        {
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);
        }
        var foot = new DockPanel { Margin = new Thickness(0, 3, 0, 0) };
        if (rich)
        {
            var tag = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = B(0, 0, 0, 0x10),
                Padding = new Thickness(4, 0, 4, 0),
                Child = new TextBlock { Text = "Aa", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = dim },
            };
            DockPanel.SetDock(tag, Dock.Left);
            foot.Children.Add(tag);
        }
        foot.Children.Add(new TextBlock { Text = footer, FontSize = 10, Foreground = dim, HorizontalAlignment = HorizontalAlignment.Right });
        DockPanel.SetDock(foot, Dock.Bottom);
        dock.Children.Add(foot);
        var body = new TextBlock
        {
            Text = text,
            Foreground = fg,
            FontSize = mono ? 10.5 : 12,
            LineHeight = mono ? 14 : 16.5,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        if (mono) body.FontFamily = new FontFamily("Consolas");
        dock.Children.Add(body);
        return dock;
    }

    static (string domain, string rest) SplitLink(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return (url, "");
        if (u.Scheme == "mailto") return (url[7..], "");
        var host = u.Host.StartsWith("www.") ? u.Host[4..] : u.Host;
        var rest = Uri.UnescapeDataString(u.PathAndQuery + u.Fragment);
        return (host, rest == "/" ? "" : rest);
    }

    // Border rounds its background but not its content; clip the content to match.
    static void Round(FrameworkElement e, double r) =>
        e.SizeChanged += (_, _) => e.Clip = new RectangleGeometry(new Rect(e.RenderSize), r, r);

    static void Fade(UIElement e, double to, int ms) =>
        e.BeginAnimation(OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)));

    static SolidColorBrush B(byte r, byte g, byte b, byte a = 0xFF) => new(Color.FromArgb(a, r, g, b));
    static LinearGradientBrush Vertical(Color top, Color bottom) => new(top, bottom, 90);
    static LinearGradientBrush Horizontal(Color a, Color b, Color c) => new(new GradientStopCollection
    {
        new GradientStop(a, 0), new GradientStop(b, 0.55), new GradientStop(c, 1),
    }, 0);

    void OnDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (e.ClickCount == 2)
        {
            Cancel();
            actions.Open(Item);
            return;
        }
        pressed = true;
        downAt = e.GetPosition(null);
        frame.CaptureMouse();
        holdTimer.Start();
    }

    void OnMove(object sender, MouseEventArgs e)
    {
        if (!pressed) return;
        var d = e.GetPosition(null) - downAt;
        if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        Cancel();
        actions.DragOut(Item, this);
    }

    void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!pressed) return;
        e.Handled = true;
        Cancel();
        actions.Primary(Item, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), this);
    }

    void OnHold(object? sender, EventArgs e)
    {
        if (!pressed) { holdTimer.Stop(); return; }
        Cancel();
        actions.Edit(Item);
    }

    void Cancel()
    {
        pressed = false;
        holdTimer.Stop();
        if (frame.IsMouseCaptured) frame.ReleaseMouseCapture();
    }
}
