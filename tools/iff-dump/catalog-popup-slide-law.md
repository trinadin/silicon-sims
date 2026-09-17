# Catalog band popup — slide ramp consumer decoded (UI-12)

2026-09-16. Binary: `game-data/The Sims/The Sims Complete` (PPC, addresses =
file offsets). Companion to r145/build-toolbar-law.md §6 (EnablePopup /
BuildMyBuffer / R148 anchor law) and the UI-06 residual.

## The ramp and its consumer

`EnablePopup` (0x26e000): SetArea anchor → AddChild to main view → sound →
`SetupConstantTimeRamp` at 0x26e0d8 (impl 0x14e890, per r243-fade):

- ramp object: embedded in the popup at **+228**
- start = float const from the TOC block at −0xaf5c (the 0.0 of r145)
- target = `(double)popup.+0x78 − doubleConst` (computed 0x26e094-0x26e0d0)
- duration = 250 (0x26e0bc)

r145 rendered the target as "height−176.0"; **+0x78 is the window's final Y**
(the placement law sums +0x74/+0x78), so the reading is really
`finalY − const`, with the TOC double const (r145's "176") unrecoverable
statically.

The consumer is the popup's paint (two sites, 0x26cfe0 and 0x26d0d8, both
`bl 0x14e930` = RampGenerator::GetVal):

- delta = `popup.+0x80 − popup.+0x78` (0x26cfdc / 0x26d0f0)
- drawn Y contribution = `delta − (doubleConst + GetVal(now))`
  (0x26cfe4-0x26cff8), then the popup rect is composed for the blit.

So the drawn Y interpolates **linearly over 250 ms** from a start position
derived from field **+0x80** (the source row — set alongside the anchor at
placement; exact producer undecoded) to the R148 anchor. r145's
"height−176" target reading was an interpretation; the mechanism is: slide-up
from the source row into the anchor, linear, 250 ms.

## Port mapping (UI-12)

- `Position` keeps the R148 anchor at all times (draw-time-only offset — the
  uibuy gate asserts the anchor synchronously at hover).
- `BeginSlideIn(fromY)` starts the 250 ms linear ramp with `fromY` = the
  hovered cell's absolute row Y (the port's stand-in for +0x80; the field's
  producer is undecoded — INTERPRETED).
- Draw offset = `(fromY − anchorY) × (1 − Slide)`, applied to the panel and
  children; restored after draw.
- The unrecovered TOC double constants are NOT modeled (no invented 176).

## Affordability tint (same round)

`BuildMyBuffer` 0x26dccc-0x26dd94: can't-afford → the engine error red on the
name/price strings (R198 SetColor(1); the port's pinned
`UILotControl.TooltipErrorColor` = RGB(255,0,0)). The port now recolors
`UIOriginalText.Color` on both labels when family funds (VM global 0) < price.
The purchase-path budget gate remains undecoded and is NOT added.
