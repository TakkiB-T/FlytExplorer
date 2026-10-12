# Phase 5 — Nordic Light theme, shell thumbnails, high-contrast, release docs

## Objective

Deliver a soft Nordic Light theme that matches Nordic Dark in feel,
shell thumbnail preview in file lists, high-contrast accessibility fallback,
and public-release documentation (README, third-party notices).

Exit criterion: both themes compile and display correctly; thumbnails appear for
image/video/document files; high-contrast check in HexColorToBrushConverter falls
back to system foreground; README and notices are complete.

## Scope

| Feature | Priority | Notes |
|---|---|---|
| Nordic Light theme (`NordicLight.xaml`) | P0 | Soft off-white matching MiniFuse 2 white model aesthetic |
| System-responsive theme switching | P0 | WinUI ThemeDictionaries; follows OS Light/Dark setting |
| `StaticResource` → `ThemeResource` in XAML | P0 | Required for ThemeDictionaries to take effect |
| `ShellThumbnailService` (App project) | P1 | WinRT `StorageFile.GetThumbnailAsync` on UI async; icon fallback |
| `ThumbnailBitmap` in `FileEntryViewModel` | P1 | INotifyPropertyChanged; thumbnail overlays glyph icon |
| High-contrast fallback in `HexColorToBrushConverter` | P1 | `UISettings.GetColorValue(Foreground)` when HighContrast |
| `README.md` | P1 | Build instructions, requirements, license, known limitations |
| `THIRD_PARTY_NOTICES.md` | P1 | Windows App SDK, Everything SDK, xUnit attribution |

## Nordic Light palette (MiniFuse white inspiration)

| Token | Value | Notes |
|---|---|---|
| Background | `#F2F5F4` | Off-white with cool-green tint; not pure white |
| Sidebar | `#E6EDE9` | Slightly deeper than background |
| Surface | `#DCE6E2` | Toolbar, cards |
| Hover | `#CDD9D5` | Hover state |
| Selected | `#C2D5CF` | Sage-green selection — same hue as dark mode |
| Accent | `#4E7A71` | Same hue as dark `#72B8C4`, darkened for light contrast |
| Secondary accent | `#4E6E7A` | |
| Primary text | `#1A2628` | Dark text on light |
| Muted text | `#5A6E74` | |
| Divider | `#BCC8C4` | |

The key design principle: both modes share the same muted sage/teal accent hue and
avoid pure black/white surfaces — just like the MiniFuse 2 black and white models
share the same blue LED accent language.

## Thumbnail approach

`ShellThumbnailService` (in `FlytExplorer.App`) uses WinRT
`StorageFile/StorageFolder.GetThumbnailAsync(ThumbnailMode.SingleItem, 32)`.
Called asynchronously from the UI-thread dispatcher after folder load; limited to
first 50 items to avoid redundant I/O for large folders.
Falls back silently to glyph icon when thumbnail is unavailable or returns `ThumbnailType.Icon`.

## Theme switching mechanism

App.xaml `ThemeDictionaries`:
- `Dark` / `Default` → merge `NordicDark.xaml`; TabView/MenuFlyout dark brushes
- `Light`            → merge `NordicLight.xaml`; TabView/MenuFlyout light brushes

`MainWindow.xaml` changes all 43 `{StaticResource NordicXxxBrush}` references to
`{ThemeResource NordicXxxBrush}` so brushes resolve from the active theme dictionary.

No code is needed in App.xaml.cs — WinUI 3 resolves `ThemeResource` keys at render
time based on `FrameworkElement.ActualTheme`, which follows the OS setting by default.

## Definition of Done

- `dotnet build` succeeds (no errors, zero warnings on Nordic brush keys).
- `dotnet test`: 0 failures, ≤6 skips (same as Phase 4 baseline of 54 total).
- App starts in both OS Dark and OS Light and applies correct palette.
- Thumbnail column loads images for image/PDF files; glyph shown for directories and
  unsupported types.
- `HexColorToBrushConverter` returns system foreground color when
  `AccessibilitySettings.HighContrast` is true.
- `README.md` covers: requirements, build, run, known limitations, license.
- `THIRD_PARTY_NOTICES.md` lists all dependencies with SPDX identifiers.

## Known limitations / deferred

- Title bar caption buttons (min/max/close) retain Nordic Dark chrome in light mode —
  acceptable for Phase 5; correct in Phase 6.
- Thumbnail loading is limited to first 50 items; scroll-triggered loading deferred.
- No signed packaging; MSIX/store signing deferred to Phase 6.
