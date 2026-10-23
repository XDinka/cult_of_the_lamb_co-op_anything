#!/usr/bin/env bash
# Build the mod, deploy it to two test copies of the game, run host + client over the loopback
# stand-in for Steam, then print the last QA reports from each side.
#
# Usage: run-pair.sh [seconds-after-join] [extra client args...]
#   HOST_EXTRA="..."  extra switches for the host, e.g. "-cotlmp-autodungeon=5 -cotlmp-autokill"
#
# Paths (environment):
#   COTL_G1, COTL_G2  two game folders with BepInEx, COTL_API and the COTLMP plugin folder
#                     (POSIX paths, e.g. /c/games/cotl-dev). The second can be a hardlinked copy
#                     with its own BepInEx folder.
#   QA_OUT            where logs go (default: ./out/qa)
#   DOTNET            the dotnet executable (default: dotnet)
set -u
SRC=$(cd "$(dirname "$0")/.." && pwd)
G1=${COTL_G1:?set COTL_G1}
G2=${COTL_G2:?set COTL_G2}
OUT=${QA_OUT:-$SRC/out/qa}
DOTNET=${DOTNET:-dotnet}
HOLD=${1:-30}; shift || true
EXTRA="$*"

winpath() { cygpath -w "$1" 2>/dev/null || echo "$1"; }

# Only the test copies, never a game started from Steam
kill_test_copies() {
    powershell.exe -NoProfile -Command "Get-Process 'Cult Of The Lamb' -ErrorAction SilentlyContinue | Where-Object { \$_.Path -like '$(winpath "$G1")\\*' -or \$_.Path -like '$(winpath "$G2")\\*' } | Stop-Process -Force" > /dev/null 2>&1
}

wait_for() { # file pattern seconds
    for _ in $(seq 1 $3); do
        grep -qE "$2" "$1" 2>/dev/null && return 0
        timeout 1 cat /dev/zero > /dev/null 2>&1
    done
    return 1
}

launch() { # folder args
    local Dir
    Dir=$(winpath "$1")
    powershell.exe -NoProfile -Command "Start-Process -FilePath '$Dir\\Cult Of The Lamb.exe' -WorkingDirectory '$Dir' -ArgumentList '$2'"
}

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
mkdir -p "$OUT"
if ! $DOTNET build "$SRC/COTLMP/COTLMP.csproj" -c Release "-p:CotlManagedDir=$(winpath "$G1/Cult Of The Lamb_Data/Managed")" > "$OUT/build.log" 2>&1; then
    grep -E "error" "$OUT/build.log" | sort -u; echo "BUILD FAILED"; exit 1
fi

kill_test_copies
rm -f "$OUT"/*-bepinex.log "$OUT"/*-player.log
for G in "$G1" "$G2"; do
    cp "$SRC"/COTLMP/bin/Release/net472/{COTLMP.dll,COTLMP.pdb,COTLMPServer.dll} "$G/BepInEx/plugins/COTLMP/"
    rm -f "$G/BepInEx/LogOutput.log" "$G/BepInEx/config/cotlmp-rejoin.json"
done

# Small windows side by side (WIN_W x WIN_H, the client WIN_X to the right of the host)
WIN_W=${WIN_W:-800}; WIN_H=${WIN_H:-450}; WIN_X=${WIN_X:-860}
WIN="-screen-fullscreen 0 -screen-width $WIN_W -screen-height $WIN_H"
LOGS=$(winpath "$OUT")
launch "$G1" "$WIN -logFile $LOGS\\host-player.log -cotlmp-window=0,0,$WIN_W,$WIN_H -cotlmp-fakesteam=1 -cotlmp-autohost -cotlmp-report -cotlmp-quitafter=600 ${HOST_EXTRA:-}"
if ! wait_for "$G1/BepInEx/LogOutput.log" "StartHosting returned True" 150; then echo "HOST DID NOT START"; fi

launch "$G2" "$WIN -logFile $LOGS\\client-player.log -cotlmp-window=$WIN_X,0,$WIN_W,$WIN_H -cotlmp-fakesteam=2 -cotlmp-join=1 -cotlmp-autoenter -cotlmp-report -cotlmp-quitafter=600 $EXTRA"
if ! wait_for "$G2/BepInEx/LogOutput.log" "Joined host" 150; then echo "CLIENT DID NOT CONNECT"; fi
wait_for "$G2/BepInEx/LogOutput.log" "QA client\].* scene=Base Biome 1 .*local=" 90

client_running() {
    powershell.exe -NoProfile -Command "@(Get-Process 'Cult Of The Lamb' -ErrorAction SilentlyContinue | Where-Object { \$_.Path -like '$(winpath "$G2")\\*' }).Count" 2>/dev/null | tr -d '\r'
}

# CLIENT_RESTART="args": if the client copy dies during the hold (a crash test), start it again with these
RESTARTED=0
for _ in $(seq 1 $HOLD); do
    timeout 1 cat /dev/zero > /dev/null 2>&1
    if [ -n "${CLIENT_RESTART:-}" ] && [ $RESTARTED = 0 ] && [ "$(client_running)" = "0" ]; then
        RESTARTED=1
        echo "client copy is gone, starting it again"
        cp "$G2/BepInEx/LogOutput.log" "$OUT/client-crashed-bepinex.log"
        launch "$G2" "$WIN -logFile $LOGS\\client2-player.log -cotlmp-window=$WIN_X,0,$WIN_W,$WIN_H -cotlmp-fakesteam=2 -cotlmp-report -cotlmp-quitafter=600 $CLIENT_RESTART"
    fi
done

cp "$G1/BepInEx/LogOutput.log" "$OUT/host-bepinex.log"
cp "$G2/BepInEx/LogOutput.log" "$OUT/client-bepinex.log"
kill_test_copies

echo "== HOST"; grep -E "QA host\]" "$OUT/host-bepinex.log" | tail -3
grep -E "STEAM|Error|Exception" "$OUT/host-bepinex.log" | tail -5
echo "== CLIENT"; grep -E "QA client\]" "$OUT/client-bepinex.log" | tail -3
grep -E "STEAM|Error|Exception" "$OUT/client-bepinex.log" | tail -5
for f in host client; do
    echo "== $f exceptions (Unity log, minus the vanilla LOD one)"
    grep -E "Exception" -A2 "$OUT/$f-player.log" | grep -vE "SkeletonAnimationLOD|^--" | grep -A2 Exception | head -12
done
