# Flyt Explorer

A standalone, Nordic Dark / Nordic Light file manager for Windows 11 x64.
Built with C# / .NET 10, WinUI 3 (Windows App SDK 2.5.1), and Everything IPC for fast search.

## Requirements

| Requirement | Version |
|---|---|
| Windows 11 x64 | 22H2 or later (build 22621+) |
| .NET 10 Runtime | 10.0 or later |
| Windows App SDK | 2.5.1 (auto-installed via NuGet on first build) |
| Everything (optional) | 1.4 or 1.5, running, for Ctrl+K global search |

## Build

```powershell
# Clone and build
git clone https://github.com/<org>/FlytExplorer.git
cd FlytExplorer\FlytExplorer-Design-v1.0
dotnet build -c Release
```

Run tests:

```powershell
dotnet test
```

Expected output: 58 passed, 1 skipped (Everything IPC — requires running Everything), 0 failed.

## Run

```powershell
# Debug
dotnet run --project src\FlytExplorer.App

# Or launch the built exe directly
.\src\FlytExplorer.App\bin\Release\net10.0-windows10.0.22621.0\FlytExplorer.App.exe
```

Or open `FlytExplorer.slnx` in Visual Studio 2022 (17.13+) and press F5.

## Release packaging

```powershell
dotnet publish src\FlytExplorer.App -c Release -r win-x64 --self-contained false
```

See `docs/PHASE_8.md` for MSIX packaging and code-signing instructions.

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| Ctrl+T | New tab |
| Ctrl+W | Close tab |
| Ctrl+K | Global search (requires Everything) |
| Ctrl+F | Search within current folder |
| Ctrl+L | Focus address bar |
| Alt+Left / Right | Back / Forward |
| Alt+Up | Parent folder |
| F2 | Rename |
| F5 | Refresh |
| Delete | Move to Recycle Bin |
| Shift+Delete | Permanent delete (confirmation required) |
| Ctrl+C / X / V | Copy / Cut / Paste |
| Ctrl+Shift+N | New folder |

## Theme

Flyt Explorer follows the Windows system Light / Dark setting automatically.

- **Nordic Dark** — deep matte blue-grey surfaces, muted sage-teal accent.
- **Nordic Light** — soft off-white surfaces with cool-green tint, same accent hue.

## Known limitations

- Shell extension context menus, virtual folders, and cloud-placeholder files are not
  supported. Explorer-specific integrations are out of scope.
- SMB/UNC shares require Everything to have indexed the share for search; browsing
  works without Everything but search does not cover unindexed locations.
- Everything must be installed and running for global search (Ctrl+K). If unavailable,
  the app falls back to folder-scoped browse without IPC.
- Signed packaging (MSIX) is not yet provided; the app runs unpackaged.

## License

MIT — see [LICENSE](LICENSE).
Third-party dependency notices: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
