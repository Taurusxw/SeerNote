# 2026-08-24 Round 009: category context-menu reuse

## Status

completed — one sidebar-owned context menu, dynamic placement-target routing, focused mouse/menu tests, rendered layout inspection, reverse-order benchmarks and full regression.

## Goal

Reduce the remaining first-construction and single-row topology cost of a 100-category sidebar without changing its visual hierarchy, keyboard/pointer commands, selection, drag/drop identity or category-row model.

## Research

- Microsoft's [`ContextMenu.PlacementTarget` documentation](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.contextmenu.placementtarget?view=windowsdesktop-10.0) states that `ContextMenuService` sets the placement target to the owning element when a menu opens. A shared menu can therefore resolve the exact `ListBoxItem` at command time instead of capturing a category per row.
- Microsoft's [`ContextMenuOpening` guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/how-to-handle-the-contextmenuopening-event) documents the timing hazard of installing a previously null menu during opening. SeerNote avoids that path: the one shared menu is assigned when the first category row is constructed, before any opening request.
- The prior WPF performance research showed that these explicit `ListBoxItem` controls are not virtualized. Reusing their identical command tree is a smaller, lower-risk improvement than replacing the sidebar interaction architecture.

## Baseline

Each category row allocated its own `ContextMenu`, two `MenuItem` controls and three captured delegates. At 100 categories that meant 100 identical menus and 200 identical command controls before the user opened any menu.

Three-process median timings on the same machine were:

- 100-category sidebar construction: `16.2901 ms`;
- single category add/remove: `0.1580 ms`;
- stable, selection-only, one-count and one-row reorder guards: `0.0404`, `0.0490`, `0.0410` and `0.1070 ms`.

## Key Decisions

- Lazily create one `ContextMenu` on the first row and assign that same instance to every current and future category row.
- Resolve rename/delete targets from the menu's WPF-managed `PlacementTarget`; never route a menu command through a mutable global selection or a captured category string.
- Retain per-row right-click selection with a shared method handler rather than a closure, preserving the established behavior before the menu opens.
- Keep menu ownership, target resolution and row creation private to `CategorySidebar`; no `MainWindow`, view-model, domain or storage changes.
- Architecture drift: `patch`; the sidebar is the existing owner, this is its second consecutive focused round, and the structure check found no new trigger.

## Risk-Mapped Verification

- Wrong-target risk: tests switch `PlacementTarget` between different reused/new rows and require rename and delete events to carry the exact corresponding category.
- Pointer-selection risk: a routed right-button event must select the clicked row before menu activation.
- Reuse risk: reordered, case-replacement and newly added rows must all reference the same menu instance while keeping their own `Tag`, tooltip, count and UIA name.
- Visual risk: final default and minimum-size WPF renders were inspected; sidebar layout, selection treatment and content reachability remained unchanged.

## Performance Verification

Direct 100-category WPF benchmark, expanded iteration counts and three process-level medians:

- initial sidebar construction: `16.2901 → 5.9152 ms` (`63.7%` faster);
- single category add/remove: `0.1580 → 0.0983 ms` (`37.8%` faster);
- stable refresh: `0.0404 → 0.0379 ms` (`6.2%` faster);
- selection-only refresh: `0.0490 → 0.0488 ms` (`0.4%` faster);
- one-count refresh: `0.0410 → 0.0399 ms` (`2.7%` faster);
- one-row reorder: `0.1070 → 0.0962 ms` (`10.1%` faster).

One whole-process run showed uniform timing inflation across untouched paths; it was retained in the three-process median rather than discarded. Both opposite-order real-window pairs remained clearly positive: 5000-Note/100-category `MainWindow` construction measured `49.1450 → 30.0773 ms` and `52.7436 → 28.3499 ms` (`38.8–46.2%` faster).

## Tests And Verification

- `build.ps1 -Task Test`: all six deterministic groups passed after final source changes.
- Default `1080 × 686` and minimum `860 × 506` WPF renders were regenerated and visually inspected with no clipping, hierarchy or selection regression.
- `structure_check.py --recent-rounds 9 --include-tests`: no architecture-drift review trigger; only the existing `MainWindow.cs` and test-file size signals were reported.
- No release, release acceptance, root executable publication, commit, push, existing external-process control or existing runtime-data operation was performed.

## Risks And Follow-Up

- The menu popup itself is hosted in a separate WPF popup window and is not captured by the existing bitmap renderer. Automated tests cover placement-target routing and right-click selection; actual pointer/keyboard popup positioning and screen-reader speech remain manual follow-up.
- Initial sidebar creation remains proportional to category count because rows are explicit controls, although identical menu-tree construction is now constant.
- The source optimization is absent from the published `1.7.0` executable until a later explicitly requested release.

## Next Step

Complete. Profile the remaining initial row visual construction or a different measured user interaction before accepting another optimization.
