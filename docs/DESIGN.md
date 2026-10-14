# WPF design

The interface retains the existing dark watchface layout, accent palette, flat-ended 60-minute arc, outside minute ticks and 24 inner hour blocks. Numeric time shows total remaining time; there is no dial caption.

WPF vector geometry is drawn in device-independent units and follows per-monitor DPI. An unwrapped total-second dependency property drives the dial. Adding crosses twelve o'clock clockwise; subtracting crosses counterclockwise. Minute increments and hour blocks share a 600 ms cubic-eased edit animation. Normal running progress uses short linear WPF animations. The 100 ms state timer does not determine animation frame rate.

The entire first hour contributes to the hour blocks: 24 hours fills all 24 segments. Wheel input preserves high-resolution deltas and modifier semantics. Whole-hour stops last 400 ms, discard overshoot and permit immediate reversal.

Buttons, switches, mode selection, tab selection, popup appearance and main/settings transitions use WPF animation clocks. Popups use rounded borders and native WPF placement with keyboard focus and dismissal. Colour selection supports eight quick swatches, the full 17-hue / 5-shade palette, custom hex input and HSV mixing with a 4.5:1 contrast guide.

Countdown, power actions, warning balloons, abort recovery, keep-awake state, quick-timer semantics and settings keys match the WinForms app. Close/minimize hide to the tray; explicit Quit confirms active countdown cancellation. Native tray icons represent overall progress and are released with their HICON handles.
