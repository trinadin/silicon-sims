# PROVENANCE — whose code is this?

This file answers that question plainly, so nobody (including us) has to guess. It is
maintained alongside PARITY.md and HANDOFF.md as part of the keep-it-honest discipline.

## What this repository is

**SimSilicon** is a **native macOS port and fidelity-verification project
built on top of an MPL-2.0 engine**. It is not a clean-room rewrite and is not "ours alone" in the sense of being
independent of FreeSO. Concretely:

- The **simulation engine is FreeSO** (MPL-2.0), living in-repo at `FreeSO/` with its **full git
  history** — merged as a subtree from our engine fork (upstream lineage:
  riperiperi/FreeSO → the alexjyong fork → our `mac-port-rel` work). The one consolidation
  change to that history: blobs over GitHub's 100 MB limit under `Other/libs/assimp-net/`
  were stripped by `git filter-repo` (author dates and messages preserved; SHAs shifted).
  The vast majority of gameplay code — the VM, IFF/FAR loader, content pipeline, entities,
  motives, relationships, sound, world — lives there (4,242 .cs files).
- **Simitone itself is FreeSO-derived**: this repo is a fork of alexjyong/Simitone, which is a
  fork of riperiperi/Simitone, which is a fork of FreeSO. The `Client/Simitone/` shell and the
  `Simitone.*` assemblies carry that lineage and MPL headers. The `Simitone.*` namespaces are
  deliberately kept (invisible to players; keeps diffs against upstream readable).
- `FreeSO/Other/libs/FSOMonoGame/` and `FreeSO/Other/libs/FSOMina.NET/` are **vendored
  snapshots** of the (formerly separate) library forks, pinned at the exact commits the port
  builds against; their upstream repositories remain public.

## What this project adds (our contribution)

Our original, value-adding work lives in a thin layer around the engine:

- The macOS packaging/build layer (`packmac.sh`, Info.plist, icon, DMG tooling) and the
  port fixes needed to run on Apple Silicon.
- The headless `-autotest` verification harness and its 144-check suite (AUTOTEST.md).
- The reverse-engineering toolkit under `tools/` and the staged-data evidence under
  `tools/iff-dump/` (tracked evidence).
- Engine-side fidelity fixes, each committed with IFF-grounded evidence (e.g. UseNeighbor
  matrix-key fix; exit-hang fix; TS1 new-sim motive-init byte-fidelity fix).
- The honest gap tracker (PARITY.md) and per-round verification log (HANDOFF.md).

All of that stands on the engine. None of it replaces FreeSO's simulation core.

## Engine projects and third-party dependencies

The in-repo `FreeSO/` tree carries the simulation and content projects
(`tso.files`, `tso.content`, `tso.simantics`, `tso.sound`, `tso.client`,
`vitaboy.*`/mesh viewers, `FSO.Server.*`), plus the Simitone client shell under
`Client/Simitone/`. Third-party dependencies are NuGet packages (see
THIRD-PARTY-NOTICES.md for the distributed set and their licenses) plus the
SDL2/OpenAL native runtimes and Eto.Forms UI toolkit.

## Licensing notes

- The repository is MPL-2.0 as a whole (LICENSE.md); per MPL §3.2/Exhibit A,
  individual file headers are optional and only a minority of files carry one.
- The FreeSO **Content directory** (the replacement content pack FreeSO ships)
  carries no license file in either upstream repo; treat it as
  redistribution-restricted until upstream clarifies, and never bundle original
  Maxis/EA game data — `game-data/` is untracked and ignored by design.
- Known open items are tracked in the REL-09 audit (Info.plist branding /
  trademark question is a repo-owner decision).

## License

The repository as a whole is licensed **MPL-2.0** (see LICENSE.md), inherited from FreeSO/Simitone.
This is not a cosmetic choice: MPL-2.0 is file-level copyleft, so any FreeSO/Simitone-derived
file stays MPL-2.0 regardless of later edits, and our additions are offered under the same
license. If we ever wanted to relicense, that would require MPL compliance on the derived files
plus upstream agreement — the directory boundary does not lift that.

## Original game data

The Sims (2000) and its game data are © Maxis / EA and are **not** bundled. The engine runs the
original IFF behavior/script logic against your legally owned game data (see README).

## TL;DR

FreeSO (MPL-2.0) is the engine; Simitone is FreeSO-derived; this repo is a macOS port of
Simitone — **SimSilicon** — that adds real verification and fidelity work on top. "Our own thing" =
the port, the harness, the RE tooling, and the verified fixes — not the engine itself.

