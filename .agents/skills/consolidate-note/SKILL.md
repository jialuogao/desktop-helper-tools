---
name: consolidate-note
description: "Use when consolidating a ResSwitcher session into durable documentation, updating implementation notes, syncing docs with current C# code, or recording verified design decisions and known limitations."
allowed-tools: Read Write Edit Grep
metadata:
  version: 1.1.0
  scope: reswitcher
---

# Consolidate ResSwitcher Notes

Consolidate durable facts from the current or a recent ResSwitcher session. Record mechanisms, invariants, verified behavior, root causes, compatibility limits, and regression-test references. Do not copy the chat timeline, one-off timestamps, or intermediate hypotheses.

## Repository Documentation Layout

This repository uses `doc/` (singular), matching the shared skill convention:

- `doc/PROJECT.md` is the Chinese behavior specification, architecture contract, test matrix, manual checklist, and repository constraint reference.
- `doc/ai-implementation-notes.md` is the English agent-facing index only. Keep it limited to document routing and source-file maps.
- `doc/impl-notes/*.md` contains detailed English implementation notes grouped by subsystem. Current modules are `01-runtime-and-configuration`, `02-display-and-switching`, `03-wpf-ui`, and `04-testing-and-operations`.
- `README.md` is the Chinese user-facing setup and operation guide.

Do not create a parallel `docs/` tree. Do not use repository memory as the canonical knowledge base; durable implementation facts belong in `doc/`. Existing memory may be stale and must never override current code.

## Routing Rules

1. Read the current source and tests before recording a fact. The working tree is authoritative.
2. Use the source-file map in `doc/ai-implementation-notes.md` to select an existing module note.
3. Update an existing module note in place. Create a new numbered module only for a genuinely new subsystem, then register it in the index table, source-file map, and reading guide.
4. Keep the index free of detailed algorithms and bug narratives. Put facts in the matching module note.
5. Update `doc/PROJECT.md` when a behavior contract, test matrix, manual checklist, or architecture constraint changes. Update `README.md` only for user-visible operation or setup changes.
6. Do not alter source code during a documentation-only consolidation. If documentation disagrees with code, correct the documentation unless a separate implementation task is explicitly requested.

## Fact Format

Write standalone statements that include the mechanism, invariant, and evidence when useful:

- Good: ``ResolutionSwitcher`` reads the live resolution before every transition; `SwitcherCollectionTests` injects fake display delegates to verify the circular collection behavior without changing a physical monitor.
- Bad: The user reported a problem, then several approaches were tried, and the final attempt seemed to work.

Record unresolved behavior as a known limitation with its scope and next technical direction. For example, distinguish a successful `CDS_TEST` preflight from a successful `ChangeDisplaySettingsExW` commit; do not claim the latter from the former.

## Learning Capture

Every consolidation must include a brief learning pass: ask whether the session exposed a reusable root cause, misleading assumption, diagnostic technique, compatibility boundary, validation gap, or repository workflow improvement. Capture it when another agent could reasonably avoid the same failure or make a better decision next time.

Route the learning to the narrowest durable location:

- Implementation mechanisms, invariants, root causes, and compatibility limits go in the matching `doc/impl-notes/*.md` module note.
- User-visible behavior, test-matrix changes, manual acceptance criteria, and architecture contracts go in `doc/PROJECT.md`.
- Repository-wide agent constraints or mandatory development workflow changes go in `AGENTS.md`.
- Changes to how this consolidation skill itself should inspect, classify, or validate work go in this `SKILL.md`.
- Keep `doc/ai-implementation-notes.md` as routing metadata; do not place learning narratives there.

Before adding a learning, search the target document and merge with an existing fact instead of appending a duplicate. State the scope and evidence when useful, separate verified facts from known limitations, and omit chat chronology, transient machine details, one-off timestamps, and speculative explanations. If the lesson suggests a skill or agent-instruction change but the correct policy is not yet clear, record the concrete gap and proposed direction in the relevant implementation note rather than silently broadening repository-wide rules.

## Validation

A documentation-only consolidation does not require a build or test run. Always check local Markdown links and editor diagnostics when available, and run `git diff --check` for tracked documentation changes. If source code is changed in the same task, follow `AGENTS.md`: Release build, full xUnit suite, architecture self-check, affected M-item checks, and `build-release.ps1`.

## Maintenance

- Keep implementation notes in English for agents; keep `PROJECT.md`, `README.md`, and user/operator text in Chinese.
- Never add product-specific assumptions from another repository.
- Never record secrets, user-specific machine identifiers, or transient log timestamps as permanent knowledge.
- Keep links relative to the file containing them and remove stale claims instead of appending contradictory notes.
