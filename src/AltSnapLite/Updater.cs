using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace AltSnapLite;

// Stilles Update aus den GitHub-Releases: neue EXE herunterladen, Prüfsumme prüfen,
// laufende EXE umbenennen, neue an ihren Platz legen und neu starten.
internal sealed class Updater : IDisposable
{
    internal const string Repository = "svenbuild/altsnap-lite";
    internal const string AssetName = "AltSnapLite.exe";
    private const long MaximumSize = 300L * 1024 * 1024;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6), RetryInterval = TimeSpan.FromMinutes(30);
    private static readonly HttpClient Http = CreateClient();

    private readonly Settings settings;
    private readonly Func<bool> idle;
    private readonly Action exit;
    private readonly SynchronizationContext ui;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 60_000 };
    private DateTime nextCheck = DateTime.MinValue;
    private string? pending;
    private bool busy;

    // Zurückgerollte Version; gilt auch, wenn sie nicht in den Einstellungen gespeichert werden konnte.
    internal static string? SkippedThisSession;

    internal static Version Current { get; } = Normalize(typeof(Updater).Assembly.GetName().Version!);
    internal static string CurrentText => Current.ToString(3);

    // Nur die veröffentlichte Einzeldatei-EXE aktualisiert sich selbst, nicht ein Entwickler-Build.
    // Eine leere Location ist hier genau das gesuchte Merkmal der Einzeldatei.
#pragma warning disable IL3000
    internal static string? ExecutablePath =>
        string.IsNullOrEmpty(typeof(Updater).Assembly.Location) ? Environment.ProcessPath : null;
#pragma warning restore IL3000

    internal Updater(Settings settings, Func<bool> idle, Action exit, SynchronizationContext ui)
    {
        this.settings = settings;
        this.idle = idle;
        this.exit = exit;
        this.ui = ui;
        timer.Tick += async (_, _) => await Tick();
        if (ExecutablePath != null) timer.Start();
    }

    private async Task Tick()
    {
        if (pending != null) { TryInstall(); return; }
        if (busy || !settings.AutoUpdate || DateTime.UtcNow < nextCheck || ExecutablePath is not { } executable) return;
        busy = true;
        // Bei einem Fehler (z. B. noch offline) in 30 Minuten erneut versuchen.
        nextCheck = DateTime.UtcNow + RetryInterval;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await Http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode) return;
            var release = ParseRelease(await response.Content.ReadAsStringAsync(timeout.Token));
            nextCheck = DateTime.UtcNow + CheckInterval;
            if (release is not { } update || update.Version <= Current || update.Version.ToString(3) == settings.SkippedVersion
                || update.Version.ToString(3) == SkippedThisSession) return;
            string download = executable + ".download";
            await Download(update.Url, update.Sha256, download, timeout.Token);
            // Die Datei muss wirklich die angekündigte Version sein, sonst entsteht eine Update-Schleife.
            if (FileVersion(download) != update.Version) { TryDelete(download); return; }
            // Nach den awaits nicht auf den Thread verlassen: Austausch und Beenden im UI-Thread.
            ui.Post(_ =>
            {
                pending = download;
                timer.Interval = 2000;
                TryInstall();
            }, null);
        }
        // Update ist Komfort: jeder Fehler (Netz, Datei, unerwartete Antwort) wird still ignoriert.
        catch (Exception) { }
        finally { busy = false; }
    }

    // Erst ersetzen, wenn gerade keine Maus-Geste läuft.
    private void TryInstall()
    {
        if (pending == null || ExecutablePath is not { } executable || !idle()) return;
        string download = pending;
        pending = null;
        timer.Interval = 60_000;
        if (!settings.AutoUpdate) { TryDelete(download); return; }
        try
        {
            Replace(executable, download);
            try { Process.Start(new ProcessStartInfo(executable, "--updated") { UseShellExecute = false }); }
            catch (Exception)
            {
                Restore(executable);
                throw;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            TryDelete(download);
            return;
        }
        // Die neue Version wartet auf den Mutex; ein Fehler beim Aufräumen darf das Beenden nicht verhindern.
        try { exit(); } catch (Exception) { }
    }

    internal static (Version Version, string Url, string Sha256)? ParseRelease(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
        if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;
        if (!Version.TryParse(root.GetProperty("tag_name").GetString()?.TrimStart('v', 'V'), out var version)) return null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != AssetName) continue;
            string? url = asset.GetProperty("browser_download_url").GetString();
            string? digest = asset.TryGetProperty("digest", out var value) ? value.GetString() : null;
            // Ohne Prüfsumme wird nichts installiert.
            if (url == null || !url.StartsWith("https://github.com/", StringComparison.Ordinal)
                || digest == null || !digest.StartsWith("sha256:", StringComparison.Ordinal)) return null;
            return (Normalize(version), url, digest[7..].ToLowerInvariant());
        }
        return null;
    }

    internal static Version? FileVersion(string path)
    {
        string? text = FileVersionInfo.GetVersionInfo(path).ProductVersion?.Split('+')[0];
        return Version.TryParse(text, out var version) ? Normalize(version) : null;
    }

    private static async Task Download(string url, string sha256, string path, CancellationToken cancel)
    {
        try
        {
            await using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaximumSize) throw new InvalidDataException();
                await using var stream = await response.Content.ReadAsStreamAsync(cancel);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await stream.ReadAsync(buffer, cancel)) > 0)
                {
                    total += read;
                    if (total > MaximumSize) throw new InvalidDataException();
                    hash.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), cancel);
                }
                if (Convert.ToHexStringLower(hash.GetHashAndReset()) != sha256) throw new InvalidDataException();
            }
        }
        catch { TryDelete(path); throw; }
    }

    // Eine laufende EXE darf umbenannt, aber nicht überschrieben werden.
    internal static void Replace(string executable, string download)
    {
        string old = executable + ".old";
        File.Move(executable, old, true);
        try { File.Move(download, executable); }
        catch { File.Move(old, executable); throw; }
    }

    internal static void Restore(string executable)
    {
        string old = executable + ".old";
        if (File.Exists(old)) File.Move(old, executable, true);
    }

    // Die neue Version scheitert beim Start: sich selbst beiseitelegen, die alte EXE zurückholen,
    // diese Version künftig überspringen und die alte starten. Gibt false zurück, wenn das nicht geht.
    internal static bool RollBack()
    {
        if (ExecutablePath is not { } executable || !File.Exists(executable + ".old")) return false;
        try
        {
            // Das Merken der Version ist zweitrangig; das Zurückrollen geht auch ohne.
            try
            {
                var settings = Settings.Load();
                settings.SkippedVersion = CurrentText;
                settings.Save();
            }
            catch (Exception) { }
            File.Move(executable, executable + ".failed", true);
            try { File.Move(executable + ".old", executable); }
            catch { File.Move(executable + ".failed", executable); throw; }
            Process.Start(new ProcessStartInfo(executable, "--restarted --skip " + CurrentText) { UseShellExecute = false });
            return true;
        }
        catch (Exception) { return false; }
    }

    // Reste eines früheren Updates entfernen. Die alte EXE bleibt 30 Sekunden für ein Zurückrollen liegen.
    internal static void CleanUp()
    {
        if (ExecutablePath is not { } executable) return;
        Task.Run(async () =>
        {
            await Task.Delay(30_000);
            for (int attempt = 0; attempt < 20; attempt++)
            {
                foreach (string suffix in new[] { ".old", ".download", ".failed" }) TryDelete(executable + suffix);
                if (!File.Exists(executable + ".old") && !File.Exists(executable + ".failed")) return;
                await Task.Delay(1000);
            }
        });
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    private static Version Normalize(Version version) =>
        new(version.Major, Math.Max(version.Minor, 0), Math.Max(version.Build, 0));

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AltSnapLite");
        return client;
    }

    public void Dispose() => timer.Dispose();
}
