What this repo does: Sleep Timer schedules sleep, shutdown or restart on one Windows desktop. It stores preferences locally and offers tray controls and global shortcuts. This review assumes one user and one running instance per Windows session.

Reviewed using the Ponytail audit, WPF and .NET guidance. Findings below were fixed in the 1.1 preparation changes.

## Must fix

1. **Taskbar progress stayed static** (`src/MainWindow.cs`, `UpdateIcon`)
   - **What this is:** The taskbar and tray show the countdown ring.
   - **Problem:** Only NotifyIcon received refreshed images. The WPF window kept its original icon throughout a countdown.
   - **Fix:** Update both from the total countdown fraction. Copy and freeze the WPF bitmap before releasing its native handle. Regression checks cover a paused timer halfway through a multi-hour duration.
   - **If we skip it:** The taskbar ring cannot show remaining progress.

2. **A second launch could lose its activation request** (`src/Platform.cs`, `SingleInstance.Claim`)
   - **What this is:** Starting the app again brings the running window forward.
   - **Problem:** The instance marker existed before its activation event. A second process starting in that gap could fail to find the event and exit silently.
   - **Fix:** Create the activation event before publishing the instance marker.
   - **If we skip it:** Rapid repeated launches can leave the existing window hidden.

3. **Release automation targeted the old release** (`.github/workflows/validate.yml`)
   - **What this is:** CI builds and packages the app.
   - **Problem:** The existing release step targeted 1.0 while the app is being prepared for 1.1.
   - **Fix:** Gate release preparation on successful portable and Windows checks and a future v1.1 tag. Create a draft requiring manual publication.
   - **If we skip it:** A release operation could replace the older version with the new build.

## Should fix

4. **Boolean compatibility missed DWORD settings** (`src/Platform.cs`, `Settings.Get`)
   - **What this is:** Saved preferences accept strings and registry numbers.
   - **Problem:** A DWORD value of 1 was read as false even though the reader documents support for numbers.
   - **Fix:** Accept 1 as true; Windows regression checks cover 0, 1 and a true string.
   - **If we skip it:** Imported numeric preferences can switch off unexpectedly.

5. **Font window behavior was spread across the main window** (`src/FontWindow.cs`)
   - **What this is:** Separate font families and weights configure the timer and UI.
   - **Problem:** An extra popup stood between the Aa button and the font gallery, and double-click did not finish selection.
   - **Fix:** Move the gallery into a dedicated window with Timer/UI tabs and apply-and-close on double-click. Keep callbacks for applying preferences in MainWindow.
   - **If we skip it:** The requested selection flow remains incomplete and font-window changes keep expanding the main coordinator.

## Validation and scope

Reviewed timer input and expiry, scheduling and daylight-saving handling, power-action dispatch and error recovery, registry persistence, global shortcut lifetime, tray placement, icon ownership, WPF drawing caches, font availability and weights, shutdown cleanup, entry points, project configuration and release automation. No new packages were introduced.

Portable checks: 208 passed. Both Release builds passed with warnings treated as errors. Windows CI exercises WPF interactions, native hotkey registration, font selection, icon progress, tray popup reopening, resource disposal and standalone startup; see the matching WPF workflow run for the final result.

Verdict: The concrete findings above are fixed. Ready for Windows user testing before publishing 1.1.

Not checked: Physical sleep/shutdown/restart (tests suppress power actions), actual keyboard delivery on the user's desktop, mixed-DPI multi-monitor placement, perceived sharpness at 100/150/200% scaling, or the pending README showcase screenshot. These need the user's Windows test. The concurrent-start ordering fix was reviewed but not stress-tested across processes.
