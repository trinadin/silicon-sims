# R187 — live-popup object-scale and pane law

Source: the owner's local `The Sims Complete` PowerPC PEF, SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.
Addresses below are executable file/code addresses. This note records decoded
behavior only; it does not copy executable or game-asset payloads.

## `SetSpecialScale` is a discrete renderer-scale override

`cWinLivePopupClient::SetSpecialScale(int pane, int scale)` at
`0x2798d0..0x2798dc` is only:

```text
entry = this + pane * 0x28
*(int *)(entry + 0x4c) = scale
```

The two fields are therefore client `+0x4c` and `+0x74`. There is no bounds
check and no pixel-size calculation in this setter.

`cWinLivePopupClient::DrawPane` at `0x279d98..0x279dd0` takes the object path
when the pane source type is 1. A zero special-scale field passes null to
`DrawObjectIcon`; a nonzero field passes its address. `DrawObjectIcon` at
`0x237c68..0x237c94` forwards the pane width, pane height, and that pointer to
`cXObject::DrawIconic`.

`cXObject::DrawIconic` proves the pointer's meaning:

- with an override, `0x0cf220..0x0cf250` reads `*override` and calls
  `RenderParam::SetScale` (`0x1db500`) with that value;
- without an override, `0x0cf2fc..0x0cf3d8` starts at scale 3 and tries
  3, 2, then 1 until the measured icon leaves at least three pixels of free
  half-width and half-height;
- both paths measure the rendered bounds and center them. At
  `0x0cf2b8..0x0cf2f4` the fixed path computes
  `x = (paneWidth - boundsWidth) / 2 - bounds.left` and
  `y = (paneHeight - boundsHeight) / 2 - bounds.top`; the automatic path uses
  the same equations at `0x0cf374..0x0cf3b4`.

Consequently, special value **2 is the fixed medium object/DGRP renderer zoom
level**, not a 2x bitmap multiplier. It bypasses the scale-3-to-1 fit search,
but still centers the scale-2 bounds. Overflow is clipped by the pane viewport.
The motive constructor applies the nonzero second scale at
`0x27fc2c..0x27fc40`, so the Hunger row's `{0, 2}` pair means automatic scale
for pane 0 and fixed medium scale for pane 1.

## Popup, text, and pane geometry

`cWinLivePopup::Init` at `0x27966c..0x279684` preserves the current left/top
and establishes an outer **559 x 127** window. `RebuildBuffer` at
`0x278630..0x278cf0` keeps width 559 and computes

```text
H = max(127, 24 + titleFontHeight + sum(wrapped-body line heights)).
```

For the shipped single-byte font path this is
`max(127, 43 + 17 * bodyLineCount)`: title height 19, body pitch 17.

The pane rectangles are literal, index-specific coordinates written at
`0x27863c..0x278678`:

| pane | initial local rect |
|---|---|
| 0 | `(0, 0, 168, 127)` |
| 1 | `(372, 0, 540, 127)` |

Pane 1 is not right-aligned to x=559; it ends at 540, leaving a 19-pixel
right inset. `HasPane` (`0x279b00..0x279b30`) tests only whether the pane's
source type is nonzero, so bitmap and object-selector sources reserve the same
168-pixel flank.

In the English/single-byte path, `0x278688..0x2786d8` selects the wrapping
bounds below. `0x278958..0x2789c4` writes the same horizontal bounds into the
title and body draw rectangles:

| populated panes | wrap width | title rect | body rect |
|---|---:|---|---|
| none | 530 | `(15, 12, 545, 31)` | `(15, 31, 545, H-12)` |
| pane 0 only | 367 | `(178, 12, 545, 31)` | `(178, 31, 545, H-12)` |
| pane 1 only | 367 | `(15, 12, 382, 31)` | `(15, 31, 382, H-12)` |
| both | 204 | `(178, 12, 382, 31)` | `(178, 31, 382, H-12)` |

Thus two selectors flank a 204-pixel text column. They are never divided into
adjacent 84-pixel halves of one left pane.

At minimum height, both pane rectangles stay exactly 168 x 127. For a taller
popup, `0x2789cc..0x278b4c` uses an effective height `A`: pane 0 uses `H-50`
only on a 1024-wide framebuffer (otherwise `H`); pane 1 uses `H-50` only in a
double-byte UI (otherwise `H`). If `A <= 127`, its vertical range stays
`0..127`. If `A > 127`, the instruction-literal result is:

```text
top = 5
bottom = 122 + 2 * floor((A - 127) / 2)
```

which is normally `A-5` and is `A-6` when `A-127` is odd.

## Bitmap panes versus selector panes

Pane reservation does not differ. Painting does:

- source type 2 (bitmap), `0x279bb4..0x279d94`, uses either its explicit source
  rectangle or the buffer bounds. Each destination dimension is reduced to
  the source dimension when the pane is larger, centered, then sent through
  the buffer stretch-blit. It never enlarges a bitmap; a source larger than
  the pane is down-fit. An 84 x 63 Job bitmap therefore lands at `(42, 32)` in
  pane 0 at H=127.
- source type 1 (object selector), `0x279d98..0x279dd0`, uses the complete
  168-pixel pane as the `DrawObjectIcon` viewport. Object bounds are rendered
  at the automatic discrete scale or the fixed special scale and centered by
  the equations above.

