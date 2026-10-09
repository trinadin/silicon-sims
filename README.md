# SimSilicon

**The Sims™ (2000), natively on Apple Silicon Macs: a byte-faithful port of the open-source
Simitone engine, running your legally-owned original game data.**

Not affiliated with EA, Maxis, or the Simitone team. This project is a
fork of [alexjyong/Simitone](https://github.com/alexjyong/Simitone) (itself a fork of
[riperiperi/Simitone](https://github.com/riperiperi/Simitone)), built on the
[FreeSO](https://freeso.org) simulation engine. "The Sims" is a trademark of EA — this is an
unaffiliated fan engine that runs the game from **your own copy** of the original data.

## Download

Grab the latest `SimSilicon-macOS-AppleSilicon.dmg` from
[**Releases**](https://github.com/trinadin/simsilicon/releases), mount it, and drag
**The Sims** to your Applications folder.

- **Requirements:** an Apple Silicon Mac (M1 or later), macOS 11 or later. No .NET runtime,
  SDL, OpenAL, or any other dependencies — the app is self-contained.
- **Game data:** you need your own copy of *The Sims: Complete Collection* (or Legacy
  Collection) — see [Game data](#game-data). Nothing EA-owned is bundled.

## What this project is

SimSilicon is **not a remake**. It is an engine that:

- loads your legally-owned copy of *The Sims: Complete Collection* game data (IFF format), and
- compiles and runs the **original game logic** — the original `Behavior.iff` scripted behavior
  (BHAV bytecode) is executed by the engine's VM — so gameplay is driven by the original data
  and the original logic.

Where behavior differs from the 2000 PowerPC original, the original binary is disassembled and the
engine is corrected to match it — constants, thresholds, formulas and all (see
[PROVENANCE.md](docs/PROVENANCE.md) and [PARITY.md](PARITY.md)).

## Status — and how you can help

**Playable:** boots to gameplay, loads and saves neighbourhoods and houses, plays the core
single-family game — verified by a 156-check automated battery that boots the real game
headlessly and exercises gameplay, UI, saves, sound, and the autonomy engine on every
change (run it yourself with `tools/run-autotest.sh`; the one-page state summary is
[PARITY.md](PARITY.md) → "Where the game stands"). Every release artifact is validated by running the battery against the packed `.app` itself, not just the build tree.

**Honest status:** this is an **engine parity project, not a finished 1:1 clone** — and it
needs real-world testing on real Macs and real saves. That's where you come in:

- **Play it.** Every hour of real play on hardware we don't have is a contribution.
- **Report what breaks.** Open a [GitHub Issue](https://github.com/trinadin/simsilicon/issues) —
  the bug template asks for your Mac model, macOS version, and game-data source, and the two logs
  below. Vague reports can't be fixed; pinned ones can.
- **Attach the logs.** `~/Documents/Simitone/game.log` (screen/content-load tracing) and the
  crash log next to it. They're plain text — paste the tail (last ~50 lines) or the whole thing.
- **Know the known gaps before filing.** The current gap list lives in
  [PARITY.md](PARITY.md) → "Where the game stands". As of 2026-10-08: footsteps,
  the neighbourhood layout montage, the UL filter toolbar, Superstar's fame
  surfaces and gendered job titles, Vacation's save-on-vacation/bookings and
  score/souvenir systems, and Makin' Magic's quest lines are on decoded native
  laws; the view-pie pop gesture stays a disclosed model (the original's
  window-manager vtable was not shipped — not statically recoverable). One
  bounded, disclosed residual: the vacation souvenir carry-home object's
  persistence tail (the purchase/carry-home spawn law is verified live; the
  created souvenir object's in-hand lifecycle is not yet modeled). Human
  verification of the whole is
  still partial (heavily automated; played and accepted hands-on in portions).
  Duplicate-gap reports will be closed with a pointer.
- **Code welcome.** MPL-2.0 — PRs are open. PARITY.md's gap table is the roadmap; small,
  well-evidenced fixes (see the PROVENANCE discipline) land fastest.

## Game data

SimSilicon needs The Sims 1 **Complete Collection / Legacy Collection** data: a folder containing
`GameData/.../Behavior.iff` and the `UserData` layout. See [PORT_STATUS.md](docs/PORT_STATUS.md)
("Required: game data") for how to obtain it legally —
[fetch-game-data.sh](fetch-game-data.sh) stages the Complete Collection DVD from archive.org
(you must own/obtain the game; it just avoids needing a disc drive). The selected path persists
in the user-data `config.ini` (`TS1HybridPath`); user data and saves live in `~/Documents/Simitone/`.

## Running

- Launch **The Sims** from Applications (or your build's `dist/The Sims-arm64.app`).
- First run: pick your game-data folder, or it is auto-detected / pre-seeded in `config.ini`.
- Flags: `-gl` (OpenGL), `-3d` (3D mode, toggle F12), `-nosound`. `-jit` and `-dx` are inherited
  from upstream and not yet soak-verified on macOS.

## Building from source

One repository, no submodules:

```sh
git clone https://github.com/trinadin/simsilicon.git
cd simsilicon && git checkout main
```

Prerequisites: .NET SDK 9. The exact publish + package commands
(and why the `NUGET_PACKAGES`/`DOTNET_CLI_HOME` redirects and `WarningsAsErrors` relaxations are
needed) are in [PORT_STATUS.md](docs/PORT_STATUS.md) → "Build / package".

## Repository layout

Everything builds from this one repository:

- `Client/` — the Simitone client (macOS shell, UI, autotest battery).
- `FreeSO/` — the FreeSO engine fork, merged in-repo with its **full git history** (grafted as a
  subtree; engine commits remain reachable with their original author dates).
- `FreeSO/Other/libs/FSOMonoGame/` — the MonoGame fork, vendored at the exact pin the port builds
  against (upstream + the macOS trackpad natural-scroll fix). Upstream remains at
  [riperiperi/FSOMonoGame](https://github.com/riperiperi/FSOMonoGame).
- `FreeSO/Other/libs/FSOMina.NET/` — vendored unmodified from upstream.
- `tools/` — the decode/verification tooling behind the fidelity program.

## License & credits

- Engine: [FreeSO](https://freeso.org) — MPL-2.0, merged in-repo at `FreeSO/` with history
  (this repo is LICENSE.md MPL-2.0 as a whole; per MPL §3.2/Exhibit A file headers are optional
  and only a minority of files carry one).
- Original game & data: © Maxis / EA — **not bundled**; the app icon is a rendering of the
  in-game plumbob generated from game data at build time (fan-project use, no EA asset files
  are distributed).
- Upstream: [riperiperi/Simitone](https://github.com/riperiperi/Simitone),
  [alexjyong/Simitone](https://github.com/alexjyong/Simitone),
  [riperiperi/FreeSO](https://github.com/riperiperi/FreeSO) and
  [riperiperi/FSOMonoGame](https://github.com/riperiperi/FSOMonoGame).
- macOS port & parity work: this repository.
- Icon/audio attributions: the app icon is generated programmatically at pack
  time by `tools/make_native_icon.py` (no proprietary or third-party bytes); the
  legacy/fallback icon and UI art are Icons8 (upstream Simitone attribution);
  `rain_loop.wav` / `thunder.wav` are CC0 freesound.org loops (upstream Simitone)

## Source code (MPL §3.2)

The complete corresponding source for this application is this repository — including the merged
engine tree and the vendored libraries — at the packaged revision (the `main` branch tip). Clone,
`git checkout` that commit, and build with `packmac.sh` per docs/PORT_STATUS.md. Third-party license
notices ship with the bundle (`Contents/Resources/THIRD-PARTY-NOTICES.md`) and as
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

For the honest answer to "whose code is this?" (engine vs port vs verification layer), read
[PROVENANCE.md](docs/PROVENANCE.md).
