# Simitone-macOS

A maintained fork of **Simitone** — the open-source re-implementation of *The Sims™* (2000) — rebuilt as a native **macOS (Apple Silicon)** application, with an ongoing effort to push gameplay as close to **1:1 fidelity with the original** as feasible.

> **Not affiliated with the Simitone team.** This repository is a fork of
> [alexjyong/Simitone](https://github.com/alexjyong/Simitone) (v0.8.20-forked), which is itself a
> fork of [riperiperi/Simitone](https://github.com/riperiperi/Simitone). The simulation engine is
> [FreeSO](https://freeso.org).

## What this project is

Simitone is **not a remake**. It is an engine that:

- loads your legally-owned copy of *The Sims: Complete Collection* game data (IFF format), and
- compiles and runs the **original game logic** — the original `Behavior.iff` scripted behavior
  (BHAV bytecode) is executed by the engine's VM — so gameplay is driven by the original data
  and the original logic.

It ships **no copyrighted game assets**: you point it at your game files (see [Game data](#game-data)).

## What this fork changes (branch `mac-port`)

- **Native macOS build.** Self-contained arm64 app bundle + DMG via [packmac.sh](packmac.sh): no .NET
  runtime, SDL or OpenAL installs required.
- **Apple Silicon verified.** Boots to gameplay, loads user neighbourhoods/houses, renders a live scene
  (pixel-confirmed) and passes durable soak runs — opened from the historical “builds but does not
  run” state to playable.
- **Graphics-backend alignment.** Uses the non-MRT rendering path on OpenGL/macOS (same as the
  iOS/Android engine paths); old Eto 2.8.4 vs 2.9.0 crash and NuGet-cache-path build failures fixed.
- **Boot diagnostics.** Screen transitions and content-load failures are written to
  `~/Documents/Simitone/game.log`; crash details land in the app's `simitone-crash.log`.
- **Legal game-data fetch.** [fetch-game-data.sh](fetch-game-data.sh) downloads the Complete Collection
  DVD ISO from archive.org and extracts `GameData/` — no disc drive needed.
- **Fidelity work.** See [PARITY.md](PARITY.md) for the honest gap tracker toward 1:1.

## Game data

Simitone needs The Sims 1 **Complete Collection / Legacy Collection** data: a folder containing
`GameData/.../Behavior.iff` and the `UserData` layout. See [PORT_STATUS.md](PORT_STATUS.md)
("Required: game data") for what is staged here and how to obtain it legally —
[fetch-game-data.sh](fetch-game-data.sh) stages the Complete Collection DVD from archive.org
(you must own/obtain the game; it just avoids needing a disc drive). The selected path persists
in the user-data `config.ini` (`TS1HybridPath`); user data and saves live in `~/Documents/Simitone/`.

## Running

- Launch `dist/The Sims-arm64.app` (or mount `dist/Simitone-macOS-AppleSilicon.dmg`).
- First run: pick your game-data folder, or it is auto-detected / pre-seeded in `config.ini`.
- Flags: `-gl` (OpenGL), `-3d` (3D mode, toggle F12), `-nosound`. `-jit` and `-dx` are inherited
  from upstream and not yet soak-verified on macOS.

## Building from source

Prerequisites: .NET SDK 9, git (for the FreeSO submodule). The exact publish + package commands
(and why the `NUGET_PACKAGES`/`DOTNET_CLI_HOME` redirects and `WarningsAsErrors` relaxations are
needed) are in [PORT_STATUS.md](PORT_STATUS.md) → "Build / package".

## Parity status

⚠ Honest status: this is an **engine parity project**, not a finished 1:1 clone. The engine runs
original logic from original data, but not everything is faithful yet. Known gaps (fame career,
vacation features, pet AI, Makin' Magic, free-will accuracy, some neighbourhood management, sound
and UI fidelity) are tracked in [PARITY.md](PARITY.md) — read it before filing issues.

## iPad / iOS (future)

The engine is shared; the iOS shell (FSO.iOS) is legacy Xamarin and needs modernization. Assessed in
[PORT_STATUS.md](PORT_STATUS.md). Not started.

## License & credits

- Engine: [FreeSO](https://freeso.org) — MPL-2.0 (this repo is LICENSE.md MPL-2.0 as a whole; per MPL §3.2/Exhibit A file headers are optional and only a minority of files carry one).
- Original game & data: © Maxis / EA — not bundled here.
- Upstream: [riperiperi/Simitone](https://github.com/riperiperi/Simitone),
  [alexjyong/Simitone](https://github.com/alexjyong/Simitone).
- macOS port & parity work: this repository.
- Icon/audio attributions from upstream (Icons8, CC0 freesound loops) apply to bundled assets; see ATTRIBUTION.md

## Source code (MPL §3.2)

The complete corresponding source for this application is this repository at the
packaged revision: parent commit `c027914` with the `FreeSO` engine submodule at
`46495bb3` (nested submodule pins are recorded in the REL-01 evidence). Clone the
repository and `git checkout` those commits to obtain the exact sources; build with
`packmac.sh`/`build-mac-linux.sh` per PORT_STATUS.md. Third-party license notices
ship with the bundle (`Contents/Resources/THIRD-PARTY-NOTICES.md`) and as
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
  history.
- For the honest answer to "whose code is this?" (engine vs port vs verification layer), read
  [PROVENANCE.md](PROVENANCE.md).
