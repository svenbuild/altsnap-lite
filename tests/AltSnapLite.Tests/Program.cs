using System.Diagnostics;
using System.Runtime.InteropServices;
using AltSnapLite;

internal static class Program
{
    private static int assertions;
    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        int shot = Array.IndexOf(args, "--screenshot");
        if (shot >= 0) { Screenshots(args.Length > shot + 1 ? args[shot + 1] : "."); return 0; }
        try
        {
            GeometryChecks();
            SettingsChecks();
            UpdaterChecks();
            if (args.Contains("--integration")) IntegrationChecks();
            Console.WriteLine($"PASS: {assertions} Prüfungen" );
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    // Rendert die Oberfläche unsichtbar in Bilder (kein sichtbares Fenster, kein Maus-Hook).
    private static void Screenshots(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (bool german in new[] { true, false })
            foreach (var theme in new[] { Theme.Light, Theme.DarkTheme })
            {
                Strings.German = german;
                using var form = new SettingsForm(new Settings(), () => { }, theme);
                Render(form, Path.Combine(directory, $"settings-{(german ? "de" : "en")}-{(theme.Dark ? "dark" : "light")}.png"));
            }
    }

    private static void Render(Control control, string path)
    {
        _ = control.Handle;
        CreateHandles(control);
        control.PerformLayout();
        using var image = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(image, new Rectangle(Point.Empty, control.Size));
        // Nur den Fensterinhalt speichern; der Rahmen sieht bei DrawToBitmap nicht wie unter Windows 11 aus.
        var client = control.RectangleToScreen(control.ClientRectangle);
        var origin = new Point(client.X - control.Left, client.Y - control.Top);
        using var content = image.Clone(new Rectangle(origin, control.ClientSize), image.PixelFormat);
        content.Save(path);
    }

    private static void CreateHandles(Control control)
    {
        foreach (Control child in control.Controls) { _ = child.Handle; CreateHandles(child); }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        assertions++;
        Console.WriteLine("PASS: " + name);
    }

    private static void GeometryChecks()
    {
        var area = new Rectangle(-1920, -400, 1921, 1081);
        Check(Geometry.Snap(new(-1920, 0), area, 24).Kind == SnapKind.Left, "Linker Rand auf negativem Monitor");
        var left = Geometry.Snap(new(-1920, 0), area, 24).Bounds;
        var right = Geometry.Snap(new(0, 0), area, 24).Bounds;
        Check(left.Width + right.Width == area.Width && left.Right == right.Left, "Ungerade Monitorbreite lückenlos geteilt");
        Check(Geometry.Snap(new(-1000, -400), area, 24).Kind == SnapKind.Maximize, "Oberer Rand maximiert");
        Check(Geometry.Snap(new(-1920, -400), area, 24).Kind == SnapKind.TopLeft, "Obere linke Ecke");
        Check(Geometry.Snap(new(0, -400), area, 24).Kind == SnapKind.TopRight, "Obere rechte Ecke");
        Check(Geometry.Snap(new(-1920, 680), area, 24).Kind == SnapKind.BottomLeft, "Untere linke Ecke");
        Check(Geometry.Snap(new(0, 680), area, 24).Kind == SnapKind.BottomRight, "Untere rechte Ecke");
        Check(Geometry.Snap(new(-900, 0), area, 24).Kind == SnapKind.None, "Kein Einrasten in der Mitte");
        var bounds = new Rectangle(100, 100, 600, 400);
        Check(Geometry.ResizeEdges(new(110, 110), bounds) == (-1, -1), "Resize oben links");
        Check(Geometry.ResizeEdges(new(400, 110), bounds) == (0, -1), "Resize obere Seite");
        Check(Geometry.ResizeEdges(new(400, 300), bounds) == (1, 1), "Resize Mitte nach rechts unten");
        var resized = Geometry.Resize(bounds, new(900, 900), -1, -1, new(160, 80));
        Check(resized.Size == new Size(160, 80) && resized.Right == bounds.Right && resized.Bottom == bounds.Bottom, "Mindestgrösse mit fester gegenüberliegender Ecke");
    }

    private static void SettingsChecks()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AltSnapLite-tests-" + Guid.NewGuid());
        var path = Path.Combine(directory, "settings.json");
        try
        {
            Check(Settings.Load(path) is { Enabled: true, AutoUpdate: true, IsNew: true }, "Fehlende Einstellungen verwenden Standards");
            new Settings { Enabled = false, Resize = false, SnapDistance = 40 }.Save(path);
            var loaded = Settings.Load(path);
            Check(!loaded.Enabled && !loaded.Resize && loaded.SnapDistance == 40 && !loaded.IsNew, "Einstellungen speichern und lesen");
            File.WriteAllText(path, "{broken");
            Check(Settings.Load(path).Enabled, "Beschädigte Datei startet mit Standards");
            File.WriteAllText(path, "{\"SnapDistance\":999}");
            Check(Settings.Load(path).SnapDistance == 64, "Ungültigen Abstand begrenzen");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    private static void UpdaterChecks()
    {
        static string Release(string tag, string name = "AltSnapLite.exe", string digest = "\"digest\":\"sha256:ABC\",", string extra = "") =>
            $$"""{"tag_name":"{{tag}}",{{extra}}"assets":[{"name":"{{name}}",{{digest}}"browser_download_url":"https://github.com/svenbuild/altsnap-lite/releases/download/{{tag}}/{{name}}"}]}""";
        var release = Updater.ParseRelease(Release("v2.3.4"));
        Check(release is { } r && r.Version == new Version(2, 3, 4) && r.Sha256 == "abc", "Release mit Version und Prüfsumme lesen");
        Check(Updater.ParseRelease(Release("v9.0.0", digest: "")) == null, "Release ohne Prüfsumme ignorieren");
        Check(Updater.ParseRelease(Release("v9.0.0", name: "Other.exe")) == null, "Release ohne passende EXE ignorieren");
        Check(Updater.ParseRelease(Release("v9.0.0", extra: "\"prerelease\":true,")) == null, "Vorabversion ignorieren");
        Check(Updater.ParseRelease(Release("nightly")) == null, "Ungültige Versionsnummer ignorieren");
        var assembly = typeof(Updater).Assembly.GetName().Version!;
        Check(Updater.Current == new Version(assembly.Major, assembly.Minor, assembly.Build), "Aktuelle Version aus der Assembly");
        Check(Updater.FileVersion(typeof(Updater).Assembly.Location) == Updater.Current, "Dateiversion der EXE lesen");

        var directory = Path.Combine(Path.GetTempPath(), "AltSnapLite-update-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            string executable = Path.Combine(directory, "AltSnapLite.exe"), download = executable + ".download";
            File.WriteAllText(executable, "old");
            File.WriteAllText(download, "new");
            Updater.Replace(executable, download);
            Check(File.ReadAllText(executable) == "new" && File.ReadAllText(executable + ".old") == "old" && !File.Exists(download), "Neue EXE ersetzt die alte");
            Updater.Restore(executable);
            Check(File.ReadAllText(executable) == "old" && !File.Exists(executable + ".old"), "Fehlgeschlagener Neustart stellt die alte EXE wieder her");
            Updater.Restore(executable);
            Check(File.ReadAllText(executable) == "old", "Wiederherstellen ohne Altdatei lässt die EXE unverändert");
            File.WriteAllText(download, "new");
            using (File.Open(Path.Combine(directory, "AltSnapLite.exe.old"), FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                bool failed = false;
                try { Updater.Replace(executable, download); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { failed = true; }
                Check(failed && File.ReadAllText(executable) == "old", "Gesperrte Altdatei lässt die EXE unverändert");
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void Pump(int milliseconds = 100)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(3); }
    }

    private static void IntegrationChecks()
    {

        using var form = new WindowTrafficForm { Text = "AltSnap Lite · Funktionstest", TopMost = true, StartPosition = FormStartPosition.Manual,
            Bounds = new Rectangle(Screen.PrimaryScreen!.WorkingArea.Left + 180, Screen.PrimaryScreen.WorkingArea.Top + 160, 640, 420), MinimumSize = new Size(220, 140) };
        form.Show();
        form.Activate();
        Pump(200);
        nint targetWindow = form.Handle;
        using var controller = new WindowController(new Settings(), installHook: false, findWindow: _ => targetWindow);
        activeController = controller;
        try
        {
            var start = form.Bounds;
            var grab = new Point(start.Left + 200, start.Top + 180);
            Drag(grab, new(grab.X + 110, grab.Y + 70), false);


            Check(Math.Abs(form.Left - start.Left - 110) < 3 && Math.Abs(form.Top - start.Top - 70) < 3, "Alt + Links verschiebt echtes Fenster");
            Check(!Event(Native.MouseMove, grab), "Mausbewegung wird an Windows weitergereicht");
            WindowTrafficChecks(form);

            start = form.Bounds;
            grab = new(start.Right - 35, start.Bottom - 35);
            Drag(grab, new(grab.X + 90, grab.Y + 60), true);
            Check(form.Width == start.Width + 90 && form.Height == start.Height + 60, "Alt + Rechts skaliert nach rechts unten");
            Check(form.Location == start.Location, "Gegenüberliegende Ecke bleibt beim Resize fest");

            start = form.Bounds;
            grab = new(start.Left + 30, start.Top + 65);
            Drag(grab, new(grab.X - 50, grab.Y - 30), true);
            Check(form.Width == start.Width + 50 && form.Height == start.Height + 30 && form.Right == start.Right && form.Bottom == start.Bottom, "Alt + Rechts skaliert von oben links");

            grab = new(form.Left + 200, form.Top + 140);
            using var wheelWindow = new NativeWindowFixture();
            targetWindow = wheelWindow.Handle;
            Check(Event(Native.MiddleDown, grab) && Event(Native.MiddleUp, grab, false), "Alt + Mittelklick wird komplett geschluckt"); Pump(100);
            Check(Native.IsTopmost(targetWindow), "Alt + Mittelklick aktiviert Topmost");
            Check(FrameVisible(), "Rahmen um Topmost-Fenster sichtbar");
            Check(!Event(Native.MiddleUp, grab), "Normales Mittel-Loslassen wird durchgereicht");
            Event(Native.MiddleDown, grab); Event(Native.MiddleUp, grab); Pump(100);
            Check(!Native.IsTopmost(targetWindow), "Alt + Mittelklick deaktiviert Topmost");
            Check(!FrameVisible(), "Rahmen verschwindet wieder");
            targetWindow = form.Handle;

            int clicks = 0; form.MouseClick += (_, _) => clicks++;
            grab = new(form.Left + 200, form.Top + 150);
            Event(Native.LeftDown, grab); Pump();
            Event(Native.MouseMove, new(grab.X + 30, grab.Y + 20), false); Pump();
            Check(Event(Native.LeftUp, new(grab.X + 30, grab.Y + 20), false), "Mouse-up wird auch nach Alt-Release konsumiert" ); Pump();
            Check(clicks == 0, "Alt vor Maustaste loslassen reicht keinen Klick durch");

            var work = Screen.FromHandle(form.Handle).WorkingArea;
            grab = new(form.Left + 100, form.Top + 120);
            Drag(grab, new(work.Left + 2, work.Top + work.Height / 2), false);
            var visible = Native.VisibleBounds(form.Handle);
            Check(Math.Abs(visible.Left - work.Left) <= 2 && Math.Abs(visible.Width - work.Width / 2) <= 2, "An linken Rand auf halbe Bildschirmbreite einrasten");

            grab = new(visible.Left + visible.Width / 2, visible.Top + 100);
            Drag(grab, new(work.Left + work.Width / 2, work.Top + 2), false);
            Check(Native.IsZoomed(form.Handle), "Am oberen Rand maximieren");

            grab = new(work.Left + work.Width / 2, work.Top + 100);
            Drag(grab, new(grab.X + 100, grab.Y + 100), false);
            Check(!Native.IsZoomed(form.Handle), "Maximiertes Fenster mit Alt herausziehen");
            Check(form.Width < work.Width, "Beim Herausziehen normale Grösse wiederherstellen");

            grab = new(form.Left + 150, form.Top + 150);
            AltClick(grab); AltClick(grab); Pump(300);
            Check(Native.IsZoomed(form.Handle), "Alt + Doppelklick maximiert");
            grab = new(work.Left + work.Width / 2, work.Top + work.Height / 2);
            AltClick(grab); AltClick(grab); Pump(300);
            Check(!Native.IsZoomed(form.Handle), "Alt + Doppelklick stellt wieder her");

            grab = new(form.Left + 150, form.Top + 150);
            AltClick(grab, true); AltClick(grab); Pump(300);
            Check(!Native.IsIconic(form.Handle) && !Native.IsZoomed(form.Handle), "Rechts + links ist kein Doppelklick");
            AltClick(grab, true); AltClick(grab, true); Pump(300);
            Check(Native.IsIconic(form.Handle), "Alt + Doppelklick rechts minimiert");
            Native.ShowWindowAsync(form.Handle, 9); Pump(300);

            using var settingsForm = new SettingsForm(new Settings(), () => { });
            settingsForm.Show(); Pump(150);
            Check(settingsForm.Controls.OfType<Card>().SelectMany(card => card.Controls.OfType<ToggleSwitch>()).Count() == 7, "Einstellungsfenster mit allen Schaltern öffnen");
            using var bitmap = new Bitmap(settingsForm.Width, settingsForm.Height);
            settingsForm.DrawToBitmap(bitmap, settingsForm.ClientRectangle with { Width = settingsForm.Width, Height = settingsForm.Height });
            bitmap.Save(Path.Combine(Path.GetTempPath(), "AltSnapLite-settings.png"));
            settingsForm.Close();
        }
        finally { form.Close(); Pump(); }
    }

    private static WindowController activeController = null!;
    private static bool FrameVisible() => Application.OpenForms.Cast<Form>().Any(f => f.GetType().Name == "Frame" && f.Visible);
    private static void WindowTrafficChecks(WindowTrafficForm form)
    {
        var start = form.Bounds;
        var from = new Point(start.Left + 180, start.Top + 140);
        var to = new Point(from.X + 35, from.Y + 25);
        Check(Event(Native.LeftDown, from), "Performance-Test startet Alt-Ziehen");
        form.ResetTraffic();
        Event(Native.MouseMove, to); Pump(160);
        Check(form.SizeRequests == 0, "Freies Verschieben sendet keine Grössenanforderungen");
        form.ResetTraffic();
        Pump(250);
        Check(form.PositionUpdates == 0, "Stillstehende Maus erzeugt keine weiteren Fensterupdates");
        Event(Native.LeftUp, to); Pump(120);
        Check(form.PositionUpdates == 0, "Loslassen sendet keine identische Position erneut");

        start = form.Bounds;
        from = new(start.Left + 180, start.Top + 140);
        to = new(from.X + 45, from.Y + 30);
        Check(Event(Native.LeftDown, from), "Release-Test startet Alt-Ziehen");
        Event(Native.MouseMove, to);
        Check(Event(Native.LeftUp, to), "Release-Position wird festgehalten");
        Event(Native.MouseMove, new(to.X + 100, to.Y + 100), false);
        Pump(200);
        Check(form.Left == start.Left + 45 && form.Top == start.Top + 30,
            "Bewegung nach Mouse-up verschiebt das Fenster nicht weiter");

        start = form.Bounds;
        from = new(start.Left + 180, start.Top + 140);
        Check(Event(Native.LeftDown, from), "Schneller Mausereignisstrom startet");
        form.ResetTraffic();
        for (int i = 0; i < 2000; i++) Event(Native.MouseMove, new(from.X + i % 90, from.Y + i % 30));
        to = new(from.X + 19, from.Y + 19);
        Event(Native.MouseMove, to); Event(Native.LeftUp, to); Pump(180);
        Check(form.PositionUpdates == 1, "2000 Mausereignisse werden zu einer letzten Fensterposition zusammengefasst");
        Check(form.SizeRequests == 0, "Auch ein Mausereignisstrom fordert kein Resize an");
    }

    private sealed class WindowTrafficForm : Form
    {
        internal int PositionUpdates, SizeRequests;
        internal void ResetTraffic() => PositionUpdates = SizeRequests = 0;
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x46 && message.LParam != 0)
            {
                uint flags = (uint)Marshal.ReadInt32(message.LParam, 2 * IntPtr.Size + 16);
                PositionUpdates++;
                if ((flags & Native.NoSize) == 0) SizeRequests++;
            }
            base.WndProc(ref message);
        }
    }
    private sealed class NativeWindowFixture : NativeWindow, IDisposable
    {
        internal NativeWindowFixture() => CreateHandle(new CreateParams
        {
            Caption = "AltSnap Lite · Topmost-Test", X = 100, Y = 100, Width = 300, Height = 200,
            Style = 0x10CF0000
        });
        public void Dispose() => DestroyHandle();
    }
    private static bool Event(int action, Point point, bool alt = true) => activeController.HandleMouse(action,
        new Native.MouseData { Point = new Native.Point { X = point.X, Y = point.Y } }, alt);

    private static void AltClick(Point at, bool right = false)
    {
        Event(right ? Native.RightDown : Native.LeftDown, at); Pump(20);
        Event(right ? Native.RightUp : Native.LeftUp, at); Pump(40);
    }

    private static void Drag(Point from, Point to, bool right)
    {
        Check(Event(right ? Native.RightDown : Native.LeftDown, from), "Alt-Geste wird erkannt");
        Pump(30); Event(Native.MouseMove, to); Pump(100);
        Event(right ? Native.RightUp : Native.LeftUp, to); Pump(150);
    }
}
