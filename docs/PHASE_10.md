# Phase 10 — Release validation

## Objective

Verify the application is ready for public GitHub release by running all automated checks,
confirming release artefacts are correct, and documenting the manual test plan for items
that require a real environment.

---

## Automated checks — PASSED

```
dotnet build -c Release                              → 0 errors, 0 warnings
dotnet test                                          → 58 passed, 1 skipped, 0 failed
dotnet publish -r win-x64 --self-contained false     → publish exe runs stably (verified PID 27632)
```

---

## Release artefacts — VERIFIED

| File | Status |
|---|---|
| `LICENSE` | MIT, present |
| `CHANGELOG.md` | Updated through v0.9.1 |
| `README.md` | Build commands corrected; known limitations current |
| `THIRD_PARTY_NOTICES.md` | Windows App SDK, Everything SDK, xUnit, .NET, Segoe MDL2 |
| `docs/PHASE_8.md` | Security review + MSIX packaging guide |
| Publish output | App.xbf, MainWindow.xbf, Themes/*.xbf, FlytExplorer.App.pri all present |

---

## Build command correction (v0.9.1)

`<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>` was removed from `FlytExplorer.App.csproj`
after discovering that the RID apphost embeds a native bootstrapper (Foundation 2.3.12) that
fails with `E_INVALIDARG` in `combase.dll` before .NET starts when only runtime 2.5.x is
installed. Development builds now use the non-RID apphost; release artefacts use `dotnet publish`.

**Correct commands:**

| Purpose | Command |
|---|---|
| Development build | `dotnet build` |
| Run (development) | `dotnet run --project src\FlytExplorer.App` |
| Release package | `dotnet publish src\FlytExplorer.App -c Release -r win-x64 --self-contained false` |

---

## Manual test plan

These tests require environments not available on the development machine.

### 1. Clean Windows 11 machine (no dev tools)

Test goal: confirm the app starts and operates without the .NET 10 SDK, Visual Studio,
or Windows App SDK development packages installed — only the runtime.

Pre-conditions:
- Fresh Windows 11 22H2+ x64
- .NET 10 Runtime (not SDK): https://dot.net/download
- Windows App SDK 2.5.1 runtime installer: included as Bootstrapper auto-install on first launch

Steps:
1. Copy `dotnet publish` output zip to the test machine.
2. Run `FlytExplorer.App.exe`.
3. Navigate to a local folder — verify listing appears.
4. Copy, rename, delete a file in `tmp\fixtures\` — verify operations complete.
5. Ctrl+T new tab, Ctrl+W close tab.
6. Alt+Left/Right/Up navigation.
7. OS theme switch (Settings → Personalisation → Colours) while app is open — verify theme updates.

### 2. Everything version compatibility

Test goal: confirm search works with both major Everything versions.

| Version | Download |
|---|---|
| Everything 1.4.x | https://www.voidtools.com/downloads/ |
| Everything 1.5.x | https://www.voidtools.com/downloads/ (beta) |

Steps for each version:
1. Install Everything and let it index a test folder.
2. Open Flyt Explorer and navigate to the indexed folder.
3. Press Ctrl+K — type a filename — verify global results appear.
4. Press Ctrl+F — type a filename — verify results are scoped to the folder.
5. Test a folder path with spaces (e.g. `C:\Test Folder\`) — verify results are correct.
6. Quit Everything — verify the search field shows an unavailable state gracefully.

### 3. High-DPI display (125% / 150% / 200%)

Test goal: confirm layout does not clip, overlap, or produce blurry text at non-100% scaling.

Steps (adjust display scale in Settings → System → Display):
1. At each scale, open the app and verify:
   - Title bar drag region and caption buttons are proportional.
   - Tab bar, address bar, file list columns are not clipped.
   - Text is sharp (not blurry from incorrect DPI scaling).
   - Sidebar shortcut links are fully visible.
2. Change scale while app is open — verify layout reflows correctly.

---

## Definition of Done

- [ ] Automated tests: 0 failures.
- [ ] Clean-machine test: app launches, basic navigation works, no missing runtime error.
- [ ] Everything 1.4 and 1.5: search returns correct results, unavailable state is graceful.
- [ ] 150% DPI: no clipped or overlapping UI elements.
- [ ] Trademark check for "FlytExplorer" name completed (external — see ARCHITECTURE.md §7).
