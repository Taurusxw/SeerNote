# 2026-08-24 Round 012: ClearTrash linear compaction

## Status

completed — one-pass in-place trash removal, preserved behavioral boundaries, positive measurements in every benchmark cell and full regression.

## Goal

Remove the quadratic worst case and temporary deleted-item list from `MainViewModel.ClearTrash()` while preserving the existing `List<Entry>` identity, survivor order, exact return count, selection reconciliation, cache invalidation, save scheduling, status and event semantics.

## Research

- Microsoft's [`List<T>.RemoveAll`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1.removeall?view=netframework-4.8.1) removes every matching element in O(n) time and returns the number removed.
- The previous path first materialized all deleted entries with `Where(...).ToList()`, then called `List.Remove` once per item. Each removal could search and shift the remaining tail, making dense or interleaved trash cleanup superlinear and allocating in proportion to the deleted set.
- A behavior contract was frozen before editing: `null` slots are tolerated and retained, non-deleted items keep their relative order and identity, a real deletion invalidates caches before selection reconciliation but publishes status/content only after the selection is valid, and an empty cleanup returns without version, status, cache or event churn.

## Key Decision

Use one cached `Predicate<Entry>` with `State.Entries.RemoveAll(...)`. This is an architecture-drift **patch**: the method keeps its existing owner, public contract, collaborators and notification sequence; no responsibility, interface, dependency, persistence format or UI structure changes. `docs/ARCHITECTURE.md` therefore does not need a Round 012 update.

## Change List

- Replaced deleted-item materialization plus repeated removal with one stable in-place compaction.
- Kept the original list object and used `RemoveAll`'s return value as the method result and no-op gate.
- Preserved cache invalidation before selection reconciliation, while deferring the single status notification until observers can no longer see a removed Note as selected; the content notification remains last.
- Added a focused application regression covering a mixed active/deleted/null list and an immediate second no-op clear.
- Updated the changelog, platform reference and Phase 001 review; the frozen handoff, project rules, progress snapshot and release acceptance were not read again or modified.

## Performance Verification

The ignored harness calls the real `MainViewModel.ClearTrash()` and excludes fixture construction. Each cell is the median of nine fresh fixtures; three independent processes use seeds `12012`, `12013` and `12014` with shuffled order. The table reports the median across those process medians.

| Entries / deleted | Baseline | Optimized | Time improvement | Thread allocation |
|---|---:|---:|---:|---:|
| 0 | `0.0012 ms` | `0.0001 ms` | `91.7%` | `112 → 0 B` |
| 5000 / 0% | `0.0329 ms` | `0.0112 ms` | `66.0%` | `112 → 0 B` |
| 25000 / 0% | `0.1614 ms` | `0.0709 ms` | `56.1%` | `112 → 0 B` |
| 5000 / 1% | `0.4424 ms` | `0.0843 ms` | `80.9%` | `2168 → 944 B` |
| 25000 / 1% | `9.7328 ms` | `0.3819 ms` | `96.1%` | `5288 → 944 B` |
| 5000 / 50% | `10.6012 ms` | `0.0570 ms` | `99.46%` | `66824 → 944 B` |
| 25000 / 50% | `262.9163 ms` | `0.2439 ms` | `99.91%` | `263480 → 944 B` |
| 5000 / 100% | `3.0050 ms` | `0.0336 ms` | `98.88%` | `132384 → 944 B` |
| 25000 / 100% | `66.2512 ms` | `0.1201 ms` | `99.82%` | `525648 → 944 B` |

All nine cells improve both elapsed time and allocation. The alternating 50% case exposes the old repeated-tail-shift worst path; the 0% controls show the new scan also removes the unconditional LINQ/list allocation.

## Tests And Verification

- `build.ps1 -Task Test`: all six deterministic groups passed after implementation.
- `ClearTrashCompactsInPlaceAndPreservesNoOpState` proves that the list instance is not replaced; active entries and tolerated `null` slots remain in order; only non-null deleted entries are removed; an active selection remains selected; one real clear emits exactly one content and status event with no selection event; filter/navigation snapshots are invalidated; and the unsaved, non-error, non-announcing status remains intact.
- The persistence-path clear test selects a trash Note and observes the synchronous status callback, proving subscribers see the already-reconciled null selection rather than a removed object.
- Independent review rejected the first candidate because it published status after removal but before selection reconciliation. The final deferred-status sequence and the new callback-time assertion passed rereview with no remaining blocker.
- The same test immediately clears again and proves a zero return, stable cache identities, unchanged status revision and no content, selection or status event.
- The benchmark harness and frozen baseline/optimized binaries stay under ignored `artifacts/`; they are evidence only and are not release output.
- No UI layout changed, so no new bitmap render was required. No release, release acceptance, root executable publication, commit, push, existing external-process control or runtime-data operation was performed.

## Risks And Follow-Up

- In-memory `null` tolerance is regression-tested, but valid persisted SeerNote data is expected to contain Entry objects rather than null array slots; no persistence-format change was made.
- Benchmark magnitudes are machine-specific, while the algorithmic reduction and positive direction were consistent across all independently ordered runs.
- These source changes remain absent from published `1.7.0` binaries until a later explicitly requested release.

## Next Step

Complete. Profile a different interaction before accepting another optimization; do not stack an unmeasured micro-change onto this proven compaction.
