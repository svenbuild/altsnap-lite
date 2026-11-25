namespace AltSnapLite;

internal sealed class SettingsForm : Form
{
    private readonly Theme theme;
    private readonly Settings settings;
    private readonly Action save;
    private readonly List<(ToggleSwitch Box, Func<bool> Read)> toggles = new();
    private readonly Slider distance;
    private readonly Bitmap logo;
    private readonly List<Font> fonts = new();
    private bool refreshing;

    internal SettingsForm(Settings settings, Action save, Theme? colors = null)
    {
        theme = colors ?? Theme.Current;
        this.settings = settings;
        this.save = save;
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "AltSnap Lite";
        Font = MakeFont(9.75f);
        ForeColor = theme.Text;
        BackColor = theme.Window;
        ClientSize = new Size(856, 494);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        logo = Assets.Logo(96);

        var header = new Panel { Location = new Point(0, 0), Size = new Size(856, 88), BackColor = theme.Window };
        header.Paint += (_, e) =>
        {
            float scale = header.DeviceDpi / 96f;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(logo, 24 * scale, 22 * scale, 46 * scale, 46 * scale);
        };
        header.Controls.Add(Label("AltSnap Lite", 84, 20, 500, 32, 16, theme.Text, FontStyle.Bold));
        header.Controls.Add(Label($"{Strings.Version} {Updater.CurrentText}  ·  {Strings.Tagline}", 85, 52, 600, 22, 9.75f, theme.Muted));
        Controls.Add(header);

        Controls.Add(Label(Strings.Gestures.ToUpperInvariant(), 24, 96, 360, 20, 8.25f, theme.Muted, FontStyle.Bold));
        Controls.Add(Label(Strings.Settings.ToUpperInvariant(), 440, 96, 360, 20, 8.25f, theme.Muted, FontStyle.Bold));

        var gestures = new Card(theme) { Location = new Point(20, 120), Size = new Size(400, 322) };
        int y = 8;
        foreach (var (keys, action) in Strings.GestureList)
        {
            gestures.Controls.Add(new GestureRow(theme, keys, action) { Location = new Point(1, y), Size = new Size(398, 51) });
            y += 51;
        }
        Controls.Add(gestures);

        var options = new Card(theme) { Location = new Point(436, 120), Size = new Size(400, 322) };
        y = 6;
        AddToggle(options, Strings.Enabled, ref y, () => settings.Enabled, value => settings.Enabled = value);
        AddToggle(options, Strings.Snap, ref y, () => settings.Snap, value => settings.Snap = value);
        AddToggle(options, Strings.Preview, ref y, () => settings.Preview, value => settings.Preview = value);
        AddToggle(options, Strings.Resize, ref y, () => settings.Resize, value => settings.Resize = value);
        AddToggle(options, Strings.Topmost, ref y, () => settings.TopmostClick, value => settings.TopmostClick = value);
        AddToggle(options, Strings.StartWithWindows, ref y, () => Autostart.Enabled, value => Autostart.Enabled = value);
        AddToggle(options, Strings.AutoUpdate, ref y, () => settings.AutoUpdate, value => settings.AutoUpdate = value);
        distance = new Slider(theme, Strings.SnapDistance, 8, 64, 4) { Location = new Point(1, y), Size = new Size(398, 44), Font = Font };
        distance.ValueChanged += (_, _) => { if (!refreshing) { settings.SnapDistance = distance.Value; save(); } };
        options.Controls.Add(distance);
        Controls.Add(options);

        Controls.Add(Label(Strings.Footer, 24, 458, 600, 22, 9, theme.Muted));
        var link = new LinkLabel
        {
            Text = Strings.Website, Location = new Point(736, 458), Size = new Size(96, 22), TextAlign = ContentAlignment.TopRight,
            LinkColor = theme.Accent, ActiveLinkColor = theme.Accent, VisitedLinkColor = theme.Accent,
            LinkBehavior = LinkBehavior.HoverUnderline, Font = MakeFont(9), BackColor = theme.Window
        };
        link.LinkClicked += (_, _) => OpenWebsite();
        Controls.Add(link);
        ResumeLayout(false);
        RefreshValues();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyWindowStyle(Handle, theme.Dark);
    }

    private static void OpenWebsite()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"https://github.com/{Updater.Repository}") { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception) { }
    }

    // Schriften gehören dem Fenster und werden mit ihm freigegeben.
    private Font MakeFont(float size, FontStyle style = FontStyle.Regular)
    {
        var font = new Font("Segoe UI", size, style);
        fonts.Add(font);
        return font;
    }

    private Label Label(string text, int x, int y, int width, int height, float size, Color color, FontStyle style = FontStyle.Regular) => new()
    {
        Text = text, Location = new Point(x, y), Size = new Size(width, height), BackColor = theme.Window,
        Font = MakeFont(size, style), ForeColor = color, UseMnemonic = false, AutoEllipsis = true
    };

    private void AddToggle(Control parent, string text, ref int y, Func<bool> read, Action<bool> write)
    {
        var box = new ToggleSwitch(theme, text) { Location = new Point(1, y), Size = new Size(398, 38), Font = Font };
        y += 38;
        box.CheckedChanged += (_, _) =>
        {
            if (refreshing) return;
            try { write(box.Checked); save(); }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                MessageBox.Show(this, Strings.ChangeFailed + "\n\n" + e.Message, "AltSnap Lite");
                RefreshValues();
            }
        };
        toggles.Add((box, read));
        parent.Controls.Add(box);
    }

    internal void RefreshValues()
    {
        refreshing = true;
        try
        {
            foreach (var (box, read) in toggles) box.Checked = read();
            distance.Value = settings.SnapDistance;
        }
        finally { refreshing = false; }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        logo.Dispose();
        foreach (var font in fonts) font.Dispose();
    }
}
