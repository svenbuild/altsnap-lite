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
    internal static string Resize => T("Grösse ändern (Rechts ziehen)", "Resize (right drag)");
    internal static string Topmost => T("Vordergrund (Mittelklick)", "Pin on top (middle click)");
    internal static string Hotkey => T("Taste für Gesten", "Gesture key");
    internal static string PressKey => T("Taste drücken …", "Press a key …");
    internal static string KeyNotAllowed => T(
        "Diese Taste geht nicht. Möglich sind Strg, Alt, Umschalt, Win, CapsLock, Rollen, Pause, Einfg, Menütaste und F1 bis F24, auch kombiniert.",
        "This key can't be used. Possible are Ctrl, Alt, Shift, Win, Caps Lock, Scroll Lock, Pause, Insert, Menu key and F1 to F24, also combined.");
    internal static string StartWithWindows => T("Mit Windows starten", "Start with Windows");
    internal static string AutoUpdate => T("Automatisch aktualisieren", "Update automatically");
    internal static string SnapDistance => T("Abstand zum Einrasten", "Snap distance");
    internal static string Settings => T("Einstellungen", "Settings");
    internal static string SettingsMenu => T("Einstellungen …", "Settings …");
    internal static string Exit => T("Beenden", "Exit");
    internal static string Gestures => T("Gesten", "Gestures");
    internal static string TrayActive(string key) => T($"AltSnap Lite · {key} + Maus", $"AltSnap Lite · {key} + mouse");
    internal static string TrayPaused => T("AltSnap Lite · pausiert", "AltSnap Lite · paused");
    internal static string Version => T("Version", "Version");
    internal static string Tagline(string key) => T($"Fenster mit {key} + Maus steuern", $"Control windows with {key} + mouse");
    internal static string Footer => T("Änderungen werden sofort gespeichert.", "Changes are saved instantly.");
    internal static string Website => "GitHub";

    internal static (string Input, string Action)[] GestureList =>
    [
        (T("Links ziehen", "left drag"), T("Verschieben", "Move")),
        (T("Rechts ziehen", "right drag"), T("Grösse ändern", "Resize")),
        (T("Doppelklick links", "double left click"), T("Maximieren / zurück", "Maximize / restore")),
        (T("Doppelklick rechts", "double right click"), T("Minimieren", "Minimize")),
        (T("Mittelklick", "middle click"), T("Immer im Vordergrund", "Always on top")),
        (T("an den Rand ziehen", "drag to an edge"), T("Einrasten", "Snap")),
    ];

    internal static string WelcomeTitle => T("AltSnap Lite läuft", "AltSnap Lite is running");
    internal static string WelcomeText(string key) => T(
        $"{key} + Links ziehen: verschieben\n{key} + Rechts ziehen: Grösse ändern\nKlick auf dieses Symbol: Einstellungen",
        $"{key} + left drag: move\n{key} + right drag: resize\nClick this icon: settings");
    internal static string UpdatedTitle => T("AltSnap Lite aktualisiert", "AltSnap Lite updated");
    internal static string UpdatedText(string version) => T($"Jetzt auf Version {version}.", $"Now on version {version}.");
    internal static string SaveFailed => T("Die Einstellung gilt für diese Sitzung, konnte aber nicht gespeichert werden.",
        "The setting applies to this session but could not be saved.");
    internal static string ChangeFailed => T("Die Einstellung konnte nicht geändert werden.", "The setting could not be changed.");
    internal static string ActionFailed => T("AltSnap Lite konnte die Aktion nicht ausführen.", "AltSnap Lite could not complete the action.");
    internal static string HookFailed => T("Der Maus-Hook konnte nicht gestartet werden.", "The mouse hook could not be started.");
}
