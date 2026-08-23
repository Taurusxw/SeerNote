# 2026-08-23 Round 002: agent-friendly UI redesign

## Status

completed — implementation, automated tests, real WPF renders, visible-window interaction and `1.7.0` portable release.

## Goal

Keep SeerNote restrained and compact while making the desktop workbench clearer for human and local-agent workflows across 1080p, 1200p, 2K and 4K workspaces.

## Key Decisions

- Search owns a visible icon, scope hint and shortcut badge; navigation and categories own live counts.
- “新建” is secondary. “复制正文” remains the only visible solid primary action for an active Note.
- Save truth is persistent beside the title: local, saving, needs attention or read-only trash state.
- Favorite state is gold; dangerous actions remain semantically and spatially isolated.
- Buttons, fields, rows, scrollbars and combos share semantic WPF templates without replacing native text/IME behavior.
- Four content-width profiles deepen the existing layout calculator instead of scaling fonts by physical pixels.
- Architecture drift: `patch-with-boundary-freeze`; `MainWindow` is a repeated large composition hotspot, but this change adds only presentation composition. Future independent interaction domains must extract a tested presentation boundary.

## Change List

- Updated `ThemeResources`, `MainWindow`, `MainWindowLayoutCalculator` and `CategorySidebar`.
- Added semantic template, geometry-stability, role, count, save-state and layout assertions to theme/presentation tests.
- Updated product, architecture, release and public-community documentation.

## Tests And Verification

- All six deterministic test groups pass.
- Real WPF PNGs inspected: minimum, default, 1080p, 1200p, 2K, 4K, deleted, empty, Graphite, Midnight, Porcelain and Sage.
- Visible-window QA confirmed UI Automation labels/descriptions, `Ctrl+N`, title focus, save state and settings rendering.
- The automation helper continued reporting the search field as focused while the screenshot showed the title focus ring; it also targeted the owner window for modal Escape. These are target/caching limitations, not accepted as product failures.
- Final portable publication and exact artifact evidence are recorded in the `1.7.0` acceptance file.

## Risks And Follow-Up

- Physical mixed-DPI multi-monitor dragging and a long Chinese IME session remain useful follow-up evidence, not release blockers.
- Do not add another independent responsibility to `MainWindow` without a boundary review.

## Next Step

Complete.
