# Claude Code working instructions — FlytExplorer

## Product and scope

Build **FlytExplorer**, a standalone, Nordic Dark Windows 11 x64 file manager, in C#/.NET 10 with WinUI 3 (Windows App SDK Stable 2.5.1) and Everything official SDK IPC for search. Browser/navigation and core file operations must work independently from Everything. Do not write a custom disk indexer. Future GitHub public release, MIT candidate.

## Mandatory documents

Read `docs/ARCHITECTURE.md`, `docs/PHASE_0.md`, and `docs/PERFORMANCE_AND_TESTS.md` before implementation. Keep docs aligned with decisions. Only implement the current Phase 0 gate until asked to continue.

## Critical rules

- NEVER replace or modify Windows Explorer/system shell, Explorer registry defaults, or file associations.
- NEVER run destructive file tests against personal data, `C:\Users`, NAS shares, or any path outside dedicated disposable `tmp/fixtures`.
- Request approval before installing/upgrading system-wide tools or changing admin/network settings.
- Prefer supported documented Windows APIs. Shell `IFileOperation` requires an STA apartment and safe COM lifetime management.
- Keep disk/network IO, Shell COM work, and Everything IPC waits off the UI thread.
- Do not claim async cancellation aborts blocking OS/network calls immediately.
- Use virtualization and bounded paging. Never create one XAML control per file for huge folders or materialize millions of results.
- Do not log secrets, SMB credentials or complete private user paths by default.
- Handle permissions, offline paths, collisions, cancellation and irreversible deletion explicitly.
- Do not bundle Everything until license review; installed Everything should be runtime dependency for fast search.
- Implement only validated features; don't claim parity with Explorer extensions before testing.
- Prefer a small, understandable dependency set; pin versions and provide notices.

## Implementation behavior

1. Inventory toolchain and check repo status.
2. Present a short task plan and a file-level change list.
3. Implement the smallest vertical slice; maintain clear boundaries.
4. Build/test locally on Windows; record precise commands and actual results.
5. Report risks, incomplete tests and measured performance; never invent metrics.
6. Commit only after successful verification and user review.

## Build/test baseline

Follow the installed WinUI template rather than hand-writing incompatible project configuration. Use `dotnet --info`, `dotnet build` and `dotnet test` where applicable. Record Windows SDK/App SDK versions. Do not assume this extracted handoff bundle is a compiled project.
