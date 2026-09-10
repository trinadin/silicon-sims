# R107 — the UL/TS1.0 water positions: the SpriteSlot machinery decoded

## The mechanism (evidence: r107-sprite-slot.txt, r107-slot-activate.txt,
## r107-slot-position.txt, r107-show-slot.txt, r107-slot-id.txt)

- The loader (0x476190, called with ticks=4 from every water/delta load
  site) creates the slot via 0x472360 and stores it into the window field.
- The DRAW path does NOT pass window-class coordinates: UL TSPaint
  (0x46b1c8..0x46b230) calls a vtable draw on the sprite OBJECT
  ([this+400]->vtable+420) with r5=r6=1 (mode flags) and NO position args;
  VC's Shutdown only frees the slots.
- The position lives INSIDE the slot machinery: activate (0x47b350..0x47b424)
  resolves it from the slot record at +396 — reading the sprite's own rect
  [108]/[112] as x/y plus offset fields [80]/[84] — i.e. the draw position
  is the SPRITE RESOURCE's stored origin (the same class of evidence as the
  IFF sprite bytes themselves), not a window-class constant.
- The position setter helper (0x473cc0) likewise composes positions from
  resource-derived rects (607/644/661 offsets into resource data).

## Conclusion

The UL waves and TS1.0 deltas have NO engine-code position constants to
recover: their placement is data-inside-the-sprite. The port's (0,0)
full-screen overlay mount is the engine-visible equivalent (a full-screen
sprite's internal origin covers the viewport). The gate now PINS the
mechanism's precondition: the UL wave family (6 frames) and the TS1.0
delta family mount at full-screen dims (800x600 / >=600 wide) — the
origin-covering condition.

r107p1: 59/59, uinbhd fsWater=True, clean exit.
