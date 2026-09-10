# R227 — the window/door classifier mechanism decoded (evidence round)

From r134's banked `Room::CollectTileStats` listing (0x1264e0-0x12652c),
now read against R225/R226's constants and collector map:

## The +0x3c law (the window/light counter)

```
styleA = (s8)tile[2];  styleX = (s8)tile[0];  styleY = (s8)tile[1]
r5   = [TOC-29524 world table]
rec  = [r5 + styleA*4 + 32]     ; per-style record
sub  = [rec + 12]               ; the u16 subtable base
flag = (u16)[sub + styleX*4 + styleY*2]... (lwzx r3 = [r4 + styleX*4];
        lhzx r0 = [r3 + styleY*2])
if (flag != 0) room+0x3c += 2
```

**room+0x3c += 2 for every wall whose style-pair subtable entry is
nonzero** — the ORIGINAL's window-ness lookup lives in the WALL RESOURCE
records (the .wll catalog in Walls.far), not in code. The port's
`WorldWallProvider` reads the same FAR3 .wll files (SPRs, styles,
patterns) but does NOT parse the +32/+12 record chain — that parse is the
implementation's last data prerequisite, and it is corpus-readable.

## What this completes

With R226's histograms + this mechanism, the collector specification is
now complete to the data-format level:
- room+56: tiles (wall-tile +1, open +2) — PORTED (R226 diagnostic)
- room+0x3c: 2 × style-subtable-flagged walls (windows) — mechanism decoded
- room+96/+100: the two-pass diagonal-segment window/door counters
  (r134 0x126420-0x1264b8) — style-code sets {1,7,8,9}/{3,5,6,15,23}
- room+0x50/+0x54/+0x80/+0x84: wear/pool/outside object stats (r134
  CollectObjectStats)
- room+0x6c: the phase-1 world-type value ([r28+12]² × the type factor
  0/1/0.5) — the world global at TOC−29216+12
- the law: R225's constants + term structure (wall term + obj·40−20 +
  ratio·40−40 ×2 + clamp-divided term), final clamp [−100, 100]

## The remaining implementation step (single round, fully specified)

1. Parse the wall-resource style records (+32/+12 chain) in the provider
   (or precompute the style→window/door flags at load).
2. Implement `CollectRoomStats` in VMContext (tiles, windows, doors, wear)
   + the ComputeRoom law in `RefreshRoomScore`.
3. Calibrate against the two stored fixtures via the roomlaw diagnostics;
   pin (86.333 Goth / 50.222 House07) and gate.

No engine change this round (decode only — nothing to verify against yet).
