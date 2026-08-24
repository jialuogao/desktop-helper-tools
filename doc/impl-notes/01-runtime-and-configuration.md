# Module Notes 01 - Runtime and Configuration (`Program`, `AppContext`, `AppConfig`, `Logger`, `AutostartManager`)

> Parent index: [`doc/ai-implementation-notes.md`](../ai-implementation-notes.md)
> Related: [`02-display-and-switching.md`](02-display-and-switching.md), [`03-wpf-ui.md`](03-wpf-ui.md), [`04-testing-and-operations.md`](04-testing-and-operations.md)

## Runtime Composition

- The application targets `net10.0-windows`, uses WPF, builds as `WinExe`, and has no runtime NuGet dependency in the main project.
- `Program.Main` is STA, creates the named local mutex `Local\\ResSwitcher`, and exits immediately when another instance owns it.
- `AppContext` is the WPF `Application` composition root. It loads configuration, synchronizes autostart, constructs `ResolutionSwitcher` and `OverlayWindow`, and wires callbacks for resolution switching, primary-monitor switching, settings, exit, and position persistence.
- UI and AppDomain unhandled-exception handlers write diagnostic details through `Logger`; the UI handler also shows the session log path and keeps the dispatcher event handled where possible.

## Persistent Configuration

- The default configuration is `%APPDATA%\\ResSwitcher\\config.json`; the application never writes configuration into the installation directory.
- `AppConfigStore.Load()` returns a usable default configuration when the file is missing or malformed. `LastError` retains the load failure and the exception is written to the session log.
- `AppConfigStore.Load(string filePath)` and `Save(AppConfig, string filePath)` are required testable path overloads. Tests use temporary directories and do not touch the real application configuration.
- Loading normalizes null nested sections, filters invalid or duplicate resolution items, clamps button size to `24..128`, clamps idle opacity to `0.1..1.0`, and supplies defaults for missing values.
- `MonitorProfiles` stores an independent resolution list per display. Each profile uses a stable monitor device ID when available, with display name and `DISPLAYn` name retained for diagnostics and compatibility matching. The legacy global `Collection` remains readable; the settings UI migrates it to the selected physical display on first save.
- Saving serializes to `<config>.tmp`, then uses `File.Replace` for an existing file or `File.Move` for a new file. The temporary file is removed in a `finally` block.

## Session Logging

- `Logger` creates `%APPDATA%\\ResSwitcher\\logs` and assigns one static session ID per process. The file format is `reswitcher-YYYYMMDD-HHmmss-GUID.log`.
- `Logger.LogFile` is stable for the lifetime of the process, so error dialogs and all components can point to the same session file.
- Writes are serialized by a process-local lock. Each write attempts to delete `reswitcher-*.log` files whose UTC last-write time is older than three days.
- Logging failures are intentionally swallowed so a filesystem or permission problem cannot break the primary application flow. Error messages retain exception type, message, inner exceptions, and stack traces.
- The logger session file is also used by the tests; L1 and L2 cover session naming, exception details, and old-log cleanup.

## Autostart

- `AutostartManager` only accesses `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run` and therefore does not require elevation.
- The value name is `ResSwitcher`. Enabling writes the quoted `Environment.ProcessPath`, which is required for single-file publishing because `Assembly.Location` may be empty. Disabling removes the value; both operations are idempotent.
- Registry failures are retained in `LastError` and logged without stopping the rest of the application.
