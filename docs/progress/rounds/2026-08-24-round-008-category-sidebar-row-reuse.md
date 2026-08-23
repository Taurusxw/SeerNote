# 2026-08-24 Round 008: category sidebar row reuse

## Status

completed — keyed category-row reconciliation, unchanged-count write elision, bidirectional paired benchmarks, focused interaction tests and full regression.

## Goal

Remove the remaining full `CategorySidebar` rebuild when one category is dragged to a new position, while preserving exact order, selection, counts, accessible names, context-menu targets and drag/drop category identity.

## Research

- Microsoft's [`ItemsControl` documentation](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.itemscontrol?view=netframework-4.8.1) confirms that explicit `ListBoxItem` objects are themselves the item containers.
- Microsoft's [WPF control-performance guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls) notes that directly adding item containers disables UI virtualization and that repeatedly creating/destroying containers is avoidable work.
- Microsoft's [layout guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-layout-and-design) identifies runtime object construction and child-position changes as layout costs. The smallest compatible change for this existing sidebar is therefore to retain its current row containers and move only the changed row.

## Baseline

At 100 categories, `CategorySidebar.Refresh` cleared and recreated all rows whenever the ordered names differed. Nine-batch medians measured stable refresh at `0.0635–0.1113 ms`, selection-only refresh at `0.0711–0.1302 ms`, one-count refresh at `0.0627–0.1193 ms`, and a real single-category move at `20.9906–30.5536 ms`.

## Key Decisions

- Keep row lifecycle inside `CategorySidebar`; `MainWindow` receives no new reconciliation state or responsibility.
- Reuse rows by exact stored display name, remove only obsolete rows, create only genuinely new rows, and preserve surviving `ListBoxItem` references.
- Simulate both forward and backward keyed ordering and apply the direction with fewer collection moves. A normal one-category drag consequently emits one remove/insert pair in either direction instead of one reset plus 100 additions.
- Treat case-only display-name changes as real replacements so visible text, `Tag`, tooltip and captured context-menu commands cannot retain stale spelling.
- Cache each row's last count on its private count label and update its text/UIA name only when the count actually changes.
- Architecture drift: `L3-boundary-work`; the optimization deepens the existing sidebar module and does not add work to the 1747-line window composition root.

## Risk-Mapped Verification

- Identity/order risk: focused tests require the same row references after moves in both directions and exactly two collection notifications per single move.
- State-crosswiring risk: tests verify selection, count text, UIA name, `Tag`, tooltip and context-menu callback identity after reuse.
- Topology risk: tests cover case-only rename replacement plus simultaneous category removal/addition while preserving unaffected rows.
- Drop-state risk: reconciliation clears a drop target only when that row is actually removed; a moved row keeps its identity until the normal drop cleanup runs.

## Performance Verification

Same-machine direct WPF sidebar benchmark, 100 categories, nine median batches; final comparisons were run in opposite orders:

- stable refresh: `0.1113 → 0.0377 ms`; reverse pair `0.0635 → 0.0380 ms` (`40.2–66.1%` faster);
- selection-only refresh: `0.1302 → 0.0470 ms`; reverse pair `0.0711 → 0.0462 ms` (`35.0–63.9%` faster);
- one-count refresh: `0.1193 → 0.0397 ms`; reverse pair `0.0627 → 0.0408 ms` (`34.9–66.7%` faster);
- one-category move: `30.5536 → 0.0913 ms`; reverse pair `20.9906 → 0.1011 ms` (`99.5–99.7%` faster).

The existing 5000-Note/100-category real-window benchmark measured real category reorder at `21.2668 → 2.5380 ms`; reverse-order fixtures measured `28.5147 → 2.7906 ms` (`88.1–90.2%` faster). Unrelated stable-picker, editor, Note-selection and search guards remained sub-millisecond to about `1.21 ms`; process-level noise was visible on the untouched Note-selection path, so those values are not used as evidence for this sidebar optimization.

## Tests And Verification

- `build.ps1 -Task Test`: all six deterministic test groups passed.
- `structure_check.py --recent-rounds 8 --include-tests`: no architecture-drift review trigger; only the existing `MainWindow.cs` size hotspot was reported.
- The measured category-index-cache follow-up was discarded because reverse-order runs did not show a repeatable net benefit; none of its source or test changes remain.
- No release, release acceptance, root executable publication, commit, push, existing external-process control or existing runtime-data operation was performed.

## Risks And Follow-Up

- The sidebar still directly owns `ListBoxItem` containers, so first construction remains proportional to category count and does not receive WPF UI virtualization. This round targets repeated refresh/reorder cost without changing interaction architecture.
- The bidirectional greedy planner minimizes current single-category drag operations; arbitrary bulk permutations are correct and reuse rows but are not claimed to be globally move-minimal.
- Real pointer drag behavior and screen-reader speech order remain manual follow-up; automated tests cover the underlying category identity and accessibility properties.
- The source improvement is not present in the published `1.7.0` executable until a later explicitly requested release.

## Next Step

Complete. Profile first-time sidebar construction or another independently measured interaction hotspot before accepting a ninth round.
