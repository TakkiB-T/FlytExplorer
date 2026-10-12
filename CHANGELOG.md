# Changelog

All notable changes to FlytExplorer are documented here.

## [Unreleased] — Phase 8

### Fixed
- **Search scope query with spaces**: Paths containing spaces (e.g. `C:\Users\John Doe`) are now
  quoted in the Everything query (`path:"..."`) so they parse correctly. Previously the query broke
  at the first space, producing wrong results or no results.
- **Context menu in light mode**: The right-click context menu background and border now use
  `{ThemeResource MenuFlyoutPresenterBackground/BorderBrush}` instead of hardcoded dark hex values.
  The menu is now visible and correctly styled in Nordic Light mode.

### Added
- `LICENSE` — MIT licence file for GitHub public release.
- `CHANGELOG.md` — this file.
- `docs/PHASE_8.md` — security review findings, release checklist, MSIX packaging guide.
- `EverythingSearchProvider.BuildQuery` — extracted as `internal static` for isolated unit tests.
- 5 new unit tests covering `BuildQuery` (no Everything installation required).

---

## [0.7.0] — Phase 7 — Live theme-change support

### Fixed
- DWM border COLORREF corrected: Dark `0x00C4B872` (#72B8C4), Light `0x00717A4E` (#4E7A71).
  Previous value `0x00ACB598` was from an old palette iteration.
- Loading ring and empty-folder label `Foreground` now update immediately when the OS theme
  changes while the app is running (previously locked to dark palette until restart).
- All code-behind `Application.Current.Resources["NordicXxx"]` lookups replaced with
  `GetThemeBrush()`, which reads from the active `ThemeDictionary` entry.

### Added
- `CurrentTheme` property — reads `(Content as FrameworkElement)?.ActualTheme`.
- `GetThemeBrush(string key)` helper — searches active theme dictionary with dark fallback.
- `OnActualThemeChanged` handler — registered on root `FrameworkElement`; updates all open
  tab UI elements and refreshes DWM border colour on OS theme change.

---

## [0.6.0] — Phase 6 — Title bar light-mode chrome, scroll thumbnails

### Fixed
- Runtime crash when switching to light mode: restored `NordicDark.xaml` to flat
  `MergedDictionaries` as code-behind fallback. `{ThemeResource}` in XAML still resolves
  from the active `ThemeDictionary`.
- `DisplayName` `TextBlock` now has explicit `Foreground="{ThemeResource NordicTextPrimaryBrush}"`
  in both `FileEntryTemplate` and `SearchEntryTemplate` — was nearly invisible in light mode.
- Thumbnail loading replaced 50-item cap with `ContainerContentChanging` progressive pattern;
  thumbnails load on scroll, no limit.

### Added
- Title bar button (`NordicTitleBarXxx`) token brushes in both `NordicDark.xaml` and
  `NordicLight.xaml`; title bar chrome now correctly responds to theme changes.

---

## [0.5.0] — Phase 5 — Nordic Light theme, shell thumbnails, high-contrast

### Added
- `NordicLight.xaml` — full light palette (Background `#F2F5F4`, Accent `#4E7A71`) inspired by
  MiniFuse 2 design language; soft, warm sage-green throughout.
- OS-responsive theme switching via `ThemeDictionaries` (`Dark`, `Light`, `Default`).
- `ShellThumbnailService` — WinRT `StorageFile.GetThumbnailAsync` overlaid on glyph icons.
- High-contrast fallback in `HexColorToBrushConverter` via `AccessibilitySettings` /
  `UISettings.GetColorValue(Foreground)`.
- `README.md` build instructions, keyboard shortcuts, known limitations.
- `THIRD_PARTY_NOTICES.md` — Windows App SDK (MIT), Everything SDK (MIT), xUnit (Apache-2.0).

---

## [0.4.0] — Phase 4 — Extension-based icons, accessibility

### Added
- `FileIconMapper` — 60+ extension-to-glyph/colour mappings (Segoe MDL2 Assets).
- Accessible labels (`AutomationProperties.Name`) on interactive controls.
- `KindToForegroundConverter` for folder/file colour distinction.
- Properties dialog (name, location, size, timestamps, attributes).
- 12 icon mapping unit tests.

---

## [0.3.0] — Phase 3 — UNC/SMB, drag-and-drop, Network sidebar

### Added
- UNC path navigation (`\\server\share`) with 10 s timeout guard.
- Drag-and-drop (copy/move) between file pane and other applications.
- Network section in sidebar with "Browse network" shortcut.
- `UnauthorizedAccessException` / `DirectoryNotFoundException` / `IOException` handling for
  network paths with Japanese-language error messages.

---

## [0.2.0] — Phase 2 — Everything search, paging, latency

### Added
- `EverythingSearchProvider` — Everything SDK IPC, global and folder-scoped search.
- 120 ms debounce, search generation tracking, obsolete-result discard.
- Pagination (200 results/page, prev/next buttons, page counter).
- Search scope badge (toggle global ↔ current folder).
- Latency and paging unit tests.

---

## [0.1.0] — Phase 1 — Core navigation and file operations

### Added
- Multi-tab navigation (Ctrl+T / Ctrl+W) via `WH_KEYBOARD` thread hook.
- Back / Forward / Up / Refresh / Address bar.
- `LocalFolderProvider` — async channel-based enumeration, 250-item batched UI updates.
- Copy / Cut / Paste / Rename (F2) / Delete (Del recycle, Shift+Del permanent with confirmation) /
  New folder (Ctrl+Shift+N) via `IFileOperation` STA COM.
- Right-click context menu (Japanese labels).
- Selection status and item count in footer.
- 23 unit tests (file operations, folder provider, icon mapper).

---

## [0.0.0] — Phase 0 — Project scaffold

### Added
- WinUI 3 / Windows App SDK 2.5.1 unpackaged project template.
- Solution layout: `FlytExplorer.App`, `.Core`, `.FileSystem`, `.Search`, `.Shell`.
- `FlytExplorer.UnitTests` with xUnit + xunit.skippable.
- Nordic Dark palette (`#181F24` background, `#72B8C4` accent).
- Custom title bar (drag region, custom caption buttons, DWM border colouring).
