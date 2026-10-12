# Phase 3 — Windows / SMB

## Objective

Extend FlytExplorer to work with UNC/SMB paths: graceful timeout handling for
unreachable shares, drag-and-drop interoperability with Windows Explorer, and a
sidebar Network entry that makes UNC navigation discoverable.
Exit criterion: UNC browse functional with network-error states; drag/drop verified.

## Scope

| Feature | Priority | Notes |
|---|---|---|
| UNC timeout guard | P0 | 10 s linked CTS in NavigateToAsync; UI returns immediately, OS thread may remain blocked |
| Network-specific error messages | P0 | Distinguish timeout / IO / permission denied |
| Drag inbound (Explorer → FlytExplorer) | P0 | DragOver + Drop; Copy default, Shift+drag = Move |
| Drag outbound (FlytExplorer → Explorer) | P0 | DragItemsStarting with DataProviderHandler for StorageItems |
| Sidebar "NETWORK" section | P1 | Button that focuses address bar pre-filled with `\\` |
| NAS index visibility in search | P1 | Already works via Everything if share is indexed |

## Step-by-step plan

### Step 1 — UNC timeout
- In `NavigateToAsync`: detect `path.StartsWith("\\\\")`.
- Create a 10 s `CancellationTokenSource` and link it with the navigation CTS.
- Wrap enumeration with linked token; on `OperationCanceledException` check which
  source fired to distinguish user-cancel vs. timeout.
- Show "ネットワーク接続タイムアウト" for timeout; keep existing messages for
  `UnauthorizedAccessException` and `DirectoryNotFoundException`.
- Add `IOException` catch for unreachable network path errors.

### Step 2 — Drag/drop
- In `AddTab()`: set `CanDragItems = true`, `AllowDrop = true`;
  wire `DragItemsStarting`, `DragOver`, `Drop`.
- `DragItemsStarting`: set `DataProviderHandler` with async deferral that resolves
  selected paths to `StorageFile`/`StorageFolder` WinRT objects.
- `DragOver`: accept `StorageItems`; set caption "ここにコピー".
- `Drop`: read `StorageItems`; call `FileOperationService(Copy)`;
  Shift modifier → `Move`.

### Step 3 — Sidebar Network section
- Add a "NETWORK" header and a `\\` button below "THIS PC" in the sidebar XAML.
- Clicking the button sets `AddressBox.Text = "\\\\"` and focuses it.

## Definition of Done

- Navigating to a reachable UNC path (`\\server\share`) works the same as local.
- Navigating to an unreachable UNC path shows a timeout error within 10 s without
  blocking the UI.
- Files can be dragged from Explorer and dropped into FlytExplorer (copied/moved).
- Files can be dragged from FlytExplorer and dropped into Explorer.
- Tests: non-existent local path throws; immediate-cancel UNC enumeration throws
  `OperationCanceledException`.
- No personal files or NAS data modified during tests.

## Known limitations

- Third-party Shell context menu extensions are not integrated (Phase 3+).
- No drag-between-tabs within FlytExplorer (Phase 4).
- Shell namespace virtual folders (OneDrive placeholders, recycle bin) not addressed.
- UNC background thread may remain blocked for up to 30 s after UI timeout; this
  is a Windows OS behaviour and cannot be avoided without injecting Win32 API
  overlapped I/O or custom SMB code (out of scope).
