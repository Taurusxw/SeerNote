# 2026-08-24 Round 010: category sidebar virtualization

## Status

completed — adaptive direct/virtual category rows, extracted container boundary, behavior/render coverage, reverse-order positive benchmarks and full regression.

## Goal

Remove the remaining category-count-proportional visual construction from a 100-category sidebar without slowing empty or small collections and without changing selection, ordering, menus, drag/drop, Tooltip or UI Automation behavior.

## Research

- Microsoft's [WPF control-performance guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls) states that directly adding `ListBoxItem` containers disables UI virtualization; data-bound `ListBox` items allow containers to be deferred to the visible viewport.
- Microsoft's [ListBox scrolling guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/how-to-improve-the-scrolling-performance-of-a-listbox) recommends `VirtualizingStackPanel.VirtualizationMode=Recycling` for reuse while scrolling.
- `PrepareContainerForItemOverride` and `ClearContainerForItemOverride` define the paired WPF lifecycle used to replace or clear category identity, count, Tooltip, context menu, drop border and UIA name on recycled rows.
- `ObservableCollection.Move` raises one collection move notification, allowing a category reorder to preserve data-item identity without remove/insert churn.

## Baseline And Prototype

The Round 009 sidebar stored 100 explicit `ListBoxItem` objects. WPF only arranged viewport rows, but all 100 row grids, texts, tooltips and container objects already existed.

An isolated ignored prototype first showed that always-on virtualization made 100 categories faster but could slow 0/1 categories. That version was rejected. The retained design uses the exact visible-capacity boundary: 1–6 categories fit without scrolling and retain direct rows; 7+ use data items and recycling; 0 categories do not create an invisible list.

The accepted prototype kept all 100 data items while realizing 10 containers and produced positive three-process medians for 0/1/100 categories before production integration.

## Key Decisions

- Extract `CategoryListBox` as the owner of adaptive mode selection, category collection reconciliation, direct-row reuse, virtual data-item identity, container prepare/clear and drop-target visual state.
- Keep `CategorySidebar` as the interaction boundary for selection events, shared-menu commands and category/Note drag/drop routing. `MainWindow` receives no new state or policy.
- Use exact ordinal category identity for reuse and replacement, invariant case-insensitive matching only for requested selection, matching the existing domain contract.
- Express a virtual category reorder with `ObservableCollection.Move`; unchanged items and the selected data item retain identity in both directions.
- Keep one shared context-menu shell on all rows, but materialize its two commands only on first open. The shell is already non-null before opening, avoiding the WPF first-open timing hazard while removing command controls from startup.
- Use a specialized container override for right-click selection without per-row delegate allocations; explicitly apply the established `ListBoxItem` style.
- Architecture drift: `L3-boundary-work`; signals were the third consecutive `CategorySidebar` task and the new recycled-container responsibility. Action: extract the stable `CategoryListBox` boundary and keep the composition root unchanged.

## Change List

- Added `src/SeerNote/Presentation/CategoryListBox.cs`.
- Reduced `CategorySidebar` from 488 to 293 lines and removed its duplicate row-collection/container policy.
- Added dense-category behavior tests for virtualization settings, realized-container limits, count/selection updates, bidirectional moves, long Chinese content, scrolling/recycling, shared-menu targets, right-click selection, Note drop, category drop and 6↔7 mode transitions.
- Added real default `1080×686` and minimum `860×506` 100-category renders.
- Updated architecture, platform references, changelog and the Phase 001 review; the frozen handoff, project rules, progress snapshot and release acceptance were not read again or modified.

## Performance Verification

Expanded layout benchmark, two opposite process orders:

- 0 categories: `1.0847 → 0.3535 ms` and `1.1643 → 0.4154 ms` (`64.3–67.4%` faster);
- 1 category: `4.9315 → 2.1383 ms` and `4.0222 → 2.0357 ms` (`49.4–56.6%` faster);
- 100 categories: `206.3878 → 15.1745 ms` and `218.8652 → 18.3217 ms` (`91.6–92.6%` faster);
- explicit category container items: `100 → 0`; the optimized data source retains 100 items and realizes 9 initially / 10 after scrolling.

Focused 100-category refresh benchmark, two opposite process orders:

- construction without layout: `91.5–94.3%` faster;
- stable refresh: `45.7–48.1%` faster;
- selection-only refresh: `45.4–46.8%` faster;
- one-count refresh: `46.5–54.6%` faster;
- single-category move: `22.4–25.5%` faster;
- single-category add/remove: `46.4–52.9%` faster.

Real 5000-Note/100-category `MainWindow` construction measured `19.5697 → 6.4258 ms` and `22.5039 → 5.4536 ms`, `67.2–75.8%` faster in opposite process orders.

## Tests And Verification

- `build.ps1 -Task Test`: all six deterministic groups passed.
- Default and minimum 100-category WPF renders were generated and inspected for clipping, selection, scrollbar reachability and long Chinese row treatment.
- `structure_check.py --recent-rounds 10 --include-tests`: no new architecture-drift trigger; only the pre-existing `MainWindow` and presentation-test size signals remained.
- `git diff --check`: passed.
- No release, release acceptance, root executable publication, commit, push, existing external-process control or existing runtime-data operation was performed.

## Risks And Follow-Up

- Routed tests cover exact category/Note drop payloads, but a physical mouse drag loop and mouse-wheel/scrollbar feel remain manual checks.
- The separate WPF menu popup is not captured by bitmap rendering. First-open command materialization and placement-target routing are automated; actual keyboard popup positioning and Narrator speech remain manual.
- Mixed-DPI multi-monitor movement and high-contrast assistive-technology output remain manual follow-up.
- These changes are source-only and absent from the published `1.7.0` binaries until a later explicitly requested release.

## Next Step

Complete. Profile a different measured interaction before accepting another optimization; do not continue patching `CategorySidebar` without a new user-visible bottleneck.
