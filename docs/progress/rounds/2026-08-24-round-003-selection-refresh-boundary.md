# 2026-08-24 Round 003: selection refresh boundary

## Status

completed — narrow selection notification, targeted editor/status refresh, cross-module behavior checks and real-window measurement.

## Goal

Make high-frequency Note selection update only the UI that depends on the selected identity, without rescanning unrelated navigation/category data or refreshing the result collection and editor twice.

## Background

`MainViewModel.SelectEntry` previously raised the general `ContentChanged` event. `MainWindow` handled that event with `RefreshAll`, which scanned all Notes for smart-view and category counts, refreshed the result list, editor and status; the list selection handler then called `RefreshEditor` a second time. A 5000-Note real WPF window measured a median `1.496 ms` for one alternating selection change.

## Key Decisions

- Add `SelectedEntryChanged` as the smallest stable interface for pure selection identity changes.
- Keep mutations, filtering and scope changes on `ContentChanged`; keep save/action feedback on `StatusChanged`.
- In the selection handler, synchronize a programmatic list selection when necessary, then refresh only the editor and selected-document state under the existing reentrancy guard.
- Subscribe and unsubscribe the new event with the same `MainWindow` lifetime as the existing view-model events.
- Architecture drift: `patch-with-boundary-freeze`; signals: `MainWindow.cs` remains above 1700 lines and is a repeated presentation hotspot; action: add no new domain responsibility and route selection through a narrow view-model event boundary.

## Change List

- Added `SelectedEntryChanged` to `MainViewModel`; repeated selection remains a no-op.
- Replaced selection-triggered `RefreshAll` plus duplicate `RefreshEditor` with one targeted selection refresh in `MainWindow`.
- Added application tests that distinguish selection notifications from content notifications.
- Extended the 3000-Note presentation test to prove selection identity, title, body and `ItemsSource` stay synchronized.
- Updated architecture, product, changelog, progress and WPF reference documentation.

## Tests And Verification

- `build.ps1 -Task Test`: all six deterministic test groups passed after the final test addition.
- The same 5000-Note real-window benchmark used nine median batches of 100 alternating selections: `1.496 ms/change` before and `0.153 ms/change` after, about `89.8%` faster.
- Presentation coverage proves the narrow path updates both editor fields and selected identity while preserving the virtualized result source.
- Microsoft WPF guidance describes layout as recursive and recommends reducing unnecessary layout invocations; the targeted event path avoids unrelated property and collection refreshes.
- `structure_check.py` reported only the static `MainWindow.cs` size signal; semantic history supplied the repeated-hotspot signal and the boundary-freeze decision.
- No release, root executable publication, process termination or runtime-data operation was run.

## Risks And Follow-Up

- Production selection currently originates from the result list. Programmatic callers are synchronized by the new handler, but future selection-dependent UI must subscribe to the narrow event rather than restoring a general content refresh.
- The source improvement is not present in the published `1.7.0` executable until a later explicitly requested build/release step.

## Next Step

Complete.
