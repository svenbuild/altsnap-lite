using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AltSnapLite;

// Farben für helles und dunkles Windows-Design.
internal sealed record Theme(bool Dark, Color Window, Color Surface, Color Border, Color Text, Color Muted,
    Color Accent, Color OnAccent, Color Hover, Color Chip)
{
    internal static readonly Theme Light = new(false, Color.FromArgb(243, 244, 248), Color.White, Color.FromArgb(226, 228, 236),
        Color.FromArgb(27, 31, 43), Color.FromArgb(96, 103, 122), Color.FromArgb(70, 92, 230), Color.White,
        Color.FromArgb(238, 240, 250), Color.FromArgb(244, 245, 249));

    internal static readonly Theme DarkTheme = new(true, Color.FromArgb(28, 29, 34), Color.FromArgb(39, 40, 47), Color.FromArgb(56, 58, 68),
        Color.FromArgb(236, 237, 242), Color.FromArgb(160, 164, 178), Color.FromArgb(124, 142, 255), Color.FromArgb(20, 22, 32),
        Color.FromArgb(50, 52, 62), Color.FromArgb(48, 50, 59));

    internal static Theme Current
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is 0 ? DarkTheme : Light;
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException) { return Light; }
        }
    }

    // Dunkle Titelleiste und abgerundete Ecken (Windows 11). Auf älteren Systemen ohne Wirkung.
    internal static void ApplyWindowStyle(nint handle, bool dark, bool roundSmall = false)
    {
        int value = dark ? 1 : 0;
        DwmSetWindowAttribute(handle, 20, ref value, sizeof(int));
        if (roundSmall) { value = 3; DwmSetWindowAttribute(handle, 33, ref value, sizeof(int)); }
    }

    internal static bool IsWindows11 => Environment.OSVersion.Version.Build >= 22000;

    internal static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) { path.AddRectangle(bounds); return path; }
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
