# Development

Sleep Timer targets .NET Framework 4.8 and uses WPF. WinForms is used only for the Windows tray icon. No third-party runtime packages are required.

## Build and checks

Install .NET SDK 8 on Windows, then run:

```powershell
./build.cmd
./scripts/check-windows.ps1
```

Portable timer checks run on Linux or Windows with Bash:

```sh
bash scripts/check-timers.sh
```

Windows checks preserve saved settings and suppress power actions. CI also checks standalone startup and uploads the executable and UI previews.

Before a release, test real power actions, global keybinds, tray positioning across monitors, and text at different Windows scaling settings.
