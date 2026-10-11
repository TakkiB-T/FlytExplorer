# Phase 0 — technical feasibility plan

## Objective

Prove that a fast, safe Windows 11 WinUI 3 application can browse large local folders, query Everything IPC, and use Windows-native file operations. This phase must produce a working **proof-of-concept** and benchmarks, *not* a production file manager.

## 0. Environment inventory

Inspect (do not blindly install/update): Windows build, architecture, Visual Studio installation and WinUI workload, .NET SDK 10, Windows App SDK package, Windows SDK, Everything version, x64 native SDK DLLs, Git and Claude Code. Do not modify the Windows shell, file associations or Explorer preferences. Prefer packaged WinUI 3 template initially to follow supported tutorials; re-evaluate packaging later for Shell integration.

## 1. Working WinUI skeleton

Build the minimal WinUI 3 app with the chosen stable SDK, document package identity, window lifecycle and commands. Implement Nordic Dark basic tokens and minimal navigation shell; avoid spending the phase on animations/icons.

Evidence: successful `dotnet build` or Visual Studio build, process starts, screenshot if available, startup timing.

## 2. High-volume file listing spike

Enumerate a selected local test folder asynchronously; display list of basic name/type and optional metadata in virtualizing ListView. Generate test data under a dedicated repository-local `tmp/fixtures` location. Compare 1k, 10k and 100k files; detect latency to first entries and scroll pauses.

Evidence: p50/p95, memory and UI-thread work; no all-item XAML creation.

## 3. Everything IPC spike

Use official x64 Everything SDK bindings; detect running Everything, show state. Query a small name pattern, count results and request bounded pages; test metadata retrieval and error handling; verify version/instance behavior, query encoding, and sorting. Test without Everything running.

Evidence: working search UI and timings; no direct read of Everything.db; no second index.

## 4. File operations spike

Use `IFileOperation` via a dedicated STA executor; test copying and renaming test files and moving test-only items to a fixture directory. Do not permanently delete user files. Verify error/error cancel and owner-window handling. Detect operations aborted.

Evidence: tests and operation logs, no UI thread blocking or COM apartment errors.

## 5. Risk/architecture review and gate

Check architectural separation, package compatibility, leak safety, UI virtualization, and test outcomes. If a critical blocker exists, pause before Phase 1 and document alternatives (e.g., adjust Shell bridge or list control). Deliver a concise feasibility conclusion including performance baseline and next three prioritized tasks.

## Definition of Done

- One Windows 11 Release x64 build demonstrably launches.
- Local 10k items browsing doesn't freeze UI; timings reported (not invented).
- Everything search works when installed and running; unavailable state is handled.
- One safe Shell copy/rename action works using test fixtures.
- Repository contains build instructions, known limitations, benchmark data, tests and commits.
- No actual personal files or NAS data modified.
