# R194 — Speech-balloon chrome + event icons

Date: 2026-08-31

## Evidence boundary

Owner's local `game-data/The Sims/The Sims Complete` PPC executable
(SHA-256 `33c76da2…c06a5f`) and `UIGraphics.far`. Constants and behavior
only.

## Decode

### Product::GenerateSpeechIcons (0x209cc0, 904 B)

- Guarded by `product+0x14 == 1` and a non-null `product+0x18`.
- Iterates two speech-icon resource groups via the RT getter 0x3b6430:
  base `+0x7d0` (**2000**) and base `+0xfa0` (**4000**) — the speech icon
  families keyed by a person field at +0x52.
- Loads **`li r3, 0x2328` (9000 = kSpeechMediumBmp)** and
  **`li r3, 0x2329` (9001 = kSpeechLargeBmp)** through the factory
  0x3b6190 into product+0x20/+0x24 (image handles).
- When both resolve, calls the balloon compositor **0x236650**
  (person, &handles): builds the balloon image with buffer vtable
  +0x28 (size), +0xd4 (a u16 state), +0x2c queries and a 4-byte-cell
  allocation + 0x48ee70 blit loop — edge/tile composition.

### The consumers

- `Product::GetSpeechImage` 0x209bf0 (144 B) is called by
  **`cXObject::DrawSpriteSlot` 0xcf74c** — the in-game sprite-slot draw
  path. This proves the 9000/9001 system is the IN-GAME speech balloon
  chrome (not export-only).
- The port's status/headline balloons already render from `Sprites.iff`
  SPRs on the engine group-offset law (`UIHeadlineRenderer`); the
  9000/9001 family is the separate speech-slot chrome.

### TragedyMask 9005

`Other\TragedyMask.BMP` 133x103 = global resource 9005, consumed by the
ObjectDialog `gzi 9005` icon command (40 shipped sites, r177 law). The
port already resolves it through the runtime Res_*.h/Res_*.RT map
(case-insensitive member `Other\TragedyMask.bmp`) and pins it in the
uidlgchrome family — re-pinned here from the balloon check.

### Art (UIGraphics.far, byte-verified)

| id | member | dims |
|---|---|---|
| 9000 | `Other\SpeechMedium.bmp` | 16x16 |
| 9001 | `Other\SpeechLarge.bmp` | 32x32 |
| 9005 | `Other\TragedyMask.BMP` | 133x103 |

## Port

- `UI/Controls/UIOriginalSpeechBalloon.cs` — the balloon chrome builder:
  a nine-slice of the 9000/9001 tiles (medium 4px corners / 8px edges,
  large 8px corners / 16px edges — modeled from the tile halves, the
  compositor's exact edge arithmetic not decoded, disclosed) with the
  glyph label inset and content-sized bounds.
- The money-change floater (UIDesktopUCP.DisplayChange) now rides the
  SpeechMedium balloon chrome instead of bare text — campaign item 10's
  "money headline off the mobile pill onto balloon-family chrome".

## Residuals (disclosed)

- The in-game speech-slot path (cXObject::DrawSpriteSlot →
  Product::GetSpeechImage with the icon groups 2000/4000) is not wired;
  the balloon chrome is proven and mounted on the floater surface.
- The nine-slice corner metrics are modeled, not instruction-exact.

## Gate

`uiballoon` pins: the three art members; the gzi 9005 resolution through
the resource-id map; the builder law (corner table, content sizing,
medium/large); the live floater on balloon chrome. Targeted soak
`uiballoon,corpus`: PASS 6/0. Full default gate: PARITY.md R194 row.
