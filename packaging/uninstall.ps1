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
if (-not (Test-Path $Manifest)) {
    # This mod's folder (its loader in it) without a list of its files: an old install over an install lost it.
    # Only that folder goes; another mod's folder of the same name is left alone
    $ModDir = Join-Path $GameDir "BepInEx\plugins\COTLMP"
    if (-not (Test-Path $ModDir)) { throw "Nothing to remove: $Manifest not found." }
    if (-not ((Test-Path (Join-Path $ModDir "COTLMP.Loader.dll")) -or (Test-Path (Join-Path $ModDir "Mod\COTLMP.bin")))) {
        throw "Nothing to remove: $Manifest not found. $ModDir is there, but it isn't this mod's files (no COTLMP.Loader.dll): left alone."
    }
    if (Get-Process "Cult Of The Lamb" -ErrorAction SilentlyContinue) { throw "Close Cult of the Lamb first." }
    Remove-Item $ModDir -Recurse -Force
    Write-Host "Found the mod's folder without its list of files and removed it: $ModDir. BepInEx itself and any other mods are left in place."
    foreach ($left in Get-ChildItem -Path $GameDir -Directory -Filter "COTLMP-backup-*") {
        Write-Host "Left as it is: $($left.FullName) holds files from before an earlier install of the mod."
    }
    return
}
if (Get-Process "Cult Of The Lamb" -ErrorAction SilentlyContinue) { throw "Close Cult of the Lamb first." }

$backup = $null
$files = @()
# Only paths inside the game folder are taken from the list
function Test-Inside([string]$Path) { $Path -and -not [IO.Path]::IsPathRooted($Path) -and -not $Path.Contains("..") }
foreach ($line in Get-Content $Manifest) {
    if ($line.StartsWith("#backup ")) { if (Test-Inside $line.Substring(8)) { $backup = Join-Path $GameDir $line.Substring(8) } }
    elseif ($line -and -not $line.StartsWith("#") -and (Test-Inside $line)) { $files += $line }
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
