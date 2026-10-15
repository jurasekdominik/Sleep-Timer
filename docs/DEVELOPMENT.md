# WPF development

Target: .NET Framework 4.8, WPF, one small framework-dependent executable.
Build with .NET SDK 8 on Windows. The framework's WinForms NotifyIcon is retained only for native tray integration; all windows, settings and popup menus use WPF.

Shared timer logic stays in Countdown.cs, TimerDial.cs, QuickTimers.cs and WheelInput.cs. Native power calls, registry persistence and single-instance activation stay in Platform.cs.
MainWindow.cs coordinates behavior. Ui.cs implements WPF styles, vector dial and popup menus. ColorPicker.cs contains the contrast-aware palette and HSV mixer.

Animations use WPF DoubleAnimation and dependency properties. Do not introduce a 16 ms DispatcherTimer to drive animation frames, or set a 60 Hz DesiredFrameRate. The 100 ms timer only polls state, saves settings and checks expiry; it does not pace rendering. Whole-hour rollover animates unwrapped seconds and wraps only while drawing.

Checks:
- bash scripts/check-timers.sh
- powershell -File scripts/check-windows.ps1 (Windows)
- CI verifies a standalone copy of SleepTimer.exe with --verify-startup and reports its exact size.

WPF checks preserve registry settings and stop expiry polling before test countdowns; TestMode also prevents power actions. They exercise wheel stops, minute counter and dial animations, mode switches, quick-action isolation, keep-awake choices, custom presets, popup reopening, Alt release and resource disposal. Rendered main/settings/appearance/timers previews are attached to CI artifacts.

Review manually at 100%, 150% and 200% DPI and on a high-refresh display. Compare smoothness, fonts, tray positioning and input on the real device before replacing the stable release.

Custom timer input accepts whole minutes (1–1440) or whole hours (1h–24h). Fractional, mixed and clock-style durations are rejected. Persisted presets remain integer minutes.

At-time targets are resolved with timezone offsets before calculating elapsed seconds. During a clock gap, use the first valid minute; during a repeated hour, use the first occurrence still in the future.

All in-window artwork, including the footer mark, is vector-based. Popups use layout rounding and device-pixel snapping. Tray icons are rasterized from vectors at the requested system icon size. Validate actual per-monitor sharpness on Windows at 100%, 150% and 200% DPI.

Tooltips use the app's dynamic theme resources. Windows owns the tray tooltip and taskbar preview chrome; the thumbnail title shows the full timer value, with action and status in the shell description.

Tray popups capture the screen position at the tray click and use custom placement against that fixed point, including after submenu navigation and while the main window is hidden. Test reopening near each taskbar edge and across monitors with different DPI.

Global keybinds use RegisterHotKey/UnregisterHotKey with MOD_NOREPEAT. Both are unbound until configured. Editing temporarily suspends registrations; duplicate or unavailable combinations are rejected without replacing the previous binding. WM_HOTKEY dispatch verifies both the current key and modifiers, so stale messages cannot trigger a different action. Start/Cancel cancels either a running or paused timer; the quick action recalls the last duration (30 minutes before the first use). Shortcut-triggered changes use native Windows notification balloons.

Windows validation tests actual shortcut registration, conflicts, persistence, release, message dispatch, notification text, themed tooltips, and stable tray reopening after choosing keep awake. TestMode suppresses power actions and notification delivery.

The timer font gallery previews six installed Windows font variations, without downloaded fonts. Only timer digits change; body text and layout remain Segoe UI. Bahnschrift and Consolas include Segoe UI fallback. Font selection is saved as `timer_font`, and the drawing cache includes the selected typeface so switching immediately replaces old glyphs. Windows validation captures the gallery and all six rendered examples.

Global shortcuts remain registered during Hide/minimize; only editing a binding or disposing the window unregisters them. Windows checks explicitly deliver start/cancel hotkey messages while minimized to the tray and confirm registrations remain active. Physical global key delivery still requires a Windows desktop test.
