# Generate test fixture files for Phase 0 file-listing benchmarks.
# SAFE: only writes to this tmp/fixtures directory. Never touches user data.
param(
    [int]$SmallCount  = 1000,
    [int]$MediumCount = 10000,
    [int]$LargeCount  = 100000
)

$base = $PSScriptRoot

function New-Files {
    param([string]$Dir, [int]$Count)
    New-Item -ItemType Directory -Force -Path $Dir | Out-Null
    # Remove existing files first for reproducibility
    Get-ChildItem $Dir -File | Remove-Item -Force

    $sw = [Diagnostics.Stopwatch]::StartNew()
    for ($i = 0; $i -lt $Count; $i++) {
        $name = "file_{0:D7}.txt" -f $i
        [IO.File]::WriteAllText([IO.Path]::Combine($Dir, $name), "x")
    }
    $sw.Stop()
    Write-Host ("  {0,7} files in {1}" -f $Count, $Dir.Split('\')[-1]) -NoNewline
    Write-Host ("  ({0:F1} ms, {1:F0} files/s)" -f $sw.Elapsed.TotalMilliseconds, ($Count / $sw.Elapsed.TotalSeconds))
}

Write-Host "Generating fixtures (only writes to $base)..."
New-Files (Join-Path $base "small_1k")    $SmallCount
New-Files (Join-Path $base "medium_10k")  $MediumCount
New-Files (Join-Path $base "large_100k")  $LargeCount

# Also create a folder with Unicode and unusual names
$unicodeDir = Join-Path $base "unicode_names"
New-Item -ItemType Directory -Force -Path $unicodeDir | Out-Null
$specialNames = @(
    "ファイル_テスト.txt",
    "file with spaces.txt",
    "emoji_🗂️_🔍.txt",
    "very_long_name_$(('x' * 200)).txt",
    ".hidden_file",
    "UPPERCASE.TXT",
    "MixedCase.TxT"
)
foreach ($n in $specialNames) {
    try { [IO.File]::WriteAllText([IO.Path]::Combine($unicodeDir, $n), "unicode test") } catch { Write-Warning "Could not create: $n" }
}
Write-Host "  Unicode names folder: $unicodeDir"

Write-Host "Done. Fixture counts:"
@("small_1k","medium_10k","large_100k","unicode_names") | ForEach-Object {
    $d = Join-Path $base $_
    $c = (Get-ChildItem $d -File -ErrorAction SilentlyContinue | Measure-Object).Count
    Write-Host ("  {0,-15} {1,7} files" -f $_, $c)
}
