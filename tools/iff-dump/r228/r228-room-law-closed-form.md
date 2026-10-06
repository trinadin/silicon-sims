> **AUD-16 CORRECTION (2026-10-05, evidence/AUD-16/verdict-data.md F1/F2/F5):**
> the t1 and t3 lines below were MISDECODED (register-tracking slips the
> Ghidra decompiler exposed): native t1 = (room60 / **room56**)·40 − 40
> (the weighted tile count, unhalved, unguarded — NOT room80), and native
> t3 = (room96 == 0) ? **−40** : (**room100 / room96**)·40 − 40 (the ratio
> is doors/windows with a −40 skip, NOT windows/doors with 0). Corrected in
> the port by ENG-24 (room108 recalibrated 2.05 → 3.11 under the corrected
> law). Also: the ENG-15 zoning citations "0x6cd2c/0x6ce8c" elsewhere are
> RVA (image-relative) addresses, not file offsets (file = RVA + 0x8E90).
> The remainder of this receipt (wall/obj/t2/clamp/outside terms, the
> collector map, the fixtures) was decompiler-CONFIRMED.

# R228 — the ComputeRoom law in CLOSED FORM (the six-round arc's specification deliverable)

Assembled from the annotated disassembly (constants substituted from the
R225 table), this is the original's room-score law, complete:

```
INSIDE rooms (Room::ComputeRoom 0x1267d0, the 0x1269e4 path):

  half  = trunc(tile56 / 2)                 # tile56 = Σ per tile (wall:+1, open:+2)
  wall  = min(half, 60) − 30                # 0x126a04-0x126a58
  obj   = room[108] × 40 − 20               # 0x126a5c-0x126a78 (the phase-1 float stat)
  f8    = clamp(half, 10, 45)               # 0x126a34-0x126a98
  t1    = (room[60] / room[80]) × 40 − 40   # 0x126a98-0x126b04 (windowish / obj-wear)
  t2    = (room[84] × 10) / f8              # 0x126af0-0x126b08 (pool-ish / wall density)
  t3    = room[96] != 0                     # 0x126ab4-0x126b68
            ? (room[96] / room[100]) × 40 − 40   # windows / doors
            : 0
  score = clamp(wall + obj + t1 + t2 + t3, −100, +100)   # → room[104]

OUTSIDE rooms (the 0x126994 path):
  score = 2·room[80] + 1·room[84]           # → room[104]
```

## Field semantics (the collector map, all rounds combined)

| field | collector | port status |
|-------|-----------|-------------|
| room[56] | tiles (wall +1, open +2) — CollectTileStats | PORTED (R226 diagnostic) |
| room[60] | 2 × window-ish walls (the .wll style-subtable flag) | mechanism decoded (R227) |
| room[80] | Σ obj+88 for outside-flagged objects — CollectObjectStats | needs obj-field mapping |
| room[84] | count of nonzero obj flags (+ pool tiles from tiles) | needs obj-field mapping |
| room[96] | windows (two-pass diagonal classifier, codes {1,7,8,9}) | classifier data banked (R226) |
| room[100] | doors (codes {3,5,6,15,23}) | classifier data banked (R226) |
| room[108] | the phase-1 value ([world+12]² × type factor 0/1/0.5) | world-global mapping needed |

## Calibration fixtures (measured)

- House07/Bob & Betty (stored 50.222): tile56=39 → half=19 → wall=−11,
  f8=19; the remaining terms must sum to +61.2.
- House05/Goths (stored 86.333): the sims load OUTSIDE (roomAt=1) — the
  ORIGINAL's outside law (2·room80 + room84) or their indoor position at
  the original's save time must be resolved during calibration.

## The implementation recipe (one mechanical session)

1. Map room[80]/[84]'s object fields (obj+88 etc.) onto the port's OBJD
   slots; map world+12 (the phase-1 global).
2. Parse the .wll style subtable ([style*4+32]→record→+12, R227) for the
   window-ish flags → room[60]; classify diagonals → room[96]/[100].
3. Implement the closed-form law in `VMContext.RefreshRoomScore` behind
   the existing collector, calibrate against the two fixtures via the
   `roomlaw`/`roomlaw-tiles` diagnostics, then pin + FULL GATE.

No engine change this round — the formula is the deliverable; implementing
it before the three object/world field mappings are verified against the
fixtures would be guesswork (the R128 rule).
