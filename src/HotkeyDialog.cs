using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DUN;

// Records a new shortcut: waits for a key pressed together with at least one modifier.
sealed class HotkeyDialog : Window
{
    readonly TextBlock display = new() { FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };

    public uint Modifiers { get; private set; }
    public uint Key { get; private set; }

    public HotkeyDialog(uint modifiers, uint key)
    {
        Title = S.ShortcutTitle;
        Width = 380;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        display.Text = HotkeyText.Describe(modifiers, key);

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = S.ShortcutPrompt, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(display);
        Content = panel;

        PreviewKeyDown += OnKey;
    }

    void OnKey(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var k = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        if (k == System.Windows.Input.Key.Escape) { DialogResult = false; return; }

        var mods = (uint)Keyboard.Modifiers;
        if (k is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl
            or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt
            or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift
            or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin)
        {
            display.Text = HotkeyText.Modifiers(mods) + "…";
            return;
        }
        if (mods == 0) return;

        Modifiers = mods;
        Key = (uint)KeyInterop.VirtualKeyFromKey(k);
        DialogResult = true;
    }
}
