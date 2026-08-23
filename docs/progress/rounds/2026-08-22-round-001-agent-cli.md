# 2026-08-22 Round 001: agent-friendly CLI and handoff UI

## Status

completed — implementation, tests, external CLI lifecycle, off-screen UI rendering and the `1.6.0` root release pass.

## Goal

Add a stable local CLI for agent workflows and make the WPF editor explicitly support handing a Note to an agent without weakening desktop recovery or deletion safeguards.

## Scope

- Separate `SeerNote.Cli.exe` with machine-readable discovery and Note operations.
- Shared `seernote.note.v1` payload for CLI and desktop clipboard handoff.
- Bottom-left ID/JSON handoff group and matching context-menu actions.
- Shared workspace lock, atomic storage and recycle-bin-only CLI deletion.
- Build, tests, rendering, documentation and `1.6.0` release records.

## Key Decisions

- One deep process interface, `CliApplication.Run`, owns parsing, validation, locking, storage, envelopes and exit codes.
- CLI commands are `schema/categories/list/get/create/update/delete/restore`; permanent deletion is intentionally absent.
- All data commands acquire `.seernote.lock`, including reads, because `PortableStore.Load()` can perform recovery writes.
- Desktop “复制正文” and `Ctrl+Enter` remain the human primary path. Stable ID/JSON handoff sits at bottom-left; destructive and recovery actions remain bottom-right.
- Architecture drift: patch; `MainWindow.cs` had only the size signal, so CLI responsibility was kept in `CliApplication` and payload serialization in `AgentNotePayload`.

## Change List

- Added `AgentNotePayload`, `AgentJson`, `CliApplication` and the console entry executable.
- Added structured stdout/stderr envelopes and exit codes 0–6.
- Added CLI lifecycle, validation, payload and workspace-lock tests.
- Added WPF handoff controls, context-menu actions and deleted-Note coverage.
- Extended `build.ps1` to build, test, publish and verify both EXEs.
- Updated product, architecture, development, changelog, progress and release documentation.

## Tests And Verification

- `build.ps1 -Task Test`: six groups pass, including `CliTests` and `PresentationTests`.
- External isolated CLI process: `schema → create --body-stdin → list → update → delete → list --view trash → restore` passes with `seernote.cli.v1` and `seernote.note.v1`.
- WPF PNGs rendered and inspected for minimum, default, 1080p, 1200p, 2K, 4K and deleted states; agent and destructive regions remain visible and separated.
- After the user safely exited the existing desktop instance, `build.ps1 -Task Verify` passed all six test groups, published both root EXEs, validated CLI `schema/version`, icon, fonts, zero DLL and size budgets, and printed `VERIFY_OK`.

## Risks And Follow-Up

- Visible pointer, Chinese IME, high-contrast and mixed-DPI multi-monitor checks remain manual residual evidence; no visible window was manipulated.

## Next Step

Close the round. Optional visible interaction and mixed-DPI checks remain follow-up evidence, not release blockers.
