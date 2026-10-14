using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DUN;

// Edit a text clip before using it again.
sealed class TextEditWindow : Window
{
    public TextEditWindow(ClipItem item, ClipStore store, Actions actions)
    {
        Title = S.EditTitle;
        Width = 760;
        Height = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var box = new TextBox
        {
            Text = item.Text ?? "",
            AcceptsReturn = true,
            AcceptsTab = true,
            FontSize = 14,
            Padding = new Thickness(6),
            TextWrapping = item.Kind == ClipKind.Code ? TextWrapping.NoWrap : TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        if (item.Kind == ClipKind.Code) box.FontFamily = new FontFamily("Consolas");

        void SaveText() => store.UpdateText(item, box.Text);
        var save = Button(S.Save, () => { SaveText(); Close(); });
        var saveCopy = Button(S.SaveAndCopy, () => { SaveText(); actions.Copy(item, plain: false); Close(); });
        var cancel = Button(S.Cancel, Close);
        cancel.IsCancel = true;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(save);
        buttons.Children.Add(saveCopy);
        buttons.Children.Add(cancel);
        var dock = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        dock.Children.Add(buttons);
        dock.Children.Add(box);
        Content = dock;

        Loaded += (_, _) => { box.Focus(); box.CaretIndex = box.Text.Length; };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; SaveText(); Close(); }
        };
    }

    static Button Button(string text, Action click)
    {
        var b = new Button { Content = text, MinWidth = 88, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(8, 0, 0, 0) };
        b.Click += (_, _) => click();
        return b;
    }
}

// Shortcut, Win+V, click behaviour, history limits, ignored apps, startup.
sealed class SettingsWindow : Window
{
    static readonly int[] RetentionChoices = { 0, 1, 7, 30, 90 };

    public Settings Result { get; }
    public bool StartWithWindows { get; private set; }

    public SettingsWindow(Settings current, bool startWithWindows, Action suspendHotkey, Action resumeHotkey)
    {
        Result = current.Clone();
        Title = S.SettingsTitle;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var grid = new Grid { Margin = new Thickness(20, 16, 20, 16) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        void Row(string? label, UIElement control)
        {
            int row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (label != null)
            {
                var l = new TextBlock { Text = label, Margin = new Thickness(0, 7, 16, 7), VerticalAlignment = VerticalAlignment.Top };
                Grid.SetRow(l, row);
                grid.Children.Add(l);
            }
            if (control is FrameworkElement fe) fe.Margin = new Thickness(0, 6, 0, 6);
            Grid.SetRow(control, row);
            Grid.SetColumn(control, 1);
            grid.Children.Add(control);
        }

        var hotkey = new Button
        {
            Content = HotkeyText.Describe(Result.HotkeyModifiers, Result.HotkeyKey),
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 140,
            Padding = new Thickness(10, 3, 10, 3),
        };
        hotkey.Click += (_, _) =>
        {
            suspendHotkey(); // otherwise the current shortcut can't be typed into the recorder
            var dlg = new HotkeyDialog(Result.HotkeyModifiers, Result.HotkeyKey) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                Result.HotkeyModifiers = dlg.Modifiers;
                Result.HotkeyKey = dlg.Key;
                hotkey.Content = HotkeyText.Describe(dlg.Modifiers, dlg.Key);
            }
            resumeHotkey();
        };
        Row(S.SetShortcut, hotkey);
        var winV = new CheckBox { Content = S.ReplaceWinV, IsChecked = Result.ReplaceWinV };
        Row(null, winV);

        var copy = new RadioButton { Content = S.ClickCopy, IsChecked = !Result.PasteOnClick, Margin = new Thickness(0, 0, 0, 4) };
        var paste = new RadioButton { Content = S.ClickPaste, IsChecked = Result.PasteOnClick };
        Row(S.ClickAction, new StackPanel { Children = { copy, paste } });

        var edge = new CheckBox { Content = S.EdgeReveal, IsChecked = Result.RevealAtTopEdge };
        Row(S.Reveal, edge);

        var max = new TextBox { Text = Result.MaxItems.ToString(), Width = 70, VerticalContentAlignment = VerticalAlignment.Center };
        var maxRow = new StackPanel { Orientation = Orientation.Horizontal };
        maxRow.Children.Add(max);
        maxRow.Children.Add(new TextBlock { Text = S.ItemsSuffix, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        Row(S.MaxItems, maxRow);

        var retention = new ComboBox { Width = 120, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var d in RetentionChoices) retention.Items.Add(d == 0 ? S.Never : string.Format(S.Days, d));
        retention.SelectedIndex = Math.Max(0, Array.IndexOf(RetentionChoices, Result.RetentionDays));
        Row(S.Retention, retention);

        var excluded = new TextBox
        {
            Text = string.Join(Environment.NewLine, Result.ExcludedApps),
            AcceptsReturn = true,
            Height = 92,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var excludedPanel = new StackPanel();
        excludedPanel.Children.Add(excluded);
        excludedPanel.Children.Add(new TextBlock { Text = S.ExcludedNote, FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(0, 3, 0, 0) });
        Row(S.Excluded, excludedPanel);

        var startup = new CheckBox { Content = S.StartWithWindows, IsChecked = startWithWindows };
        Row(S.Startup, startup);

        var ok = new Button { Content = S.OK, MinWidth = 88, Padding = new Thickness(10, 4, 10, 4), IsDefault = true };
        var cancel = new Button { Content = S.Cancel, MinWidth = 88, Padding = new Thickness(10, 4, 10, 4), IsCancel = true, Margin = new Thickness(8, 0, 0, 0) };
        ok.Click += (_, _) =>
        {
            Result.ReplaceWinV = winV.IsChecked == true;
            Result.PasteOnClick = paste.IsChecked == true;
            Result.RevealAtTopEdge = edge.IsChecked == true;
            if (int.TryParse(max.Text.Trim(), out var n)) Result.MaxItems = Math.Clamp(n, 10, 5000);
            Result.RetentionDays = RetentionChoices[Math.Max(0, retention.SelectedIndex)];
            Result.ExcludedApps = excluded.Text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            StartWithWindows = startup.IsChecked == true;
            DialogResult = true;
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        Grid.SetColumnSpan(buttons, 2);
        Row(null, buttons);
        Grid.SetColumn(buttons, 0);

        Content = grid;
    }
}
