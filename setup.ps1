#Requires -Version 5.1
<#
.SYNOPSIS
    FlytExplorer prerequisite installer.
    Checks for .NET 10 Desktop Runtime and Windows App SDK 2.5 Runtime.
    Downloads and installs any missing components, then launches FlytExplorer.
#>

$ErrorActionPreference = 'Stop'

function Write-Step { param($msg) Write-Host "  >> $msg" -ForegroundColor Cyan }
function Write-Ok   { param($msg) Write-Host "  OK $msg" -ForegroundColor Green }
function Write-Warn { param($msg) Write-Host "  !! $msg" -ForegroundColor Yellow }

Write-Host ""
Write-Host "FlytExplorer Setup" -ForegroundColor White
Write-Host "------------------"

# ── 1. Check .NET 10 Desktop Runtime ─────────────────────────────────────────

Write-Step "Checking .NET 10 Desktop Runtime..."

$dotnetOk = $false
try {
    $runtimes = & dotnet --list-runtimes 2>$null
    $dotnetOk = $runtimes | Where-Object { $_ -match 'Microsoft\.WindowsDesktop\.App\s+10\.' }
} catch {}

if ($dotnetOk) {
    Write-Ok ".NET 10 Desktop Runtime is installed."
} else {
    Write-Warn ".NET 10 Desktop Runtime not found."
    Write-Host ""
    Write-Host "  Please install .NET 10 Desktop Runtime (x64) from:" -ForegroundColor Yellow
    Write-Host "  https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor White
    Write-Host ""
    $open = Read-Host "  Open the download page in browser? [Y/n]"
    if ($open -ne 'n' -and $open -ne 'N') {
        Start-Process "https://dotnet.microsoft.com/download/dotnet/10.0"
    }
    Write-Host ""
    Write-Host "  After installing, re-run this script." -ForegroundColor Yellow
    exit 1
}

# ── 2. Check Windows App SDK 2.5 Runtime ─────────────────────────────────────

Write-Step "Checking Windows App SDK 2.5 Runtime..."

$wasdkOk = $false
$wasdkPackages = Get-AppxPackage -Name "Microsoft.WindowsAppRuntime.1.6*" -ErrorAction SilentlyContinue
# Note: SDK 2.5 ships as WindowsAppRuntime package family 1.6.x
if (-not $wasdkPackages) {
    $wasdkPackages = Get-AppxPackage -Name "Microsoft.WindowsAppRuntime*" -ErrorAction SilentlyContinue |
        Where-Object { $_.Version -ge [version]"1.6.0" }
}
$wasdkOk = $null -ne $wasdkPackages

if ($wasdkOk) {
    Write-Ok "Windows App SDK Runtime is installed."
} else {
    Write-Warn "Windows App SDK 2.5 Runtime not found. Downloading installer..."
    Write-Host ""

    $installerUrl  = "https://aka.ms/windowsappsdk/2.5/latest/windowsappruntimeinstall-x64.exe"
    $installerPath = Join-Path $env:TEMP "WindowsAppRuntimeInstall-x64.exe"

    try {
        Write-Step "Downloading from $installerUrl ..."
        $wc = New-Object System.Net.WebClient
        $wc.DownloadFile($installerUrl, $installerPath)
        Write-Ok "Download complete."

        Write-Step "Running installer (may require elevation)..."
        $proc = Start-Process -FilePath $installerPath -ArgumentList "--quiet" -Wait -PassThru
        if ($proc.ExitCode -eq 0) {
            Write-Ok "Windows App SDK 2.5 Runtime installed."
        } else {
            Write-Warn "Installer exited with code $($proc.ExitCode). Trying without --quiet..."
            Start-Process -FilePath $installerPath -Wait
        }
    } catch {
        Write-Warn "Automatic download failed: $_"
        Write-Host ""
        Write-Host "  Please download and install manually from:" -ForegroundColor Yellow
        Write-Host "  https://aka.ms/windowsappsdk/2.5/latest/windowsappruntimeinstall-x64.exe" -ForegroundColor White
        exit 1
    }
}

# ── 3. Launch FlytExplorer ────────────────────────────────────────────────────

Write-Host ""
Write-Ok "All prerequisites satisfied."
Write-Step "Launching FlytExplorer..."

$exe = Join-Path $PSScriptRoot "FlytExplorer.App.exe"
if (-not (Test-Path $exe)) {
    Write-Warn "FlytExplorer.App.exe not found in $PSScriptRoot"
    exit 1
}

Start-Process -FilePath $exe
