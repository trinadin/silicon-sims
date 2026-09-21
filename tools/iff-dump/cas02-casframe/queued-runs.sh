#!/bin/bash
# CAS-02 queued gated runs (zcode-uiwave-20260920a/casframe).
# Serialized-slot law: wait-then-launch 40x75s on the live owner; nothing is
# ever stopped or killed; two focused runs max; private userdirs; shared
# game-data READ-ONLY; worktree-private publish.
set -u
WT="/Users/nathannoom/Developer/Games/The Sims/worktrees/casframe"
EV="/Users/nathannoom/Developer/Games/The Sims/coordination/evidence/CAS-02"
BIN="$WT/dist/The Sims-arm64.app/Contents/MacOS/TheSims"
GDATA="/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims"

mkdir -p "$EV"

for i in $(seq 1 40); do
  others="$(pgrep -x TheSims || true)"
  if [ -z "$others" ]; then break; fi
  echo "$(date -u '+%H:%M:%SZ') slot busy (pid $others), wait $i/40"
  sleep 75
done
others="$(pgrep -x TheSims || true)"
if [ -n "$others" ]; then
  echo "SLOT NEVER FREED (pid $others); banking diff + queued run per accepted outcome"
  exit 3
fi

run() {
  local uddir="$1" log="$2" tag="$3"
  echo "$(date -u '+%H:%M:%SZ') launching $tag"
  SIMTONE_BIN="$BIN" SIMTONE_GDATA="$GDATA" SIMTONE_USERDIR="$uddir" \
    SIMTONE_CHECKS="uicasorig,uicasflow" SIMTONE_LOG="$log" \
    bash "$WT/tools/run-autotest.sh"
  local rc=$?
  echo "$(date -u '+%H:%M:%SZ') $tag rc=$rc"
  grep -E 'AUTOTEST (SUMMARY|RESULT)|uicasorig original-cas-screens|nameSingleLine|vitaCam|uicasflow' "$log" 2>/dev/null | tail -20
  # copy the casflow character-preview captures named in the log
  grep -oE 'capture 03-characteredit-preview[^>]*-> [^ ]+' "$log" 2>/dev/null \
    | sed 's/.*-> //' | while read -r p; do
        [ -f "$p" ] && cp "$p" "$EV/preview-$tag-$(basename "$p")" && echo "captured $p"
      done
  return $rc
}

cd "$WT"
run "$WT/userdirs/cas02-800"  "$EV/cas02-run-800.log"  800
rcA=$?
run "$WT/userdirs/cas02-1024" "$EV/cas02-run-1024.log" 1024
rcB=$?
echo "RUNS DONE rcA=$rcA rcB=$rcB"
