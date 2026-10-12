# Phase 8 — Security review and release preparation

## Objective

Perform a focused security review of the codebase, fix identified issues, add standard release
artefacts (LICENSE, CHANGELOG), and document the MSIX packaging path for future distribution.

---

## Security review findings

### Finding 1 — Everything scope path not quoted (FIXED)

**File:** `src/FlytExplorer.Search/EverythingSearchProvider.cs`

**Severity:** Medium (incorrect search results, not a code-execution risk)

**Problem:** When the user browses to a folder whose path contains spaces and then performs a
folder-scoped search (Ctrl+F), the Everything query was built as:

```
path:C:\Users\John Doe\Documents *.jpg
```

Everything's query parser treats spaces as token separators, so it searched for
`path:C:\Users\John` AND `Doe\Documents` AND `*.jpg` instead of the intended scoped query.
Results either returned nothing or returned unrelated files.

**Fix:** `BuildQuery` now quotes the scope path:

```
path:"C:\Users\John Doe\Documents" *.jpg
```

The method was extracted as `public static` to allow isolated unit testing without requiring
a running Everything instance. Five new `[Fact]` tests cover plain paths, paths with spaces,
and UNC paths.

---

### Finding 2 — Context menu hardcoded dark colours (FIXED)

**File:** `src/FlytExplorer.App/MainWindow.xaml.cs` — `GetOrCreateContextMenu()`

**Severity:** Low (visual defect in light mode; no security impact)

**Problem:** The `MenuFlyoutPresenter` style was created in code-behind with hardcoded dark
hex values for `Background` (`#363636`) and `BorderBrush` (`#505050`). In Nordic Light mode
the menu background stayed dark, making it visually broken.

**Fix:** `NordicMenuFlyoutPresenterStyle` is now defined in `MainWindow.xaml` using
`{ThemeResource MenuFlyoutPresenterBackground}` and `{ThemeResource MenuFlyoutPresenterBorderBrush}`.
These keys are already defined per-theme in `App.xaml` ThemeDictionaries (light: `#F0F4F2` /
`#BCC8C4`; dark: `#363636` / `#505050`). Code-behind now simply references the XAML resource.

---

### Finding 3 — `OpenFileAsync` uses `UseShellExecute = true` (ACCEPTED)

**File:** `MainWindow.xaml.cs` — `OpenFileAsync()`

`Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })` is the standard and
safe way to open a file with its registered application — identical to double-clicking in
Explorer. The file must be explicitly selected by the user in the file list. No change required.

---

### Finding 4 — File name validation on rename / new folder (VERIFIED OK)

Both `RenameSelectedAsync` and `NewFolderAsync` call `Path.GetInvalidFileNameChars()` before
dispatching to `IFileOperation`. Invalid characters are rejected with a user-visible dialog.
No change required.

---

### Finding 5 — Address bar path accepted without normalisation (ACCEPTED)

`AddressBox.Text.Trim()` → `Directory.Exists(path)` → `Navigate(path)`. The existence check
is a sufficient guard for a local file manager where the user is expected to type arbitrary
paths. The app does not execute the path as code, only passes it to `DirectoryInfo`. No change
required.

---

### Finding 6 — Status bar displays user-controlled path strings (ACCEPTED)

`StatusText.Text` shows paths and error messages. This text is only visible within the
application UI; it is not transmitted anywhere. For a local desktop app this is acceptable and
aids debugging. No change required.

---

### Finding 7 — Exception messages may include internal paths in error dialogs (ACCEPTED)

`ShowErrorAsync(ex.Message)` can surface OS error strings containing file paths. Because all
paths are under the user's own access context and the dialog is local, this poses no escalation
risk. If diagnostic logging is added in a future phase, paths should be redacted as noted in
`ARCHITECTURE.md` section 5.

---

## Changed files (Phase 8)

| File | Change |
|---|---|
| `src/FlytExplorer.Search/EverythingSearchProvider.cs` | Extract `BuildQuery` (internal static); quote scope path |
| `tests/FlytExplorer.UnitTests/EverythingSearchProviderTests.cs` | +5 `BuildQuery` unit tests |
| `src/FlytExplorer.App/MainWindow.xaml` | Add `NordicMenuFlyoutPresenterStyle` using ThemeResource |
| `src/FlytExplorer.App/MainWindow.xaml.cs` | Replace inline dark style with XAML resource reference |
| `LICENSE` | MIT licence (new) |
| `CHANGELOG.md` | Project changelog (new) |

---

## MSIX packaging guide (future distribution)

The app currently ships as **unpackaged** (`<WindowsPackageType>None</WindowsPackageType>`).
This is suitable for development and GitHub releases as a `.zip` archive. MSIX packaging
provides cleaner install/uninstall and is required for Microsoft Store submission.

### Steps to enable MSIX packaging

1. **Create `Package.appxmanifest`** in `src/FlytExplorer.App/`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">
  <Identity Name="FlytExplorer" Publisher="CN=FlytExplorer Dev" Version="0.8.0.0"
            ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>Flyt Explorer</DisplayName>
    <PublisherDisplayName>FlytExplorer Dev</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0"
                        MaxVersionTested="10.0.26100.0" />
  </Dependencies>
  <Resources>
    <Resource Language="en-US" />
    <Resource Language="ja-JP" />
  </Resources>
  <Applications>
    <Application Id="App"
                 Executable="$targetnametoken$.exe"
                 EntryPoint="$targetentrypoint$">
      <uap:VisualElements DisplayName="Flyt Explorer" Description="Nordic file manager"
                          Square150x150Logo="Assets\Square150x150Logo.png"
                          Square44x44Logo="Assets\Square44x44Logo.png"
                          BackgroundColor="transparent">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" />
        <uap:SplashScreen Image="Assets\SplashScreen.png" />
      </uap:VisualElements>
    </Application>
  </Applications>
  <Capabilities>
    <!-- Required for browsing arbitrary file system paths outside app sandbox -->
    <rescap:Capability Name="broadFileSystemAccess" />
  </Capabilities>
</Package>
```

2. **Update `FlytExplorer.App.csproj`**: remove `<WindowsPackageType>None</WindowsPackageType>`.

3. **Create a self-signed development certificate**:

```powershell
$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject "CN=FlytExplorer Dev" `
    -KeyUsage DigitalSignature `
    -FriendlyName "FlytExplorer Dev Cert" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

$pwd = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath FlytExplorerDev.pfx -Password $pwd
```

Then set in `.csproj`:
```xml
<AppxPackageSigningEnabled>true</AppxPackageSigningEnabled>
<PackageCertificateKeyFile>FlytExplorerDev.pfx</PackageCertificateKeyFile>
<PackageCertificatePassword>YourPassword</PackageCertificatePassword>
```

4. **Build MSIX**:
```
dotnet publish src/FlytExplorer.App/FlytExplorer.App.csproj -c Release -r win-x64
```

> **Note:** The self-signed certificate must be installed in the `Trusted People` certificate
> store on the target machine for sideloading without the Store. Production distribution
> requires a code-signing certificate from a trusted CA (or Microsoft Store signing).

---

## GitHub release checklist

- [x] `LICENSE` (MIT)
- [x] `CHANGELOG.md`
- [x] `THIRD_PARTY_NOTICES.md`
- [x] `README.md` with build instructions
- [x] Security review performed and findings documented
- [ ] Trademark / name availability check for "FlytExplorer" (see ARCHITECTURE.md §7)
- [ ] Test on clean Windows 11 machine without dev tools installed
- [ ] Test with Everything 1.4.x and 1.5.x (different feature sets)
- [ ] Test with high-DPI monitor (125%, 150%, 200% scaling)
- [ ] Test Nordic Dark and Nordic Light with OS theme switch while app is open
- [ ] Sign release artefact or document unsigned sideloading steps for users

---

## Definition of Done

- `dotnet build` succeeds with 0 errors on the App project.
- `dotnet test` passes all tests; new `BuildQuery` tests included in run.
- Context menu visible and correctly styled in both Nordic Dark and Nordic Light modes.
- Folder-scoped search returns correct results for paths containing spaces.
