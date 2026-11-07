#!/bin/bash
# Launcher for the macOS port: runs JumpKing.exe on the bundled Mono, connected to the Steam client.
# macOS doesn't let the game write inside the .app, and it saves next to itself, so the
# bundled game is cloned to Application Support and run from there.
# Mono lives in Contents/MacOS as "Jump King" and this script execs it, so macOS treats the
# game as this app (name, icon, one Dock tile) instead of as "mono".

CONTENTS="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$CONTENTS/Resources/JumpKing"
MONO="$CONTENTS/MacOS/Jump King"
DATA="${JK_DATA_DIR:-$HOME/Library/Application Support/Jump King Mac}"
GAME_DIR="$DATA/game"
LOG="${JK_LOG:-$HOME/Library/Logs/JumpKing-mac.log}"
APP_ID=1061090
STEAM_WAIT=120
KEEP_DIRS=("Content/Saves" "Content/SavesPerma" "Content/JKMods" "Content/settings" "Content/ControllerBinds")

alert() {
    osascript -e "display alert \"Jump King\" message \"$1\" as critical" >/dev/null 2>&1
}

notify() {
    osascript -e "display notification \"$1\" with title \"Jump King\"" >/dev/null 2>&1
}

# Refresh the writable copy when the app was rebuilt, keeping saves, settings and mods.
if ! cmp -s "$SRC/.build-stamp" "$GAME_DIR/.build-stamp"; then
    mkdir -p "$DATA"
    NEW="$DATA/game.new"
    rm -rf "$NEW"
    cp -c -R "$SRC" "$NEW" 2>/dev/null || cp -R "$SRC" "$NEW" || { alert "Could not copy game files to $DATA"; exit 1; }
    for d in "${KEEP_DIRS[@]}"; do
        if [ -d "$GAME_DIR/$d" ]; then
            rm -rf "$NEW/$d"
            mv "$GAME_DIR/$d" "$NEW/$d"
        fi
    done
    rm -rf "$GAME_DIR"
    mv "$NEW" "$GAME_DIR"
fi

# Start Steam silently in the background if needed; the game checks its license through it.
STARTED_STEAM=0
if ! pgrep -q steam_osx; then
    notify "Starting Steam…"
    open -g -a Steam --args -silent || { alert "Steam is not installed. Install Steam and sign in to the account that owns Jump King."; exit 1; }
    STARTED_STEAM=1
fi

cd "$GAME_DIR" || exit 1
export SteamAppId=$APP_ID SteamGameId=$APP_ID
export MONO_XMLSERIALIZER_THS=no

# The game deletes steam_appid.txt on every start; the env vars let SteamAPI_Init find the app afterwards.
echo "$APP_ID" > steam_appid.txt

# A freshly started Steam needs time to sign in: wait until the Steam API is available.
if [ "$STARTED_STEAM" = 1 ]; then
    DEADLINE=$(( $(date +%s) + STEAM_WAIT ))
    until "$MONO" SteamProbe.exe >/dev/null 2>&1; do
        if [ "$(date +%s)" -ge "$DEADLINE" ]; then
            open -g "steam://exit"
            alert "Steam didn't finish signing in. Make sure Steam is signed in (online or offline mode) to the account that owns Jump King."
            exit 1
        fi
        sleep 3
    done
fi

# Once the game (this process, after exec) exits: report an early failure, and quit Steam if we started it.
GAME_PID=$$
START=$(date +%s)
(
    while kill -0 "$GAME_PID" 2>/dev/null; do sleep 2; done
    if [ $(( $(date +%s) - START )) -lt 8 ]; then
        alert "Jump King closed right after starting. Make sure Steam is signed in to the account that owns Jump King. Log: $LOG"
    fi
    if [ "$STARTED_STEAM" = 1 ]; then
        open -g "steam://exit"
    fi
) >/dev/null 2>&1 &

exec "$MONO" JumpKing.exe "$@" > "$LOG" 2>&1
