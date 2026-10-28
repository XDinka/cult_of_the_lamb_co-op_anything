#!/usr/bin/env bash
# Build the mod, deploy it to two test copies of the game, run host + client over the loopback
# stand-in for Steam, then print the last QA reports from each side.
#
# Usage: run-pair.sh [seconds-after-join] [extra client args...]
#   HOST_EXTRA="..."  extra switches for the host, e.g. "-cotlmp-autodungeon=5 -cotlmp-autokill"
#   LOBBY=6|new       the host opens the lobby from the main menu (on the hidden save slot 6, or a new
#                     game there) and the client joins it from the online menu; Play follows
#
# Paths (environment):
#   COTL_G1, COTL_G2  two game folders with BepInEx, COTL_API and the COTLMP plugin folder
#                     (POSIX paths, e.g. /c/games/cotl-dev). The second can be a hardlinked copy
#                     with its own BepInEx folder.
#   QA_OUT            where logs go (default: ./out/qa)
#   DOTNET            the dotnet executable (default: dotnet)
#   MIN_AVAIL_MB      the pair starts only with this much memory available (free and standby,
#   MEM_WAIT          4096 MB), waiting for it up to MEM_WAIT seconds (600) and checking every
#                     minute; else no game starts (exit 3)
#   CRASH_CHECK=DIR   only look for crashes in the logs a run left in DIR (no build, no games)
#
# A game that crashed (the crash handler's "Crash!!!" in its log, a new crash report, or its process
# gone before the end) is told as "CRASH host|client ..." and the script exits with 4.
set -u
SRC=$(cd "$(dirname "$0")/.." && pwd)
G1=${COTL_G1:?set COTL_G1}
G2=${COTL_G2:?set COTL_G2}
OUT=${QA_OUT:-$SRC/out/qa}
DOTNET=${DOTNET:-dotnet}
HOLD=${1:-30}; shift || true
EXTRA="$*"

winpath() { cygpath -w "$1" 2>/dev/null || echo "$1"; }

crash_logs() { # dir: the crash handler's mark in either side's Unity log
    local Found=0
    for f in host client; do
        grep -q "Crash!!!" "$1/$f-player.log" 2>/dev/null && { echo "CRASH $f $1/$f-player.log"; Found=1; }
    done
    return $Found
}
if [ -n "${CRASH_CHECK:-}" ]; then
    crash_logs "$CRASH_CHECK" && echo "no crash in $CRASH_CHECK" && exit 0
    exit 4
fi

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
for P in COTLMP/COTLMP.csproj Loader/COTLMP.Loader.csproj; do
    if ! $DOTNET build "$SRC/$P" -c Release "-p:CotlManagedDir=$(winpath "$G1/Cult Of The Lamb_Data/Managed")" > "$OUT/build.log" 2>&1; then
        grep -E "error" "$OUT/build.log" | sort -u; echo "BUILD FAILED"; exit 1
    fi
done

kill_test_copies
rm -f "$OUT"/*-bepinex.log "$OUT"/*-player.log

# Two games take ~2.5 GB: with less available, the crashes say nothing about the mod
avail_mb() {
    powershell.exe -NoProfile -Command "(Get-CimInstance Win32_PerfFormattedData_PerfOS_Memory).AvailableMBytes" 2>/dev/null | tr -d '\r'
}
MIN_AVAIL_MB=${MIN_AVAIL_MB:-4096}; MEM_WAIT=${MEM_WAIT:-600}
Waited=0
while :; do
    Avail=$(avail_mb)
    [ "${Avail:-0}" -ge "$MIN_AVAIL_MB" ] && break
    if [ "$Waited" -ge "$MEM_WAIT" ]; then echo "NOT ENOUGH MEMORY: available ${Avail:-?} MB, need $MIN_AVAIL_MB; no game started"; exit 3; fi
    echo "WAIT available ${Avail:-?} MB, need $MIN_AVAIL_MB"
    for _ in $(seq 1 60); do timeout 1 cat /dev/zero > /dev/null 2>&1; done
    Waited=$((Waited + 60))
done
echo "available $Avail MB"
CRASHES="$(cygpath -u "$LOCALAPPDATA")/Temp/Massive Monster/Cult Of The Lamb/Crashes"
CrashesBefore=$(ls "$CRASHES" 2>/dev/null | sort)
for G in "$G1" "$G2"; do
    bash "$SRC/tools/deploy-mod.sh" "$G/BepInEx/plugins/COTLMP"
    rm -f "$G/BepInEx/LogOutput.log" "$G/BepInEx/config/cotlmp-rejoin.json"
done

# Small windows side by side (WIN_W x WIN_H, the client WIN_X to the right of the host)
WIN_W=${WIN_W:-800}; WIN_H=${WIN_H:-450}; WIN_X=${WIN_X:-860}
WIN="-screen-fullscreen 0 -screen-width $WIN_W -screen-height $WIN_H"
LOGS=$(winpath "$OUT")
# The game's save folder is the player's own: tests only ever write the hidden slot 6
SAVES="$(cygpath -u "$LOCALAPPDATA")/../LocalLow/Massive Monster/Cult Of The Lamb/saves"
if [ "${LOBBY:-}" = new ]; then
    rm -f "$SAVES/slot_6.mp" "$SAVES/meta_6.mp"
elif [ -n "${LOBBY:-}" ] && [ ! -f "$SAVES/slot_6.mp" ] && [ -f "$SAVES/slot_0.mp" ]; then
    cp "$SAVES/slot_0.mp" "$SAVES/slot_6.mp"; cp "$SAVES/meta_0.mp" "$SAVES/meta_6.mp"
fi
if [ -n "${LOBBY:-}" ]; then
    HOST_MODE="-cotlmp-lobby=$LOBBY"; HOST_READY="Lobby open on"; CLIENT_MODE="-cotlmp-lobbyjoin"
else
    HOST_MODE="-cotlmp-autohost"; HOST_READY="StartHosting returned True"; CLIENT_MODE="-cotlmp-join=1"
fi
launch "$G1" "$WIN -logFile $LOGS\\host-player.log -cotlmp-window=0,0,$WIN_W,$WIN_H -cotlmp-fakesteam=1 $HOST_MODE -cotlmp-report -cotlmp-quitafter=600 ${HOST_EXTRA:-}"
if ! wait_for "$G1/BepInEx/LogOutput.log" "$HOST_READY" 150; then echo "HOST DID NOT START"; fi

# CLIENT_MENU=1: the client joins from the main menu and plays on the host's save
CLIENT_ENTER=$([ -n "${CLIENT_MENU:-}" ] || [ -n "${LOBBY:-}" ] || echo -cotlmp-autoenter)
launch "$G2" "$WIN -logFile $LOGS\\client-player.log -cotlmp-window=$WIN_X,0,$WIN_W,$WIN_H -cotlmp-fakesteam=2 $CLIENT_MODE $CLIENT_ENTER -cotlmp-report -cotlmp-quitafter=600 $EXTRA"
if ! wait_for "$G2/BepInEx/LogOutput.log" "Joined host" 150; then echo "CLIENT DID NOT CONNECT"; fi
wait_for "$G2/BepInEx/LogOutput.log" "QA client\].* scene=Base Biome 1 .*local=" 90

client_running() {
    powershell.exe -NoProfile -Command "@(Get-Process 'Cult Of The Lamb' -ErrorAction SilentlyContinue | Where-Object { \$_.Path -like '$(winpath "$G2")\\*' }).Count" 2>/dev/null | tr -d '\r'
}

for _ in $(seq 1 $HOLD); do
    timeout 1 cat /dev/zero > /dev/null 2>&1
done

running() { # folder
    powershell.exe -NoProfile -Command "@(Get-Process 'Cult Of The Lamb' -ErrorAction SilentlyContinue | Where-Object { \$_.Path -like '$(winpath "$1")\\*' }).Count" 2>/dev/null | tr -d '\r'
}
Crashed=0
[ "$(running "$G1")" = 0 ] && { echo "CRASH host (its game was gone before the end)"; Crashed=1; }
[ "$(running "$G2")" = 0 ] && { echo "CRASH client (its game was gone before the end)"; Crashed=1; }

cp "$G1/BepInEx/LogOutput.log" "$OUT/host-bepinex.log"
cp "$G2/BepInEx/LogOutput.log" "$OUT/client-bepinex.log"
kill_test_copies

echo "== HOST"; grep -E "QA host\]" "$OUT/host-bepinex.log" | tail -3
grep -E "STEAM|Error|Exception" "$OUT/host-bepinex.log" | tail -5
echo "== CLIENT"; grep -E "QA client\]" "$OUT/client-bepinex.log" | tail -3
grep -E "STEAM|Error|Exception" "$OUT/client-bepinex.log" | tail -5
crash_logs "$OUT" || Crashed=1
for Dir in $(comm -13 <(echo "$CrashesBefore") <(ls "$CRASHES" 2>/dev/null | sort)); do
    Who=$(ls "$CRASHES/$Dir" 2>/dev/null | grep -oE "^(host|client)-player" | head -1)
    echo "CRASH ${Who%-player} report $CRASHES/$Dir"; Crashed=1
done
for f in host client; do
    echo "== $f exceptions (Unity log, minus the vanilla LOD one)"
    grep -E "Exception" -A2 "$OUT/$f-player.log" | grep -vE "SkeletonAnimationLOD|^--" | grep -A2 Exception | head -12
done
[ "$Crashed" = 0 ] || exit 4
