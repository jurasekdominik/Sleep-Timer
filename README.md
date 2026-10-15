# Sleep Timer — WPF branch

A small Windows timer for shutdown, sleep and restart. This branch replaces the WinForms interface with WPF while preserving the existing countdown and registry settings.

Requires Windows 10/11 with .NET Framework 4.8 (included on current Windows versions). There is no bundled .NET runtime, third-party UI toolkit or font. The Windows validation workflow measures the executable and verifies that a copy of **SleepTimer.exe alone** starts successfully.

## Use

- Left-click the dial to start, pause or resume; right-click to cancel.
- Scroll: 5 minutes. Alt: 1 minute. Shift: 15 minutes. Ctrl: 30 minutes. Ctrl+Shift: 60 minutes.
- Wheel edits stop at whole hours for 400 ms. Reversing direction bypasses the hold.
- Dial edits animate for 600 ms; the counter follows in minute steps. Hour rollovers retain the scroll direction.
- Duration and At time modes, keep awake, keep screen on, quick presets, custom presets, startup, force close and accent settings remain available.
- Close or minimize to hide in the tray. Double-click the tray icon to reopen.
- Custom timers accept whole minutes (`90`) or whole hours (`2h`, `5h`), up to 24 hours.
- At-time scheduling respects daylight-saving changes. Skipped times move to the first valid minute; repeated times use the next future occurrence.
- In Settings, the **Aa** icon beside the gear opens **Timer font** and **UI font**. Each selection is independent and defaults to Segoe UI Regular. Choose a family and then an installed weight (such as Light, Regular or Bold). UI choices favor readable families; Bahnschrift and monospaced choices are reserved for timer digits. Aptos appears when installed. No fonts are downloaded or bundled; missing saved fonts fall back to Segoe UI.
- Settings → **Keybinds** offers global **Start / Cancel** and **Start last used quick timer** shortcuts. Both start unbound. Click a field and press a combination with Ctrl, Alt or Win; Delete clears it. Shortcut actions show a Windows notification (subject to Windows notification settings). With no previous quick timer, the quick shortcut starts 30 minutes.
- Tray quick-action changes apply to the next quick timer without changing an active countdown.

## Build and validation

Install .NET SDK 8 on Windows and run `build.cmd`.
Run `bash scripts/check-timers.sh` for portable logic checks and `powershell -File scripts/check-windows.ps1` for WPF UI checks.

The WPF branch's Validate workflow publishes **SleepTimer-WPF** with the standalone executable, executable size and rendered UI previews. It does not replace the stable release or merge into main.

WPF animation clocks drive the vector dial, controls and transitions without a fixed 60 Hz UI timer. Actual refresh behavior depends on Windows' compositor, GPU, display setup and workload; automated UI checks cannot certify the feel of a physical 144 Hz monitor.
