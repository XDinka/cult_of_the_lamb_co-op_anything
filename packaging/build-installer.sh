#!/usr/bin/env bash
# Builds the one-file installer (CotlCoopInstaller.exe): the package's game folder from
# build-package.sh goes inside it. Same environment as build-package.sh.
#
# Usage: build-installer.sh            (builds the package first)
#        PACKAGE_DIR=... build-installer.sh   (uses an already built package folder)
set -eu
SRC=$(cd "$(dirname "$0")/.." && pwd)
REL=${OUT_DIR:-$SRC/out}
DOTNET=${DOTNET:-dotnet}
VERSION=$(grep -o 'CotlMpVer = "[^"]*"' "$SRC/COTLMP/Data/Version.cs" | cut -d'"' -f2)
PKG=${PACKAGE_DIR:-$REL/cotl-coop-anything-$VERSION}

if [ -z "${PACKAGE_DIR:-}" ]; then
    bash "$SRC/packaging/build-package.sh"
fi
[ -d "$PKG/game" ] || { echo "No package at $PKG"; exit 1; }

mkdir -p "$SRC/installer/obj"
python -I "$SRC/tools/zip-folder.py" "$PKG/game" "$SRC/installer/obj/payload.zip"
[ -f "$SRC/installer/icon.ico" ] || python -I "$SRC/tools/make-icon.py" "$SRC/installer/icon.ico"

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
$DOTNET build "$SRC/installer/CotlCoopInstaller.csproj" -c Release "-p:Version=$VERSION" > "$REL/installer-build.log" 2>&1 || {
    grep -E "error" "$REL/installer-build.log" | sort -u; echo "INSTALLER BUILD FAILED"; exit 1; }

EXE=$REL/CotlCoopInstaller-$VERSION.exe
cp "$SRC/installer/bin/Release/net472/CotlCoopInstaller.exe" "$EXE"
ls -la "$EXE"; sha256sum "$EXE"
