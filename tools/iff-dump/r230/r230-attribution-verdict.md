# R230 — the attribution verdict: the inside room[80] comes from the LIGHT subsystem

The stack-slot→room-field attribution pass over the FULL CollectObjectStats
listing (0x125df0-0x1261d0) finds **no float stores into the room struct at
all**. The light/geometry staging slots (r1+96/100/104) feed
**`LightLayer::PositionLight(const CTilePt&, cXObject::ObjectLightS*)`**
(0x126100) — the room's inside stats are produced by the LIGHT LAYER
subsystem, not by the collector directly.

## Consequence for the implementation plan

R229's "mechanical attribution pass" characterization was wrong — the
bridge needed is a DESIGN decision, not a decode pass: the original's
inside room[80] (and the room[108] phase-1 stat) are aggregates over
per-position LIGHT values (the port has its own `RoomLighting`/
`RefreshLighting` model with different internals). A faithful
`RefreshRoomScore` therefore requires either:

1. exporting the original's per-room light aggregates from the PORT's
   light model (an approximation bridging two different light systems —
   must be calibrated against the fixtures to be honest), or
2. porting the original's LightLayer position math (0x1dd6f0 + its
   aggregation) — a further subsystem decode.

The honest scope of the room-score fix is now fully bounded: the law
(R228, closed form), the constants (R225), the tile collector (R226,
ported), the classifier (R227 mechanism + R226 data), the obj88 branches
(R229) — and the light-aggregate bridge (this round's verdict), which is
the one remaining design+decode item. No engine change (the verdict IS the
deliverable; guessing the bridge without calibration would violate R128).
