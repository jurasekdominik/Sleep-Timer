# Sleep Timer design

## Appearance

Background `#111111`, primary text `#EDEDED`, default accent `#0CD29D`. Neutrals and accent shades are computed in `Theme`. Cards use a 12 px radius, menus 10 px, fields 8 px, menu rows 6 px, switches a capsule. Spacing uses 8 px units and 4 px half steps. UI font is Segoe UI Variable Text with Segoe UI fallback.

## Behavior

`src/Ui.cs` owns the shared palette and controls. The timer remains the main interaction: left-click starts, pauses or resumes; right-click cancels; scrolling changes time. No new power actions are introduced.

The watchface is always a 60-minute clockwise dial from twelve o’clock. Each subtle outside tick is one minute; every fifth tick is longer. The flat-colour ring is 16 px thick at 96 DPI, with a quiet outer border. An idle timer uses a faded accent; paused timers are grey. Both the dial and tray turn amber for the final minute and red for the final ten seconds.

Additional whole hours appear as adjacent 10 px thick, 15-degree annular rectangles, each with its own outline and squared radial ends. The unused inner circle has no track, background fill or border. There are no gaps or pill caps; the last hour segment slowly fades with the current hour, with linear 100 ms interpolation between updates, and the caption names their count. A 2 h 30 min timer has two hour marks and a half ring. An exact 2 h timer has one hour mark and a full ring; as it crosses an hour boundary, one mark disappears and the minute ring starts again from full. Boundary changes snap instead of animating backwards across the whole circle. The numeric countdown remains the total remaining time, and the hour count follows the numeric display’s rounded seconds, while the arc and hour fading retain subsecond precision for smooth motion. Edits immediately update the hour marks and minute position, including paused timers. Tray icons show overall remaining progress and use the same flat ring and state colours, without minute ticks or hour segments. Paused tray icons include a pause symbol. The tooltip states remaining time and the overall completion percentage.

Windows font fallback is retained; Cascadia Code is used if installed, with Cascadia Mono and Consolas fallbacks. No font binary is bundled. The colour picker retains a stricter 4.5:1 guide for small accent text; the appearance note flags accents below the design system’s 3:1 minimum.

Runtime review requires Windows 10/11: inspect the dial at 1, 30 and 60 minutes, 2, 12 and 24 hours; pause/resume, switch to At time, adjust time across hour boundaries, and check settings, menus, fields and switch animations at 100%, 150% and 200% scaling. Keep destructive power actions out of visual review.

Quick timers in the tray start 30-minute, 1-hour, 2-hour and 4-hour countdowns with the selected power action. Choosing a preset replaces the current timer and switches to Duration mode. Settings → Timers accepts whole minutes from 1 to 1440. Custom presets persist in `HKCU\Software\SleepTimer` as `quick_timers`, are deduplicated and sorted among the built-in values, and can be removed from the Custom timers list. Long menus scroll with the mouse wheel.

Switches use the updated 28 × 20 capsule thumb on a 44 × 24 track, with 2 px inset. Compact controls preserve the capsule proportions, and press stretching is clamped inside the track. Popup borders share a 10 px outer radius with their native clipping mask; the painted stroke is inset with a concentric radius so clipping does not cut through antialiased border pixels. Default ToolStrip borders are suppressed.

The tray starts with Keep awake, then Quick timers showing the selected action. An idle timer offers Quick start for the most recently used preset (default 30 min), and one Start timer / Cancel timer row. Active timers also offer Pause / Resume. Quick start is near the bottom, above Quit; Settings is accessed in the window. Start timer shows the configured duration or At time target. Double left-click opens the window. Keep awake choices use accent semibold text and a subtle selected-row fill rather than tick boxes; Keep screen on states On / Off in words.

Both ends of the main minute arc and tray progress arc are flat, matching the squared hour segments. Duration / At time selection, row movement and field fading share one 320 ms cubic ease-out animation, with stable font weight throughout.

Shared motion tokens in `src/Ui.cs` keep progress at 100 ms linear, button presses at 90 ms linear, switches at 240 ms, tabs at 220 ms, reveal transitions at 160 ms, and the coordinated Duration / At time transition at 320 ms. State changes use cubic ease-out; progress never eases or bounces. The timer samples at 100 ms and uses subsecond geometry, while the text remains a whole-second countdown. Hour rollovers reset the minute arc directly to avoid a misleading backward sweep.

Quick timers → Action opens a selectable list of Shut down, Sleep and Restart, with the current choice highlighted. Idle action changes synchronize with the window picker. During an active timer, action selection is saved for the next quick timer and does not change the running or paused timer’s action. Starting a quick preset applies that saved action while replacing the current countdown.

## Resource ownership and wheel input

Controls own and dispose their animation timers. Popups, tooltips, images and native icon handles are released on shutdown. Wheel deltas retain fractional notches, reset partial input on direction or modifier changes, and are coalesced into one update per 16 ms. Animation retargeting samples the current interpolated value. Duration settings are saved after 250 ms without wheel input rather than on every event.
