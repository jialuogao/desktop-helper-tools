# ResSwitcher

[简体中文](README.zh.md) | English

A lightweight Windows overlay for quickly switching display resolutions and changing the primary monitor with a draggable button.

It is designed for workflows that move frequently between high and lower resolutions, such as gaming, remote desktop, presentations, and streaming. ResSwitcher runs in the background, does not occupy the taskbar window list or Alt+Tab, and remains available from the Windows notification area.

## Features

- Two-zone overlay button: switch the primary monitor on the left and the resolution on the right
- `auto` resolution targeting follows the current primary monitor, including after a primary-monitor swap
- Independent resolution profiles for each monitor, with unsupported modes filtered at runtime
- Single-item round trips and circular multi-item resolution lists
- Windows Display Configuration API (CCD) for complete topology updates, with the legacy API retained as a compatibility fallback
- Draggable placement, position persistence, hover reveal, and configurable idle opacity
- Optional startup registration, single-instance execution, and human-readable JSON configuration
- Notification-area icon with quick access to Settings and Exit
- Built with WPF and .NET 10, with no third-party runtime dependency in the main project

## Quick Start

### Run the published application

Run the release script from the repository to create `dist\\ResSwitcher.exe`, then launch it. The overlay button appears in the top-right area of the current primary monitor, and a ResSwitcher icon appears in the Windows notification area.

The framework-dependent build requires the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). To run without an installed .NET runtime, publish a self-contained build as described below.

### Basic usage

1. Drag the button to the desired position.
2. Click the left half to swap the primary monitor with another active display.
3. Click the right half to cycle through the configured resolution list.
4. Right-click the overlay or the notification-area icon to open **Settings** or **Exit**. Left-click the notification-area icon to open Settings.
5. In Settings, choose **Auto (current primary monitor)** or a fixed monitor, then add supported resolutions for each monitor.

Settings apply immediately. The configuration file is stored at `%APPDATA%\\ResSwitcher\\config.json`; deleting it restores the defaults.

## Build and Test

Development and publishing require the .NET 10 SDK. Check the installed SDKs with `dotnet --list-sdks`.

```powershell
# Build
dotnet build -c Release

# Run the full test suite
dotnet test tests/ResSwitcher.Tests -c Release

# Build, test, and publish a win-x64 single-file application
.\\build-release.ps1
```

The release script writes the result to `dist\\ResSwitcher.exe`. A manual publish is also available:

```powershell
dotnet publish src/ResSwitcher -c Release -r win-x64 `
  -p:PublishSingleFile=true -p:SelfContained=false `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

For machines without the .NET 10 Desktop Runtime, change `-p:SelfContained=false` to `-p:SelfContained=true`. The resulting file will be larger.

The automated suite currently contains 38 tests covering the switching state machine, monitor profiles, configuration, logging, startup registration, CCD mode indices, and primary-monitor target selection. Real display-driver commits still require manual validation on the target machine.

## Technical Overview

```text
src/ResSwitcher/
|-- Core/                 Display API, switching, configuration, logging, startup
|-- Ui/                   WPF overlay and settings window
|-- AppContext.cs         Application composition root
`-- Program.cs            Single-instance entry point

tests/ResSwitcher.Tests/  Offline unit and API-boundary tests
doc/                      Behavior specification and implementation notes
```

All Win32 P/Invoke declarations are kept in `Core/DisplayApi.cs`. Display configuration uses `QueryDisplayConfig` and `SetDisplayConfig`, and resolution changes read the target monitor's live supported-mode list before submitting a request. See [`doc/PROJECT.md`](doc/PROJECT.md) for the behavior specification and [`doc/ai-implementation-notes.md`](doc/ai-implementation-notes.md) for the implementation index.

## Known Limitations

- Resolution and primary-monitor changes can be affected by the Windows session, graphics driver, display topology, and exclusive-fullscreen applications. Rejected requests show diagnostic context and are written to the session log.
- Changing the primary monitor changes Windows virtual-desktop coordinates. ResSwitcher adjusts the overlay position, but mixed-DPI monitor layouts should still be checked on the target machine.
- The release script produces a framework-dependent `win-x64` single-file application by default.

## Contributing

Issues and pull requests are welcome. Behavior changes should include focused tests. Before submitting a change, run:

```powershell
dotnet build -c Release
dotnet test tests/ResSwitcher.Tests -c Release
.\\build-release.ps1
```

The behavior specification, manual acceptance checklist, and architecture constraints are documented in [`doc/PROJECT.md`](doc/PROJECT.md).

## License

ResSwitcher is released under the MIT License. See [`LICENSE`](LICENSE).
