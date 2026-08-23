# 2026-08-23 Round 004: scoped status live regions

## Status

completed — status-source gating, WPF live-region wiring and automated semantic checks.

## Goal

Make important action results and recoverable failures available to screen-reader users without turning routine autosave transitions into repetitive speech.

## Key Decisions

- Follow Microsoft's WPF 4.8 live-region mechanism: set `AutomationProperties.LiveSetting`, update the accessible status name, then raise `LiveRegionChanged` after the text changes.
- Use `Polite` for explicit successful actions and `Assertive` for actionable failures.
- Keep “尚未保存 / 保存中 / 普通自动保存完成” visible but outside the announcement queue; input pauses must not create speech noise.
- Track status revisions so unrelated UI refreshes cannot replay an already-consumed announcement, while repeated user actions with the same text remain eligible as new feedback.

## Change List

- Added announcement eligibility and monotonically increasing status revisions to `MainViewModel`.
- Added scoped live-region properties, dynamic UIA status names and one-event-per-revision delivery to `MainWindow`.
- Added application and presentation tests for explicit actions, errors, autosave suppression and UIA priority.
- Updated product, changelog, progress and platform reference documentation.

## Tests And Verification

- `build.ps1 -Task Test`: all six deterministic test groups passed after correcting one compile-time peer base type.
- Tests confirm action feedback is eligible and `Polite`, failures are eligible and `Assertive`, and automatic save transitions remain visible but ineligible for live announcement.
- Existing default/minimum renders remain valid because this change does not alter content, geometry, colors or responsive behavior.
- No release, root executable publication, process termination or runtime-data operation was run.

## Risks And Follow-Up

- UI Automation properties, gating and event wiring are covered; actual Narrator voice order and wording still require a short manual assistive-technology session.
- The source improvement is not present in the published `1.7.0` executable until a later explicitly requested build/release step.

## Next Step

Complete.
