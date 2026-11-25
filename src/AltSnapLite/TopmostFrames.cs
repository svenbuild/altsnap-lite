namespace AltSnapLite;

// Zeichnet einen farbigen Rahmen um jedes Fenster, das mit Alt + Mittelklick in den Vordergrund geheftet wurde.
internal sealed class TopmostFrames : IDisposable
{
    private readonly Dictionary<nint, Frame> frames = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };

    internal TopmostFrames() => timer.Tick += (_, _) => Track();

    internal void Set(nint window, bool pinned)
    {
        if (pinned && !frames.ContainsKey(window)) frames[window] = new Frame();
        else if (!pinned) Remove(window);
        Track();
    }

    private void Remove(nint window)
    {
        if (frames.Remove(window, out var frame)) frame.Dispose();
    }

    private void Track()
    {
        foreach (var (window, frame) in frames.ToArray())
        {
            // Fenster geschlossen oder von aussen wieder normal gemacht: Rahmen entfernen.
            if (!Native.IsWindow(window) || !Native.IsTopmost(window)) { Remove(window); continue; }
            if (!Native.IsWindowVisible(window) || Native.IsIconic(window) || Native.IsCloaked(window)) frame.Hide();
            else frame.Place(window);
        }
        if (frames.Count > 0) timer.Start(); else timer.Stop();
    }

    public void Dispose()
    {
        timer.Dispose();
        foreach (var frame in frames.Values) frame.Dispose();
        frames.Clear();
    }

    private sealed class Frame : Form
    {
        internal Frame()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(82, 105, 236);
            Opacity = 0.9;
            StartPosition = FormStartPosition.Manual;
        }

        protected override bool ShowWithoutActivation => true;
        // Kein Fokus, klickdurchlässig, nicht in Alt+Tab.
        protected override CreateParams CreateParams
        {
            get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x20 | 0x80; return p; }
        }

        internal void Place(nint target)
        {
            var bounds = Native.VisibleBounds(target);
            if (Bounds != bounds)
            {
                Bounds = bounds;
                // Nur ein schmaler Rand innerhalb der Fensterkante ist sichtbar; so bleibt er auch bei maximierten Fenstern sichtbar.
                var region = new Region(new Rectangle(Point.Empty, bounds.Size));
                int thickness = Math.Max(3, 3 * DeviceDpi / 96);
                region.Exclude(Rectangle.Inflate(new Rectangle(Point.Empty, bounds.Size), -thickness, -thickness));
                Region = region;
            }
            if (!Visible) Show();
            // Direkt über das Zielfenster legen, nicht über andere Fenster, die davor liegen.
            nint above = Native.GetWindow(target, 3);
            if (above != Handle)
                Native.SetWindowPos(Handle, above == 0 ? Native.Topmost : above, 0, 0, 0, 0, Native.NoMove | Native.NoSize | Native.NoActivate);
        }
    }
}
