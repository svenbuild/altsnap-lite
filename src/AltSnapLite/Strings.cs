using System.Globalization;

namespace AltSnapLite;

// Alle Texte der Oberfläche; Deutsch bei deutscher Windows-Sprache, sonst Englisch.
internal static class Strings
{
    internal static bool German = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";
    private static string T(string german, string english) => German ? german : english;

    internal static string Enabled => T("Aktiv", "Enabled");
    internal static string Paused => T("Pausiert", "Paused");
    internal static string Snap => T("An Bildschirmrändern einrasten", "Snap to screen edges");
    internal static string Preview => T("Vorschau beim Einrasten", "Show snap preview");
    internal static string Resize => T("Grösse mit Alt + Rechts ändern", "Resize with Alt + right drag");
    internal static string Topmost => T("Vordergrund mit Alt + Mittelklick", "Pin on top with Alt + middle click");
    internal static string StartWithWindows => T("Mit Windows starten", "Start with Windows");
    internal static string AutoUpdate => T("Automatisch aktualisieren", "Update automatically");
    internal static string SnapDistance => T("Abstand zum Einrasten", "Snap distance");
    internal static string Settings => T("Einstellungen", "Settings");
    internal static string SettingsMenu => T("Einstellungen …", "Settings …");
    internal static string Exit => T("Beenden", "Exit");
    internal static string Gestures => T("Gesten", "Gestures");
    internal static string TrayActive => T("AltSnap Lite · Alt + Maus", "AltSnap Lite · Alt + mouse");
    internal static string TrayPaused => T("AltSnap Lite · pausiert", "AltSnap Lite · paused");
    internal static string Version => T("Version", "Version");
    internal static string Tagline => T("Fenster mit Alt + Maus steuern", "Control windows with Alt + mouse");
    internal static string Footer => T("Änderungen werden sofort gespeichert.", "Changes are saved instantly.");
    internal static string Website => "GitHub";

    internal static (string Keys, string Action)[] GestureList =>
    [
        (T("Alt + Links ziehen", "Alt + left drag"), T("Verschieben", "Move")),
        (T("Alt + Rechts ziehen", "Alt + right drag"), T("Grösse ändern", "Resize")),
        (T("Alt + Doppelklick links", "Alt + double left click"), T("Maximieren / zurück", "Maximize / restore")),
        (T("Alt + Doppelklick rechts", "Alt + double right click"), T("Minimieren", "Minimize")),
        (T("Alt + Mittelklick", "Alt + middle click"), T("Immer im Vordergrund", "Always on top")),
        (T("Alt + an den Rand ziehen", "Alt + drag to an edge"), T("Einrasten", "Snap")),
    ];

    internal static string WelcomeTitle => T("AltSnap Lite läuft", "AltSnap Lite is running");
    internal static string WelcomeText => T(
        "Alt + Links ziehen: verschieben\nAlt + Rechts ziehen: Grösse ändern\nKlick auf dieses Symbol: Einstellungen",
        "Alt + left drag: move\nAlt + right drag: resize\nClick this icon: settings");
    internal static string UpdatedTitle => T("AltSnap Lite aktualisiert", "AltSnap Lite updated");
    internal static string UpdatedText(string version) => T($"Jetzt auf Version {version}.", $"Now on version {version}.");
    internal static string SaveFailed => T("Die Einstellung gilt für diese Sitzung, konnte aber nicht gespeichert werden.",
        "The setting applies to this session but could not be saved.");
    internal static string ChangeFailed => T("Die Einstellung konnte nicht geändert werden.", "The setting could not be changed.");
    internal static string ActionFailed => T("AltSnap Lite konnte die Aktion nicht ausführen.", "AltSnap Lite could not complete the action.");
    internal static string HookFailed => T("Der Maus-Hook konnte nicht gestartet werden.", "The mouse hook could not be started.");
}
