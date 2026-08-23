# 2026-08-24 Round 001: Note list virtualization

## Status

completed — data-item list binding, recycled WPF containers, large-collection behavior checks and rendered inspection.

## Goal

Remove result-count-proportional control creation from the Note list while preserving the complete filtered collection, selection, context actions, drag mapping, visual hierarchy and UI Automation semantics.

## Background

`RefreshResults` previously created one complete `ListBoxItem` tree for every filtered Note. Supplying containers directly also disabled WPF UI virtualization, so first layout time grew sharply with the result count. On the same machine, median construction-and-layout time measured about `47.9 ms` for 100 Notes, `969.6 ms` for 1000, and `2522.9 ms` for 3000.

## Key Decisions

- Bind `Entry` data items and let the `ListBox` generate containers so WPF virtualization remains active.
- Use `VirtualizingStackPanel` with logical scrolling and `Recycling`; refresh every data-dependent visual and UIA field whenever a recycled container is prepared.
- Extract the container lifecycle into `EntryListBox` / `EntryListRow`. `MainWindow` retains filtered results, selection, drag routing and business actions, but no longer constructs rows.
- Architecture drift: `patch-with-boundary-freeze`; signals: `MainWindow.cs` is about 1780 lines and has been a repeated patch hotspot; action: keep the new independent list-container responsibility behind the extracted presentation boundary.
- Accept only measured positive change: retain the implementation because it materially reduces first layout time and passes behavior/render checks without changing data or user workflows.

## Change List

- Added reusable virtualized Note containers with title, preview, category/time metadata, favorite marker, context menu, tooltip and UI Automation refresh.
- Changed result refresh, selection, right-click selection and drag lookup to use `Entry` data items.
- Added a 3000-Note presentation test covering complete item retention, virtualization flags, bounded realized containers, selection identity, first/last item mapping, recycled UIA names and action menus.
- Added default and minimum-size dense-list renders and updated architecture, product, changelog, progress and reference documentation.

## Tests And Verification

- `build.ps1 -Task Test`: all six deterministic test groups passed after the final source change.
- A 3000-Note layout retained all 3000 data items while realizing 7 viewport-near containers; scrolling to the final Note refreshed its UIA name and retained the complete context menu.
- Same-machine median construction-and-layout results: 100 Notes `20.9 ms` (about 56% faster), 1000 Notes `21.0 ms` (about 97.8% faster), 3000 Notes `32.4 ms` (about 98.7% faster). Each optimized sample realized 7 rows.
- Inspected real WPF renders at `1080×686` and `860×506`: text, scrollbar, selection and three-column layout remained coherent.
- Research basis: Microsoft Learn guidance for WPF control virtualization and `ListBox` recycling.
- No release, root executable publication, process termination or runtime-data operation was run.

## Risks And Follow-Up

- Automated scrolling covers container recycling, but a real mouse-wheel/scrollbar-drag session and actual Note drag-to-category session remain manual follow-up.
- The source improvement is not present in the published `1.7.0` executable until a later explicitly requested build/release step.

## Next Step

Complete.
