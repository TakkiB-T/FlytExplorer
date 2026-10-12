# Phase 11 — Column sort

## Objective

Add click-to-sort on the file list column headers (Name, Size, Date modified).
Sort state is per-tab and persists across navigation within the same tab.

---

## Added features

| Feature | Detail |
|---|---|
| Click column header to sort | Name ↑/↓, Size ↑/↓, Date modified ↑/↓ |
| Sort indicator in header | Active column shows ↑ (ascending) or ↓ (descending) |
| Directories-first ordering | Folders always appear before files regardless of sort column or direction |
| Per-tab sort state | Each tab remembers its own sort column and direction |
| Auto-sort on navigate | `ApplySort` called automatically after enumeration completes |

Default: Name ascending.

---

## Implementation

### `SortColumn` enum

Added to `MainWindow` private scope:

```csharp
private enum SortColumn { Name, Size, Date }
```

### Sort state in `TabData`

```csharp
public SortColumn SortColumn    { get; set; } = SortColumn.Name;
public bool       SortAscending { get; set; } = true;
```

### `BuildListHeader(TabData tab)`

Changed from `static` to instance method. Column labels are rendered as `TextBlock` with `Tapped`
event handlers. On tap:
1. Toggle direction if same column, else switch column and set ascending.
2. Call `ApplySort(tab)`.
3. Call `UpdateIndicators()` to update ↑/↓ text in-place without rebuilding the header.

### `ApplySort(TabData tab)`

```csharp
private void ApplySort(TabData tab)
{
    var dirs = tab.Items.OrderBy(x => x.Kind != FileEntryKind.Directory);
    var sorted = (tab.SortColumn, tab.SortAscending) switch
    {
        (SortColumn.Name, true)  => dirs.ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase),
        (SortColumn.Name, false) => dirs.ThenByDescending(x => x.DisplayName, StringComparer.OrdinalIgnoreCase),
        (SortColumn.Size, true)  => dirs.ThenBy(x => x.SizeBytes ?? long.MinValue),
        (SortColumn.Size, false) => dirs.ThenByDescending(x => x.SizeBytes ?? long.MinValue),
        (SortColumn.Date, true)  => dirs.ThenBy(x => x.DateModified),
        (SortColumn.Date, false) => dirs.ThenByDescending(x => x.DateModified),
        _                        => dirs.ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase),
    };
    var list = sorted.ToList();
    tab.Items.Clear();
    foreach (var vm in list) tab.Items.Add(vm);
}
```

`ApplySort` is called in `NavigateToAsync` after all items are received and before `ExitSearchMode`.
It is also called from the header `Tapped` handler.

### `FileEntryViewModel` additions

```csharp
public long?           SizeBytes    => entry.SizeBytes;
public DateTimeOffset? DateModified => entry.DateModified;
```

---

## Changed files

| File | Change |
|---|---|
| `src/FlytExplorer.App/MainWindow.xaml.cs` | `SortColumn` enum; sort fields in `TabData`; `BuildListHeader(TabData)` with `Tapped`; `ApplySort(TabData)`; `NavigateToAsync` calls `ApplySort` |
| `src/FlytExplorer.App/FileEntryViewModel.cs` | Added `SizeBytes` and `DateModified` properties |
| `CHANGELOG.md` | Added `[0.10.0]` entry |

---

## Definition of Done

- [x] Build: 0 errors, 0 warnings (Debug).
- [x] Tests: 58 passed, 1 skipped, 0 failed.
- [x] Clicking Name/Size/Date header sorts the file list with ↑/↓ indicator.
- [x] Directories appear before files in all sort modes.
- [x] Sort state is independent per tab.
