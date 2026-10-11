# FlytExplorer performance and test plan (v1.0)

## Benchmark protocol

Record Windows 11 build; CPU, RAM, storage type; Everything version and index scope; share/server/RTT; run configuration (Release x64; debugger detached). Compare native Explorer and FlytExplorer for **identical folders**, sorted the same way, cold and warm cache separately. Repeat each test >= 20 times after warm-up; report median, p95, p99 and maximum; prefer ETW / Windows Performance Recorder when needed.

## Initial engineering targets (not measured guarantees)

| Metric | Phase 0/1 provisional target |
| --- | --- |
| App warm startup, first responsive frame | p95 <= 500 ms; revisit if not feasible |
| App cold startup, first responsive frame | p95 <= 1500 ms; revisit based on package/runtime |
| Local folder (SSD, ~1k files), first visible entries | p95 <= 150 ms |
| Search (Everything warm, indexed local data) to first results | p95 <= 200 ms (including UI) |
| Input feedback | <= 50 ms in typical interactions |
| Continuous scroll | Target 60 FPS; report frame-time p95 and stalls |
| 10k+ items | No blocking full-DOM/full-XAML-item materialization |
| 1M indexed paths | Bounded request pages; no full materialization |

**Acceptance is comparative:** No material regression from Explorer for typical operations on the test hardware. The thresholds above are hypotheses. If Windows Shell or cold-start constraints make them unrealistic, report measurements and revise deliberately rather than falsifying results.

## Tests required

### Navigation and file list

- Empty folder, 100, 1k, 10k, 100k items; folder names with Japanese, emoji, long paths and unusual characters.
- Files and folders with same basename; case differences; symlink/junction/reparse point, avoid cycles.
- Files modified during enumeration; filter/sort changes while loading; rapid tab switching.
- Details vs icon view; keyboard selection and multi-select; viewport virtualization.

### Search

- Everything running / not running / restarting during request; not-yet-loaded database; unsupported features.
- Name, extension, size, date, path scope, regex only if supported.
- Repeated fast input: stale results never replace later query results.
- Exact match, zero result, offset pagination, 100k+ matches, network indexed results.
- Duplicate paths and stale/offline result behavior.

### File operations

- Copy/move/rename/new folder in temporary fixtures; safe permission handling; conflict name exists.
- Cancel in-progress transfers, disk full, locked file, disconnected destination, invalid filename.
- Delete/trash with explicit user confirmation; share with no server-side recycle support.
- Multi-select operations, rename only extension vs basename, cut/paste across drives.
- No live user file, real NAS contents or source-code files may be modified by tests.

### SMB/UNC

- Working path (`\\server\share`), credential prompt from OS, slow share, offline, permission denied.
- Disconnect network during enumeration and during copy; retry and recovery without UI freeze.
- Network share not indexed by Everything; indexed but stale; no surprise recursive scan.

### Accessibility and stability

- DPI 100/125/150/200%, window resize, 1080p and ultra-wide, reduced motion and high contrast.
- Keyboard-only flows, UI Automation names, focus visibility, screen-reader basics.
- Memory use after repeated navigation; COM reference leak and handle leak; background activity at idle.

## Deliverables each milestone

- `benchmarks/results/<date>/baseline.csv` (where raw benchmark data resides)
- Before/after medians and percentiles with tested machine recorded
- Test result: PASS/FAIL/SKIPPED with reason, no unsupported green claims
- Known limitations, risks, affected modules and issue backlog
