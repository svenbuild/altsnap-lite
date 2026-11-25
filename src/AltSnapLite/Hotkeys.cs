namespace AltSnapLite;

// Die Taste oder Tastenkombination, die zusammen mit der Maus die Gesten auslöst (Standard: Alt).
// Gespeichert als virtuelle Tastencodes; linke und rechte Variante gelten als dieselbe Taste.
internal static class Hotkeys
{
    internal const int Shift = 0x10, Control = 0x11, Alt = 0x12, Win = 0x5B, Escape = 0x1B;
    internal static int[] Default => [Alt];

    internal static bool IsModifier(int key) => key is Shift or Control or Alt or Win;

    // Erlaubt sind Zusatztasten und Tasten, die beim Tippen nicht fehlen: CapsLock, Pause, Rollen, Einfg, Menütaste, F1 bis F24.
    internal static bool IsAllowed(int key) =>
        IsModifier(key) || key is 0x14 or 0x13 or 0x91 or 0x2D or 0x5D || key is >= 0x70 and <= 0x87;

    internal static bool IsValid(int[]? keys) =>
        keys is { Length: > 0 and <= 4 } && keys.All(IsAllowed) && keys.Distinct().Count() == keys.Length;

    internal static int Normalize(int key) => key switch
    {
        0xA0 or 0xA1 => Shift,
        0xA2 or 0xA3 => Control,
        0xA4 or 0xA5 => Alt,
        0x5C => Win,
        _ => key,
    };

    internal static string Name(int[] keys) => string.Join(" + ", keys.OrderBy(Rank).Select(KeyName));

    private static int Rank(int key) => key switch { Control => 0, Win => 1, Alt => 2, Shift => 3, _ => 4 + key };

    private static string KeyName(int key) => key switch
    {
        Control => Strings.German ? "Strg" : "Ctrl",
        Shift => Strings.German ? "Umschalt" : "Shift",
        Alt => "Alt",
        Win => "Win",
        0x14 => "CapsLock",
        0x13 => "Pause",
        0x91 => Strings.German ? "Rollen" : "Scroll Lock",
        0x2D => Strings.German ? "Einfg" : "Insert",
        0x5D => Strings.German ? "Menütaste" : "Menu key",
        >= 0x70 and <= 0x87 => "F" + (key - 0x6F),
        _ => "0x" + key.ToString("X2"),
    };
}
