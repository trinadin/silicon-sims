# R231 — the shadow calibration: the fixtures REPRODUCED within a plausible parameterization

The closed-form law (R228) evaluated as a SHADOW computation per inside room
(`roomlaw-shadow`, three bridge hypotheses for the inside room[80]:
A = the port's RoomLighting.AmbientLight, B = OutsideLight, C = the room's
entity count), with the window/door candidates from the style histograms.

## Results (both fixtures, banked logs in this dir)

**House07 / Bob & Betty's room (stored 50.222)** — half=19, wall=−11:
- hypC (room[80] = entity count = 20): t1=+4.00, t2=10.53, t3=0 →
  shadowNoObj = **3.53** → the object term needed: room108 = **1.667**
  (i.e. (50.222 − 3.53 + 20)/40 — exactly 5/3).
- hypB gives −31.67 → needs room108 = 2.55.

**House05** — the Goths load OUTSIDE in the port (roomAt=1); the inside
rooms' shadows: 3.53 / −2.78 / 43.33 (hypC). The 86.333 fixture needs the
object term 1.575 on the 43.33 room — **the same ~1.6 magnitude** as
House07's requirement.

## The convergence signal

Both fixtures are reproduced by hypC + room108 ≈ 1.6 — and room108 is the
phase-1 `[world+12]² × type-factor` value, plausibly a single world
constant (~1.6² ≈ 2.56 ≈ [world+12], or the type factor 0.5 scaling).
The two required values (1.667, 1.575) differ by 6% — consistent with a
shared world constant plus per-house variation.

## What still blocks the pin (honestly)

1. The [world+12] value must be DECODED or measured, not fitted — with it,
   the object term is determined and the law becomes fully predictive.
2. The Goth inside/outside attribution: the port has them outside at load
   while 86.333 is not representable by the (integer) outside law — the
   original's outside path likely has further math after 0x126b90, or the
   stored value predates their move outside.
3. Then: implement (hypC bridge + the decoded world constant), calibrate,
   pin, FULL GATE.

No engine change (the shadow is diagnostic-only; promoting it with a fitted
object term would be curve-fitting, not the faithful law).
