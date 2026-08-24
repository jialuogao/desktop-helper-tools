# ResSwitcher Implementation Notes

> Agent-facing index for durable implementation knowledge.
> User-facing behavior and repository constraints remain in [`doc/PROJECT.md`](PROJECT.md); usage instructions are available in [`README.md`](../README.md) and [`README.zh.md`](../README.zh.md).

## Document Index

| Document | Covers |
| --- | --- |
| [`impl-notes/01-runtime-and-configuration.md`](impl-notes/01-runtime-and-configuration.md) | Runtime composition, persistent configuration, session logging, startup, and self-start |
| [`impl-notes/02-display-and-switching.md`](impl-notes/02-display-and-switching.md) | Win32 display enumeration, resolution state machine, primary-monitor behavior, and API limitations |
| [`impl-notes/03-wpf-ui.md`](impl-notes/03-wpf-ui.md) | WPF composition, overlay interaction, DPI/position handling, and settings UI |
| [`impl-notes/04-testing-and-operations.md`](impl-notes/04-testing-and-operations.md) | Test boundaries, validation commands, release packaging, and manual verification |
| [`PROJECT.md`](PROJECT.md) | Canonical Chinese architecture and behavior specification |
| [`../README.md`](../README.md), [`../README.zh.md`](../README.zh.md) | User-facing setup and operation guides |

## Agent Workflows

| Skill | Use |
| --- | --- |
| [`../.agents/skills/consolidate-note/SKILL.md`](../.agents/skills/consolidate-note/SKILL.md) | Consolidate durable implementation facts into `doc/` |
| [`../.agents/skills/consolidate-todo/SKILL.md`](../.agents/skills/consolidate-todo/SKILL.md) | Maintain a roadmap or TODO file when one is explicitly introduced |
| [`../.agents/skills/review-fix-items/SKILL.md`](../.agents/skills/review-fix-items/SKILL.md) | Coordinate static review and C#/.NET/WPF fix rounds |
| [`../.agents/skills/update-instructions/SKILL.md`](../.agents/skills/update-instructions/SKILL.md) | Propose safe changes to root `AGENTS.md` |

## How to Read This Note

- Start with `01` when changing startup, configuration, logging, registry integration, or runtime packaging.
- Start with `02` when changing display APIs, resolution behavior, primary-monitor behavior, or error results.
- Start with `03` when changing WPF rendering, pointer interaction, window placement, or settings controls.
- Start with `04` before changing tests, validation commands, release output, or manual acceptance checks.
- Use `PROJECT.md` as the behavior contract and current repository constraint reference.

## Source Files -> Module Doc Map

| Source area | Module note |
| --- | --- |
| `src/ResSwitcher/Program.cs`, `src/ResSwitcher/AppContext.cs` | [`01-runtime-and-configuration.md`](impl-notes/01-runtime-and-configuration.md), [`03-wpf-ui.md`](impl-notes/03-wpf-ui.md) |
| `src/ResSwitcher/Core/AppConfig.cs`, `Core/Logger.cs`, `Core/AutostartManager.cs` | [`01-runtime-and-configuration.md`](impl-notes/01-runtime-and-configuration.md) |
| `src/ResSwitcher/Core/DisplayApi.cs`, `Core/ResolutionSwitcher.cs` | [`02-display-and-switching.md`](impl-notes/02-display-and-switching.md) |
| `src/ResSwitcher/Ui/OverlayWindow.cs`, `Ui/SettingsWindow.cs` | [`03-wpf-ui.md`](impl-notes/03-wpf-ui.md) |
| `tests/ResSwitcher.Tests/**`, `build-release.ps1`, `ResSwitcher.slnx` | [`04-testing-and-operations.md`](impl-notes/04-testing-and-operations.md) |

## Maintenance Boundary

These notes record durable mechanisms, invariants, compatibility limits, and verified operational facts. They do not record chat chronology or one-off debugging timestamps. When implementation changes, update the matching module note and keep this file limited to routing metadata.
