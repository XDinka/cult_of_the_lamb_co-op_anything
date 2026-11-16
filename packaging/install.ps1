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

# An earlier install (this script's or the installer's) is updated in place, as the installer does: its
# files are ours and its backup keeps what was there before the first install
$Manifest = Join-Path $GameDir "COTLMP-installed.txt"
$Earlier = @()
$BackupName = $null
# Only paths inside the game folder are taken from the list
function Test-Inside([string]$Path) { $Path -and -not [IO.Path]::IsPathRooted($Path) -and -not $Path.Contains("..") }
if (Test-Path $Manifest) {
    foreach ($line in Get-Content $Manifest -Encoding UTF8) {
        $text = $line.Trim([char]0xFEFF, ' ')
        if ($text.StartsWith("#backup ")) { if (Test-Inside $text.Substring(8)) { $BackupName = $text.Substring(8) } }
        elseif ($text -and -not $text.StartsWith("#") -and (Test-Inside $text)) { $Earlier += $text }
    }
}
else {
    # This mod's folder (its loader in it) without a list of its files: an old install over an install lost it.
    # The mod's own files, not the game's, so they are replaced and not kept aside. Another mod's folder of
    # the same name is kept aside as anything else is.
    $ModDir = Join-Path $GameDir "BepInEx\plugins\COTLMP"
    if ((Test-Path (Join-Path $ModDir "COTLMP.Loader.dll")) -or (Test-Path (Join-Path $ModDir "Mod\COTLMP.bin"))) {
        Write-Host "Found the mod's folder without its list of files: taken as an earlier install of the mod"
        $Earlier = @(Get-ChildItem -Path $ModDir -Recurse -File -Force | ForEach-Object { $_.FullName.Substring($GameDir.TrimEnd('\').Length + 1) })
    }
}
$Ours = @{}
foreach ($rel in $Earlier) { $Ours[$rel.ToLowerInvariant()] = $true }

# Before 0.3.6 the mod itself was the plugin; now the loader is, and the mod is in Mod\ (both would load).
# Only files an earlier install of ours listed go: another mod's of the same name stay
foreach ($old in "COTLMP.dll", "COTLMP.pdb", "COTLMPServer.dll", "COTLMPServer.pdb") {
    $rel = "BepInEx\plugins\COTLMP\$old"
    $path = Join-Path $GameDir $rel
    if ($Ours.ContainsKey($rel.ToLowerInvariant()) -and (Test-Path $path)) { Remove-Item $path -Force }
}
# Before 0.3.6 the mod had asset bundles for COTLMP's server browser, which is gone
$oldAssets = Join-Path $GameDir "BepInEx\plugins\COTLMP\Assets"
if ((Test-Path $oldAssets) -and ($Earlier | Where-Object { $_ -like "BepInEx\plugins\COTLMP\Assets\*" })) { Remove-Item $oldAssets -Recurse -Force }
if (-not $BackupName) { $BackupName = "COTLMP-backup-" + (Get-Date -Format "yyyyMMdd-HHmmss") }
$Backup = Join-Path $GameDir $BackupName

# Anything else the mod would overwrite is moved aside first, so uninstall.ps1 can put it back
$Installed = @()
$payloadFiles = Get-ChildItem -Path $Payload -Recurse -File -Force
foreach ($f in $payloadFiles) {
    $rel = $f.FullName.Substring($Payload.Length + 1)
    $dest = Join-Path $GameDir $rel
    if ((Test-Path $dest) -and -not $Ours.ContainsKey($rel.ToLowerInvariant())) {
        $bak = Join-Path $Backup $rel
        New-Item -ItemType Directory -Force (Split-Path -Parent $bak) | Out-Null
        Copy-Item $dest $bak -Force
    }
    New-Item -ItemType Directory -Force (Split-Path -Parent $dest) | Out-Null
    Copy-Item $f.FullName $dest -Force
    $Installed += $rel
}

# Files an earlier version put in and this one doesn't have stay listed, to be removed later
$listed = @{}
foreach ($rel in $Installed) { $listed[$rel.ToLowerInvariant()] = $true }
foreach ($rel in $Earlier) {
    if (-not $listed.ContainsKey($rel.ToLowerInvariant()) -and (Test-Path (Join-Path $GameDir $rel))) { $Installed += $rel }
}

$lines = @("# Files installed by the COTLMP Steam co-op test build. uninstall.ps1 removes them.")
if (Test-Path $Backup) { $lines += "#backup " + $BackupName }
$lines += $Installed
Set-Content -Path $Manifest -Value $lines -Encoding UTF8

Write-Host "Done: $($Installed.Count) files. Start the game from Steam as usual."
