---
name: consolidate-todo
description: "Use when maintaining a ResSwitcher roadmap or TODO file, consolidating completed implementation work, preserving unfinished requirements, or synchronizing roadmap status with verified code and tests."
allowed-tools: Read Write Edit Grep
metadata:
  version: 1.0.0
  scope: reswitcher
---

# Maintain ResSwitcher Roadmaps

Use this skill only when the user asks to maintain a roadmap or a TODO file. The current repository has no canonical `todo.md`; `PLAN.md` is a historical WinForms design document and must not be treated as the current implementation plan without explicit user direction. Do not create a new roadmap file merely because this skill was loaded.

## Before Editing

1. Read the whole target roadmap and inspect the current source, tests, and recent documentation changes.
2. Preserve user-authored unfinished requirements, constraints, examples, and continuation lines.
3. Decide whether each item is completed, awaiting acceptance, active work, an open defect, a user question, or optional work.

## ResSwitcher Status Rules

- Verified implementation belongs in `待验收` until the user accepts it; only explicit user acceptance moves it to `已完成`.
- Keep open display-driver failures and other user-reported regressions in `新缺陷` until the code and the applicable manual check are verified.
- Keep implementation details and compatibility findings in `doc/impl-notes/`; roadmap entries should link to those notes rather than becoming a second implementation document.
- Preserve section order when a user-created roadmap uses these headings: `已完成`, `待验收`, `下一步`, `问题`, `新缺陷`, and `可选优化`.
- Do not remove unfinished details because a neighboring feature is partially implemented. Do not mark hardware-dependent M3/M13 behavior complete from unit tests alone.
- Do not change source code while performing roadmap-only maintenance.

## Repository Validation

For documentation-only roadmap changes, run `git diff --check -- <roadmap-path>` and inspect the final `下一步` section against the pre-edit content. Do not run tests unless source code also changed. For behavior changes, follow `AGENTS.md` and run:

```powershell
dotnet build -c Release --nologo
dotnet test tests/ResSwitcher.Tests -c Release --nologo
.\build-release.ps1
```

Report which entries were merged or moved, which unfinished requirements were preserved, and any ambiguous completion claims left open.
