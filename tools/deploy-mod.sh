#!/usr/bin/env bash
# Lays the built mod out in a plugin folder as a release does: the loader is the plugin BepInEx loads,
# the mod goes in Mod/ as .bin files (BepInEx leaves those alone, the loader loads them from memory).
# The DLLs of builds from before the loader go.
#
# Usage: deploy-mod.sh PLUGIN_DIR        (a game's BepInEx/plugins/COTLMP)
#        deploy-mod.sh --package ZIP VERSION
#                                        (the release's CotlCoopMod.zip: the mod and what it needs,
#                                         for the update in the running game)
# Build COTLMP and Loader (Release) first.
set -eu
SRC=$(cd "$(dirname "$0")/.." && pwd)
BIN=$SRC/COTLMP/bin/Release/net472
LOADER=$SRC/Loader/bin/Release/net472/COTLMP.Loader.dll

# The versions the build is for, as the mod reads them in the game
BEPINEX=5.4.23.5
COTLAPI=0.3.4
CONTRACT=$(grep -o 'Contract = [0-9]*' "$SRC/COTLMP/Network/HotUpdate.cs" | grep -o '[0-9]*$')

if [ "${1:-}" = "--package" ]; then
    ZIP=$2 VERSION=$3
    python -I - "$BIN" "$ZIP" "$VERSION" "$CONTRACT" "$BEPINEX" "$COTLAPI" <<'PY'
import json, os, sys, zipfile
bin, out, version, contract, bepinex, cotlapi = sys.argv[1:]
files = {"COTLMP.bin": "COTLMP.dll", "COTLMP.pdb": "COTLMP.pdb",
         "COTLMPServer.bin": "COTLMPServer.dll", "COTLMPServer.pdb": "COTLMPServer.pdb"}
# tools/release.py checks MOD.zip's manifest.json is exactly these keys and values: a new key needs it too
manifest = {"version": version, "loader": int(contract), "bepinex": bepinex, "cotlapi": cotlapi}
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    z.writestr("manifest.json", json.dumps(manifest, indent=2))
    for name, built in files.items():
        if os.path.exists(os.path.join(bin, built)):
            z.write(os.path.join(bin, built), name)
print(out, manifest)
PY
    exit 0
fi

DIR=${1:?usage: deploy-mod.sh PLUGIN_DIR}
mkdir -p "$DIR/Mod"
rm -f "$DIR/COTLMP.dll" "$DIR/COTLMP.pdb" "$DIR/COTLMPServer.dll" "$DIR/COTLMPServer.pdb"
cp "$LOADER" "$DIR/"
cp "$BIN/COTLMP.dll" "$DIR/Mod/COTLMP.bin"
cp "$BIN/COTLMPServer.dll" "$DIR/Mod/COTLMPServer.bin"
for Name in COTLMP COTLMPServer; do
    if [ -f "$BIN/$Name.pdb" ]; then cp "$BIN/$Name.pdb" "$DIR/Mod/$Name.pdb"; else rm -f "$DIR/Mod/$Name.pdb"; fi
done
