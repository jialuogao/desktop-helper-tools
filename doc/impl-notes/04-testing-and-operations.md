# Module Notes 04 - Testing and Operations (`tests`, `build-release.ps1`)

> Parent index: [`doc/ai-implementation-notes.md`](../ai-implementation-notes.md)
> Related: [`01-runtime-and-configuration.md`](01-runtime-and-configuration.md), [`02-display-and-switching.md`](02-display-and-switching.md), [`03-wpf-ui.md`](03-wpf-ui.md)

## Test Boundaries

- The test project targets `net10.0-windows` and uses xUnit plus Microsoft.NET.Test.Sdk. The main project remains free of additional runtime packages.
- `ResolutionSwitcher` tests inject fake monitor, current-resolution, support-list, set-resolution, and set-primary delegates. These tests do not change physical display settings.
- Configuration tests use `AppConfigStore` path overloads and unique temporary directories. They cover missing/corrupt files, null nested sections, normalization, round trips, and atomic-save cleanup.
- Logger tests use the process session log. L1 verifies session filename/content; L2 verifies exception details and removal of a uniquely named stale log older than three days.
- Display geometry and `DEVMODEW` layout tests exercise real monitor enumeration and managed ABI size. Autostart tests touch the current user's `Run` key and restore its original state in `finally` blocks.
- The current suite contains 38 tests: D1-D14 switcher behavior, C1-C7 configuration, A1-A3 autostart, L1-L6 logging/display API, E1-E3 error context, three CCD virtual-mode index cases, and two CCD topology geometry tests.

## Required Validation

```powershell
dotnet build -c Release
dotnet test tests/ResSwitcher.Tests -c Release
.\\build-release.ps1
```

- Release build must finish with zero errors and zero warnings.
- The release script builds, runs the full test project, stops a running `ResSwitcher` process to avoid a file lock, and publishes a framework-dependent `win-x64` single-file executable to `dist\\ResSwitcher.exe`.
- `ResSwitcher.lnk` targets `dist\\ResSwitcher.exe`; `ResSwitcher-Build.lnk` runs the release script.
- The application needs the .NET 10 Desktop Runtime unless a self-contained publish is explicitly requested.

## Manual Acceptance

- Core tests do not cover WPF rendering, input routing, window lifetime, visual opacity, or real display-driver commits. Use the M1-M13 manual checklist in [`doc/PROJECT.md`](../PROJECT.md) after relevant UI or display changes.
- M3 and M13 are hardware-dependent. A passing mode enumeration, CCD validation, or `CDS_TEST` is not sufficient to claim that a display change was committed; verify the actual resolution or primary-monitor result on the target machine.

## Regression Rules

- A failing automated test blocks further feature work until repaired.
- A bug fix requires a regression test that reproduces the failure before the implementation fix is considered complete.
- Keep user-visible error context and the session log path intact when changing failure handling.
