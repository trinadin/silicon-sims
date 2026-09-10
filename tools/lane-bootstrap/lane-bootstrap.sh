#!/bin/bash
# ENV-02 lane bootstrap: create a fully isolated worker lane for the Simitone
# macOS port. A lane is an independent detached git checkout (recursive
# submodules at the pinned baseline) plus lane-private NuGet/DOTNET_CLI_HOME
# caches, its own copy of the mutable game data, lane-namespaced IFF probe
# fixtures, and explicit application of "current changes" patches (e.g. the
# uncommitted ENV-01 launcher fix) so a lane never silently diverges from the
# reviewed working-tree state that bare HEAD omits.
#
# Isolation contract (ENV-02 acceptance):
#   - source/submodules: detached worktree of --baseline, submodules pinned;
#   - bin/obj/publish/dist + NuGet + DOTNET_CLI_HOME: all under the lane;
#   - game data: private APFS-clone copy; the shared original is never written;
#   - probes: generated per lane, installed to the engine's fixed /tmp probe
#     paths only via atomic rename and only when content differs;
#   - every symlink in the lane resolves inside the lane (verify_lane.py).
#
# Usage: tools/lane-bootstrap/lane-bootstrap.sh --name <lane> [options]
#   --lane-root <dir>        parent directory for lanes
#                            (default: <repo>/../lanes, i.e. beside the repo)
#   --baseline <commit>      commit to check out (default: source repo HEAD)
#   --apply-diff <patch>     git-apply this patch inside the lane after
#                            checkout (repeatable, applied in order)
#   --submodule-mirror <dir> clone top-level submodules from <dir>/<path> when
#                            that path is a git repo (default: the source repo
#                            itself, so FreeSO comes from the local disk)
#   --game-data <dir>        game data tree to clone privately
#                            (default: <repo>/game-data/The Sims)
#   --nuget-source <dir>     NuGet cache to seed the lane from
#                            (default: <repo>/.nuget-packages; ignored with
#                            --fresh-nuget; useful when <repo> is itself a
#                            worktree without untracked caches)
#   --no-game-copy           skip the private game-data copy (probes then come
#                            from --game-data directly and are still lane files)
#   --fresh-nuget            start from an empty lane NuGet cache instead of
#                            seeding an APFS clone of <repo>/.nuget-packages
#   --no-nuget-seed          synonym for --fresh-nuget
#   --force                  replace an existing lane of the same name
#   -x                       shell trace
# Exit codes: 0 ok; 2 bad usage or environment; 3 lane exists without --force.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$SCRIPT_DIR/../.." && pwd)"
NAME=""
LANE_ROOT=""
BASELINE=""
DIFFS=()
MIRROR="$REPO"
GDATA_SRC=""
NUGET_SRC=""
GAME_COPY=1
NUGET_SEED=1
FORCE=0

die() { printf 'lane-bootstrap: %s\n' "$1" >&2; exit "${2:-2}"; }

default_lane_root() { # lanes live beside the workspace repo; climb out of a lanes/ parent
  local p="$(cd "$REPO/.." && pwd)"
  while [ "$(basename "$p")" = "lanes" ]; do p="$(cd "$p/.." && pwd)"; done
  printf '%s/lanes' "$p"
}

while [ $# -gt 0 ]; do
  case "$1" in
    --name) NAME="${2:?--name needs a value}"; shift 2;;
    --lane-root) LANE_ROOT="${2:?--lane-root needs a value}"; shift 2;;
    --baseline) BASELINE="${2:?--baseline needs a value}"; shift 2;;
    --apply-diff) DIFFS+=("${2:?--apply-diff needs a value}"); shift 2;;
    --submodule-mirror) MIRROR="${2:?--submodule-mirror needs a value}"; shift 2;;
    --game-data) GDATA_SRC="${2:?--game-data needs a value}"; shift 2;;
    --nuget-source) NUGET_SRC="${2:?--nuget-source needs a value}"; shift 2;;
    --no-game-copy) GAME_COPY=0; shift;;
    --fresh-nuget|--no-nuget-seed) NUGET_SEED=0; shift;;
    --force) FORCE=1; shift;;
    -x) set -x; shift;;
    *) die "unknown option: $1 (see header for usage)";;
  esac
done

[ -n "$NAME" ] || die "--name <lane> is required"
case "$NAME" in
  *[!A-Za-z0-9._-]*|'') die "lane name must be [A-Za-z0-9._-] only: $NAME";;
esac
command -v git >/dev/null || die "git not found"
command -v python3 >/dev/null || die "python3 not found"
command -v shasum >/dev/null || die "shasum not found"

git -C "$REPO" rev-parse --verify HEAD >/dev/null 2>&1 || die "not a git repo: $REPO"
BASELINE="$(git -C "$REPO" rev-parse --verify "${BASELINE:-HEAD}^{commit}")"
if [ -z "$GDATA_SRC" ]; then
  GDATA_SRC="$REPO/game-data/The Sims"
  [ -d "$GDATA_SRC" ] || die "default game data missing: $GDATA_SRC (pass --game-data or --no-game-copy)"
fi
[ -d "$GDATA_SRC" ] || die "game data missing: $GDATA_SRC"

LANE_ROOT="${LANE_ROOT:-$(default_lane_root)}"
LANE="$LANE_ROOT/$NAME"
if [ -e "$LANE" ]; then
  [ "$FORCE" = 1 ] || die "lane exists: $LANE (pass --force to replace)" 3
  rm -rf "$LANE"
fi
mkdir -p "$LANE/logs" "$LANE/run" "$LANE/probes"

OWNER="${LANE_OWNER:-$(id -un)@$(hostname -s)}"
NOW="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

echo "== lane $NAME: worktree at $BASELINE"
git -C "$REPO" worktree add --detach "$LANE/src" "$BASELINE" >/dev/null

echo "== lane $NAME: recursive submodules (mirror: $MIRROR)"
SUB_CARGS=(-c protocol.file.allow=always)
while IFS= read -r entry; do
  key="${entry%% *}"
  path="${entry#* }"
  name="${key#submodule.}"; name="${name%.path}"
  if [ -e "$MIRROR/$path/.git" ] || [ -d "$MIRROR/$path" ] && [ -e "$MIRROR/$path/.git" ]; then
    SUB_CARGS+=(-c "submodule.$name.url=$MIRROR/$path")
    echo "   $name <- $MIRROR/$path"
  fi
done < <(git -C "$LANE/src" config -f .gitmodules --get-regexp '^submodule\..*\.path$' || true)
git -C "$LANE/src" "${SUB_CARGS[@]}" submodule update --init --recursive

echo "== lane $NAME: applying current-changes diffs"
: >"$LANE/manifest.diffs"
for d in "${DIFFS[@]}"; do
  [ -f "$d" ] || die "diff not found: $d"
  git -C "$LANE/src" apply --whitespace=nowarn "$d"
  printf 'diff=%s sha256=%s\n' "$d" "$(shasum -a 256 "$d" | cut -d' ' -f1)" | tee -a "$LANE/manifest.diffs"
done
[ "${#DIFFS[@]}" -eq 0 ] && echo "   (none)"

clone_copy() { # clone_copy <src> <dst>: APFS clonefile copy, fallback plain copy
  mkdir -p "$2"
  if ! cp -Rc "$1/." "$2/" 2>/dev/null; then
    rm -rf "$2"
    mkdir -p "$2"
    cp -R "$1/." "$2/"
  fi
}

if [ "$GAME_COPY" = 1 ]; then
  echo "== lane $NAME: private game-data copy (APFS clone of $GDATA_SRC)"
  clone_copy "$GDATA_SRC" "$LANE/game-data/The Sims"
  PROBE_DATA="$LANE/game-data/The Sims"
else
  echo "== lane $NAME: no private game-data copy (--no-game-copy)"
  PROBE_DATA="$GDATA_SRC"
fi

echo "== lane $NAME: hashing lane game data into game-data.manifest"
if [ "$GAME_COPY" = 1 ]; then
  ( cd "$LANE/game-data/The Sims" && find . -type f -print0 | sort -z | xargs -0 shasum -a 256 ) \
    >"$LANE/game-data.manifest"
  DATA_NOTE="manifest lines=$(wc -l <"$LANE/game-data.manifest" | tr -d ' ')"
else
  : >"$LANE/game-data.manifest"
  DATA_NOTE="no private copy; manifest empty"
fi
echo "   $DATA_NOTE"

NUGET_SRC="${NUGET_SRC:-$REPO/.nuget-packages}"
if [ "$NUGET_SEED" = 1 ] && [ -d "$NUGET_SRC" ]; then
  echo "== lane $NAME: seeding lane NuGet cache (APFS clone of $NUGET_SRC)"
  clone_copy "$NUGET_SRC" "$LANE/src/.nuget-packages"
else
  echo "== lane $NAME: empty lane NuGet cache (restore will download)"
  mkdir -p "$LANE/src/.nuget-packages"
fi
mkdir -p "$LANE/src/.dotnet-cli"

echo "== lane $NAME: generating + installing IFF probes"
python3 "$SCRIPT_DIR/make_lane_probes.py" --game-data "$PROBE_DATA" \
  --probe-dir "$LANE/probes" --install | tee "$LANE/probes/install.log"

SUBSTATUS="$(git -C "$LANE/src" submodule status)"
{
  echo "lane=$NAME"
  echo "created_utc=$NOW"
  echo "owner=$OWNER"
  echo "source_repo=$REPO"
  echo "baseline=$BASELINE"
  echo "submodule_mirror=$MIRROR"
  echo "game_data_source=$GDATA_SRC"
  echo "game_copy=$GAME_COPY"
  echo "nuget_seed=$NUGET_SEED"
  echo "bootstrap_utility=$SCRIPT_DIR"
  echo "# submodule status at creation:"
  printf '%s\n' "$SUBSTATUS"
  echo "# applied diffs:"
  cat "$LANE/manifest.diffs"
  echo "# probe sha256:"
  ( cd "$LANE/probes" && shasum -a 256 ./*.iff 2>/dev/null || echo "(none)" )
  echo "# lane game-data manifest sha256:"
  shasum -a 256 "$LANE/game-data.manifest" | cut -d' ' -f1
} >"$LANE/manifest.txt"

cat >"$LANE/lane.env" <<EOF
# Worker lane "$NAME" (created $NOW by $OWNER). Source me: . "$LANE/lane.env"
export LANE_ROOT="$LANE"
export LANE_SRC="\$LANE_ROOT/src"
# launcher overrides (tools/run-autotest.sh): lane binary + lane-private data.
export SIMTONE_BIN="\$LANE_SRC/dist/The Sims-arm64.app/Contents/MacOS/TheSims"
export SIMTONE_GDATA="$([ "$GAME_COPY" = 1 ] && printf '%s' "$LANE/game-data/The Sims" || printf '%s' "$GDATA_SRC")"
export SIMTONE_LOG="\$LANE_ROOT/logs/last-run.log"
# build env: caches inside the lane; packmac.sh expects \$PWD/.nuget-packages.
export NUGET_PACKAGES="\$LANE_SRC/.nuget-packages"
export DOTNET_CLI_HOME="\$LANE_SRC/.dotnet-cli"
# SIMTONE_USERDIR stays unset: the launcher makes a private mktemp userdir.
EOF

echo
echo "lane ready: $LANE"
echo "  verify:  python3 $SCRIPT_DIR/verify_lane.py --lane $LANE"
echo "  build:   . $LANE/lane.env && cd \"\$LANE_SRC\" && dotnet publish Client/Simitone/Simitone.Desktop/Simitone.Desktop.csproj -c Release -r osx-arm64 --self-contained true -o \"\$LANE_SRC/publish/osx-arm64\" /p:TreatWarningsAsErrors=false /p:WarningsAsErrors=\"\" -p:NoWarn=NU1605 && ./packmac.sh arm64"
echo "  test:    . $LANE/lane.env && cd \"\$LANE_SRC\" && SIMTONE_CHECKS=\"<checks>\" ./tools/run-autotest.sh"
