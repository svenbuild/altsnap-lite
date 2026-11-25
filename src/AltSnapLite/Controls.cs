using System.Drawing.Drawing2D;

namespace AltSnapLite;

// Selbst gezeichnete Bedienelemente, damit hell und dunkel gleich sauber aussehen.

internal sealed class Card : Panel
{
    private readonly Theme theme;
    internal Card(Theme theme)
    {
        this.theme = theme;
        BackColor = theme.Surface;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(theme.Window);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float radius = 8 * DeviceDpi / 96f;
        using var path = Theme.RoundedRectangle(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), radius);
        using var fill = new SolidBrush(theme.Surface);
        using var border = new Pen(theme.Border);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
    }
}

internal sealed class ToggleSwitch : CheckBox
{
    private readonly Theme theme;
    private bool hover;

    internal ToggleSwitch(Theme theme, string text)
    {
        this.theme = theme;
        Text = text;
        AccessibleName = text;
        AutoSize = false;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        float scale = DeviceDpi / 96f;
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? theme.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var row = new RectangleF(4 * scale, 2 * scale, Width - 8 * scale, Height - 4 * scale);
        if (hover)
        {
            using var path = Theme.RoundedRectangle(row, 6 * scale);
            using var brush = new SolidBrush(theme.Hover);
            g.FillPath(brush, path);
        }

        float trackWidth = 38 * scale, trackHeight = 20 * scale;
        var track = new RectangleF(Width - trackWidth - 16 * scale, (Height - trackHeight) / 2, trackWidth, trackHeight);
        var textBounds = new Rectangle((int)(16 * scale), 0, (int)(track.Left - 24 * scale), Height);
        TextRenderer.DrawText(g, Text, Font, textBounds, Enabled ? theme.Text : theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        using (var path = Theme.RoundedRectangle(track, trackHeight / 2))
        {
            if (Checked)
            {
                using var brush = new SolidBrush(theme.Accent);
                g.FillPath(brush, path);
            }
            else
            {
                using var pen = new Pen(theme.Muted, Math.Max(1, scale));
                g.DrawPath(pen, path);
            }
        }
        float knob = (Checked ? 14 : 12) * scale;
        float knobX = Checked ? track.Right - knob - 3 * scale : track.Left + 4 * scale;
        using (var brush = new SolidBrush(Checked ? theme.OnAccent : theme.Muted))
            g.FillEllipse(brush, knobX, track.Top + (trackHeight - knob) / 2, knob, knob);

        if (Focused && ShowFocusCues)
        {
            var focus = RectangleF.Inflate(track, 3 * scale, 3 * scale);
            using var path = Theme.RoundedRectangle(focus, focus.Height / 2);
            using var pen = new Pen(theme.Text, Math.Max(1, scale));
            g.DrawPath(pen, path);
        }
    }
}

internal sealed class Slider : Control
{
    private readonly Theme theme;
    private readonly int minimum, maximum, step;
    private int value;
    private bool dragging;
    internal event EventHandler? ValueChanged;

    internal Slider(Theme theme, string text, int minimum, int maximum, int step)
    {
        this.theme = theme;
        this.minimum = minimum;
        this.maximum = maximum;
        this.step = step;
        Text = text;
        AccessibleName = text;
        AccessibleRole = AccessibleRole.Slider;
        TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int Value
    {
        get => value;
        set
        {
            value = Math.Clamp(value, minimum, maximum);
            if (this.value == value) return;
            this.value = value;
            AccessibleDescription = value + " px";
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private float DpiScale => DeviceDpi / 96f;

    // Bildschirmleser können den Wert lesen und setzen.
    protected override AccessibleObject CreateAccessibilityInstance() => new SliderAccessibleObject(this);

    private sealed class SliderAccessibleObject(Slider owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.Slider;
        public override string? Value
        {
            get => owner.Value.ToString();
            set { if (int.TryParse(value, out int number)) owner.Value = number; }
        }
    }
    private RectangleF Track => new(Width - 200 * DpiScale, Height / 2f - 2 * DpiScale, 128 * DpiScale, 4 * DpiScale);

    protected override void OnPaint(PaintEventArgs e)
    {
        float scale = DpiScale;
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? theme.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        TextRenderer.DrawText(g, Text, Font, new Rectangle((int)(16 * scale), 0, (int)(Track.Left - 24 * scale), Height),
            theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        var track = Track;
        float ratio = (value - minimum) / (float)(maximum - minimum);
        float x = track.Left + track.Width * ratio;
        using (var path = Theme.RoundedRectangle(track, track.Height / 2))
        using (var brush = new SolidBrush(theme.Border))
            g.FillPath(brush, path);
        using (var path = Theme.RoundedRectangle(new RectangleF(track.Left, track.Top, x - track.Left, track.Height), track.Height / 2))
        using (var brush = new SolidBrush(theme.Accent))
            g.FillPath(brush, path);
        float knob = 16 * scale;
        using (var brush = new SolidBrush(theme.Surface))
        using (var pen = new Pen(theme.Border, Math.Max(1, scale)))
        {
            g.FillEllipse(brush, x - knob / 2, Height / 2f - knob / 2, knob, knob);
            g.DrawEllipse(pen, x - knob / 2, Height / 2f - knob / 2, knob, knob);
        }
        using (var brush = new SolidBrush(theme.Accent))
            g.FillEllipse(brush, x - 5 * scale, Height / 2f - 5 * scale, 10 * scale, 10 * scale);
        if (Focused && ShowFocusCues)
            using (var pen = new Pen(theme.Text, Math.Max(1, scale)))
                g.DrawEllipse(pen, x - knob / 2 - 3 * scale, Height / 2f - knob / 2 - 3 * scale, knob + 6 * scale, knob + 6 * scale);

        TextRenderer.DrawText(g, value + " px", Font,
            new Rectangle((int)(track.Right + 8 * scale), 0, (int)(Width - track.Right - 24 * scale), Height),
            theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix);
    }

    private void SetFromMouse(int x)
    {
        var track = Track;
        float ratio = Math.Clamp((x - track.Left) / track.Width, 0, 1);
        int raw = minimum + (int)Math.Round(ratio * (maximum - minimum) / step) * step;
        Value = raw;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        dragging = true;
        Capture = true;
        SetFromMouse(e.X);
    }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) SetFromMouse(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = false; Capture = false; }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Left or Keys.Down: Value -= step; break;
            case Keys.Right or Keys.Up: Value += step; break;
            case Keys.Home: Value = minimum; break;
            case Keys.End: Value = maximum; break;
        }
    }
}

// Eine Zeile "Aktion ... Tasten" in der Gestenliste.
internal sealed class GestureRow : Control
{
    private readonly Theme theme;
    private readonly string keys, action;

    internal GestureRow(Theme theme, string keys, string action)
    {
        this.theme = theme;
        this.keys = keys;
        this.action = action;
        AccessibleName = action + ": " + keys;
        AccessibleRole = AccessibleRole.StaticText;
        TabStop = false;
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        float scale = DeviceDpi / 96f;
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? theme.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var chipFont = new Font(Font.FontFamily, Font.Size - 0.75f, FontStyle.Bold);
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        // "Alt + Links ziehen" wird zu den Chips "Alt" und "Links ziehen", rechtsbündig.
        var parts = keys.Split(" + ");
        var sizes = parts.Select(part => TextRenderer.MeasureText(g, part, chipFont, Size.Empty, flags)).ToArray();
        var plus = TextRenderer.MeasureText(g, "+", Font, Size.Empty, flags);
        float pad = 8 * scale, gap = 6 * scale;
        float total = sizes.Sum(size => size.Width + 2 * pad) + (parts.Length - 1) * (plus.Width + 2 * gap);
        float x = Width - 16 * scale - total;

        TextRenderer.DrawText(g, action, Font, new Rectangle((int)(16 * scale), 0, (int)(x - 28 * scale), Height), theme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | flags);
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0)
            {
                TextRenderer.DrawText(g, "+", Font, new Point((int)(x + gap), (Height - plus.Height) / 2), theme.Muted, flags);
                x += plus.Width + 2 * gap;
            }
            var chip = new RectangleF(x, (Height - sizes[i].Height) / 2f - 4 * scale, sizes[i].Width + 2 * pad, sizes[i].Height + 8 * scale);
            using (var path = Theme.RoundedRectangle(chip, 5 * scale))
            using (var fill = new SolidBrush(theme.Chip))
            using (var border = new Pen(theme.Border, Math.Max(1, scale)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
            TextRenderer.DrawText(g, parts[i], chipFont, new Point((int)(chip.X + pad), (int)(chip.Y + 4 * scale)),
                i == 0 && parts.Length > 1 ? theme.Accent : theme.Text, flags);
            x = chip.Right;
        }
    }
}

// Tray-Menü im Stil von Windows 11.
internal sealed class MenuRenderer(Theme theme) : ToolStripRenderer
{
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(theme.Surface);

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if (Theme.IsWindows11) return; // Windows 11 zeichnet Rand und runde Ecken selbst.
        using var pen = new Pen(theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        float scale = e.ToolStrip?.DeviceDpi / 96f ?? 1;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new RectangleF(4 * scale, 1 * scale, e.Item.Width - 8 * scale, e.Item.Height - 2 * scale);
        using var path = Theme.RoundedRectangle(bounds, 4 * scale);
        using var brush = new SolidBrush(theme.Hover);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? theme.Text : theme.Muted;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        float scale = e.ToolStrip?.DeviceDpi / 96f ?? 1;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var box = e.ImageRectangle;
        float cx = box.Left + box.Width / 2f, cy = box.Top + box.Height / 2f;
        using var pen = new Pen(theme.Accent, 1.8f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(pen, [new PointF(cx - 5 * scale, cy), new PointF(cx - 1.5f * scale, cy + 3.5f * scale), new PointF(cx + 5 * scale, cy - 4 * scale)]);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        float scale = e.ToolStrip?.DeviceDpi / 96f ?? 1;
        int y = e.Item.Height / 2;
        using var pen = new Pen(theme.Border);
        e.Graphics.DrawLine(pen, 12 * scale, y, e.Item.Width - 12 * scale, y);
    }
}
