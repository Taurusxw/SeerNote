# Phase 001 Review: large-collection responsiveness

## Status

completed as an unreleased source optimization phase; no publication or release acceptance performed.

## Objective

Keep SeerNote responsive as Note counts grow by removing result-count-proportional UI construction, duplicate query work and unrelated refresh work, while preserving complete data, keyboard behavior, accessibility, menus, selection and save semantics.

## Included Rounds

- [Round 001: Note list virtualization](../../rounds/2026-08-24-round-001-entry-list-virtualization.md)
- [Round 002: filter snapshot reuse](../../rounds/2026-08-24-round-002-filter-snapshot-reuse.md)
- [Round 003: selection refresh boundary](../../rounds/2026-08-24-round-003-selection-refresh-boundary.md)
- [Round 004: text edit refresh](../../rounds/2026-08-24-round-004-text-edit-refresh.md)
- [Round 005: navigation snapshot](../../rounds/2026-08-24-round-005-navigation-snapshot.md)
- [Round 006: navigation invalidation](../../rounds/2026-08-24-round-006-navigation-invalidation.md)
- [Round 007: category picker stability](../../rounds/2026-08-24-round-007-category-picker-stability.md)
- [Round 008: category sidebar row reuse](../../rounds/2026-08-24-round-008-category-sidebar-row-reuse.md)
- [Round 009: category context-menu reuse](../../rounds/2026-08-24-round-009-category-context-menu-reuse.md)
- [Round 010: category sidebar virtualization](../../rounds/2026-08-24-round-010-category-sidebar-virtualization.md)
- [Round 011: Note context-menu reuse](../../rounds/2026-08-24-round-011-entry-context-menu-reuse.md)
- [Round 012: ClearTrash linear compaction](../../rounds/2026-08-24-round-012-clear-trash-linear-compaction.md)

## Outcomes

- 3000-Note first layout: `2522.9 → 32.4 ms`, about `98.7%` faster, with all data retained and 7 realized rows.
- 5000-Note search change plus refresh: all scope about `37.9%` faster; category scope about `84.4%` faster.
- 5000-Note selection: `1.496 → 0.153 ms`, about `89.8%` faster.
- 5000-Note text edit plus deferred refresh: `3.066 → 1.038 ms`, about `66.1%` faster.
- 5000-Note/100-category unchanged navigation refresh: `1.669 → 1.220 ms`, about `26.9%` faster; changed counts remain about `6.4%` faster.
- 5000-Note/100-category navigation cache, across two paired reruns: unchanged refresh `1.2079–1.2815 → 0.0001 ms`; view switching about `66.3–69.0%` faster; search switching about `56.7–60.7%` faster; real favorite changes still about `5.6–10.7%` faster.
- 5000-Note/100-category stable category picker, across two reverse-order paired reruns: picker refresh over `99.9%` faster; editor refresh about `99.1%` faster; real Note selection about `83.0–85.6%` faster; search about `18.0–26.2%` faster; real category reorder still about `2.6–4.4%` faster.
- 100-category sidebar refresh, across two reverse-order paired reruns: stable, selection-only and one-count refreshes are `34.9–66.7%` faster; a one-category row move is `99.5–99.7%` faster, reducing real-window category reorder by `88.1–90.2%`.
- One shared category context menu reduces 100-category sidebar construction by `63.7%`, single-row add/remove by `37.8%`, and real 5000-Note/100-category window construction by `38.8–46.2%`.
- Adaptive category virtualization keeps the complete 100-item data source while replacing 100 eager `ListBoxItem` trees with 9–10 viewport containers. Across two opposite-order expanded runs, 0/1/100-category construction plus layout is `64.3–67.4%`, `49.4–56.6%` and `91.6–92.6%` faster; real 5000-Note/100-category window construction is `67.2–75.8%` faster.
- Adaptive Note-menu reuse keeps the 1–6-row direct path and shares one active/deleted command tree from row 7 onward. In two opposite-order final 5000-Note runs, 0/1/100-category window construction is neutral to `3.7%`, `3.0–6.0%` and `20.7–26.2%` faster while allocating `9.0%`, `5.7%` and `21.8%` less; 7 rows/100 categories are `34.1–42.6%` faster with `34.9%` less allocation.
- Linear `ClearTrash` compaction is positive in all nine measured size/density scenarios. The 25000-Note alternating 50%-deleted case falls from `262.9163` to `0.2439 ms` (`99.91%` faster) and from `263480` to `944 B` allocated, while the empty and 0%-deleted paths eliminate their former temporary allocation.

## Boundary Results

- `EntryListBox` owns virtualized/recycled row lifecycle, the 1–6/7+ adaptive Note-menu cache boundary and per-open direct-menu targets; `EntryContextMenu` owns shared target/category refresh. Both release their target on command or close.
- `MainViewModel` owns versioned filtered-result snapshots, the current navigation snapshot invalidation boundary, and separates pure selection notification from content/status changes.
- `MainViewModel.ClearTrash` mutates the existing Entry list through one stable linear compaction; it keeps the established save, selection, cache and notification boundaries rather than introducing a second collection owner.
- The text-edit timer owns only result dependencies.
- `NavigationSnapshot` owns navigation aggregation and content equivalence.
- `NavigationSnapshot` also owns exact category-order equivalence; the editor category picker preserves its WPF item collection until that order changes.
- `CategorySidebar` owns category selection, menu-command routing and drag/drop event orchestration; it no longer owns row-collection reconciliation or recycled visual state.
- `CategoryListBox` owns the 0/1–6/7+ adaptive boundary, stable category data items, one-notification moves, direct-row reuse and complete recycled-container prepare/clear state. Its shared menu shell is created with the first category and materializes commands only on first open.
- `MainWindow` remains the presentation composition root; these responsibilities must not be reabsorbed into it.

## Verification

- All six deterministic groups pass after the twelfth round; navigation-cache tests cover every current count/order mutation, and real-window/component tests distinguish stable items, reusable moves, true display-name replacements, adaptive threshold changes and shared-menu target routing.
- Clear-trash regression coverage proves in-place list identity, stable survivor/null ordering, exact removal count, active selection preservation, selection reconciliation before synchronous status observers, cache invalidation on a real deletion, and zero revision/event/cache churn on a second empty clear.
- Direct and shared Note-menu tests deliberately change selection after opening before invoking favorite, move, soft-delete and restore commands, including a 6→7→6 transition, proving the prepared row remains the sole target; command and close paths clear retained Entry references.
- Real WPF tests cover Note and category virtualization, recycled UIA/menu/Tooltip state, category/Note routed drops, selection/editor synchronization and delayed title/body preview refresh.
- Same-machine benchmarks use median batches and paired before/after fixtures; no failing or discarded measurement is treated as evidence.

## Residual Risk

- Real mouse-wheel/scrollbar dragging, actual Note drag-to-category, Narrator speech order, mixed-DPI multi-monitor movement and long Chinese IME sessions remain manual follow-up.
- These improvements are source-only and absent from the published `1.7.0` binaries until an explicitly requested later release.
