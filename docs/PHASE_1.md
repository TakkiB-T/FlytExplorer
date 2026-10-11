# Phase 1 — Core local file management

## Objective

Deliver dependable local file management: multi-select, rename, delete, copy/cut/paste, keyboard shortcuts, and basic tab support. No SMB/UNC or virtual folders yet. Exit criterion: a user can navigate and manage files without touching Explorer.

## Scope

| Feature | Priority | Notes |
|---|---|---|
| Multi-select + selection status bar | P0 | ListView CheckBox mode + "N items selected" |
| F2 inline rename | P0 | ContentDialog with TextBox, calls FileOperationService |
| Delete key → Recycle Bin | P0 | FOF_ALLOWUNDO already set |
| Shift+Delete permanent delete (with warning) | P0 | Confirmation dialog required — irreversible |
| Ctrl+C / Ctrl+X / Ctrl+V clipboard | P1 | Windows Shell clipboard format (CF_HDROP) |
| Context menu (right-click) | P1 | MenuFlyout: Open, Rename, Copy, Cut, Delete, Properties stub |
| Basic tabs (Ctrl+T / Ctrl+W) | P1 | TabView, each tab holds its own navigation state |
| New folder (Ctrl+Shift+N) | P1 | FileOperationKind.NewFolder |
| Properties stub | P2 | Simple ContentDialog: name, size, dates, attributes |
| Keyboard navigation in list (↑↓ Enter Space) | P0 | Handled by ListView; Enter = open/navigate |

## Step-by-step plan

### Step 1 — Selection + status bar
- Enable `ListView.SelectionMode = Multiple`
- Wire `SelectionChanged` → update status bar ("N items selected" vs "{count} items")
- Track selected items in `MainWindow`

### Step 2 — Rename (F2)
- Handle `KeyDown` on `FileListView`: F2 opens rename dialog
- `RenameDialog` ContentDialog: pre-fill current name, validate non-empty, no path separators
- Call `FileOperationService.ExecuteAsync(Rename, ...)`
- Refresh item in list (navigate to same folder or update ViewModel)

### Step 3 — Delete key
- Handle `Delete` key: call `FileOperationService(Delete, selectedPaths)` with `FOF_ALLOWUNDO`
- Handle `Shift+Delete`: confirmation ContentDialog, then `FileOperationService(Delete)` without undo flag

### Step 4 — Clipboard (Ctrl+C / Ctrl+X / Ctrl+V)
- Use Windows `DataPackage` / `Clipboard` API with `StorageFile` items
- Copy: `Clipboard.SetContent(DataPackage with StorageFiles)`
- Cut: same but mark as Move operation (standard CF_PREFERREDDROPEFFECT)
- Paste: read clipboard StorageFiles → `FileOperationService(Copy or Move, dest=_currentPath)`

### Step 5 — Context menu
- `MenuFlyout` on `FileListView.RightTapped`
- Items: Open (Enter), Rename (F2), Copy (Ctrl+C), Cut (Ctrl+X), Paste (Ctrl+V), Delete (Del), Separator, Properties (stub)

### Step 6 — New folder (Ctrl+Shift+N)
- Call `FileOperationService(NewFolder, dest=_currentPath, name="New Folder")`
- Navigate / refresh after creation

### Step 7 — Basic tabs (Ctrl+T / Ctrl+W)
- `TabView` control wrapping navigation content
- Each tab maintains its own `_currentPath`, `_backStack`, `_forwardStack`, `_items`
- Rename each tab to the folder name

## Definition of Done

- Multi-select works; selected count shown in status bar.
- F2 rename works for files and folders in tmp/fixtures.
- Delete (recycle) and Shift+Delete (permanent, confirmed) work without touching personal data.
- Ctrl+C/V copy works between two fixture directories.
- Context menu surfaces core commands.
- Tests cover rename, delete, and copy operations.
- No personal files modified; no UI thread blocking.
