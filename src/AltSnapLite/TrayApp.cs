namespace AltSnapLite;

internal sealed class TrayApp : ApplicationContext
{
    private readonly Settings settings = Settings.Load();
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu;
    private readonly ToolStripMenuItem enabled;
    private readonly WindowController controller;
    private readonly Updater updater;
    private readonly Icon icon, pausedIcon;
    private readonly SynchronizationContext context;
    private SettingsForm? form;

    internal TrayApp(bool updated)
    {
        int size = SystemInformation.SmallIconSize.Width;
        icon = Assets.Icon(size);
        pausedIcon = CreatePausedIcon(icon);
        menu = new ContextMenuStrip { Font = new Font("Segoe UI", 9.75f), ShowImageMargin = false, ShowCheckMargin = true, Padding = new Padding(0, 4, 0, 4) };
        context = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        var title = new ToolStripMenuItem("AltSnap Lite " + Updater.CurrentText) { Enabled = false };
        menu.Items.Add(title);
        menu.Items.Add(new ToolStripSeparator());
        enabled = AddToggle(Strings.Enabled, () => settings.Enabled, value => settings.Enabled = value);
        menu.Items.Add(new ToolStripSeparator());
        AddToggle(Strings.Snap, () => settings.Snap, value => settings.Snap = value);
        AddToggle(Strings.Resize, () => settings.Resize, value => settings.Resize = value);
        AddToggle(Strings.Topmost, () => settings.TopmostClick, value => settings.TopmostClick = value);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.SettingsMenu, null, (_, _) => ShowSettings());
        menu.Items.Add(Strings.Exit, null, (_, _) => ExitThread());
        foreach (ToolStripItem item in menu.Items) item.Padding = new Padding(4, 5, 12, 5);
        menu.Opening += (_, _) => RefreshMenu();
        menu.HandleCreated += (_, _) => Theme.ApplyWindowStyle(menu.Handle, Theme.Current.Dark, roundSmall: true);

        tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowSettings(); };
        tray.BalloonTipClicked += (_, _) => ShowSettings();
        controller = new WindowController(settings);
        updater = new Updater(settings, () => controller.IsIdle && form is not { Visible: true, ContainsFocus: true }, ExitForUpdate, context);
        RefreshMenu();

        if (updated) tray.ShowBalloonTip(5000, Strings.UpdatedTitle, Strings.UpdatedText(Updater.CurrentText), ToolTipIcon.None);
        else if (settings.IsNew)
        {
            tray.ShowBalloonTip(8000, Strings.WelcomeTitle, Strings.WelcomeText(Hotkeys.Name(settings.Hotkey)), ToolTipIcon.None);
            SaveSettings();
        }
    }

    // Für das Update beenden. Falls das Beenden hängt, nach 10 Sekunden hart beenden,
    // damit die neue Version den Mutex bekommt.
    private void ExitForUpdate()
    {
        new Thread(() => { Thread.Sleep(10_000); Environment.Exit(0); }) { IsBackground = true }.Start();
        ExitThread();
    }

    // Zweiter Start der EXE: Einstellungen der laufenden Instanz zeigen.
    internal void ShowSettingsFromAnyThread()
    {
        try { context.Post(_ => ShowSettings(), null); }
        catch (InvalidOperationException) { } // App beendet sich gerade.
    }

    private ToolStripMenuItem AddToggle(string text, Func<bool> read, Action<bool> write)
    {
        var item = new ToolStripMenuItem(text) { Checked = read(), CheckOnClick = true, Tag = read };
        item.Click += (_, _) =>
        {
            write(item.Checked);
            SaveSettings();
            if (form is { IsDisposed: false }) form.RefreshValues();
        };
        menu.Items.Add(item);
        return item;
    }

    private void RefreshMenu()
    {
        menu.Renderer = new MenuRenderer(Theme.Current);
        foreach (ToolStripItem item in menu.Items)
            if (item is ToolStripMenuItem toggle && toggle.Tag is Func<bool> read) toggle.Checked = read();
        tray.Text = settings.Enabled ? Strings.TrayActive(Hotkeys.Name(settings.Hotkey)) : Strings.TrayPaused;
        tray.Icon = settings.Enabled ? icon : pausedIcon;
        enabled.Text = settings.Enabled ? Strings.Enabled : Strings.Paused;
    }

    private void ShowSettings()
    {
        if (form == null || form.IsDisposed) form = new SettingsForm(settings, SaveSettings, controller: controller) { Icon = icon };
        form.RefreshValues();
        form.Show();
        if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
        form.Activate();
    }

    private void SaveSettings()
    {
        try { settings.Save(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(Strings.SaveFailed + "\n\n" + e.Message, "AltSnap Lite");
        }
        controller.UpdateKeyboardHook();
        RefreshMenu();
    }

    // Graues Symbol, solange die App pausiert ist.
    private static Icon CreatePausedIcon(Icon source)
    {
        using var bitmap = source.ToBitmap();
        using var gray = new Bitmap(bitmap.Width, bitmap.Height);
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
            {
                var c = bitmap.GetPixel(x, y);
                int l = (int)(c.R * 0.3 + c.G * 0.59 + c.B * 0.11);
                gray.SetPixel(x, y, Color.FromArgb(c.A * 2 / 5, l, l, l));
            }
        nint handle = gray.GetHicon();
        using var temporary = System.Drawing.Icon.FromHandle(handle);
        var copy = (Icon)temporary.Clone();
        Native.DestroyIcon(handle);
        return copy;
    }

    protected override void ExitThreadCore()
    {
        updater.Dispose();
        controller.Dispose();
        form?.Dispose();
        tray.Visible = false;
        tray.Dispose();
        menu.Dispose();
        icon.Dispose();
        pausedIcon.Dispose();
        base.ExitThreadCore();
    }
}
