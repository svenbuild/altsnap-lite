using System.Runtime.InteropServices;
using System.Text;

namespace AltSnapLite;

internal static class Native
{
    internal const int KeyboardHook = 13, MouseHook = 14, LeftDown = 0x201, LeftUp = 0x202,
        RightDown = 0x204, RightUp = 0x205, MiddleDown = 0x207, MiddleUp = 0x208, MouseMove = 0x200;
    internal const uint NoSize = 0x1, NoMove = 0x2, NoActivate = 0x10, NoZOrder = 0x4, AsyncWindowPos = 0x4000;
    internal static readonly nint Topmost = new(-1), NotTopmost = new(-2);
    internal delegate nint HookProc(int code, nint message, nint data);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point { public int X, Y; public readonly System.Drawing.Point Drawing => new(X, Y); }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left, Top, Right, Bottom;
        public readonly Rectangle Drawing => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseData { public Point Point; public uint Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyData { public uint Key, Scan, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Placement
    {
        public uint Length, Flags, Show;
        public Point Min, Max;
        public Rect Normal;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct MinMaxInfo { public Point Reserved, MaxSize, MaxPosition, MinTrack, MaxTrack; }

    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetWindowsHookEx(int type, HookProc proc, nint module, uint thread);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandle(string? module);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern void keybd_event(byte key, byte scan, uint flags, nuint extra);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool GetWindowPlacement(nint window, ref Placement placement);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(nint window, int command);
    [DllImport("user32.dll")] internal static extern bool IsZoomed(nint window);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] internal static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] internal static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(nint window, StringBuilder name, int capacity);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(nint window, int attribute, out Rect value, int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
    [DllImport("user32.dll")] internal static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint SendMessageTimeout(nint window, uint message, nint wParam, ref MinMaxInfo info, uint flags, uint timeout, out nint result);
    [DllImport("kernel32.dll")] internal static extern nint OpenProcess(uint access, bool inherit, uint process);
    [DllImport("kernel32.dll")] internal static extern bool CloseHandle(nint handle);
    [DllImport("advapi32.dll")] internal static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("advapi32.dll")] internal static extern bool GetTokenInformation(nint token, int type, out int value, int size, out int returned);

    internal static bool IsElevated(uint processId)
    {
        nint process = OpenProcess(0x1000, false, processId);
        if (process == 0) return true;
        try
        {
            if (!OpenProcessToken(process, 8, out var token)) return true;
            try { return !GetTokenInformation(token, 20, out int elevated, 4, out _) || elevated != 0; }
            finally { CloseHandle(token); }
        }
        finally { CloseHandle(process); }
    }

    internal static Rectangle Bounds(nint window) { GetWindowRect(window, out var rect); return rect.Drawing; }
    internal static bool IsTopmost(nint window) => (GetWindowLong(window, -20) & 8) != 0;
    // Fenster auf einem anderen virtuellen Desktop sind "cloaked", aber trotzdem IsWindowVisible.
    internal static bool IsCloaked(nint window) => DwmGetWindowAttribute(window, 14, out int cloaked, 4) == 0 && cloaked != 0;

    internal static Rectangle VisibleBounds(nint window)
    {
        if (DwmGetWindowAttribute(window, 9, out Rect frame, Marshal.SizeOf<Rect>()) == 0) return frame.Drawing;
        return Bounds(window);
    }
}
