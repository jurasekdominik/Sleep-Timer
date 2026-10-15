# Project plan

## Where we ended

The WPF branch targets .NET Framework 4.8 and uses framework-provided WPF plus native tray integration. Build on Windows with .NET SDK 8; run bash scripts/check-timers.sh for portable logic and scripts/check-windows.ps1 for Windows UI checks. Test mode prevents real power actions. Never merge to main without the owner's instruction.

## Next

- Reproduce and fix wheel input over blank dial interiors and the action selector.
- Show current time and status in the window title, separated with ·.
- Let the force-close setting be switched off even when Sleep is selected.
- Align independent animation-duration fields in Appearance.
- Apply the supplied desktop design system using shared tokens/components, native Segoe UI, and the existing accent. Render and inspect all affected screens and a component gallery.

## Decisions

- The supplied workflow and design-system references remain private working references; do not commit AGENTS.md or DESIGN_SYSTEM.md copied from the other repository.
- Retain the mint default accent and user colour choices. Do not bundle fonts, a runtime, or a UI toolkit.
- Preserve the circular timer, 600 ms default edit animations, independent configurable counter/circle timings and 400 ms hour stop.
- Apply compact controls, role colours, a sidebar and unboxed settings groups. The large numeric timer and circular dial are application-specific displays rather than standard form controls.
- Keep established scheduled power-action behavior: tests must never execute it; no confirmation is inserted at countdown expiry.
