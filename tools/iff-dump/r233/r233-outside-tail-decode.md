# R233 — the outside-tail decode: per-tile seeding, not the missing term

The 0x126ba0-0x126dec block decoded:

```
f0 = 1.0 + 255 · room[108]              ; r29[0] + r30[4] × the phase-1 stat
r11 = (int)f0                            ; fctiwz truncate
for i in room[8]-count records (24-byte stride):
    look up per-record tables via room[0x78] pointer + 0x50-style indices
    stbx r11 → byte tables at [table + x*4 + y] per record position
```

**Verdict**: the outside tail SEEDS per-tile byte maps with
`int(1 + 255·room108)` — it is the per-tile score/lighting initialization
(the same value every tile), not an additive room term. The consistently
missing ~26.7/+23 from R232 is therefore NOT here; its source is further
downstream — most likely the 0x1270d0+ aggregation loops (the `stbx r11`
walk seen at the function tail) that re-sum the seeded maps, or a term in
the second-phase inside path not yet isolated.

## Honest close of this session's room-score arc

Eleven rounds have taken the defect from "unexplained −9 vs 86" to: the
closed-form inside law, all constants, the tile collector ported, the
classifier calibrated, the entity-count bridge validated by shadow
calibration on BOTH fixtures, [world+12] identified, and now the outside
tail identified as per-tile seeding. The remaining gap is one additive
term whose source is localized to the tail aggregation loops. Completing
it — and then implementing, calibrating, and pinning — is a fresh-session
project with every input now specified. No engine change (decode only).
