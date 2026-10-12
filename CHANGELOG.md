# Changelog

All notable changes to FlytExplorer are documented here.

## [0.12.0] — New file

### Added
- **「新規ファイル」** context menu item — creates a new empty file in the current folder.
  A dialog prompts for the filename (default: "新規ファイル.txt" with the base name pre-selected).
  Invalid characters and name collisions are validated before creation.
  The folder view refreshes automatically after the file is created.

---

## [0.11.0] — Copy path, Open terminal, Selection size

### Added
- **「パスをコピー」** context menu item — copies the full path(s) of selected items to the
  clipboard as plain text (newline-separated for multi-selection).
- **「ターミナルで開く」** context menu item — opens Windows Terminal (`wt.exe`) at the selected
  directory, or at the current folder if no directory is selected. Falls back to `cmd.exe` if
  Windows Terminal is not installed.
- **Selection total size** in the footer status bar — "5 件選択 (2.3 MB)" shows the combined
  size of all selected files. Directories without a known size are excluded from the total.

---

## [0.10.0] — Column sort

### Added
- **Click-to-sort column headers** — Name, Size, and Date modified headers are now clickable.
  Clicking a column sorts ascending (↑); clicking again reverses to descending (↓).
  Directories always sort before files. Sort state is preserved per tab.
- `SortColumn` enum and `SortAscending`/`SortColumn` state added to `TabData`.
- `ApplySort(TabData)` — in-place LINQ sort of the `ObservableCollection`; called automatically
  after each navigation and on header click.
- `FileEntryViewModel.SizeBytes` and `FileEntryViewModel.DateModified` — exposed for sorting.

---

## [0.9.3] — App icon

### Added
- `FlytExplorer.ico` — 4-resolution ICO (16/32/48/256 px). Nordic Dark background (#181F24),
  bold 'F' glyph in Nordic Accent teal (#72B8C4). Embedded via `<ApplicationIcon>` in csproj
  and set at runtime with `AppWindow.SetIcon()`.

---

## [0.9.2] — Publish package fix

### Fixed
- **`dotnet publish` crash at startup**: WinUI 3 compiled XAML (`.xbf`) and package resource
  index (`.pri`) files were not copied to the publish output, causing `Microsoft.UI.Xaml.dll`
  to throw `STATUS_FAIL_FAST_EXCEPTION` (0xc000027b) at first XAML load.
  Added `CopyWinUIResourcesOnPublish` MSBuild target to `FlytExplorer.App.csproj` that copies
  `*.xbf`, `Themes/*.xbf`, and `*.pri` from the RID build output into the publish directory
  after publish completes.

### Verified
- `dotnet publish -r win-x64 --self-contained false` → publish exe runs stably.
- Release package: `FlytExplorer-v0.9.2-win-x64.zip` (26.5 MB), contains all required runtime files.
- DPI awareness: PerMonitor (value 2) confirmed at 125% display scale.
- Drag region: `UpdateDragRect` uses `XamlRoot.RasterizationScale` — correctly converts DIP to physical pixels.

---

## [0.9.1] — Launch crash fix

### Fixed
- **Win-x64 RID exe crash on startup**: Removed `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>`
  from `FlytExplorer.App.csproj`. The win-x64 RID apphost embeds a native bootstrapper (from
  `WindowsAppSDK.Foundation 2.3.12`) that calls `MddBootstrapInitialize2` before .NET starts;
  with only `WindowsAppRuntime 2.5.x` installed this fails with `E_INVALIDARG` in `combase.dll`.
  The non-RID apphost has no embedded bootstrapper — `Bootstrap.Net.dll` handles initialization
  correctly. `<Platforms>x64</Platforms>` is retained. Release packaging uses `dotnet publish -r win-x64`.

---

## [0.9.0] — Phase 9 — Keyboard shortcut completion

### Added
- **`Ctrl+L`** — Focus address bar and select all text (`FocusAddressBar`).
- **`Alt+Left`** — Back navigation (`GoBack`).
- **`Alt+Right`** — Forward navigation (`GoForward`).
- **`Alt+Up`** — Navigate to parent folder (`GoUp`).

All four shortcuts fire via the existing `WH_KEYBOARD` thread hook regardless of focused element.
`GoBack`, `GoForward`, `GoUp` extracted from toolbar button handlers; buttons delegate to the
same helpers. `alt && !ctrl` guard prevents accidental interception of AltGr key combinations.

---

## [0.8.0] — Phase 8 — Security review and release preparation

### Fixed
- **Search scope query with spaces**: Paths containing spaces (e.g. `C:\Users\John Doe`) are now
  quoted in the Everything query (`path:"..."`) so they parse correctly. Previously the query broke
  at the first space, producing wrong results or no results.
- **Context menu in light mode**: The right-click context menu background and border now use
  `{ThemeResource MenuFlyoutPresenterBackground/BorderBrush}` instead of hardcoded dark hex values.
  The menu is now visible and correctly styled in Nordic Light mode.

### Added
- `LICENSE` — MIT licence file for GitHub public release.
- `docs/PHASE_8.md` — security review findings, release checklist, MSIX packaging guide.
- `EverythingSearchProvider.BuildQuery` — extracted as `public static` for isolated unit tests.
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
  thumbnails load on scroll with no item limit.

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
