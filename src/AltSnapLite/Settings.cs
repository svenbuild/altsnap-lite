using System.Text.Json;
using Microsoft.Win32;

namespace AltSnapLite;

internal sealed class Settings
{
    public bool Enabled { get; set; } = true;
    public bool Snap { get; set; } = true;
    public bool Resize { get; set; } = true;
    public bool TopmostClick { get; set; } = true;
    public bool Preview { get; set; } = true;
    public bool AutoUpdate { get; set; } = true;
    public int SnapDistance { get; set; } = 24;
    // Taste oder Kombination für die Gesten als virtuelle Tastencodes, Standard Alt.
    public int[] Hotkey { get; set; } = Hotkeys.Default;
    // Version, die nach einem Update nicht starten konnte; wird nicht erneut installiert.
    public string? SkippedVersion { get; set; }
    // Keine Einstellungsdatei gefunden: erster Start.
    [System.Text.Json.Serialization.JsonIgnore] internal bool IsNew { get; private set; }

    internal static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AltSnapLite");
    internal static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");

    internal static Settings Load(string? path = null)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path ?? FilePath)) ?? new();
            settings.SnapDistance = Math.Clamp(settings.SnapDistance, 8, 64);
            if (!Hotkeys.IsValid(settings.Hotkey)) settings.Hotkey = Hotkeys.Default;
            return settings;
        }
        catch (FileNotFoundException) { return new() { IsNew = true }; }
        catch (DirectoryNotFoundException) { return new() { IsNew = true }; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    internal void Save(string? path = null)
    {
        path ??= FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}

internal static class Autostart
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run", Name = "AltSnapLite";
    // Beim Autostart kein Einstellungsfenster einer schon laufenden Instanz öffnen.
    private static string Command => "\"" + Environment.ProcessPath + "\" --autostart";

    internal static bool Enabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(Key); return key?.GetValue(Name) is string; }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(Key);
            if (value) key.SetValue(Name, Command);
            else key.DeleteValue(Name, false);
        }
    }

    // Ältere Einträge dieser EXE ohne --autostart ergänzen. Einträge für eine andere Kopie bleiben unverändert.
    internal static void Refresh()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Key, true);
            if (key?.GetValue(Name) is string current && current != Command
                && current.Trim().Trim('"').Equals(Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
                key.SetValue(Name, Command);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
    }
}
