# Sleep Timer desktop design

The WPF interface uses shared role colours and geometry in DesignTokens.cs and shared templates in Ui.cs. The accent remains the user's choice, default mint. Neutral roles, 28 px controls, 4 px control/menu corners, compact typography, 32 × 18 switches, sidebar navigation and a 28 px status bar follow the supplied portable desktop guidance. Segoe UI replaces the reference's Inter; no font files or UI packages are bundled.

Settings are live pages composed of unboxed section groups in a real ScrollViewer. Animation durations share an equal-column grid with units in the labels; text values are right-aligned. Fields save on Enter or focus loss and show invalid-input messages. The keyboard reference is the one reference card.

The timer remains a circular 60-minute watchface with 24 inner hour blocks. The counter and circle retain independent WPF animation clocks and configurable durations. Transparent dial geometry and main-page wheel preview handling make empty interior areas and the action dropdown respond consistently without extending wheel edits outside the circle.

Action, keep-awake and custom-timer choices share the field/dropdown implementation. Lists continue the field frame, mark the selected item and support keyboard navigation. Tray menus use the same rows as a separate card. The former logo/author footer and global gear are replaced by navigation and a plain status bar; the GitHub link is in the tray menu.

The window title always shows the current time and a status separated by ·. Ready, Scheduled, Running, Paused and executing power-action states are named in words. Power states retain fixed warning/danger colours. A sample gallery plus rendered main, clock, general, appearance, timer and popup pages are produced by Windows validation.
