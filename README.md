# FlytExplorer — Design Handoff v1.0

**Status:** Design only / not yet implemented  
**Date:** 2026-10-11  
**App:** FlytExplorer.exe (display name: Flyt Explorer)  
**Target:** Windows 11 x64 / WinUI 3 / C# / .NET 10 / Everything

## Files

- `docs/ARCHITECTURE.md` — product specification, layers, interfaces, UI, security, milestones
- `docs/PERFORMANCE_AND_TESTS.md` — baseline and acceptance tests
- `docs/PHASE_0.md` — executable Phase 0 plan, gates and risk register
- `CLAUDE.md` — persistent Claude Code project rules (this is intentionally *CLAUDE.md*, not AGENTS.md)
- `CLAUDE_CODE_START.md` — first prompt to paste into Claude Code

## How to use

1. Extract this directory under your intended Git working location.
2. Open a terminal at its root and launch Claude Code (`claude`).
3. Paste the text in `CLAUDE_CODE_START.md`, or ask Claude to read it.
4. Have Claude implement Phase 0 **only**, show build/test evidence, and commit only after your review.

**Important:** The design files are documentation. This ZIP contains no compiled application and has not been built or executed on Windows.

## Constraints

- Do not replace `explorer.exe` or change Windows shell registry settings.
- Everything is an external runtime prerequisite for fast search; it is not bundled in Phase 0.
- No independent indexing engine is planned.
- Never perform destructive tests on actual personal or NAS files.
- Initial product is a Windows 11 standalone file manager; GitHub publication is planned after QA.
