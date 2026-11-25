# Changelog

## 1.2.0

- Choose your own gesture key: instead of Alt you can use Ctrl, Shift, Win, Caps Lock, Scroll Lock, Insert, the Menu key or F1 to F24, also combined (for example Ctrl + Alt). Click the key in the settings and press the new one.
- Keys like Caps Lock or F13 are reserved for AltSnap Lite while they are the gesture key.
- After a gesture with Alt or Win, releasing the key no longer opens the Start menu or the menu bar of the window.

## 1.1.1

- Fix: after a silent update the previous version now exits reliably, so the new version takes over right away instead of at the next start.

## 1.1.0

- New settings window: two columns, light and dark mode, toggle switches, gesture overview.
- Silent automatic updates from GitHub Releases (checked every 6 hours, verified by SHA-256, can be turned off).
- English and German interface, chosen from the Windows display language.
- New app icon; the tray icon turns gray while AltSnap Lite is paused.
- Clicking the tray icon or starting the EXE again opens the settings.
- The welcome tip only shows on the first start.
- Alt + double left click maximizes or restores a window, Alt + double right click minimizes it.

## 1.0.2

- Smoother moving and resizing: updates follow every mouse move, slow apps no longer lag behind.
- Alt + middle click toggles "always on top"; pinned windows get a colored frame.
- More robust: errors in the mouse hook no longer end the app, a lost mouse-up no longer blocks gestures.

## 1.0.1

- No repeated position requests while the mouse rests.
- Moving never asks a window to resize.

## 1.0.0

- First version: Alt + drag to move, Alt + right drag to resize, snap to halves, quarters and full screen.
