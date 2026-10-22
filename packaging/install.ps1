# Installs the COTLMP Steam co-op test build into Cult of the Lamb.
# Usage: right-click install.cmd -> Run, or: powershell -ExecutionPolicy Bypass -File install.ps1 [-GameDir "D:\...\Cult of the Lamb"]
param([string]$GameDir = "")

$ErrorActionPreference = "Stop"
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Payload = Join-Path $Here "game"
$ExpectedBuild = "25223484"
$ExpectedVersion = "1.5.26"

function Find-Game {
    $steam = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name SteamPath -ErrorAction SilentlyContinue).SteamPath
    if (-not $steam) { return $null }
    $libs = @($steam)
    $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
    if (Test-Path $vdf) {
        foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
            $libs += $m.Groups[1].Value -replace '\\\\', '\'
        }
    }
    foreach ($lib in $libs | Select-Object -Unique) {
        $acf = Join-Path $lib "steamapps\appmanifest_1313140.acf"
        if (Test-Path $acf) {
            $text = Get-Content $acf -Raw
            $dir = [regex]::Match($text, '"installdir"\s+"([^"]+)"').Groups[1].Value
            $build = [regex]::Match($text, '"buildid"\s+"([^"]+)"').Groups[1].Value
            return @{ Dir = (Join-Path $lib "steamapps\common\$dir"); Build = $build }
        }
    }
    return $null
}

if (-not $GameDir) {
    $found = Find-Game
    if (-not $found) { throw "Cult of the Lamb wasn't found in your Steam libraries. Run again with -GameDir `"<game folder>`"." }
    $GameDir = $found.Dir
    if ($found.Build -ne $ExpectedBuild) {
        Write-Warning "Your game is Steam build $($found.Build); this test build was made for build $ExpectedBuild (version $ExpectedVersion). It may not work."
    }
}

if (-not (Test-Path (Join-Path $GameDir "Cult Of The Lamb.exe"))) { throw "No 'Cult Of The Lamb.exe' in $GameDir" }
if (Get-Process "Cult Of The Lamb" -ErrorAction SilentlyContinue) { throw "Close Cult of the Lamb first." }

Write-Host "Installing into $GameDir"

# Anything the mod would overwrite is moved aside first, so uninstall.ps1 can put it back
$Backup = Join-Path $GameDir ("COTLMP-backup-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
$Installed = @()
$payloadFiles = Get-ChildItem -Path $Payload -Recurse -File -Force
foreach ($f in $payloadFiles) {
    $rel = $f.FullName.Substring($Payload.Length + 1)
    $dest = Join-Path $GameDir $rel
    if (Test-Path $dest) {
        $bak = Join-Path $Backup $rel
        New-Item -ItemType Directory -Force (Split-Path -Parent $bak) | Out-Null
        Copy-Item $dest $bak -Force
    }
    New-Item -ItemType Directory -Force (Split-Path -Parent $dest) | Out-Null
    Copy-Item $f.FullName $dest -Force
    $Installed += $rel
}

$Manifest = Join-Path $GameDir "COTLMP-installed.txt"
$lines = @("# Files installed by the COTLMP Steam co-op test build. uninstall.ps1 removes them.")
if (Test-Path $Backup) { $lines += "#backup " + (Split-Path -Leaf $Backup) }
$lines += $Installed
Set-Content -Path $Manifest -Value $lines -Encoding UTF8

Write-Host "Done: $($Installed.Count) files. Start the game from Steam as usual."
