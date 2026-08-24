---
name: review-fix-items
description: "Use when coordinating a ResSwitcher static code review and fix loop, writing review findings, validating agreed C#/.NET/WPF repairs, or re-checking a review against the current working tree."
allowed-tools: Read Write Edit Grep
metadata:
  version: 1.0.0
  scope: reswitcher
---

# ResSwitcher Review/Fix Loop

Coordinate explicit reviewer and implementer/fixer rounds. The current working tree is the only ground truth; review documents and previous summaries are snapshots that must be checked against live C# code.

## Review Artifact

- Store review documents under `.local/review/`, never under `.agents/skills/` or `doc/`.
- Review documents may be written in Chinese. Use stable finding IDs, severity, evidence with file/symbol references, impact, and a suggested fix.
- Keep the review document while findings are open, disputed, or awaiting validation. Do not delete it automatically; remove it only through an explicit cleanup request after the final review is clean.

## Reviewer Role

1. Read the requested scope, relevant tests, and the latest fixer summary if present.
2. Inspect current code once per finding. Do not repeatedly re-verify an unchanged fixed item.
3. Review C# behavior, WPF boundaries, Win32 ABI declarations, configuration persistence, error propagation, tests, and documentation consistency as applicable.
4. Do not run builds, tests, publishing, display-changing commands, registry-mutating commands, or network checks during a reviewer round.
5. Remove findings verified fixed in current code, retain unresolved findings, and append a `Reviewer Summary` with remaining and disputed IDs.

## Implementer/Fixer Role

1. Read the active findings and latest summary before editing.
2. Fix only agreed findings and append disagreements with concrete current-code evidence; do not silently change the review intent.
3. After each edit, run the narrowest available check, normally a filtered xUnit test, `dotnet build`, or editor diagnostics for the touched slice.
4. For behavior changes, complete the repository validation loop:

```powershell
dotnet build -c Release --nologo
dotnet test tests/ResSwitcher.Tests -c Release --nologo
.\build-release.ps1
```

5. Run affected manual checks from `doc/PROJECT.md` section 6. Display changes require real hardware checks such as M3 or M13; injected tests cannot prove a display-driver commit.
6. Append a `Fixer Summary` listing fixed IDs, changed files, validation commands/results, disagreements, and the next action.

## ResSwitcher Boundaries

- The main project targets `net10.0-windows`, `WinExe`, and WPF with no added runtime packages.
- All P/Invoke declarations belong in `src/ResSwitcher/Core/DisplayApi.cs`.
- `Core/` must not reference WPF or WinForms controls. UI behavior belongs in `src/ResSwitcher/Ui/`.
- `ResolutionSwitcher` display access must remain behind injectable internal delegates; `AppConfigStore` must retain its path overloads.
- The application must preserve actionable `LastError`/`SwitchResult` behavior and point failures to the current session log.
- Do not treat a passing `CDS_TEST` as proof that legacy `ChangeDisplaySettingsExW` successfully committed a resolution or primary-monitor change.

## Final State

A review is clean only when every active finding is verified fixed or explicitly accepted by the user. The final report must list item statuses, concrete evidence, validation results from fixer rounds only, and whether the review document was retained or removed by explicit request.
