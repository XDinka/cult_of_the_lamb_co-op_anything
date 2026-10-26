#!/usr/bin/env bash
# Assembles the player-ready zip: BepInEx + COTL_API + our build + installer.
#
# Paths (environment):
#   COTL_MANAGED      the game's "Cult Of The Lamb_Data\Managed" folder (Windows path), for Steamworks.NET
#   COTL_DL           folder with the pinned downloads (check their SHA-256 before use):
#                       bepinex/BepInEx_win_x64_5.4.23.5.zip
#                       cotlapi/xhayper-COTL_API-0.3.4.zip
#   COTL_BEPINEX_CFG  a BepInEx.cfg with Preloader.Entrypoint Assembly = Assembly-CSharp.dll, Type = TwitchManager
#   OUT_DIR           where the zip goes (default: ./out)
#   DOTNET            the dotnet executable (default: dotnet)
set -eu
SRC=$(cd "$(dirname "$0")/.." && pwd)
DL=${COTL_DL:?set COTL_DL}
REL=${OUT_DIR:-$SRC/out}
MANAGED=${COTL_MANAGED:?set COTL_MANAGED}
CFG=${COTL_BEPINEX_CFG:?set COTL_BEPINEX_CFG}
VERSION=$(grep -o 'CotlMpVer = "[^"]*"' "$SRC/COTLMP/Data/Version.cs" | cut -d'"' -f2)
NAME=cotl-coop-anything-$VERSION
OUT=$REL/$NAME
DOTNET=${DOTNET:-dotnet}

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
$DOTNET build "$SRC/COTLMP/COTLMP.csproj" -c Release "-p:CotlManagedDir=$MANAGED" | grep -E "error|Errors|Ошибок" || true
$DOTNET build "$SRC/Loader/COTLMP.Loader.csproj" -c Release "-p:CotlManagedDir=$MANAGED" | grep -E "error|Errors|Ошибок" || true

rm -rf "$OUT" "$OUT.zip"
mkdir -p "$OUT/game/BepInEx/config" "$OUT/game/BepInEx/plugins/COTLMP" "$OUT/LICENSES"

python -I - "$DL" "$OUT/game" <<'PY'
import sys, zipfile
dl, game = sys.argv[1], sys.argv[2]
zipfile.ZipFile(dl + "/bepinex/BepInEx_win_x64_5.4.23.5.zip").extractall(game)
api = zipfile.ZipFile(dl + "/cotlapi/xhayper-COTL_API-0.3.4.zip")
for n in api.namelist():
    if n.startswith("plugins/") and not n.endswith("/"):
        api.extract(n, game + "/BepInEx")
PY

bash "$SRC/tools/deploy-mod.sh" "$OUT/game/BepInEx/plugins/COTLMP"
cp "$CFG" "$OUT/game/BepInEx/config/BepInEx.cfg"
grep -q "Type = TwitchManager" "$OUT/game/BepInEx/config/BepInEx.cfg"

cp "$SRC"/packaging/{install.ps1,uninstall.ps1,README.txt} "$OUT/"
printf '@echo off\r\npowershell -NoProfile -ExecutionPolicy Bypass -File "%%~dp0install.ps1"\r\npause\r\n' > "$OUT/install.cmd"
printf '@echo off\r\npowershell -NoProfile -ExecutionPolicy Bypass -File "%%~dp0uninstall.ps1"\r\npause\r\n' > "$OUT/uninstall.cmd"
cp "$SRC/LICENSE" "$OUT/LICENSES/cotl-coop-anything-MIT.txt"
cp "$SRC"/packaging/LICENSES/* "$OUT/LICENSES/"

# README.txt in Windows line endings with a BOM, so Notepad shows it right everywhere
python -I -c "import sys; p=sys.argv[1]; d=open(p,'rb').read().replace(b'\r\n',b'\n').replace(b'\n',b'\r\n'); open(p,'wb').write(b'\xef\xbb\xbf'+d if not d.startswith(b'\xef\xbb\xbf') else d)" "$OUT/README.txt"

(cd "$REL" && python -I -c "import shutil,sys; shutil.make_archive(sys.argv[1], 'zip', '.', sys.argv[1])" "$NAME")
ls -la "$OUT.zip"; sha256sum "$OUT.zip"

# The mod alone, for the update in the running game (a release asset next to the installer)
bash "$SRC/tools/deploy-mod.sh" --package "$REL/CotlCoopMod-$VERSION.zip" "$VERSION"
sha256sum "$REL/CotlCoopMod-$VERSION.zip"
