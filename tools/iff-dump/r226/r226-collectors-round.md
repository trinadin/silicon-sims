# R226 — the tile-collector port (diagnostic) + the law's remaining prerequisites

The R225 implementation plan executed as far as honesty allows this round:
the TILE COLLECTOR is ported (as a permanent mood-gated diagnostic), both
fixtures are measured with it, and the remaining prerequisites for a
faithful `RefreshRoomScore` are now precisely enumerated. No engine
behavior changed (log-only) — the law cannot be completed without the two
decode items below, and R128's rule applies.

## The collector (new `roomlaw-tiles` diagnostic)

Mirrors r134's `Room::CollectTileStats` semantics over the port's own
`VMArchitecture` (WallTile.Segments + the room Map): per room —
`tile56` (the original's +56: wall-tile +1, open tile +2), `wallTiles`,
and the full wall STYLE/PATTERN histogram (TopLeftStyle/TopRightStyle/
TopLeftPattern/TopRightPattern per wall tile).

**Measured (both fixtures banked):**
- House05: 7 level-1 rooms incl. the outside base (tile56=5162, 62 wall
  tiles, styles 13/250/26/6/16/5/21/4...), bathroom-ish room (tile56=26,
  14 wall tiles), the small room (tile56=9, patterns 16), living rooms
  (tile56=47 patterns 25, tile56=37 patterns 2 with style 1s and 6s);
  Cassandra at load: pos (440,360), roomAt=1 (the OUTSIDE base — the
  Goths load outside, matching the port's −9 for them).
- House07: Bob & Betty's room (the 50.222 fixture): **tile56=39,
  wallTiles=17**, styles = patterns 2 with styles 1 and SIX 6-style
  segments — the original's door-ish code set {3,5,6,15,23} CONTAINS 6
  and the window-ish {1,7,8,9} contains 1 — the classifier's inputs are
  present in the port's style fields.

## The law evaluation so far

With the constants (R225) and the collected +56: Bob/Betty's room gives
wallsHalf = 39>>1 = 19 → the wall term `min(19,60) − 30 = −11`; the
stored 50.222 therefore needs the remaining terms (object stat ·40 −20,
the two ratio terms ·40−40, the clamp-divided term) to sum to ≈ +61.2 —
consistent with decorated-room object scores. The terms are decodable but
their inputs are NOT yet computed by the port (below).

## The two remaining prerequisites (the honest blocker list)

1. **The window/door style classifier**: the original classifies DIAGONAL
   wall segments by style codes into windows (room+96) / doors (room+100);
   the port's `WallStyle.IsDoor` is NEVER SET for TS1 walls and the port
   has no window classification at all. Needed: map the original's code
   sets {1,7,8,9}/{3,5,6,15,23} onto the port's WallTile style/pattern
   values (the histogram above is the calibration data — House07's room
   shows live 1s and 6s exactly where doors/windows sit).
2. **The phase-1/collector stat semantics**: ComputeRoom reads room+0x3c
   (the light/geometry stat, r134's `0.1875·(floorAlt−0.5) · 3+5` block),
   room+0x6c (the phase-1 world-type value), +0x44/+0x50/+0x54 — the
   object/light collector outputs. Decoding CollectObjectStats's remaining
   stores + the phase-1 math is the last decode step.

## Gate

Log-only change; FULL GATE **128 passed, 0 failed** (r226-gate-run.log),
wrapper exit 0, dist byte-matches publish. The suite count stays 128.
