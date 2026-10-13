# Development

Install .NET SDK 8. On Windows, run `build.cmd` to create `SleepTimer.exe`.

## Checks

- Logic checks: `bash scripts/check-timers.sh`.
- Windows UI and resource checks: `powershell -File scripts/check-windows.ps1`.

Windows checks exercise quick-timer actions, fast wheel input, disposal during animations, and GDI resource growth. Expiry polling is stopped before test countdowns; no power action is executed. Use a disposable test machine because the checks write app settings.

CI builds with warnings treated as errors and runs both suites. Pushing the `v1.0` tag also replaces the release executable after the Windows checks pass. Screenshots are maintained manually.
