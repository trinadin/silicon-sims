# R101 — the R91 "car hotspot +3/+4" residual CLOSED BY REFUTATION

The R91 residual claimed "InitCars writes +3/+4 pixel constants into per-car
records (hotspot, unresolved)". Full decode of the car path (this directory)
shows those numbers are RECORD FIELD OFFSETS, not pixels:

- The per-car animation/draw function is 0x476150..0x4763E4 (the function
  whose loop-rotated entry at 0x476320 InitCars bl's into). It loops 9 items
  (cmpwi 9 @0x476378) walking a pointer array via [r30+968], reads the car
  object's fields +456/+504/+508, and draws.
- Every small r27-relative immediate is a POINTER to a field of the per-car
  display record used by the engine's own helpers:
    addi r4, r27, 3    @0x476274  -> the record's SPRITE-SLOT field, passed
                                      to the sprite-load thunk 0x473f00
                                      (which forwards r4 to ActivateForTicks
                                      0x476190 with ticks=4)
    addi r5, r27, 107  @0x476278  -> rect field, same call and the final draw
    addi r4, r27, 121  @0x476334  -> rect field, blit-ish call 0x47a320
    addi r4, r27, 132  @0x4763a0  -> rect field, final draw
  The final draw (@0x4763b0) passes r6 = r7 = 0 — NO extra pixel offsets;
  positions live in the record rects built from the lane state.
- NOWHERE in the car path (InitCars 0x475dd0..0x476108, the animation
  0x476150..0x4763E4, the thunk 0x473f00) is there a `+3`/`+4` PIXEL
  constant: the only 3 is the slot pointer, the only 4s are tick counts
  (`addi r5, r0, 4`).

CONCLUSION: the R91 note was an artifact of the old scanner's weak-anchor
view (it printed `addi r4, r27, 3` without register-roles and the small
numbers pattern-matched "pixel offsets"). The R91 port's raw (x, y) top-left
car draw MATCHES the engine — nothing to port. Per the project's
engine-literalism rule (never pin a false claim), the residual is closed as
REFUTED, not ported; the uinbhd lane pins (CarX within lane range at raw
coordinates) remain the correct assertions.

Evidence: r101-percar-builder.txt (0x476110-0x476330),
r101-loop-tail.txt (0x476330-0x4763E8), r101-rect-helper.txt (0x473f00 —
the sprite-load thunk, NOT a rect helper: forwards r4 to ActivateForTicks).
