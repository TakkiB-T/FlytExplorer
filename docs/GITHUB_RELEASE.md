# GitHub release guide

Step-by-step instructions for publishing FlytExplorer to GitHub.

---

## 1. Create the GitHub repository

1. Go to https://github.com/new
2. Repository name: `FlytExplorer`
3. Description: `Nordic Dark/Light file manager for Windows 11 — WinUI 3 / .NET 10`
4. Visibility: **Public**
5. **Do NOT** initialise with README, .gitignore, or licence (the repo already has these)
6. Click **Create repository**

---

## 2. Push the local repository

```powershell
cd FlytExplorer-Design-v1.0

# Replace <your-username> with your GitHub username
git remote add origin https://github.com/<your-username>/FlytExplorer.git
git branch -M main
git push -u origin main
```

> The local branch is `master`. `git branch -M main` renames it to `main` before pushing.

---

## 3. Build the release package

```powershell
dotnet publish src\FlytExplorer.App -c Release -r win-x64 --self-contained false
```

Output: `src\FlytExplorer.App\bin\Release\net10.0-windows10.0.22621.0\win-x64\publish\`

Create a ZIP for upload:

```powershell
$pub = "src\FlytExplorer.App\bin\Release\net10.0-windows10.0.22621.0\win-x64\publish"
Compress-Archive -Path "$pub\*" -DestinationPath "FlytExplorer-v0.9.3-win-x64.zip" -Force
```

Pre-built: `FlytExplorer-v0.9.3-win-x64.zip` is already at the repo root.

---

## 4. Create the GitHub release

### Option A — GitHub CLI (recommended once installed)

```powershell
# Install gh CLI: https://cli.github.com/
gh auth login
gh release create v0.9.3 FlytExplorer-v0.9.3-win-x64.zip `
  --title "v0.9.3 — App icon, publish fix" `
  --notes-file CHANGELOG.md
```

### Option B — GitHub web UI

1. Go to `https://github.com/<your-username>/FlytExplorer/releases/new`
2. Tag: `v0.9.3` (create new tag on `main`)
3. Title: `v0.9.3 — App icon, publish fix`
4. Description: copy the `[0.9.3]` through `[0.9.1]` sections from `CHANGELOG.md`
5. Attach `FlytExplorer-v0.9.3-win-x64.zip`
6. Click **Publish release**

---

## 5. Install GitHub CLI (optional, for future releases)

```powershell
winget install --id GitHub.cli
gh auth login
```

---

## 6. Required runtime — end-user note

Users need **Windows App SDK 2.5.1** runtime. If not installed, the app will prompt to
install it on first launch (bootstrapper auto-install). Alternatively, direct them to:

```
https://aka.ms/windowsappsdk/2.5/latest/windowsappruntimeinstall-x64.exe
```

---

## Pre-release checklist

- [x] `dotnet test` — 58 passed, 0 failed
- [x] `dotnet publish -r win-x64` — publish exe runs, icon visible in taskbar
- [x] Everything 1.4.1 search — IPC tests pass
- [x] DPI 125% — PerMonitorV2, no layout clipping
- [ ] Trademark check "FlytExplorer"
- [ ] Clean Windows 11 machine test
- [ ] Everything 1.5.x test
