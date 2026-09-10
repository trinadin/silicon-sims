# tools/iff-dump — evidence tree + retention policy

One directory per round (`rNNN/`), plus shared decoders in `tools/`. This file governs retention;
HANDOFF.md holds current state.

## Kept permanently (the knowledge)

- `*.md` round docs, errata, decode notes — the narrative and conclusions.
- `*.py` extractors / canon generators / decoders — everything below is
  regenerable from `game-data/` with these.
- `*.txt` decode listings, canon tables, pin lists (`r*-resource-canons.txt`,
  `r90/RE-ENGINE-CONSTANTS.md`, …), and small reference outputs
  (`r153/r153-symbols.json`, `r104/sec1-unpacked.bin`).
- Small loose render/canon images at round roots (visual companions to docs).
- Run logs for the **last four rounds only** (computed from the highest numbered directory) — the live
  evidence chain. Older rounds' conclusions live in their docs + the PARITY
  ledger; the raw logs are pruned.

## Pruned (regenerable bulk — do not recommit)

- **Whole-archive / family art extractions**: `uigr-orig/`, `r87|r88|r89/orig/`,
  `r140/fonts/`, `r121|r124|r141/art/`, `r142/art|tsui/`, `r148/far-full/`.
  These were byte-copies of original game assets — bulk, and better not carried
  in a repo at all (licensing hygiene). Regenerate when needed:
  - `uigr-orig/` → `python3 tools/extract_uigr.py` (reads game-data UIGraphics.far)
  - `r142/art|tsui/` → `python3 tools/iff-dump/r142/extract_art.py` / `extract_res.py`
  - round `orig/`/`art/` dirs → the round's own extractor or `tools/extract_far_any.py`
  - then re-run the round's `make_rNN_canon.py` to rebuild a canon list.
- **`dumps/` subdirs** — harness-rendered UI-survey PNGs; regenerate with a
  `-uisurvey`-style autotest run.
- **Generated bulk indexes**: `r154/r154-bhav-index.json`;
  `r103/sec1-partial.bin` (superseded by r104's complete unpack, kept).
- **`*.log` older than the last four rounds**, and the root `logs/` build logs
  (build commands live in PORT_STATUS.md).

Delete disposable evidence directly; do not create archives or backup copies.
Unique decode notes, contracts and reproducers are maintained evidence, not archives.
Historical links to pruned raw logs describe past runs; use the round conclusions
or rerun the documented probe. Do not restore old logs merely to satisfy a link.

Run `python3 tools/prune-audit-logs.py` to preview stale numbered-round logs;
add `--apply` to delete. It leaves current/non-numbered work and source untouched.
At round completion, update HANDOFF in place and run this cleanup; do not append
another historical narrative to startup context.

## Latest cleanup — 2026-09-08

Pruned 430 raw logs from rounds before R250 across main/support (43,250,258
bytes). Retained R250–R253 logs, unique research and active unnumbered work.
Replaced stale startup summaries with current integration status; no archives
created. Original assets and binary extracts previously removed remain excluded.
