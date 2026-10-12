# Phase 2 — Search

## Objective

Deliver the full Everything-backed search experience: global and folder-scoped search,
pagination for large result sets, path visibility in search results, and a clear
unavailable state. Exit criterion: query functionality verified with latency tests.

## Scope

| Feature | Priority | Notes |
|---|---|---|
| Ctrl+K global search | P0 | Focus SearchBox, scope = all indexed content |
| Ctrl+F folder-scoped search | P0 | Focus SearchBox, scope = current folder (path: prefix) |
| Scope badge toggle | P0 | Button left of SearchBox: "すべて" / "フォルダ" |
| Path column in search results | P0 | SearchEntryTemplate: icon, name, parent path, size, date |
| Pagination prev/next | P0 | Status bar buttons; page size = 200 |
| Everything unavailable state | P1 | Status text already shown; no extra UI needed |
| Search latency display | P1 | Already shown in status bar (ms) |

## Step-by-step plan

### Step 1 — Scope state + shortcuts
- Add `SearchScope` enum (`Global`, `CurrentFolder`) and `_searchScope` field.
- Add Ctrl+K (VK 0x4B) and Ctrl+F (VK 0x46) to the WH_KEYBOARD hook so they fire regardless of focus.
- `ActivateSearch(scope)`: set scope, update scope badge label, focus SearchBox, SelectAll.
- `SearchScopeBadge_Click`: toggle scope and re-run search if SearchBox has text.

### Step 2 — Path column
- Add `ParentPath` property to `FileEntryViewModel`.
- Add `SearchEntryTemplate` DataTemplate in XAML (5 columns: icon, name, path, size, date).
- Add `BuildSearchListHeader()` code-behind method (mirrors BuildListHeader with Path column).
- `EnterSearchMode()`: swap ItemTemplate + Header; show pagination panel.
- `ExitSearchMode()`: restore default template + header; hide pagination; reset offset.
- Call `EnterSearchMode` from `SearchAsync`; call `ExitSearchMode` from `NavigateToAsync`.

### Step 3 — Pagination
- Add `_searchOffset`, `_searchTotalCount`, `_searchLastText` fields.
- Status bar: add `SearchPagination` StackPanel (Prev button, page label, Next button).
- `SearchPrevButton_Click` / `SearchNextButton_Click`: update `_searchOffset`, re-run search.
- Enable/disable Prev/Next based on current offset and total count.

## Definition of Done

- Ctrl+K activates global search; Ctrl+F activates folder-scoped search.
- Scope badge reflects current mode and toggles on click.
- Search results show a parent-path column so users can identify file locations.
- Prev/next pagination works for result sets larger than the page size (200).
- Tests: latency < 500 ms for a simple query; folder-scoped search includes path: prefix;
  generation tracking prevents stale results from replacing a newer query's results.
- No personal files modified; no UI thread blocking.
