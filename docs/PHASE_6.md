# Phase 6 — Title bar light-mode chrome, scroll thumbnails, resource fix

## Objective

Fix a runtime resource-lookup regression introduced in Phase 5, complete the
light-mode UI by theming the title bar caption buttons, and replace the 50-item
thumbnail batch loader with scroll-triggered loading via
`ListView.ContainerContentChanging`.

## Issues fixed from Phase 5

### Runtime crash: NordicXxxBrush not found in code-behind

Phase 5 moved all Nordic token brushes into `ThemeDictionaries`.
Code-behind that calls `Application.Current.Resources["NordicXxxBrush"]` does NOT
search `ThemeDictionaries` — only flat `MergedDictionaries`. This would throw
`KeyNotFoundException` at runtime when creating tabs or the properties dialog.

**Fix:** Restore `NordicDark.xaml` to the flat `MergedDictionaries` section of
App.xaml as a default fallback. `{ThemeResource}` in XAML still resolves to the
correct per-theme dictionary; code-behind gets the dark-mode fallback.

### DisplayName TextBlock had no explicit Foreground

`FileEntryTemplate` and `SearchEntryTemplate` display names inherited
`ListView.Foreground` (set from the flat dark palette in code-behind).
In light mode this rendered near-white text on near-white background.

**Fix:** Add explicit `Foreground="{ThemeResource NordicTextPrimaryBrush}"` to the
DisplayName TextBlock in both templates.

## New features

### Title bar light-mode chrome

`TitleBarButtonStyle` and `TitleBarCloseStyle` had hardcoded dark hex values
(`#2A3C48`, `#5C8896`, `#D4E2E0`).

**Fix:** Add `NordicTitleBarGlyphForeground`, `NordicTitleBarHoverBackground`,
`NordicTitleBarPressedBackground`, `NordicTitleBarHoverForeground` token brushes to
both themes; update ControlTemplate VisualState setters to `{ThemeResource}`.

| Token | Dark | Light |
|---|---|---|
| `NordicTitleBarGlyphForeground` | `#5C8896` | `#4E7A71` |
| `NordicTitleBarHoverBackground` | `#2A3C48` | `#CDD9D5` |
| `NordicTitleBarPressedBackground` | `#334856` | `#BCC8C4` |
| `NordicTitleBarHoverForeground` | `#D4E2E0` | `#1A2628` |

Close button danger-red hover (`#C85050` / `#A34040`) is kept identical in both
themes — universally understood.

### Scroll-triggered thumbnail loading (ContainerContentChanging)

Phase 5 loaded thumbnails for only the first 50 items after folder navigation.
Phase 6 replaces this with WinUI's progressive rendering pattern:
`ListView.ContainerContentChanging` fires when a virtualised container becomes
visible. Phase 0 data binding completes first; a `RegisterUpdateCallback` triggers
the async thumbnail fetch in a subsequent pass so layout is never blocked.

**No 50-item cap** — any item that scrolls into view gets its thumbnail loaded
(subject to `ShellThumbnailService.GetThumbnailAsync` returning a non-icon result).

## Definition of Done

- `dotnet build` succeeds, 0 errors, 0 warnings.
- `dotnet test`: 0 failures, ≤6 skips.
- App starts without runtime exception in both OS Dark and OS Light.
- In OS Light: sidebar, toolbar, file list, tab strip and title bar buttons
  all use the Nordic Light palette.
- Thumbnails load progressively as the user scrolls; not limited to first 50 items.

## Known limitations / deferred

- Code-behind-created controls (loading spinner, empty-folder label) use the dark
  palette fallback from flat MergedDictionaries and do not update on theme change.
  Full live theme-change support deferred to Phase 7.
- MSIX signed packaging deferred to Phase 7.
