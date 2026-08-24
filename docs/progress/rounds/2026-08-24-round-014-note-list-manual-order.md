# 2026-08-24 Round 014: Note list manual order

## Status

completed — persistent same-group Note ordering with filtered-slot preservation, keyboard parity, schema compatibility and full regression.

## Goal

Allow the middle Note list to be reordered vertically without weakening favorite pinning, filtered-view stability, selection, virtualization, portable storage, CLI consistency or the existing Note-to-category drag path.

## Completion Contract

- Drag visible Notes before or after another Note in the same group; active favorite and non-favorite groups cannot be crossed, while deleted Notes form one recycle-bin group.
- In search or category filters, replace only the backing slots occupied by visible same-group Notes; hidden Notes retain their slots and relative order.
- Preserve selection, show a theme-colored non-layout insertion line, auto-scroll near long-list edges and expose `Alt+Up/Down` as the keyboard equivalent.
- Preserve the legacy first display after upgrading existing portable data, then persist the manual order for desktop and CLI consumers.
- Keep real `data/`, root executables, release records and frozen handoff untouched.

## Architecture Decision

Architecture drift review returned **patch**. `EntryListBox` already owns virtualized row/container behavior, so it now owns reorder hit testing, insertion adorners and edge scrolling. The new domain-level `EntryOrder` owns legacy migration, group membership, group-top insertion and filtered visible-slot replacement. `MainViewModel`, `CliApplication` and `StorageContract` reuse that policy; `MainWindow` only packages the two drag formats, routes the reorder event and supplies the keyboard command.

## Change List

- Added same-group drag-over/drop handling to `EntryListBox` with a 2 DIP theme focus insertion line and edge scrolling.
- Kept `SeerNote.EntryId` for active Note-to-category moves and introduced an independent `SeerNote.EntryOrder` format for active and deleted Note ordering.
- Added `MainViewModel.ReorderEntry`; new Note, favorite changes, soft delete and restore move to the target group top without time-based re-sorting.
- Made `EntrySearch` preserve stored order inside pinned groups and the recycle bin.
- Advanced portable storage to schema 3. Schema 1/2 loads apply the prior favorite/update/deletion-time display order once before migration; schema 3 round-trips the `entries` array unchanged.
- Updated CLI mutation paths to share the same target-group insertion policy.
- Added accessibility help for drag and `Alt+Up/Down` ordering.

## Tests And Verification

- `build.ps1 -Task Test`: all six deterministic groups passed.
- Domain coverage proves favorite pinning, recycle-bin manual order, filtered visible-slot replacement, hidden-slot stability and cross-group rejection.
- Storage coverage proves schema 1/2 compatibility and authoritative schema 3 round-trip order.
- Application coverage proves filtered reorder, selection preservation, cache invalidation, persistence and hidden-slot stability.
- CLI coverage proves create/favorite/delete/restore target-group order.
- Real WPF routed drag tests prove move effects, insertion-adorners, actual order changes and rejected favorite-boundary drops; existing category drop tests continue to pass.
- Current default and minimum rendered surfaces were inspected with no text overlap, clipping or three-column regression. No release, root executable overwrite or real runtime-data operation was performed.

## Risks And Follow-Up

- Edge auto-scroll is implemented through the list's `ScrollViewer` and covered structurally by the drag path, but a sustained physical-mouse drag over thousands of Notes remains a manual follow-up.
- Schema 1/2 files migrate in memory and are written as schema 3 on the next normal save; repeatedly opening without any change is safe and repeats the deterministic legacy ordering.
- The current source is newer than the published `1.8.0` root binaries until the user explicitly requests a release.

## Next Step

Complete. Await the next product request; do not publish or replace release evidence implicitly.
