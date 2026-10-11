# FlytExplorer architecture and product specification (v1.0)

## 1. Product definition

A standalone, Nordic Dark, responsive Windows 11 file manager with local and SMB/UNC navigation, everyday Explorer-like commands, tabs, and a powerful search UI backed by installed Everything software. It **does not replace the Windows desktop shell** and does **not develop an original search indexer**.

### Confirmed decisions

| Area | Decision |
| --- | --- |
| Product/app ID | FlytExplorer; UI label Flyt Explorer |
| Platform | Windows 11 x64 only |
| Primary stack | C#, .NET 10 LTS, WinUI 3 / XAML, Windows App SDK 2.5.1 Stable |
| UI philosophy | Nordic Dark, restrained borders, muted sage accent, keyboard-first |
| Search | Everything SDK using local IPC; installed, running Everything needed |
| Search data | Names/paths, extension, size/date and available metadata, indexed SMB folders |
| File operations | Windows Shell `IFileOperation` (STA), with Windows-native progress/errors |
| Source license | MIT candidate; require third-party notices and name/trademark clearance before distribution |
| Delivery | First local dev version; GitHub release after compatibility/performance/security tests |

**Compatibility caveat:** An independent UI cannot promise complete parity with all Explorer-only shell extensions, virtual folders, cloud placeholders, context menus and drag/drop handlers. Track gaps explicitly, never silently claim parity.

## 2. User experience and interactions

### Primary layout

- Titlebar/tab strip (approx. 44–48 px), conventional minimize/maximize/close controls.
- Command/address toolbar (approx. 48–56 px), back/forward/up, breadcrumb/UNC path input, refresh, global search.
- Resizable left sidebar (initial width 230 px): Home, Favorites, This PC, local drives, Network (explicit pinned UNC shares).
- Main file pane: Details view first; Icons and thumbnails later. Use ListView/GridView virtualization and incremental data retrieval.
- Optional details/preview pane (Phase 4) and status/footer (item count, selection count, connection/search status).
- Clear, separate search scopes: **Ctrl+K** global indexed search; **Ctrl+F** within current folder. Disclose index coverage and offline/stale results.

### Visual tokens

| Token | Value | Application |
| --- | --- | --- |
| Background | `#232D32` | App background |
| Sidebar | `#2B363C` | Navigation |
| Surface | `#354249` | Toolbars, cards |
| Hover | `#405057` | Hover state |
| Selected | `#40564F` | Selected row |
| Accent | `#98B5AC` | Active state, focus |
| Secondary accent | `#96ADB9` | Secondary indicators |
| Primary text | `#E6ECEA` | Content |
| Muted text | `#A2B0B4` | Descriptions |
| Divider | `#45535A` | Minimal separators |

Use Segoe UI Variable (Windows system font), 4 px base spacing, 8–12 px modest corner radii, no full-pane acrylic blur. Respect Windows reduced motion, high contrast and UI Automation. Check accessibility contrast in actual WinUI theme.

### Keyboard conventions

`Ctrl+K` global search; `Ctrl+F` current-folder search; `Ctrl+L` address; `Ctrl+T` new tab; `Ctrl+W` close tab; `Ctrl+Shift+N` new folder; `Alt+Left/Right` history; `Alt+Up` parent; `F2` rename; `F5` refresh; `Delete` recycle if available; `Shift+Delete` explicit permanent deletion warning; `Ctrl+C/X/V` clipboard. Implement shortcuts only when corresponding commands really work.

## 3. Logical architecture

```text
FlytExplorer.App  (WinUI 3 XAML + MVVM)
    |
FlytExplorer.Core (navigation, state, commands, interfaces)
    |-----------------------|----------------------|
FlytExplorer.FileSystem  FlytExplorer.Search   FlytExplorer.Shell
(Local and SMB/UNC)     (Everything IPC)      (COM / STA IFileOperation)
    |-----------------------|----------------------|
FlytExplorer.Infrastructure (settings, bounded caches, telemetry opt-in/off)
```

### Suggested solution layout

```text
src/FlytExplorer.App/
src/FlytExplorer.Core/
src/FlytExplorer.FileSystem/
src/FlytExplorer.Search/
src/FlytExplorer.Shell/
src/FlytExplorer.Infrastructure/
tests/FlytExplorer.UnitTests/
tests/FlytExplorer.IntegrationTests/
tests/FlytExplorer.PerformanceTests/
benchmarks/
docs/
CLAUDE.md
FlytExplorer.sln
```

Start with a small functional skeleton; projects can be split only when boundaries are exercised. Avoid abstract factories and premature microservices.

### Required abstractions (illustrative signatures, refine after Phase 0)

```csharp
public interface IFolderProvider
{
    IAsyncEnumerable<FileEntry> EnumerateAsync(
        string path, CancellationToken cancellationToken);
}

public interface ISearchProvider
{
    Task<SearchPage> SearchAsync(SearchRequest request,
        CancellationToken cancellationToken);
    Task<SearchHealth> GetHealthAsync(CancellationToken cancellationToken);
}

public interface IFileOperationService
{
    Task<FileOperationOutcome> ExecuteAsync(FileOperationRequest request,
        CancellationToken cancellationToken);
}
```

Suggested data types: `FileEntry` (full path, kind, display name, optional size/date, attributes, origin); `SearchRequest` (text, scope, filter, sort, offset, limit, generation); `SearchPage` (items, total count if known, continuation token); `SearchHealth` (Everything process/IPC availability, supported version and index readiness). **Unvalidated:** Exact API shapes are not locked until the Phase 0 spike.

## 4. Search architecture

- `EverythingSearchProvider` talks to a locally running Everything instance through the official SDK IPC API; it does not open the Everything database directly.
- Prefer explicit UTF-16 SDK calls. Validate SDK bitness and DLL loading for x64. Bind only to documented entrypoints.
- Use query offset/max and request flags where supported to fetch bounded pages. Do not build thousands of row controls or eagerly pull a million items into ViewModels.
- Search UI: debounce around 120–200 ms as a tuning starting point; only display results belonging to the latest query generation. Cancellation prevents obsolete results from being shown; a blocking native query may still finish in a worker before cancellation takes effect.
- Escape/construct Everything search syntax deliberately. Do not concatenate arbitrary untrusted filter strings into expressions without tests. Regex optional and user-controlled.
- Query metadata only when available; handle unknown dates and sizes gracefully.
- If IPC fails: display “Everything unavailable,” show a setup/reconnect action and retain browsing. No full-disk recursive search fallback.
- Use official Everything configuration for network shares; **do not implement an extra SMB indexer**. Initial NAS indexing can be slow; off-line results may remain indexed. Revalidate existence/permission on open.
- Folder indexing exists in 1.4 and 1.5, but capabilities and settings vary by version; detect actual installed version and feature availability rather than assuming identical behavior.
- Search scope defaults to all indexed content (Ctrl+K) or current subtree (Ctrl+F); do not imply every NAS location is indexed.

## 5. File system and Windows Shell

- File browsing uses background enumeration with cancellation-aware UI update and bounded concurrency. Some remote file system APIs may block in OS calls despite cancellation; isolate such tasks so UI remains responsive and show an offline state.
- Keep UI thread free of UNC lookup, file metadata fetching, thumbnail decoding, file operations and blocking COM.
- `IFileOperation` performs copy/move/rename/delete/new-folder where appropriate, on a dedicated STA dispatcher. Respect owner HWND, cancellation, error/result HRESULT and `GetAnyOperationsAborted`.
- Do not assume SMB deletion is recyclable; distinguish “move to Recycle Bin” from permanent deletion based on actual provider behavior; demand explicit confirmation for irreversible cases.
- No silent file overwrite: use Windows conflict handling or an explicit app-level policy. Include progress and safe cancel.
- Phase 1: conventional file paths, drives and UNC. Phase 3+: Shell namespace virtual items and third-party context-menu compatibility as scoped enhancements. Do not pretend every virtual item has a filesystem path.
- Clipboard: Windows data formats / Explorer interoperability, including copy vs cut; verify drag/drop between third-party applications during integration testing.
- Avoid leaking file paths to external analytics. Telemetry is off by default; store local diagnostic logs with bounded retention and redact identifying paths in exported reports.

## 6. Feature staging

| Milestone | Features | Exit criterion |
| --- | --- | --- |
| Phase 0: spikes | WinUI template, local 10k-file view, Everything query, Shell safe copy, benchmark harness | Real Windows build/test evidence + risks |
| Phase 1: core | folders and tabs, navigation, selection, copy/move/rename/delete, keyboard commands | Dependable local file management |
| Phase 2: search | Everything provider, global/subtree search, metadata filters, paged results, unavailable states | Query functionality + latency tests |
| Phase 3: Windows/SMB | UNC browse, network errors, drag/drop, property dialogs, Shell interoperability, NAS index visibility | Integration tests under real SMB share |
| Phase 4: polish | thumbs/previews, icons, accessibility, memory/CPU profiling, UX details | Performance and UX acceptance |
| Phase 5: release | signed/distribution planning, installer, license notices, docs, security, GitHub release | Audited public beta |

Estimated time: 11–19 weeks of sustained development; uncertainty particularly high for Shell integration. This is a planning estimate, not a schedule promise.

## 7. Major risks

1. Shell extension compatibility: third-party menu handlers and virtual folders may be Explorer-specific, unstable or in-process; prefer controlled core commands first.
2. STA/COM marshaling: accidental wrong apartment affinity can hang or fail; isolate, test and dispose correctly.
3. UNC hang/unavailable shares: timeouts do not cancel all underlying Windows I/O; bound concurrency, avoid blocking UI, prevent retry storms.
4. Virtualization: a virtualizing UI alone is insufficient if ViewModel and search result sets are materialized eagerly.
5. Everything external dependency: distinct versions/instances and licensing/redistribution require explicit checks.
6. Irreversible data loss: permanent deletion, cross-volume move partial failures, reparse points and collisions need exhaustive tests.
7. Public name: “FlytExplorer” availability and trademarks require fresh clearance before release.

## 8. Stable primary references

- Windows App SDK releases: https://learn.microsoft.com/windows/apps/windows-app-sdk/release-channels
- WinUI project startup: https://learn.microsoft.com/en-us/windows/apps/get-started/start-here
- WinUI virtualization: https://learn.microsoft.com/en-us/windows/apps/develop/performance/listview-and-gridview-data-optimization
- Everything SDK: https://www.voidtools.com/en-us/support/everything/sdk/
- Everything folder indexing: https://www.voidtools.com/en-us/support/everything/folder_indexing/
- Windows IFileOperation: https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ifileoperation
- Claude project memory: https://code.claude.com/docs/en/memory
