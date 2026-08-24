---
name: update-instructions
description: "Use when proposing changes to ResSwitcher AGENTS.md, repository-wide coding constraints, validation workflow, or agent instructions. Do not use for ordinary source or documentation edits."
allowed-tools: Read Write Edit Grep
metadata:
  version: 1.0.0
  scope: reswitcher
---

# Update ResSwitcher Instructions

Use this skill only when the user explicitly asks to update agent instructions, or when a durable repository rule has been emphasized repeatedly and should be proposed for `AGENTS.md`.

## Current Instruction Surface

- `AGENTS.md` at the repository root is the active instruction file.
- `.agents/skills/*/SKILL.md` contains on-demand workflows.
- `doc/PROJECT.md` is the behavior, architecture, test, and manual-check contract.
- `doc/ai-implementation-notes.md` and `doc/impl-notes/` contain agent-facing implementation knowledge; they are not substitutes for repository-wide rules.

Do not import commands, product assumptions, directory names, or technology rules from another repository. This project is C#/.NET 10, `net10.0-windows`, WPF, and Win32 display API based.

## Safe Proposal Workflow

1. Read the current `AGENTS.md` and the relevant implementation note or `doc/PROJECT.md` section.
2. Preserve all existing ResSwitcher constraints unless the user explicitly changes them.
3. Create a complete replacement named `AGENTS.proposed.md` at the repository root. Do not edit `AGENTS.md` directly: the active instruction file is injected into the agent context and direct edits cause unnecessary cache invalidation.
4. Validate the proposed file's Markdown/YAML-like structure and compare it with the original.
5. Tell the user that the proposal is ready and provide the manual replacement command:

```powershell
Copy-Item AGENTS.proposed.md AGENTS.md
```

Do not delete the proposed file unless the user requests cleanup.

## Rules That Must Remain Consistent

- Target `net10.0-windows`; keep the main project free of new NuGet packages.
- Keep all P/Invoke in `src/ResSwitcher/Core/DisplayApi.cs`.
- Keep UI logic in `src/ResSwitcher/Ui/`; Core must not depend on WPF/WinForms controls.
- Preserve injectable display delegates in `ResolutionSwitcher` and the path overload in `AppConfigStore`.
- Store configuration under `%APPDATA%\\ResSwitcher`; use `OutputType=WinExe` and keep the overlay out of the taskbar.
- For behavior changes, require Release build, the full `tests/ResSwitcher.Tests` suite, affected M-item manual checks, and `build-release.ps1` as specified by `AGENTS.md`.
- Keep user-facing/operator text in Chinese and agent-facing implementation notes in English.
