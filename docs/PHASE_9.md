# Phase 9 — Keyboard shortcut completion

## Objective

Implement the remaining keyboard shortcuts listed in `ARCHITECTURE.md` that were deferred
from earlier phases. All shortcuts fire regardless of which UI element currently has focus
because they run through the existing `WH_KEYBOARD` thread-local hook.

## Added shortcuts

| Shortcut | Action | VK code |
|---|---|---|
| `Ctrl+L` | Focus address bar and select all text | `0x4C` |
| `Alt+Left` | Back (navigate to previous folder) | `0x25` |
| `Alt+Right` | Forward (navigate to next folder in history) | `0x27` |
| `Alt+Up` | Up (navigate to parent folder) | `0x26` |

## Implementation notes

### Keyboard hook extension

`KeyboardHookProc` previously only checked `ctrl`. Phase 9 adds:

```csharp
bool alt = (GetKeyState(0x12) & 0x8000) != 0; // VK_MENU
```

The guard `ctrl && !alt` / `alt && !ctrl` ensures `Ctrl+Alt` combinations (AltGr on some
keyboards) are not accidentally intercepted.

### Navigation helper methods

`GoBack`, `GoForward`, and `GoUp` were extracted from the button click handlers so they can
be called from both the keyboard hook (via `DispatcherQueue.TryEnqueue`) and the toolbar
buttons. `FocusAddressBar` is new — it focuses `AddressBox` and selects all text so the
user can immediately type a new path.

```csharp
private void FocusAddressBar() { AddressBox.Focus(FocusState.Keyboard); AddressBox.SelectAll(); }
private void GoBack()    { if (ActiveTab.BackStack.TryPop(out var prev)) { ActiveTab.ForwardStack.Push(ActiveTab.CurrentPath); _ = NavigateToAsync(prev); } }
private void GoForward() { if (ActiveTab.ForwardStack.TryPop(out var next)) { ActiveTab.BackStack.Push(ActiveTab.CurrentPath); _ = NavigateToAsync(next); } }
private void GoUp()      { var parent = Path.GetDirectoryName(ActiveTab.CurrentPath); if (parent != null) Navigate(parent); }
```

Button click handlers become one-liners delegating to these methods.

## Complete keyboard shortcut status

| Shortcut | Action | Status |
|---|---|---|
| `Ctrl+T` | New tab | ✓ Phase 1 |
| `Ctrl+W` | Close tab | ✓ Phase 1 |
| `Ctrl+K` | Global search | ✓ Phase 2 |
| `Ctrl+F` | Folder search | ✓ Phase 2 |
| `Ctrl+Shift+N` | New folder | ✓ Phase 1 |
| `Ctrl+C` | Copy | ✓ Phase 1 |
| `Ctrl+X` | Cut | ✓ Phase 1 |
| `Ctrl+V` | Paste | ✓ Phase 1 |
| `F2` | Rename | ✓ Phase 1 |
| `F5` | Refresh | ✓ Phase 1 |
| `Delete` | Delete (recycle) | ✓ Phase 1 |
| `Shift+Delete` | Permanent delete with confirmation | ✓ Phase 1 |
| `Enter` | Open / navigate into folder | ✓ Phase 1 |
| `Ctrl+L` | Focus address bar | ✓ **Phase 9** |
| `Alt+Left` | Back | ✓ **Phase 9** |
| `Alt+Right` | Forward | ✓ **Phase 9** |
| `Alt+Up` | Up (parent) | ✓ **Phase 9** |

All keyboard shortcuts from `ARCHITECTURE.md §2` are now implemented.

## Changed files

| File | Change |
|---|---|
| `src/FlytExplorer.App/MainWindow.xaml.cs` | Add `alt` check to hook; add `FocusAddressBar`, `GoBack`, `GoForward`, `GoUp` helpers; simplify button handlers |

## Definition of Done

- `dotnet build` succeeds with 0 errors.
- `dotnet test` passes with 0 failures.
- `Ctrl+L` from the file list focuses the address bar with text selected.
- `Alt+Left` / `Alt+Right` navigate the history stack (same as toolbar buttons).
- `Alt+Up` navigates to the parent folder.
- All shortcuts fire when the file list, address bar, or search box has focus.
