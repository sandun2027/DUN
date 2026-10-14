using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DUN;

// Decides when the line comes out. Resting the pointer on the top edge shows it on that monitor
// (mouse mode); moving away or clicking elsewhere puts it back. The shortcut opens keyboard mode,
// which stays until Esc, a pick, or focus moves elsewhere.
sealed class RevealController
{
    static readonly TimeSpan Dwell = TimeSpan.FromMilliseconds(320), Linger = TimeSpan.FromMilliseconds(400);
    const double CloseButtonCorner = 140; // DIPs at the top-right left alone for maximized windows' ✕

    readonly Func<IReadOnlyList<LineWindow>> windows;
    readonly Func<bool> edgeEnabled;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    bool armed = true, lastButton;
    DateTime? edgeSince, outsideSince;
    LineWindow? edgeWindow;

    // While > 0 (dragging, context menu open) the line stays out no matter what.
    public int HoldOpen { get; set; }

    // The window that had focus when keyboard mode opened; pasting goes back there.
    public IntPtr PasteTarget { get; private set; }

    public RevealController(Func<IReadOnlyList<LineWindow>> windows, Func<bool> edgeEnabled)
    {
        this.windows = windows;
        this.edgeEnabled = edgeEnabled;
        timer.Tick += (_, _) => Tick();
    }

    public void Start() => timer.Start();
    public void Stop() => timer.Stop();

    public void Toggle()
    {
        var open = windows().FirstOrDefault(w => w.IsOut);
        if (open is { KeyboardMode: true }) { RetractAll(); return; }
        var fg = Native.GetForegroundWindow();
        PasteTarget = Native.IsOwn(fg) ? IntPtr.Zero : fg;
        if (open == null)
        {
            Native.GetCursorPos(out var p);
            open = windows().FirstOrDefault(w => w.Screen.Bounds.Contains(p.X, p.Y)) ?? windows().FirstOrDefault();
        }
        open?.Reveal(keyboard: true);
    }

    public void RetractAll()
    {
        armed = false; // don't pop straight back out if the pointer is still on the edge
        outsideSince = null;
        // Leaving keyboard mode: hand focus back to where it came from.
        bool restore = windows().Any(w => w.IsOut && w.KeyboardMode) && Native.IsOwn(Native.GetForegroundWindow());
        if (restore && PasteTarget != IntPtr.Zero && Native.IsWindow(PasteTarget)) Native.ForceForeground(PasteTarget);
        foreach (var w in windows()) w.Retract();
    }

    void Tick()
    {
        if (!Native.GetCursorPos(out var p)) return;
        bool button = Native.KeyDown(0x01) || Native.KeyDown(0x02) || Native.KeyDown(0x04);
        bool click = button && !lastButton;
        lastButton = button;
        var now = DateTime.UtcNow;

        var open = windows().FirstOrDefault(w => w.IsOut);
        if (open != null)
        {
            if (HoldOpen > 0) { outsideSince = null; return; }
            if (open.KeyboardMode) return; // closes on Esc, a pick, or losing focus
            if (FullScreen.Covers(open.Screen)) { RetractAll(); return; }
            bool inside = open.HoverZoneContains(p);
            if (click && !inside) { RetractAll(); return; }
            if (inside) { outsideSince = null; return; }
            outsideSince ??= now;
            if (now - outsideSince >= Linger) RetractAll();
            return;
        }

        var here = windows().FirstOrDefault(w => w.Screen.Bounds.Contains(p.X, p.Y));
        bool atEdge = here != null
            && p.Y <= here.Screen.Bounds.Top + 1
            && p.X < here.Screen.Bounds.Right - CloseButtonCorner * here.Scale;
        if (!atEdge) { armed = true; edgeSince = null; return; }
        if (!edgeEnabled() || !armed || button || FullScreen.Covers(here!.Screen)) { edgeSince = null; return; }
        if (edgeWindow != here || edgeSince == null) { edgeWindow = here; edgeSince = now; return; }
        if (now - edgeSince >= Dwell)
        {
            edgeSince = null;
            PasteTarget = IntPtr.Zero;
            here.Reveal(keyboard: false);
        }
    }
}

// The line stays away while something is full screen (games, videos, presentations).
static class FullScreen
{
    public static bool Covers(Forms.Screen screen)
    {
        // QUNS_RUNNING_D3D_FULL_SCREEN = 3, QUNS_PRESENTATION_MODE = 5
        if (Native.SHQueryUserNotificationState(out var state) == 0 && state is 3 or 5) return true;

        var fg = Native.GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == Native.GetShellWindow() || fg == Native.GetDesktopWindow()) return false;
        if (Native.IsOwn(fg)) return false;
        var cls = new StringBuilder(64);
        Native.GetClassName(fg, cls, cls.Capacity);
        if (cls.ToString() is "WorkerW" or "Progman" or "Shell_TrayWnd") return false;
        if (Native.IsZoomed(fg)) return false; // maximized isn't full screen
        if (!Native.GetWindowRect(fg, out var r)) return false;
        var b = screen.Bounds;
        return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
    }
}
