# 2026-08-24 Round 002: filter snapshot reuse

## Status

completed — stable result caching, precise invalidation, category pre-filtering, behavior regression coverage and same-machine measurement.

## Goal

Avoid repeating the same full Note filter, sort and result allocation during selection reconciliation and the immediately following UI refresh, while preserving every search, view, category, ordering and selection rule.

## Background

`SetSearchText` calls selection reconciliation, which reads the filtered results. Its `ContentChanged` notification then causes `MainWindow` to read the identical result again. `GetFilteredEntries` rebuilt and sorted a new list on both calls, and category views sorted the wider smart-view result before discarding other categories.

With 5000 Notes on the same machine, an unchanged result read had a median cost of about `4.334 ms`; one alternating search change followed by the UI-style result read measured `5.461 ms` in the all scope and `5.106 ms` in a category scope.

## Key Decisions

- Cache a read-only result snapshot by content version, exact query, selected smart view and selected category.
- Recompute immediately when any cache key changes; existing `MarkChanged` calls cover supported Note mutations, including sticky-window notifications.
- Filter the selected category before the shared domain search performs its stable sort, preserving identical order while sorting fewer candidates.
- Move `ClearTrash` version invalidation before selection reconciliation so cached results can never retain removed entries.
- Keep the existing `EntrySearch` domain policy. Microsoft documents WPF collection views as a valid filtering/sorting layer, but migrating the shared policy into presentation would widen this measured optimization without demonstrated benefit.

## Change List

- Added a versioned filtered-result snapshot to `MainViewModel` and removed the second list copy around `EntrySearch.Filter`.
- Added category-first source narrowing without changing case-insensitive matching or sort precedence.
- Added application tests for stable snapshot identity, query and category invalidation, mutation invalidation and selection reconciliation.
- Updated product, changelog, progress and platform-reference documentation.

## Tests And Verification

- `build.ps1 -Task Test`: all six deterministic test groups passed.
- A 5000-Note same-machine benchmark used nine median batches. Search change plus refresh improved from `5.461 ms` to `3.390 ms` in the all scope (about `37.9%`) and from `5.106 ms` to `0.794 ms` in a category scope (about `84.4%`).
- Stable snapshot access measured `0.000889 ms/call` over 10,000-call batches, versus the uncached baseline of about `4.334 ms/call`; the larger loop only improves timer resolution and does not alter the result set.
- Tests prove unchanged states reuse one read-only snapshot, while query, category and Note mutation paths return fresh correct results and reconcile selection against them.
- No release, root executable publication, process termination or runtime-data operation was run.

## Risks And Follow-Up

- Cache correctness assumes supported mutations continue through `MainViewModel` or `NotifyExternalEntryChanged`; direct mutation through the public `State` object would bypass version invalidation and remains outside the UI workflow.
- The source improvement is not present in the published `1.7.0` executable until a later explicitly requested build/release step.

## Next Step

Complete.
