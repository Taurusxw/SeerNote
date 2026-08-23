# 2026-08-23 Round 001: copy body bottom placement

## Status

completed — code, automated tests, off-screen rendering and the `1.6.1` root publication pass.

## Goal

Move the visible “复制正文” button from the upper Note command bar to the bottom-left handoff group shown in the user screenshot.

## Key Decisions

- Bottom order is body, ID, JSON; body copy remains the only primary-colored handoff action.
- `Ctrl+Enter`, template-variable collection, clipboard feedback and context-menu copy remain unchanged.
- Deleted Notes continue to hide body copy while exposing ID/JSON; destructive and recovery actions stay bottom-right.
- Compact padding keeps all active-Note handoff actions on one row at the `860×540` minimum.
- Architecture drift review not triggered: this moves an existing control and adds no responsibility to `MainWindow`.

## Tests And Verification

- `build.ps1 -Task Test`: all six groups pass.
- `build.ps1 -Task Verify`: all six groups, portable publication structure and the final verification pass.
- WPF renders inspected at minimum, default, 2K and deleted states.
- Minimum layout keeps all three copy actions and “移到回收站” visible without reducing the editor below its tested minimum.
- Deleted layout hides “复制正文” and keeps ID/JSON, restore and permanent delete available.
- Root `SeerNote.exe` and `SeerNote.Cli.exe` are `1.6.1.0`; the root DLL count and post-release SeerNote process count are both zero.

## Next Step

Complete. Physical mixed-DPI monitor and Chinese IME checks remain optional follow-up evidence, not release blockers.
