# Module Notes 02 - Display and Switching (`DisplayApi`, `ResolutionSwitcher`)

> Parent index: [`doc/ai-implementation-notes.md`](../ai-implementation-notes.md)
> Related: [`01-runtime-and-configuration.md`](01-runtime-and-configuration.md), [`03-wpf-ui.md`](03-wpf-ui.md), [`04-testing-and-operations.md`](04-testing-and-operations.md)

## DisplayApi Boundary

- `Core/DisplayApi.cs` is the only file allowed to contain P/Invoke declarations. The Core layer does not use WPF or WinForms display-control types.
- The wrapper uses classic Unicode `DllImport` declarations for `user32.dll`: `EnumDisplayDevicesW`, `EnumDisplaySettingsW`, `ChangeDisplaySettingsExW`, `EnumDisplayMonitors`, `GetMonitorInfoW`, `GetDisplayConfigBufferSizes`, `QueryDisplayConfig`, `DisplayConfigGetDeviceInfo`, and `SetDisplayConfig`.
- `DEVMODEW` must preserve the Win32 field order and union width. The managed structure is 220 bytes and is initialized with `dmSize` before enumeration. `DISPLAY_DEVICEW` and `MONITORINFOEXW` use fixed-width Unicode buffers.
- Monitor enumeration keeps active display devices only. Monitor geometry comes from `EnumDisplayMonitors` and `GetMonitorInfoW`, with a current-resolution fallback if a matching monitor is not found.

## Resolution Behavior

- `GetSupportedResolutions` enumerates all display modes, removes duplicate width/height pairs, and sorts by pixel count descending. Refresh rate is not part of the public `Resolution` record.
- `TrySetResolution` first confirms the requested width/height pair appears in the enumerated support list, then asks `SetDisplayConfig` to validate and apply the source mode across the complete active topology. With virtual-mode-aware paths, the source mode index is the high 16 bits of the source-info union; the low 16 bits carry the clone group. When the queried path reports an invalid source index, the implementation associates the source mode by adapter LUID plus source ID and rewrites the path index before submission. If the CCD path is unavailable or rejected, it falls back to the legacy `DEVMODEW`/`ChangeDisplaySettingsExW` call.
- `ResolutionSwitcher` resolves the physical target, reads its supported modes before computing the transition, selects the matching `MonitorProfile`, and treats configured items absent from that support list as nonexistent. This prevents a mode configured for one monitor from being submitted to another monitor in `auto` mode.
- Failures return `false`, preserve a human-readable `LastError`, and log the native return code. The UI maps this to an actionable failure dialog instead of swallowing it.

## Primary Monitor Behavior

- `GetPrimaryDeviceName` uses `MONITORINFOF_PRIMARY`, which is authoritative and avoids inferring primary status solely from coordinates.
- `ResolutionSwitcher.TogglePrimary` reads the current primary independently of the configured resolution target, then selects the first active monitor with a different device name. This keeps primary switching independent of whether the resolution target is `auto` or a fixed monitor. For resolution toggles, `auto` still resolves the current primary, so the resolution target follows the new primary after a primary swap.
- `TrySetPrimaryMonitor` verifies at least two active monitors, queries the complete active CCD topology, moves the target source to `(0, 0)`, shifts every other source by the same offset, puts the target path first, and submits `SetDisplayConfig` with validation and persistence. It falls back to `CDS_UPDATEREGISTRY | CDS_SET_PRIMARY` when the CCD path fails.
- On success, `LastPrimaryShift` stores the negative of the target's original virtual-desktop position. `OverlayWindow` applies that physical-pixel offset after converting through the current WPF DPI scale, so the button remains on the same physical display.
- A previous multi-monitor `CDS_NORESET` batch-layout approach is intentionally not part of the current implementation because the target environment rejected that layout submission. Do not reintroduce it without hardware validation.

## ResolutionSwitcher State Machine

- All real `DisplayApi` operations are held behind internal delegates so tests can inject fakes without changing physical display settings.
- Resolution toggles resolve the target device first, read the live current resolution, compute a target, validate support, apply the change, and read the result again. The live current value, not a stale cached state, drives the next transition.
- An empty collection returns `NotConfigured` without calling the display API.
- A single configured item preserves the legacy current/target round trip. The first click samples the original current mode; when startup is already at the configured target, it chooses the largest supported alternative as the original when one exists.
- Two or more configured items form a circular list: current item `i` advances to item `(i + 1) mod n`; a current value outside the list converges to item 1.
- Results distinguish `Success`, `NotConfigured`, `UnsupportedResolution`, and `ApiFailed`. Primary switching separately returns `Success` or `ApiFailed`.

## API Compatibility Limits

- The original legacy path returned `-1` for both resolution and primary-monitor commits in the affected dual-monitor setup, even though mode enumeration and `CDS_TEST` preflight succeeded. That result was not evidence that the configured width/height pair was absent from the enumerated mode list.
- The primary path now uses the Windows Display Configuration API. `SetDisplayConfig` receives every active path because it exclusively enables the active paths supplied by the caller; omitting another monitor could disable it. The legacy API remains only as a compatibility fallback.
- After correcting virtual-mode index handling, an actual resolution commit was verified by reading back `3440x1440 -> 2560x1440 -> 3440x1440`. The latest session log also records successful primary-layout shifts in both directions; the automated D13-D14 coverage protects the target-selection semantics without changing physical displays.
