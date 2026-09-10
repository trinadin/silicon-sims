# SOP.md — Standard Operating Procedure (READ FIRST)

**Mission:** port *The Sims* (2000) to macOS by any means necessary. The port RUNS today on
Apple Silicon; the ongoing work is pushing playability/fidelity as far as feasible, honestly.

**Engine:** FreeSO/Simitone (MPL-2.0). The game's original IFF logic runs against legally owned
game data (not bundled here). IFF-literalism is the *verification standard* for fidelity claims —
it is a means to the port goal, not an end in itself. Never let pin-polishing block shipping.

If you have 30 minutes, do this in order. If you have 5, read §1 and §5 and §6.

## 1. What this repo is

- `Client/Simitone/` — macOS app shell (Eto forms + MonoGame DesktopGL).
- `FreeSO/` — pinned engine submodule (`mac-port-rel`); gameplay VM, IFF/FAR loader, content.
- `tools/` — reverse-engineering tools (IFF decoders, probes) + evidence under `tools/iff-dump/`.
- `game-data/The Sims/` — staged original game data; every pin verifies against this.
- `dist/The Sims-arm64.app` — shipped build.

Read `HANDOFF.md` first for current state and integration boundaries, then
`PROVENANCE.md`. Consult the relevant sections of `PARITY.md`, `AUTOTEST.md`
and `PORT_STATUS.md` as needed; do not load entire historical ledgers/logs.

## 2. Current state

`HANDOFF.md` is the current-state entry point. Verify its revision against
`git status`, `git rev-parse HEAD` and `git submodule status`; recorded test
results apply to the documented build, not automatically to later edits.

## 3. First actions for a new agent

1. `git status` + `git submodule status` — confirm the real branch/pointer/WIP.
2. Run the suite: `./tools/run-autotest.sh` (binary directly; never `open --args`).
   Confirm `AUTOTEST RESULT PASS passed=N failed=0` + the clean exit-probe chain.
3. Read `PARITY.md` → "Current gaps", pick the single highest user-visible gap.
4. Restate that gap in your own words before touching code. Do not wander.

## 4. Standard round workflow

1. **Confirm starting state:** working tree and suite green; record the `git rev-parse HEAD`.
2. **Pick ONE gap** from PARITY's current gap list.
3. **Research it:** original IFF (via `tools/`), engine source, public docs. Find the source of truth.
4. **Decide:** if the divergence is IFF-falsifiable → fix in the FreeSO submodule + add/extend an
   `-autotest` pin. If it needs eyes/live play → verify what's headlessly verifiable, then KEEP
   the gap marked `[GAP]` with the narrowed scope written down.
5. **Never weaken an existing pin** — extend it, keep the suite green.
6. **Verify (all of these, no shortcuts):** publish + `./packmac.sh arm64` → run harness → clean
   exit-probe chain → dist DLLs byte-match publish → park evidence in `tools/iff-dump/` and
   commit with `git add -f`.
7. **Record honestly:** update only the affected lines of PARITY (gaps/pins/ledger) and HANDOFF
   (state). Replace HANDOFF state in place; do not append prior rounds or duplicate narrative across files.
8. **Commit** with a clear one-line subject (`Round N: …`).

## 5. Hard rules

- Honesty over optimism: every claim needs a source; correct past claims when data disproves them.
- Never convert a `[GAP]` to done without live/visual verification where live is required.
- Never weaken a pin. Never ship a red suite.
- Evidence lives in `tools/iff-dump/` (tracked) — commit the logs you cite normally.
- Evidence retention (see `tools/iff-dump/README.md`): round docs/decodes/canon
  lists/tools are permanent; raw run logs older than the last 4 rounds, harness
  `dumps/` dirs, and regenerable bulk extracts (FAR/art/font copies, generated
  indexes) are pruned instead of committed.
- Use `dotnet publish` (not `build`) per PORT_STATUS; respect `NUGET_PACKAGES`/`DOTNET_CLI_HOME`.
- Watch the real child PID; stop only your own process if necessary. Serialize
  game tests, use private `-autotest-userdir`, and never `open --args`.

## 6. Where to look

- Which check runs what / how to add one → `AUTOTEST.md`.
- What's proven vs open → `PARITY.md` (gaps first; ledger at the bottom).
- Commands to build/package/run → `PORT_STATUS.md`.
- Licensing/lineage → `PROVENANCE.md`.
- Who to credit / what's inherited → same upstreams listed in README + PROVENANCE.