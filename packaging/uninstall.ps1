# Removes the COTLMP Steam co-op test build and restores anything it replaced.
# Usage: right-click uninstall.cmd -> Run, or: powershell -ExecutionPolicy Bypass -File uninstall.ps1 [-GameDir "D:\...\Cult of the Lamb"]
param([string]$GameDir = "")

$ErrorActionPreference = "Stop"

if (-not $GameDir) {
    $steam = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name SteamPath -ErrorAction SilentlyContinue).SteamPath
    $libs = @($steam)
    $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
    if (Test-Path $vdf) {
        foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) { $libs += $m.Groups[1].Value -replace '\\\\', '\' }
    }
    foreach ($lib in $libs | Select-Object -Unique) {
        $acf = Join-Path $lib "steamapps\appmanifest_1313140.acf"
        if (Test-Path $acf) {
            $dir = [regex]::Match((Get-Content $acf -Raw), '"installdir"\s+"([^"]+)"').Groups[1].Value
            $GameDir = Join-Path $lib "steamapps\common\$dir"
        }
    }
}

$Manifest = Join-Path $GameDir "COTLMP-installed.txt"
if (-not (Test-Path $Manifest)) { throw "Nothing to remove: $Manifest not found." }
if (Get-Process "Cult Of The Lamb" -ErrorAction SilentlyContinue) { throw "Close Cult of the Lamb first." }

$backup = $null
$files = @()
foreach ($line in Get-Content $Manifest) {
    if ($line.StartsWith("#backup ")) { $backup = Join-Path $GameDir $line.Substring(8) }
    elseif ($line -and -not $line.StartsWith("#")) { $files += $line }
}

foreach ($rel in $files) {
    $p = Join-Path $GameDir $rel
    if (Test-Path $p) { Remove-Item $p -Force }
}

# Folders the install created and that are now empty, deepest first
$dirs = @{}
foreach ($rel in $files) {
    $d = Split-Path -Parent $rel
    while ($d) { $dirs[$d] = $true; $d = Split-Path -Parent $d }
}
foreach ($d in ($dirs.Keys | Sort-Object { $_.Length } -Descending)) {
    $p = Join-Path $GameDir $d
    if ((Test-Path $p) -and -not (Get-ChildItem $p -Force)) { Remove-Item $p -Force }
}

if ($backup -and (Test-Path $backup)) {
    Get-ChildItem $backup -Recurse -File -Force | ForEach-Object {
        $rel = $_.FullName.Substring($backup.Length + 1)
        $dest = Join-Path $GameDir $rel
        New-Item -ItemType Directory -Force (Split-Path -Parent $dest) | Out-Null
        Copy-Item $_.FullName $dest -Force
    }
    Remove-Item $backup -Recurse -Force
}

Remove-Item $Manifest -Force
Write-Host "Removed. BepInEx's own log, cache and config folders are left in $GameDir\BepInEx; delete that folder if no other mods use it."
