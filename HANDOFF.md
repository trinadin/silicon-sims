# HANDOFF — current state of the port

The one-page answer to "where does the game stand?" Detail lives in
[PARITY.md](PARITY.md) (gap tracker + full round history) and
[PORT_STATUS.md](PORT_STATUS.md) (build/run). Updated 2026-10-08 — v0.9.0,
the first public release.

## What this is

**SimSilicon** — The Sims (2000) as a native Apple Silicon Mac application:
the Simitone/FreeSO engine (merged in this repository with full history)
running the original game logic from your legally-owned Complete Collection
data. Fidelity claims are decode-backed: where the port's behavior was in
doubt, the original PowerPC binary was disassembled and the port corrected
to the recovered law (constants, thresholds, formulas, UI geometry, art).

## Verified state

- **Default battery: 144 checks, PASS** — boots the real game headlessly,
  exercises gameplay/UI/saves/sound/autonomy, and runs on every change
  (`./tools/run-autotest.sh`; see [AUTOTEST.md](AUTOTEST.md)). Focused gates
  (freewillwin, moodlaw, roomlaw, …) cover the decode-pinned laws.
- **From-dist validation:** every release artifact is validated by running
  the battery against the packed `.app` itself, not just the build tree.
- **v0.9.0 distribution:** app 173 MB, DMG 84 MB; ships zero EA/Maxis
  assets (the TSO online-game payload was removed — battery-verified
  allowlist; see packmac.sh).

## The honest remainder

See PARITY.md → "Where the game stands" for the full list. Headlines:

- **Human verification is partial** — every system above is machine-verified, and
  portions have been played and accepted hands-on (e.g. the plumbob render scenarios);
  a systematic end-to-end human acceptance pass is still pending.
- Base game: neighbourhood management (bulldoze/rezone/evict) partial;
  sound audible-fidelity trace not run; custom-animated objects partial;
  assorted modeled UI interactions (disclosed in PARITY).
- Expansions: Hot Date / Unleashed / Vacation / Superstar / Makin' Magic
  all have substantial verified systems and open completeness gaps.
- Platform: Apple Silicon only; `-3d` experimental; `-jit`/`-dx` unverified.

## Working on it

- Decode discipline: every behavioral claim cites a source (IFF bytes or a
  binary address). AUTOTEST.md describes the harness; SOP.md the working
  rules; ERRORS.md the observed hazards.
- Reporting bugs: use the issue template (Mac model, macOS version,
  game-data source, `~/Documents/Simitone/game.log` tail). Check PARITY.md
  first — known gaps live there.
