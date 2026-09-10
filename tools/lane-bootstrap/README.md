# lane-bootstrap — isolated worker lanes (ENV-02)

One command creates a fully isolated worker lane; one command verifies it.
A lane lets a worker build, run and test the port without touching any other
writer's source, caches, build outputs or the shared game data.

## Create a lane

    tools/lane-bootstrap/lane-bootstrap.sh --name <lane-name> [options]

What it produces under `<repo>/../lanes/<lane-name>/`:

    lane.env            source this in your shell; exports SIMTONE_* launcher
                        overrides plus lane-private NUGET_PACKAGES/DOTNET_CLI_HOME
    manifest.txt        baseline SHA, submodule SHAs, applied diff sha256s,
                        probe sha256s, owner, creation time — the lane identity
    src/                independent detached git worktree at --baseline with
                        recursive submodules; bin/obj/publish/dist, .nuget-packages
                        and .dotnet-cli all live (and stay) inside here
    game-data/          private APFS-clone copy of the mutable game data; the
                        shared original is read-only to the lane
    game-data.manifest  sha256 of every copied file (the "original unchanged" proof)
    probes/             lane-generated IFF probe fixtures, atomically installed
                        to the engine's fixed /tmp probe paths (identical bytes
                        are never churned; see make_lane_probes.py header)
    logs/, run/         run logs and scratch space

Key options:

- `--baseline <commit>` — defaults to the source repo's HEAD. Use the reviewed
  pinned baseline unless you know you need newer commits.
- `--apply-diff <patch>` (repeatable) — supplies "current changes" that bare
  HEAD omits, e.g. the uncommitted shared launcher fix:
  `--apply-diff ../../../coordination/evidence/ENV-01/env01.diff`
- `--fresh-nuget` — skip seeding the lane NuGet cache from the source repo's
  `.nuget-packages` (default seeds an APFS clone: instant, copy-on-write).
- `--no-game-copy` — skip the private game-data copy (not recommended; the
  data-hash proof then has nothing lane-private to verify).
- `--force` — replace an existing lane of the same name.
- `--submodule-mirror <dir>` — clone top-level submodules from `<dir>/<path>`
  (default: the source repo itself, so FreeSO never hits the network).

## Build and test inside the lane

    . <lane>/lane.env
    cd "$LANE_SRC"
    dotnet publish Client/Simitone/Simitone.Desktop/Simitone.Desktop.csproj \
      -c Release -r osx-arm64 --self-contained true -o "$LANE_SRC/publish/osx-arm64" \
      /p:TreatWarningsAsErrors=false /p:WarningsAsErrors="" -p:NoWarn=NU1605
    ./packmac.sh arm64
    SIMTONE_CHECKS="glob-fix,chunk-reg" ./tools/run-autotest.sh

`packmac.sh` expects `$PWD/.nuget-packages`, which is exactly where the lane
cache lives — the lane binary gets its Eto/SDL dylibs from lane-private bytes.

## Verify the lane

    python3 tools/lane-bootstrap/verify_lane.py --lane <lane> \
        --original "<repo>/game-data/The Sims" \
        [--last-log <lane>/logs/last-run.log --expect-pass]

Checks: manifest/checkout identity, symlink-escape audit (no lane link reaches
another writer's source/output), probe integrity, lane game-data and shared
original byte-identity against the bootstrap manifest, and (with a run log)
that the run used a private userdir and produced the expected verdict.
`--skip-data-hash` skips the (multi-GB) hash passes for quick iterations.

## Limitations

- The engine reads its two probe IFFs from fixed `/tmp` paths; lanes share
  those bytes by design (identical content, atomic installs). True per-lane
  /tmp namespacing needs an AutotestRunner probe-path override.
- The lane's git worktree shares the source repo's object store via its `.git`
  pointer file (standard git mechanics, exempt from the symlink audit); all
  working files, caches and outputs are lane-private.
