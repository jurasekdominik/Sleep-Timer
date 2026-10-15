# Project plan

## Where we ended

Work stays on WPF; merge to main only when the owner requests it. Target .NET Framework 4.8. Build with .NET SDK 8 on Windows and run scripts/check-windows.ps1; run bash scripts/check-timers.sh for portable checks. TestMode blocks real power actions. Inspect rendered page and component-gallery PNGs attached to CI.

This batch fixes dial-interior and descendant wheel input, timer/status window titles, force-close editability under Sleep, and aligned animation duration inputs. The force-close regression and native wheel reproduction were committed before the fixes.

## Next

- Owner checks actual wheel interaction and high-refresh animation feel on Windows.

## Decisions

- The supplied AGENTS.md and DESIGN_SYSTEM.md remain private working references. Neither imported file is part of the repository.
- Retain the mint accent and user-selected colours. Use installed Segoe UI and installed monospace fonts; do not bundle fonts, a runtime or a UI toolkit.
- The desktop design is implemented by shared role and geometry tokens. Sidebar navigation replaces the global gear and settings tabs. Unboxed setting blocks replace decorative cards; the keyboard reference is a reference card.
- The logo/author footer is replaced by a plain status bar, as the supplied shell guideline specifies. Its GitHub action moves to the tray menu.
- The inline action pill becomes the shared dropdown. Keep its three choices and active-timer restrictions.
- The timer's large numerical display and circular geometry are domain-specific. Preserve 600 ms default edit motion, independent counter/circle overrides, and the 400 ms wheel stop.
- Force close remains configurable with every action; it applies to shutdown/restart and is explicitly described as ignored for Sleep.
- Scheduled power actions retain their behavior; no new confirmation is inserted at expiry. UI tests never execute them.
