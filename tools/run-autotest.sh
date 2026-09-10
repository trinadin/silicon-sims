#!/bin/bash
# ENV-01 safe autotest launcher (coordinator-owned shared entry point).
# Replaces the previous wrapper that hardcoded the main checkout's absolute
# paths and masked the game's exit status with a trailing echo.
#
# Defaults are checkout-relative. SIMTONE_* overrides exist for workers and
# path probes and are never second-guessed by a fallback: if the configured
# binary or data directory is missing, this script fails without launching
# anything, so a stub/demo run can never start the installed game by accident.
#
# Exit codes: 0 pass; the child's own code on child failure;
# 2 bad config or missing paths; 3 another TheSims process already active;
# 4 missing/unfinished verdict; 5 foreign TheSims process appeared mid-run;
# 124 wall-budget timeout;
# 6 zero-verdict / skipped-checks summary (REL-11).
set -u

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
bin="${SIMTONE_BIN:-$repo/dist/The Sims-arm64.app/Contents/MacOS/TheSims}"
gdata="${SIMTONE_GDATA:-$repo/game-data/The Sims}"
checks="${SIMTONE_CHECKS:-}"
userdir="${SIMTONE_USERDIR:-$(mktemp -d "${TMPDIR:-/tmp}/simtone-autotest.XXXXXX")}"
log="${SIMTONE_LOG:-$userdir/run.log}"
# -autotest-timeout 1800000 is REQUIRED since R84: carseek waits the VM clock ~4 sim-min past
# the pins (carpool-portal soak); the 5-min default would abort the gate mid-window (and the
# headless SDL/Cocoa event wait can also stall ~10-16 min, see AUTOTEST.md).
timeout_ms="${SIMTONE_TIMEOUT_MS:-1800000}"
budget_s="${SIMTONE_WALL_BUDGET_S:-2100}"

fail() { printf 'run-autotest: %s\n' "$1" >&2; exit "${2:-2}"; }

[ -x "$bin" ] || fail "game binary missing or not executable: $bin (publish first; refusing to guess)"
[ -d "$gdata" ] || fail "game data directory missing: $gdata"
mkdir -p "$userdir" || fail "cannot create private userdir: $userdir"
[ "$timeout_ms" -gt 0 ] 2>/dev/null || fail "SIMTONE_TIMEOUT_MS must be a positive integer: $timeout_ms"
[ "$budget_s" -gt 0 ] 2>/dev/null || fail "SIMTONE_WALL_BUDGET_S must be a positive integer: $budget_s"
[ "$timeout_ms" -lt $((budget_s * 1000)) ] || fail "SIMTONE_TIMEOUT_MS ($timeout_ms) must stay under the ${budget_s}s wall budget or the in-game timeout can never fire first"

# Same guard as the reviewed validate.py recipe, refined 2026-09-08 (UI-02
# finding, coordinator harness maintenance): match the game's PROCESS NAME
# exactly (-x), not any command line containing the substring. Worker poll
# scripts and other checkouts' build rails legitimately carry
# "The Sims"/"TheSims" in their argument text and were killing valid runs as
# phantom strays. A real game process from ANY checkout still matches: its
# executable name is always TheSims. Nothing is ever stopped or reaped by
# pattern — only this script's own child is.
others="$(pgrep -x TheSims || true)"
[ -z "$others" ] || fail "another TheSims process is active (pid $others); wait for its owner; nothing was stopped" 3
: >"$log" 2>/dev/null || fail "cannot write log file: $log" 2

set -- "$bin" -ApplePersistenceIgnoreState YES -path"$gdata" -autotest 5 \
  -autotest-userdir "$userdir" -autotest-timeout "$timeout_ms"
[ -z "$checks" ] || set -- "$@" -autotest-opts "$checks"

printf 'run-autotest: repo=%s\nrun-autotest: binary=%s\nrun-autotest: data=%s\nrun-autotest: userdir=%s\nrun-autotest: log=%s\nrun-autotest: checks=%s\n' \
  "$repo" "$bin" "$gdata" "$userdir" "$log" "${checks:-<default suite>}"

NSUnbufferedIO=YES "$@" >"$log" 2>&1 &
child=$!
# A ctrl-C'd or terminated worker must not orphan the game child in the
# serialized run slot: pass the interrupt on to our child only.
trap 'kill -0 "$child" 2>/dev/null && kill -TERM "$child" 2>/dev/null' INT TERM
start=$(date +%s)
while kill -0 "$child" 2>/dev/null; do
  if [ "$(( $(date +%s) - start ))" -ge "$budget_s" ]; then
    kill -TERM "$child" 2>/dev/null
    grace=0
    while [ "$grace" -lt 5 ] && kill -0 "$child" 2>/dev/null; do sleep 1; grace=$((grace + 1)); done
    kill -KILL "$child" 2>/dev/null
    wait "$child" 2>/dev/null
    fail "wall budget ${budget_s}s exceeded; stopped only this test process (pid $child); verdict not trusted" 124
  fi
  strays="$(pgrep -x TheSims | grep -vwx "$child" || true)"
  if [ -n "$strays" ]; then
    kill -TERM "$child" 2>/dev/null
    wait "$child" 2>/dev/null
    fail "another TheSims process appeared (pid $strays); stopped only this test process" 5
  fi
  sleep 1
done
wait "$child"; rc=$?

if [ "$rc" -ne 0 ]; then
  printf 'run-autotest: child exited %s; log: %s\n' "$rc" "$log" >&2
  exit "$rc"
fi
if grep -q 'AUTOTEST RESULT FAIL' "$log"; then
  fail "verdict FAIL; log: $log" 1
fi
grep -q 'AUTOTEST RESULT PASS' "$log" || fail "no AUTOTEST RESULT verdict; unfinished run rejected; log: $log" 4
# REL-11: PASS with zero verdicted checks (unknown check name, all-skip) is not a
# pass. The runner now counts skipped checks and only prints PASS when passed>0;
# this guard independently rejects any passed=0 / skipped>0 summary.
summary="$(grep -m1 'AUTOTEST SUMMARY' "$log" || true)"
rpassed="$(printf '%s' "$summary" | sed -E 's/.*passed=([0-9]+).*/\1/')"
rskipped="$(printf '%s' "$summary" | sed -E 's/.*skipped=([0-9]+).*/\1/')"
case "$rpassed" in ''|*[!0-9]*) fail "AUTOTEST SUMMARY missing or malformed; cannot trust verdict; log: $log" 6;; esac
[ "$rpassed" -gt 0 ] || fail "passed=0: no check verdicted; refusing to treat as PASS; log: $log" 6
case "$rskipped" in ''|*[!0-9]*) fail "AUTOTEST SUMMARY missing skipped count; log: $log" 6;; esac
[ "$rskipped" -eq 0 ] || fail "skipped=$rskipped checks did not verdict; refusing PASS; log: $log" 6
grep -E 'AUTOTEST (SUMMARY|RESULT|recordfmt passed=)' "$log" || true
printf 'run-autotest: PASS (log: %s, private userdir kept: %s)\n' "$log" "$userdir"
