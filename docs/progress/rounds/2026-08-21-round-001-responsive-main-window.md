# 2026-08-21 Round 001: responsive main window

## Status

completed — four resolution profiles render correctly off-screen; automated tests and the `1.5.0` root release pass.

## Goal

Adapt the SeerNote main window to 1080p, 1200p, 2560×1440, and 3840×2160 displays without overriding user-selected window bounds or duplicating Windows DPI scaling.

## Design Contract

- Keep the three-pane retrieval and editing model at every supported size.
- Use logical DIPs under the existing Per-Monitor V2 manifest; never infer text scale directly from physical pixels.
- On first launch, choose about 68% of available width and 75% of available height, bounded by `1080×720` and `1920×1280` DIPs.
- Preserve valid stored window bounds. Reflow columns whenever the actual content width changes.
- Keep `860×540` as the supported minimum and preserve the editor as the priority pane.

## Boundary Decision

Architecture drift: patch; the structure check found only the existing `MainWindow.cs` size signal. Startup and pane policy were still extracted into pure `MainWindowLayoutCalculator` so the large window class only applies layout results.

## Change List

- Added work-area-aware startup sizing and four content-driven pane profiles.
- Bound the responsive policy to the main content grid's real width, including off-screen and user-resized layouts.
- Added deterministic profile tests and WPF PNG renders for all four requested display classes.
- Updated product, architecture, development, changelog, progress, and release records.

## Verification

- `build.ps1 -Task Test` passes all five test groups.
- WPF content rendered at `1304×742`, `1304×830`, `1736×1014`, and `1920×1246`; long CJK/Latin content, controls, editor, destructive region, and status remain reachable.
- Final artifact and release-structure evidence are recorded in `../releases/v1.5.0/ACCEPTANCE.md`.

## Remaining Risk

- Physical 150%/200% multi-monitor movement, visible pointer interaction, and Chinese IME remain manual checks; Per-Monitor V2 and logical-DIP behavior are covered statically and by off-screen rendering.
