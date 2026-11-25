using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace AltSnapLite;

internal sealed class WindowController : IDisposable
{
    private readonly Settings settings;
    private readonly Func<Native.Point, nint> findWindow;
    private readonly Native.HookProc callback, keyboardCallback;
    private readonly bool live;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    private readonly SnapPreview preview = new();
    private readonly TopmostFrames frames = new();
    private readonly bool elevated = Native.IsElevated((uint)Environment.ProcessId);
    private readonly Dictionary<nint, (uint Process, Rectangle Bounds)> restored = new();
    private nint hook, keyboardHook;
    // Nur für Nicht-Zusatztasten (z. B. CapsLock): Zeit des letzten Drückens und ob es geschluckt wurde.
    private readonly Dictionary<int, long> keyTimes = new();
    private readonly HashSet<int> swallowed = new(), recordHeld = new(), recorded = new(), heldBefore = new();
    private Action<int[]?>? recorder;
    private long recordStart;
    private SynchronizationContext? recordContext;
    private Gesture? gesture;
    private nint pendingTopmost;
    private bool middleConsumed, updatePosted;
    private (nint Window, long Time, Point Point, bool Right) lastClick;
    private readonly Action postedUpdate;

    private sealed class Gesture(nint window, bool resize, Point cursor)
    {
        internal nint Window = window;
        internal bool Resize = resize, Initialized, Released, Moved, DoubleClick;
        internal Point Origin = cursor, Cursor = cursor;
        internal Rectangle Bounds;
        internal int EdgeX, EdgeY;
        internal Size Minimum = new(160, 80), Maximum = new(int.MaxValue, int.MaxValue);
        internal SnapTarget Snap;
        internal Point? LastCursor;
        internal Rectangle? LastRequestedBounds;
        internal long RequestTime;
        internal Rectangle BeforeRequest;
        internal bool RestoreSize;
    }

    internal WindowController(Settings settings, bool installHook = true, Func<Native.Point, nint>? findWindow = null)
    {
        this.settings = settings;
        this.findWindow = findWindow ?? FindWindow;
        _ = preview.Handle;
        callback = OnMouse;
        keyboardCallback = OnKey;
        live = installHook;
        postedUpdate = () => { updatePosted = false; Update(); };
        if (installHook)
        {
            hook = Native.SetWindowsHookEx(Native.MouseHook, callback, Native.GetModuleHandle(null), 0);
            if (hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), Strings.HookFailed);
        }
        timer.Tick += (_, _) => Update();
        UpdateKeyboardHook();
    }

    // Der Tastatur-Hook läuft nur, wenn er gebraucht wird: beim Aufnehmen einer Taste oder für Tasten wie CapsLock.
    // Zusatztasten (Strg, Alt, Umschalt, Win) werden direkt abgefragt.
    internal void UpdateKeyboardHook()
    {
        bool needed = live && (recorder != null || settings.Hotkey.Any(key => !Hotkeys.IsModifier(key)));
        if (needed && keyboardHook == 0)
            keyboardHook = Native.SetWindowsHookEx(Native.KeyboardHook, keyboardCallback, Native.GetModuleHandle(null), 0);
        else if (!needed && keyboardHook != 0)
        {
            Native.UnhookWindowsHookEx(keyboardHook);
            keyboardHook = 0;
        }
        if (!needed) { keyTimes.Clear(); swallowed.Clear(); }
    }

    // Nächste gedrückte Taste oder Kombination aufnehmen; null bei Esc oder Abbruch.
    internal void Record(Action<int[]?> done, long now)
    {
        recorder = done;
        recordStart = now;
        recordContext = SynchronizationContext.Current;
        recordHeld.Clear();
        recorded.Clear();
        // Schon gedrückte Tasten (z. B. Enter, mit dem die Aufnahme gestartet wurde) gehören nicht dazu.
        heldBefore.Clear();
        for (int key = 0x08; key <= 0xFE; key++)
            if (live && (Native.GetAsyncKeyState(key) & 0x8000) != 0) heldBefore.Add(Hotkeys.Normalize(key));
        UpdateKeyboardHook();
        // Ohne Tastatur-Hook kommt nie eine Taste an: Aufnahme gleich wieder beenden.
        if (live && keyboardHook == 0) FinishRecording(null);
    }

    internal void CancelRecording() { if (recorder != null) FinishRecording(null); }

    // Das Ergebnis erst nach dem Hook verarbeiten: Speichern oder eine Meldung darf den Hook nicht aufhalten.
    private void FinishRecording(int[]? keys)
    {
        var done = recorder;
        recorder = null;
        UpdateKeyboardHook();
        if (done == null) return;
        if (recordContext != null) recordContext.Post(_ => done(keys), null);
        else done(keys);
    }

    private nint OnKey(int code, nint message, nint pointer)
    {
        if (code < 0) return Native.CallNextHookEx(keyboardHook, code, message, pointer);
        try
        {
            var data = Marshal.PtrToStructure<Native.KeyData>(pointer);
            if (HandleKey((int)message, (int)data.Key, (data.Flags & 0x10) != 0, Environment.TickCount64)) return 1;
        }
        catch (Exception) { }
        return Native.CallNextHookEx(keyboardHook, code, message, pointer);
    }

    // true = Tastenereignis schlucken.
    internal bool HandleKey(int message, int key, bool injected, long now)
    {
        if (injected) return false;
        key = Hotkeys.Normalize(key);
        bool down = message is 0x100 or 0x104;
        if (recorder != null)
        {
            // Sicherheitsnetz: eine Aufnahme, deren Loslassen verloren ging (z. B. Sperrbildschirm), endet nach 10 s.
            if (now - recordStart > 10_000) { FinishRecording(null); return false; }
            // Esc bricht immer ab.
            if (down && key == Hotkeys.Escape) { FinishRecording(null); return true; }
            // Tasten, die schon vor der Aufnahme gedrückt waren, normal durchlassen.
            if (heldBefore.Contains(key))
            {
                if (!down) heldBefore.Remove(key);
                return false;
            }
            // Beim Aufnehmen geht keine Taste an andere Programme (sonst öffnet z. B. Win das Startmenü).
            if (down)
            {
                recordHeld.Add(key);
                recorded.Add(key);
            }
            else if (!recordHeld.Remove(key)) return false;
            else if (recordHeld.Count == 0) FinishRecording(recorded.ToArray());
            return true;
        }
        if (Hotkeys.IsModifier(key) || !settings.Hotkey.Contains(key)) return false;
        if (down)
        {
            bool first = !keyTimes.ContainsKey(key);
            keyTimes[key] = now;
            if (swallowed.Contains(key)) return true;
            // Nur beim ersten Drücken entscheiden (nie mitten in einer Wiederholung), nicht im Pause-Modus,
            // und nur, wenn die übrigen Tasten der Kombination gedrückt sind.
            if (first && settings.Enabled && settings.Hotkey.Where(Hotkeys.IsModifier).All(IsPhysicallyDown))
            {
                swallowed.Add(key);
                return true;
            }
            return false;
        }
        keyTimes.Remove(key);
        return swallowed.Remove(key);
    }

    // Eine gehaltene Taste wiederholt sich laufend; ohne Wiederholung seit 1,5 s gilt sie als losgelassen.
    internal bool IsHotkeyHeld(long now) => settings.Hotkey.All(key => Hotkeys.IsModifier(key)
        ? IsPhysicallyDown(key)
        : keyTimes.TryGetValue(key, out long time) && now - time < 1500);

    private static bool IsPhysicallyDown(int key) => key == Hotkeys.Win
        ? ((Native.GetAsyncKeyState(0x5B) | Native.GetAsyncKeyState(0x5C)) & 0x8000) != 0
        : (Native.GetAsyncKeyState(key) & 0x8000) != 0;

    // Nach einem geschluckten Klick mit Alt oder Win: eine unbelegte Taste senden, damit beim Loslassen
    // weder die Menüleiste des Programms noch das Startmenü aufgeht.
    private void SuppressMenu()
    {
        if (!live || !settings.Hotkey.Any(key => key is Hotkeys.Alt or Hotkeys.Win)) return;
        Native.keybd_event(0xE8, 0, 0, 0);
        Native.keybd_event(0xE8, 0, 2, 0);
    }

    // Keine Geste aktiv: ein Update darf die App jetzt neu starten.
    internal bool IsIdle => gesture == null;

    private nint OnMouse(int code, nint message, nint pointer)
    {
        if (code < 0) return Native.CallNextHookEx(hook, code, message, pointer);
        try
        {
            var data = Marshal.PtrToStructure<Native.MouseData>(pointer);
            if (HandleMouse((int)message, data, IsHotkeyHeld(Environment.TickCount64))) return 1;
        }
        // Eine Ausnahme im Hook würde die ganze App beenden; das Ereignis dann lieber normal durchreichen.
        catch (Exception) { }
        return Native.CallNextHookEx(hook, code, message, pointer);
    }

    internal bool HandleMouse(int action, Native.MouseData data, bool hotkeyHeld)
    {
        // Zum geschluckten Mittelklick gehört auch das Loslassen, sonst sieht die App eine halbe Klickfolge.
        if (action == Native.MiddleUp && middleConsumed) { middleConsumed = false; return true; }
        if (action == Native.MiddleDown) middleConsumed = false; // verlorenes Loslassen nicht ewig mitschleppen
        // Always finish a consumed mouse-down, even if Alt or the setting is released first.
        if (gesture is { } current)
        {
            // A move arriving between mouse-up and the next timer tick belongs to no drag.
            if (current.Released) return false;
            if (action == Native.MouseMove)
            {
                current.Cursor = data.Point.Drawing;
                RequestUpdate();
                return false;
            }
            if (action == (current.Resize ? Native.RightUp : Native.LeftUp))
            {
                current.Cursor = data.Point.Drawing;
                current.Released = true;
                RequestUpdate();
                return true;
            }
            // Erneutes Drücken derselben Taste: das Loslassen ging verloren (z. B. Sperrbildschirm). Alte Geste verwerfen.
            if (action == (current.Resize ? Native.RightDown : Native.LeftDown)) { gesture = null; preview.Hide(); }
        }
        if (!settings.Enabled || gesture != null || !hotkeyHeld || recorder != null) return false;

        bool resize = action == Native.RightDown, pin = action == Native.MiddleDown;
        if (action != Native.LeftDown && !(resize && settings.Resize) && !(pin && settings.TopmostClick))
            return false;
        nint window = findWindow(data.Point);
        if (window == 0 || (resize && !CanResize(window)))
            return false;

        if (pin) { pendingTopmost = window; middleConsumed = true; RequestUpdate(); }
        else
        {
            gesture = new Gesture(window, resize, data.Point.Drawing) { DoubleClick = IsDoubleClick(window, data.Point.Drawing, resize) };
            timer.Start();
        }
        SuppressMenu();
        return true;
    }

    // Sofort nach dem Hook aktualisieren statt auf den nächsten Timer-Tick zu warten.
    // Viele Mausereignisse hintereinander ergeben trotzdem nur eine Aktualisierung.
    private void RequestUpdate()
    {
        if (updatePosted) return;
        updatePosted = true;
        preview.BeginInvoke(postedUpdate);
    }

    private bool IsDoubleClick(nint window, Point point, bool right)
    {
        var size = SystemInformation.DoubleClickSize;
        return window == lastClick.Window && right == lastClick.Right && Environment.TickCount64 - lastClick.Time <= SystemInformation.DoubleClickTime
            && Math.Abs(point.X - lastClick.Point.X) <= size.Width && Math.Abs(point.Y - lastClick.Point.Y) <= size.Height;
    }

    private nint FindWindow(Native.Point point)
    {
        nint window = Native.GetAncestor(Native.WindowFromPoint(point), 2);
        if (window == 0 || window == preview.Handle || !Native.IsWindowVisible(window)) return 0;
        var name = new StringBuilder(256);
        Native.GetClassName(window, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "DV2ControlHost") return 0;
        int style = Native.GetWindowLong(window, -16), extended = Native.GetWindowLong(window, -20);
        if ((style & 0x40000000) != 0 || (extended & 0x08000000) != 0) return 0;
        Native.GetWindowThreadProcessId(window, out uint process);
        if (!elevated && process != Environment.ProcessId && Native.IsElevated(process)) return 0;
        return window;
    }

    private static bool CanResize(nint window) => (Native.GetWindowLong(window, -16) & 0x40000) != 0;

    private void Initialize(Gesture drag)
    {
        drag.Initialized = true;
        Native.GetWindowRect(drag.Window, out var rect);
        drag.Bounds = rect.Drawing;
        bool maximized = Native.IsZoomed(drag.Window);
        Native.GetWindowThreadProcessId(drag.Window, out uint process);
        Rectangle original = drag.Bounds;
        bool wasSnapped = restored.TryGetValue(drag.Window, out var saved) && saved.Process == process;
        drag.RestoreSize = maximized || wasSnapped;
        if (maximized)
        {
            var placement = new Native.Placement { Length = (uint)Marshal.SizeOf<Native.Placement>() };
            if (Native.GetWindowPlacement(drag.Window, ref placement))
            {
                original = placement.Normal.Drawing;
                var screen = Screen.FromHandle(drag.Window);
                original.Offset(screen.WorkingArea.Left - screen.Bounds.Left, screen.WorkingArea.Top - screen.Bounds.Top);
            }
            Native.ShowWindowAsync(drag.Window, 9);
        }
        else if (wasSnapped) original = saved.Bounds;

        if ((maximized || wasSnapped) && !drag.Resize)
        {
            double ratio = Math.Clamp((drag.Origin.X - drag.Bounds.Left) / (double)drag.Bounds.Width, 0, 1);
            int offsetY = Math.Min(Math.Max(drag.Origin.Y - drag.Bounds.Top, 0), 32);
            drag.Bounds = new Rectangle(drag.Origin.X - (int)(original.Width * ratio), drag.Origin.Y - offsetY,
                original.Width, original.Height);
        }
        else if (maximized) drag.Bounds = original;
        restored.Remove(drag.Window);
        (drag.EdgeX, drag.EdgeY) = Geometry.ResizeEdges(drag.Origin, drag.Bounds);
        var limits = new Native.MinMaxInfo();
        if (drag.Resize && Native.SendMessageTimeout(drag.Window, 0x24, 0, ref limits, 2, 30, out _) != 0)
        {
            drag.Minimum = new Size(Math.Max(80, limits.MinTrack.X), Math.Max(40, limits.MinTrack.Y));
            drag.Maximum = new Size(limits.MaxTrack.X > 0 ? limits.MaxTrack.X : int.MaxValue,
                limits.MaxTrack.Y > 0 ? limits.MaxTrack.Y : int.MaxValue);
        }
        Native.SetForegroundWindow(drag.Window);
    }

    private void Update()
    {
        if (pendingTopmost != 0)
        {
            nint window = pendingTopmost;
            pendingTopmost = 0;
            bool pin = !Native.IsTopmost(window);
            if (Native.IsWindow(window) && Native.SetWindowPos(window, pin ? Native.Topmost : Native.NotTopmost, 0, 0, 0, 0, Native.NoSize | Native.NoMove | Native.NoActivate | 0x200)) // 0x200: Besitzer-Fenster nicht mitbewegen
                frames.Set(window, pin);
        }
        if (gesture is not { } drag) { timer.Stop(); return; }
        if (!Native.IsWindow(drag.Window)) { EndGesture(drag); return; }
        Point delta = new(drag.Cursor.X - drag.Origin.X, drag.Cursor.Y - drag.Origin.Y);
        if (!drag.Moved && (Math.Abs(delta.X) >= 3 || Math.Abs(delta.Y) >= 3)) drag.Moved = true;
        if (drag.Moved && (drag.LastCursor != drag.Cursor || drag.Released))
        {
            if (!drag.Initialized) Initialize(drag);
            Rectangle bounds;
            if (drag.Resize)
            {
                bounds = Geometry.Resize(drag.Bounds, delta, drag.EdgeX, drag.EdgeY, drag.Minimum);
                if (bounds.Width > drag.Maximum.Width)
                    bounds = new Rectangle(drag.EdgeX < 0 ? drag.Bounds.Right - drag.Maximum.Width : bounds.X, bounds.Y, drag.Maximum.Width, bounds.Height);
                if (bounds.Height > drag.Maximum.Height)
                    bounds = new Rectangle(bounds.X, drag.EdgeY < 0 ? drag.Bounds.Bottom - drag.Maximum.Height : bounds.Y, bounds.Width, drag.Maximum.Height);
            }
            else
            {
                bounds = drag.Bounds;
                bounds.Offset(delta);
                drag.Snap = settings.Snap && CanResize(drag.Window)
                    ? Geometry.Snap(drag.Cursor, Screen.FromPoint(drag.Cursor).WorkingArea, settings.SnapDistance) : default;
                if (settings.Preview && drag.Snap.Kind != SnapKind.None) preview.ShowBounds(drag.Snap.Bounds);
                else if (preview.Visible) preview.Hide();
            }
            // Never keep posting identical asynchronous window updates while the pointer rests.
            // Moving must not ask the target to resize/rebuild its rendering surfaces.
            if (drag.LastRequestedBounds != bounds)
            {
                // Erst weitermachen, wenn das Fenster die letzte Position übernommen hat. Sonst stauen sich bei
                // langsamen Apps alte Positionen in deren Warteschlange und das Fenster ruckelt hinterher.
                // Nach 100 ms trotzdem senden, falls die App die Grösse z. B. selbst begrenzt.
                long now = Environment.TickCount64;
                // "Übernommen" heisst: das Fenster hat sich seit dem Senden verändert (Apps passen Grösse teils selbst an, z. B. bei DPI-Wechsel)
                // oder steht bereits am Ziel. Das letzte Stück beim Loslassen wird nie zurückgehalten.
                if (!drag.Released && drag.LastRequestedBounds is { } last && now - drag.RequestTime < 100)
                {
                    var actual = Native.Bounds(drag.Window);
                    if (actual == drag.BeforeRequest && !Near(actual, last)) return;
                }
                drag.BeforeRequest = Native.Bounds(drag.Window);
                uint flags = Native.NoActivate | Native.NoZOrder | Native.AsyncWindowPos;
                if ((!drag.Resize && !drag.RestoreSize) || drag.LastRequestedBounds?.Size == bounds.Size)
                    flags |= Native.NoSize;
                if (drag.LastRequestedBounds?.Location == bounds.Location) flags |= Native.NoMove;
                if (Native.SetWindowPos(drag.Window, 0, bounds.X, bounds.Y, bounds.Width, bounds.Height, flags))
                {
                    drag.LastRequestedBounds = bounds;
                    drag.RequestTime = now;
                    drag.RestoreSize = false;
                }
            }
            drag.LastCursor = drag.Cursor;
        }
        if (drag.Released) EndGesture(drag);
    }

    // Kleine Rundungsabweichungen (z. B. DPI-Umrechnung) zählen als übernommen.
    private static bool Near(Rectangle a, Rectangle b) => Math.Abs(a.Left - b.Left) <= 2 && Math.Abs(a.Top - b.Top) <= 2
        && Math.Abs(a.Right - b.Right) <= 2 && Math.Abs(a.Bottom - b.Bottom) <= 2;

    private void EndGesture(Gesture drag)
    {
        preview.Hide();
        // Alt + Doppelklick links: maximieren bzw. wiederherstellen. Alt + Doppelklick rechts: minimieren.
        if (!drag.Moved && Native.IsWindow(drag.Window))
        {
            if (drag.DoubleClick && drag.Resize) { Native.ShowWindowAsync(drag.Window, 6); lastClick = default; }
            else if (drag.DoubleClick && CanResize(drag.Window))
            {
                restored.Remove(drag.Window);
                Native.ShowWindowAsync(drag.Window, Native.IsZoomed(drag.Window) ? 9 : 3);
                lastClick = default;
            }
            else lastClick = (drag.Window, Environment.TickCount64, drag.Origin, drag.Resize);
        }
        if (drag.Moved && !drag.Resize && drag.Snap.Kind != SnapKind.None && Native.IsWindow(drag.Window))
        {
            if (restored.Count > 256) restored.Clear();
            Native.GetWindowThreadProcessId(drag.Window, out uint process);
            restored[drag.Window] = (process, drag.Bounds);
            if (drag.Snap.Kind == SnapKind.Maximize) Native.ShowWindowAsync(drag.Window, 3);
            else
            {
                Native.GetWindowRect(drag.Window, out var outer);
                var frame = Native.VisibleBounds(drag.Window);
                var target = drag.Snap.Bounds;
                Native.SetWindowPos(drag.Window, 0,
                    target.Left - (frame.Left - outer.Left), target.Top - (frame.Top - outer.Top),
                    target.Width + outer.Drawing.Width - frame.Width, target.Height + outer.Drawing.Height - frame.Height,
                    Native.NoActivate | Native.NoZOrder | Native.AsyncWindowPos);
            }
        }
        if (gesture == drag) gesture = null;
        if (gesture == null) timer.Stop();
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Dispose();
        if (hook != 0) { Native.UnhookWindowsHookEx(hook); hook = 0; }
        if (keyboardHook != 0) { Native.UnhookWindowsHookEx(keyboardHook); keyboardHook = 0; }
        preview.Dispose();
        frames.Dispose();
    }
}
