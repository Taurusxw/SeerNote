# 2026-08-24 Round 007: category picker stability

## Status

completed — stable editor category items, exact order comparison, real-window behavior tests, reverse-order paired benchmarks and full regression.

## Goal

Stop Note selection, search and count-only changes from clearing and recreating every editor category option when the ordered category topology is unchanged, while preserving immediate category rename and reorder updates.

## Background

`RefreshCategoryPicker` previously called `Items.Clear()` and added “未分类” plus every custom category on every editor refresh. At 5000 Notes and 100 categories, a stable picker refresh measured `0.2913–0.3090 ms`; this work also appeared in real Note selection and search paths.

Microsoft's WPF guidance explains that an `ItemsControl` generates content from its `ItemCollection`, and that clearing the collection releases its item references. Rebuilding an unchanged collection therefore creates avoidable UI-thread and notification work.

## Key Decisions

- Add exact, count-independent category-order equivalence to `NavigationSnapshot`, which already owns the immutable ordered category copy.
- Keep the current picker snapshot reference in `MainWindow`; when the category order is equal, preserve `ComboBox.Items` and update only `SelectedIndex` when required.
- Rebuild once when category create, rename, delete or reorder changes the exact sequence.
- Retain case-insensitive selected-category matching while preserving case-sensitive stored display order.
- Architecture drift: `L3-boundary-work`; signals: the existing 1700-line window hotspot and seven-round phase history; action: reuse the established `NavigationSnapshot` boundary and add only mapping state to the composition root, with no new domain responsibility.

## Risk-Mapped Verification

- Stale-list risk: a real-window test renames a category and requires collection change notifications, new ordered items and the renamed selected value.
- Selection risk: switching between Notes in different categories must update the selected option without any item-collection mutation.
- Equality-policy risk: pure tests separate category-order equality from changing navigation counts and reject reordered categories.
- Changed-path regression risk: the benchmark performs a real category reorder on every measured iteration; both final paired reruns remain faster.

## Tests And Verification

- `build.ps1 -Task Test`: all six deterministic test groups passed.
- Same-machine real-window benchmark at 5000 Notes and 100 categories, nine median batches; final comparisons were run in opposite orders:
  - stable picker: `0.3090 → 0.0002 ms`, reverse pair `0.2913 → 0.0002 ms` (more than `99.9%` faster);
  - stable editor: `0.3697 → 0.0033 ms`, reverse pair `0.2463 → 0.0023 ms` (about `99.1%` faster);
  - real Note selection: `0.3520 → 0.0507 ms`, reverse pair `0.3526 → 0.0599 ms` (`83.0–85.6%` faster);
  - search switching: `1.5081 → 1.2364 ms`, reverse pair `1.4855 → 1.0961 ms` (`18.0–26.2%` faster);
  - real category reorder: `29.5417 → 28.7605 ms`, reverse pair `32.3532 → 30.9425 ms` (`2.6–4.4%` faster).
- `structure_check.py` reported only the static `MainWindow.cs` size signal; semantic review retains the existing L3 phase boundary instead of adding a parallel picker model.
- `git diff --check` passed after source and documentation updates.
- No release, root executable publication, commit, push, process control or existing runtime-data operation was performed.

## Risks And Follow-Up

- A 100-category real reorder still costs about `29–31 ms`, mostly in required sidebar/picker topology reconstruction; this round only proves it did not regress while removing stable-path work.
- Real open-dropdown pointer interaction and screen-reader speech order remain manual follow-up; existing rendered ComboBox templates and keyboard behavior are unchanged.
- The source improvement is not present in the published `1.7.0` executable until a later explicitly requested release.

## Next Step

Complete.
