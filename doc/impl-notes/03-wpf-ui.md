# Module Notes 03 - WPF UI (`OverlayWindow`, `SettingsWindow`, `AppContext`)

> Parent index: [`doc/ai-implementation-notes.md`](../ai-implementation-notes.md)
> Related: [`01-runtime-and-configuration.md`](01-runtime-and-configuration.md), [`02-display-and-switching.md`](02-display-and-switching.md), [`04-testing-and-operations.md`](04-testing-and-operations.md)

## Notification-Area Icon

- `TrayIcon` attaches an `HwndSource` message hook to the already-created `OverlayWindow`; the overlay keeps `ShowInTaskbar=false` and is explicitly marked with `WS_EX_TOOLWINDOW` while clearing `WS_EX_APPWINDOW`, so it is hidden from both the taskbar window list and Alt+Tab. `DisplayApi` registers the icon through `Shell_NotifyIconW`.
- A left click on the notification-area icon opens Settings. A right click uses a native `TrackPopupMenuEx` menu owned by the overlay HWND, with the same Settings/Exit callbacks as the overlay menu. Native menu tracking handles outside-click dismissal; a trailing `WM_NULL` completes notification-area menu deactivation after the hidden-icons flyout closes. The actual desktop interaction remains part of the M14 manual acceptance check.
- `AppContext.OnExit` removes the icon and unregisters the message hook. Tray registration failures are logged and do not prevent the overlay from starting.

## Overlay Window

- `OverlayWindow` is a borderless, transparent, topmost WPF window with `ShowInTaskbar=false`, no XAML, and a code-built visual tree. The content is a rounded rectangle with two equal zones: left for primary-monitor switching and right for resolution switching.
- The left and right icons are WPF `Geometry` objects. Fixed icon dimensions avoid unreliable `NaN`/`MaxWidth` layout combinations inside the grid.
- The window receives behavior through callbacks supplied by `AppContext`; it does not own display-switching or configuration persistence logic.
- Right-click on the overlay uses a WPF `ContextMenu` with settings and exit commands. The notification-area right-click path intentionally uses a native menu because it is invoked from the Windows hidden-icons flyout and needs native menu tracking.

## Pointer and Opacity Rules

- A left-button press begins a potential drag and captures the mouse. Movement greater than four physical pixels switches to drag mode; the window becomes fully opaque and follows the cursor.
- A click that stays within the four-pixel threshold invokes the callback for the zone that received the mouse-up event.
- Hovering the root immediately sets opacity to `1.0`; each zone also receives a subtle local highlight. Leaving the root animates opacity back to the configured idle value over 600 ms unless a drag is active.
- A completed drag stores the button's physical position and invokes the configuration-dirty callback. Closing the window stores the position as well.

## Position and DPI

- WPF window coordinates are device-independent pixels, while saved button coordinates and Win32 monitor bounds are treated as physical pixels. `CompositionTarget.TransformToDevice` supplies the scale conversion.
- A saved position uses `ButtonCfg.NoPosition` (`int.MinValue`) as the missing-value sentinel. Negative coordinates are valid for monitors placed to the left of the primary display.
- Startup restores a saved position, checks intersection against every active monitor, and moves the button to the primary work area with a 16-pixel edge margin when the saved position is missing or completely off-screen.
- After a successful primary-monitor switch, `ApplyPrimaryShift` moves the window by the reported physical-pixel shift, persists the compensated position, and logs the new coordinates.

## Settings Window

- `SettingsWindow` is a code-built, non-resizable WPF dialog. It edits autostart, target monitor (`auto` or a fixed device), button size, idle opacity, color, and the ordered resolution collection.
- Monitor and resolution choices come from `DisplayApi`; `auto` uses the current primary monitor for support-list lookup. Duplicate resolution entries are not added.
- Before confirmation, button size is validated against `24..128` and every collection item is checked against the selected device's support list. The dialog only mutates the shared configuration after validation and sets `Confirmed` before closing.
- `AppContext` applies confirmed settings to the overlay, resets the switcher state, persists the configuration, and reports autostart or save failures with the current session log path.

## UI Failure Boundary

- Display and persistence failures remain in Core result/error properties. `AppContext` translates them into Chinese dialogs containing the system detail, a suggested action, and `Logger.LogFile`.
- The UI migration from WinForms to WPF requires manual checks for rendering, window lifecycle, DPI placement, interaction thresholds, and settings dialog behavior; Core-only tests cannot validate those surfaces.
