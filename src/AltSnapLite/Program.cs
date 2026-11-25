namespace AltSnapLite;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        bool smokeTest = args.Contains("--smoke-test"), updated = args.Contains("--updated"), autostart = args.Contains("--autostart");
        bool restarted = updated || args.Contains("--restarted");
        int skip = Array.IndexOf(args, "--skip");
        if (skip >= 0 && skip + 1 < args.Length) Updater.SkippedThisSession = args[skip + 1];
        string name = smokeTest ? @"Local\AltSnapLite.SmokeTest" : @"Local\AltSnapLite";
        using var mutex = new Mutex(false, name);
        using var show = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Show");
        // Nach einem Update oder Zurückrollen warten, bis sich die vorige Instanz beendet hat.
        if (!Acquire(mutex, restarted ? 300_000 : 0))
        {
            // Läuft schon: beim Doppelklick auf die EXE die Einstellungen der laufenden Instanz öffnen.
            if (!smokeTest && !autostart) show.Set();
            return;
        }
        try { Run(smokeTest, updated, show); }
        finally { mutex.ReleaseMutex(); }
    }

    private static bool Acquire(Mutex mutex, int timeout)
    {
        try { return mutex.WaitOne(timeout); }
        catch (AbandonedMutexException) { return true; }
    }

    private static void Run(bool smokeTest, bool updated, EventWaitHandle show)
    {
        // Mausereignisse auch bei ausgelasteter CPU zügig verarbeiten.
        using (var self = System.Diagnostics.Process.GetCurrentProcess())
            try { self.PriorityClass = System.Diagnostics.ProcessPriorityClass.AboveNormal; } catch (System.ComponentModel.Win32Exception) { }

        Application.ThreadException += (_, e) => ReportError(e.Exception);
        try
        {
            if (Updater.ExecutablePath != null && !smokeTest) Autostart.Refresh();
            using var app = new TrayApp(updated);
            // Erst nach erfolgreichem Start aufräumen, damit ein Zurückrollen die alte EXE noch findet.
            Updater.CleanUp();
            var wait = ThreadPool.RegisterWaitForSingleObject(show, (_, _) => app.ShowSettingsFromAnyThread(), null, -1, false);
            using var smokeTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            if (smokeTest)
            {
                smokeTimer.Tick += (_, _) => { smokeTimer.Stop(); app.ExitThread(); };
                smokeTimer.Start();
            }
            Application.Run(app);
            wait.Unregister(null);
        }
        catch (Exception e)
        {
            // Startet eine frisch installierte Version nicht, still zur vorigen zurückkehren.
            if (updated && Updater.RollBack()) return;
            Environment.ExitCode = 1;
            ReportError(e);
        }
    }

    private static bool reported;
    // Nur eine Meldung: Fehler in einem Timer würden sonst eine Meldung nach der anderen öffnen.
    private static void ReportError(Exception error)
    {
        if (reported) return;
        reported = true;
        MessageBox.Show(Strings.ActionFailed + "\n\n" + error.Message, "AltSnap Lite", MessageBoxButtons.OK, MessageBoxIcon.Error);
        reported = false;
    }
}
