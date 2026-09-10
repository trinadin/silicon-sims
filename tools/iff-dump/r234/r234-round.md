# R234 — the aggregation loops: seeding continuation; the decision point

The 0x1270d0+ region decoded (131 instructions, banked): it is the
UNROLLED CONTINUATION of R233's per-tile seeding (the same `stbx r11`
pattern over 8 record positions per iteration, r11 still
`int(1 + 255·room108)`). **No float aggregation and no additive term
exist anywhere in ComputeRoom's tail** — the function's complete float
arithmetic is the R228 closed form plus the phase-1 stat.

## The consequence: the missing term is room[108] itself

With every code path now decoded, the ~26.7/+23 gap can only come from
the TRUE value of room[108] (the phase-1 stat): R232's [world+12]=1.0
candidate yields room108 = 1.0, but the fixtures require **1.667 (exactly
5/3, House07)** and **1.575 (House05)** — light-subsystem-computed values
(BSS, runtime) that differ per house. R231's "fit" was in fact measuring
the real runtime values of a computed constant.

## The honest decision point (surfaced for the project owner)

Two legitimate paths, previously implicit, now explicit:

**(a) Ship the calibrated approximation**: implement the closed-form law
with the entity-count bridge and room108 calibrated to the fixtures
(~1.6), DISCLOSED as an approximation of the original's light-computed
phase-1 stat — exactly the class of disclosed port approximation the
project already carries (the censor mosaic palette, the Vita sway
amplitude). This closes the −9-vs-86/50 defect NOW, is honest about its
one modeled constant, and pins against both fixtures.

**(b) Hold for exactness**: measure the original's room108 at runtime
(emulator-assisted RE of the BSS light computation) before implementing.

Twelve rounds of decode have produced everything static RE can; choosing
(a) or (b) is a fidelity-bar decision that belongs to the project owner.
No engine change this round.
