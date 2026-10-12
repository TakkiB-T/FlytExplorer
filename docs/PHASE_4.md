# Phase 4 — Icons, Accessibility, UX polish

## Objective

Add extension-based file type icons with color differentiation, accessibility metadata
for assistive technologies, and search pagination accessibility.
Exit criterion: all extensions have distinct visual cues; key controls readable by
screen reader; tests pass.

## Scope

| Feature | Priority | Notes |
|---|---|---|
| Extension-based icon glyphs | P0 | Segoe MDL2 Assets glyph + hex color per category |
| `FileIconMapper` in Core | P0 | Testable static lookup; ViewModel delegates to it |
| `HexColorToBrushConverter` | P0 | Converts `#RRGGBB` string → `SolidColorBrush` in XAML |
| `AutomationProperties.Name` on toolbar buttons | P0 | Back/Forward/Up/Refresh/Min/Max/Close |
| `AutomationProperties.Name` on TextBoxes | P0 | AddressBox, SearchBox, SearchScopeBadge |
| `AutomationProperties.Name` on TabView + ListView | P1 | Container-level names for screen readers |

## Implementation

### Icon categories

| Category | Extensions | Glyph (MDL2) | Color |
|---|---|---|---|
| Folder | (directory) | ED41 FolderFill | #98B5AC NordicAccent |
| Image | .png .jpg .jpeg .gif .bmp .webp .ico .svg .tiff .heic | EB9F Photo | #9B8EC4 soft purple |
| Audio | .mp3 .wav .flac .ogg .aac .m4a .wma | E8D6 MusicNote | #C4778E rose |
| Video | .mp4 .avi .mkv .mov .wmv .webm .flv .m4v | E8A5 Document | #C47878 soft red |
| Archive | .zip .rar .7z .tar .gz .bz2 .cab .xz | E8B1 ZipFolder | #C4A06E amber |
| Code | .cs .py .js .ts .html .css .json .xml .xaml .yaml .sh .ps1 .bat .cpp .c .h .java .go .rs .rb .php | E8A5 Document | #7EB5C4 cyan |
| Word | .docx .doc .odt .rtf | E8A5 Document | #7EA8C4 steel blue |
| Spreadsheet | .xlsx .xls .csv .ods | E8A5 Document | #7EC49E mint green |
| Presentation | .pptx .ppt .odp | E8A5 Document | #C4A86E golden |
| PDF | .pdf | E8A5 Document | #C47878 soft red |
| Executable | .exe .msi .appx .msix | E8A5 Document | #A0A8B0 light steel |
| System | .dll .sys .drv | E8A5 Document | #7A8088 dark gray |
| Default | * | E8A5 Document | #A2B0B4 NordicTextMuted |

Note: MDL2 glyph EB9F (Photo) and E8D6 (MusicNote) provide richer visual cues for
images and audio; other categories use the Document glyph differentiated by color.
Shell thumbnail extraction is deferred to Phase 5.

### Accessibility

`AutomationProperties.Name` is added to all interactive controls that have
`ToolTipService.ToolTip` set, ensuring screen readers announce the control purpose.

## Definition of Done

- `FileIconMapper.GetIconInfo` returns distinct glyph+color pairs for image, audio,
  archive, code, and default extensions.
- Icon glyph column in both `FileEntryTemplate` and `SearchEntryTemplate` binds to
  `IconGlyph` / `IconColorHex` via `HexColorToBrushConverter`.
- All toolbar buttons, AddressBox, SearchBox, TabView, and Sidebar items have
  `AutomationProperties.Name` set.
- `FileIconMapperTests`: 10+ assertions covering each category.
- `dotnet test` full suite passes with no new failures.

## Known limitations

- No shell thumbnail extraction (deferred to Phase 5).
- No animated icon for current-folder-loading state.
- High-contrast theme uses the converter's computed `SolidColorBrush`, not system
  high-contrast brushes. Phase 5 should use `AccessibilitySettings.HighContrast` to
  switch to `SystemAccentColor` / `SystemColorButtonFace` brushes.
