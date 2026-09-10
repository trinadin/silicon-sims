# R229 — the field-mapping refinements (evidence round)

## obj[88] → room[80]/room[84] (decoded, CollectObjectStats tail 0x126160-0x1261a0)

```
v = (s16)obj[88]                       # a signed per-object slot
if IsOutside(room):
    if v < 0: room[80] += v            # OUTSIDE rooms sum NEGATIVE obj88 values
else:
    if v >= 0: room[84]++              # INSIDE rooms count obj88 >= 0
room[76] += (s16)obj[152]              # the wear accumulator (r134)
```

**Correction to the R228 field table**: the inside-law's `t1 = room[60]/room[80]`
CANNOT read this obj88 accumulator (it stays 0 for inside rooms → division by
zero). The inside room[80] must be fed by the collector's LIGHT/GEOMETRY block
(0x125fe0-0x126080 — decoded this round: per-object position/light float math
with the r30 constants +8=0.1875, +12=3, +16=5, i.e. r134's
`0.1875·(floorAlt−0.5)` / `floorAlt·3+5` laws, results staged to the stack
then aggregated). One more pass over the collector's stores (which stack slot
lands in which room field) fixes the table — the constants and the arithmetic
are already recovered.

## Status

All formula constants: recovered (R225). The law: closed form (R228). The
collectors: tile collector ported (R226), classifier mechanism + calibration
data banked (R226/R227), the obj88 branch law + the light block arithmetic
(this round). Remaining before implementation: the stack-slot→room-field
attribution pass in CollectObjectStats (mechanical), the world+12 mapping,
and the .wll record parse. Then: implement → calibrate vs 86.333/50.222 →
pin → FULL GATE.

No engine change this round (decode refinement only).
