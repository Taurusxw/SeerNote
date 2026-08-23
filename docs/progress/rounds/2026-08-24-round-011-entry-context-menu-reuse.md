# 2026-08-24 Round 011: Note context-menu reuse

## Status

completed — adaptive shared Note menus, stable command targeting, bounded target lifetime, positive reverse-order benchmarks and full regression.

## Goal

Remove the remaining category-count-proportional context-menu construction from realized Note rows while preserving exact command order, active/trash behavior, favorite labels, category checks, keyboard/UIA targeting and the 1–6-row small-collection path.

## Research

- Microsoft's [ContextMenuOpening guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/how-to-handle-the-contextmenuopening-event) recommends adjusting an existing menu before display and documents the first-open timing hazard when a null menu is replaced too late.
- Microsoft's [`ContextMenu.PlacementTarget` contract](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.contextmenu.placementtarget?view=windowsdesktop-10.0) identifies the owning UI element used for placement. Placement alone is not a durable business-command identity, so the current container must be resolved and frozen before invocation.
- The virtualized Note viewport realizes about seven rows. Before this round, each realized active row built its own top-level commands plus every category submenu item, making startup work proportional to both realized rows and category count.

## Baseline And Prototype

An ignored baseline executable was frozen before implementation. The benchmark uses real `MainWindow` construction, explicit container preparation and menu-tree topology across 0, 1 and 100 categories, plus focused empty/1/6/7-row thresholds.

An always-shared path was not accepted for small collections. The retained boundary keeps direct menus through six rows and switches at the seventh, where sharing first provides a stable allocation reduction. One intermediate correctness repair captured `Entry` in direct-menu closures and added 192 B per six-row window; another stored an open-menu field on every list and added 8 B even to empty windows. Both were rejected. The final list-level opening/closing override freezes direct targets only while a menu is active and returns empty/1/6-row construction to baseline-identical allocation.

## Key Decisions

- Add `EntryContextMenu` as the owner of the shared active/trash command tree, category-order reconciliation, favorite label, checked category and per-open target.
- Let each `EntryListBox` cache at most one active and one deleted shared menu through `ConditionalWeakTable`; the cache cannot keep a discarded list alive.
- Override the list-level `ContextMenuOpening`/`ContextMenuClosing` boundary so direct and shared menus resolve the exact recycled container without a per-row event subscription. Opening synchronizes list/view-model selection and freezes the target; this preserves ordinary right-click behavior and covers keyboard/UI Automation opening paths.
- Direct menus keep their target only on the open menu; shared callbacks receive the prepared `Entry`. A later selection change cannot redirect copy, favorite, sticky, delete, restore or permanent-delete actions; category moves use the same target rule.
- Clear either target before invoking a command and again when the menu closes, so neither a direct row nor cached shared menu can retain the last removed Note or its body.
- Preserve separate active and trash trees so unavailable commands never leak across modes; keep direct per-row menus for one through six results.

## Change List

- Added `src/SeerNote/Presentation/EntryContextMenu.cs`.
- Extended `EntryListBox` with the adaptive menu cache and exact `ContextMenuOpening` target preparation.
- Routed shared menu actions in `MainWindow` through stable Entry-aware callbacks without adding storage, domain or external-process responsibilities.
- Added real-window tests for shared identity, labels, category checks, direct/shared selection synchronization, selection-change resistance, move, favorite, soft-delete, deleted-menu restore, command/close target release and 6→7→6 adaptation.
- Updated architecture, changelog, platform references and the Phase 001 review; the frozen handoff, project rules, progress snapshot and release acceptance were not read again or modified.

## Performance Verification

Final optimized build against the frozen baseline, two opposite process orders:

- 5000 Notes / 0 categories: `28.0949 → 27.0465 ms` (`3.7%` faster) and `23.7725 ↔ 23.7881 ms` (within `0.1%`, neutral); allocation `4,702,920 → 4,281,902 B` (`9.0%` less).
- 5000 Notes / 1 category: `53.9430 → 50.6997 ms` and `51.4840 → 49.9167 ms` (`3.0–6.0%` faster); allocation `7,480,706 → 7,052,772 B` (`5.7%` less).
- 5000 Notes / 100 categories: `116.3420 → 92.2307 ms` and `113.3003 → 83.5657 ms` (`20.7–26.2%` faster); allocation `16,133,798 → 12,615,500 B` (`21.8%` less).
- 7 Notes / 100 categories: `57.3644 → 32.9059 ms` and `52.6636 → 34.6805 ms` (`34.1–42.6%` faster); allocation `10,177,478 → 6,629,066 B` (`34.9%` less).
- Seven prepared containers now share `1` menu instead of `7`; command controls fall from `70/77/770` to `10/11/110` at 0/1/100 categories.
- Container preparation is `98.8–98.9%` faster at 0/1 categories and about `99.9%` faster at 100 categories; stable repeated layout remains `0.0001 ms`.
- Empty, one-row and six-row windows retain baseline-identical allocation. Paired six-row timing is noise-sensitive because the direct code path is intentionally unchanged; it is a no-regression guard, not a claimed speedup.

## Tests And Verification

- `build.ps1 -Task Test`: all six deterministic groups passed after the final target and allocation repair.
- Default/minimum and 3000-Note dense default/minimum WPF renders were regenerated and inspected; no clipping, scrollbar, three-column or editor-layout change was found.
- Bug-discriminating tests open a direct or shared menu for B, change selection back to A, then invoke favorite, move, soft-delete or restore; only B changes. The old selected-entry callback design fails this invariant before and after the 6→7→6 boundary.
- Command and menu-close coverage confirm both direct and cached shared menus release prepared `Entry` references.
- Final governance structure, whitespace and scoped-diff checks are recorded at closure.
- No release, release acceptance, root executable publication, commit, push, existing external-process control or existing runtime-data operation was performed.

## Risks And Follow-Up

- The separate WPF popup is not captured in the static bitmap renders. Command structure, opening route and targets are automated; physical mouse/Shift+F10 placement and Narrator speech remain manual checks.
- Permanent delete still uses its existing modal confirmation and is covered through the shared stable-target callback contract plus restore/destructive-target integration; automatically accepting the irreversible dialog was intentionally not added.
- These source changes remain absent from published `1.7.0` binaries until a later explicitly requested release.

## Next Step

Complete. Profile a different measured interaction before accepting another optimization; the highest-value read-only candidate is the quadratic `ClearTrash` removal path, which needs an isolated behavior/performance contract before any edit.
