# Phase 7 — Live theme-change support for code-behind elements

## Objective

All UI elements created programmatically in code-behind should respond correctly
when the user switches the OS theme while the app is running. Phase 6 deferred this
for loading spinners, empty-folder labels, and the properties dialog.

## Problem summary

`Application.Current.Resources["NordicXxxBrush"]` always returns the flat
`MergedDictionaries` value (the dark fallback). Elements constructed from code-behind
that set `Foreground` this way are locked to the dark palette regardless of OS theme.

Additionally, `ApplyNordicBorderColor()` had an incorrect COLORREF (a leftover from
an earlier iteration) and was not theme-aware.

## Solution

### `GetThemeBrush(string key)` helper

Reads from the active `ThemeDictionaries` entry first (which includes nested
`MergedDictionaries` — so NordicDark.xaml/NordicLight.xaml keys resolve correctly),
then falls back to flat `MergedDictionaries`:

```csharp
private Brush GetThemeBrush(string key)
{
    var themeKey = CurrentTheme == ElementTheme.Light ? "Light" : "Dark";
    if (Application.Current.Resources.ThemeDictionaries.TryGetValue(themeKey, out var dict) &&
        dict is ResourceDictionary rd && rd.TryGetValue(key, out var val) && val is Brush b)
        return b;
    return Application.Current.Resources[key] as Brush
           ?? new SolidColorBrush(Colors.Transparent);
}
```

### `CurrentTheme` property

```csharp
private ElementTheme CurrentTheme =>
    (Content as FrameworkElement)?.ActualTheme ?? ElementTheme.Dark;
```

`ActualTheme` on a loaded `FrameworkElement` is always `Light` or `Dark`, never `Default`.

### `ActualThemeChanged` listener

Registered after `InitializeComponent()` on the root content `FrameworkElement`.
When fired, updates all tab loading rings and empty-folder labels, then refreshes
the DWM border colour.

### `ApplyNordicBorderColor()` — theme-aware + corrected COLORREF

| Theme | Accent | COLORREF (0x00BBGGRR) |
|---|---|---|
| Dark | `#72B8C4` | `0x00C4B872` |
| Light | `#4E7A71` | `0x00717A4E` |

Previous code used `0x00ACB598` (old palette value — now corrected).

## Changed files

| File | Change |
|---|---|
| `MainWindow.xaml.cs` | `CurrentTheme`, `GetThemeBrush`, `OnActualThemeChanged`, corrected `ApplyNordicBorderColor`, replace all `Resources["Nordic…"]` calls |

## Definition of Done

- `dotnet build` succeeds, 0 errors.
- `dotnet test`: 0 failures.
- DWM border colour matches accent in both Dark (`#72B8C4`) and Light (`#4E7A71`).
- Switching OS theme while app runs updates loading ring and empty-folder label colours
  without requiring a restart.
