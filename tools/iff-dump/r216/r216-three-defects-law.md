# R216 — three residuals of the R215 defect round, root-caused

User report after R215: (1) the audio sliders "look better but now stretch
across the entire screen"; (2) the friend count digit "still goes too low off
the screen"; (3) "the floor tool still seems.. odd".

## 1 — the slider middle piece: scale is a MULTIPLIER, not a pixel width

The R215 port of `cTSWinSlider::TSPaint` @0x52c7c0 (4-piece strip: caps and
thumb at natural 11px, middle stretched across the logical width) was
correctly decoded but wrongly transcribed: `UIElement.DrawLocalTexture`
(FSO.UI/Framework/UIElement.cs:902) passes its `scale` argument straight into
`SpriteBatch.Draw` — an XNA **multiplier** on the source rectangle. R215
passed the pixel width (130−22 = 108) as the multiplier: an 11px sample at
×108 = 1188px — literally across the screen, with both caps correct at the
ends (the user's "look better but…").

PORT: `UIOptSlider.Draw` now divides — `new Vector2((w − 2·pw) / pw, 1f)` —
so the trough spans exactly x = 11..119 on the 130px logical width. Gate:
`uiopts` pins `MiddleScaleXForProbe × PieceWidthForProbe == 108`.

## 2 — the friend digit sits ON the money line (the R205/R215 vertical drop was a misread)

Fresh decode of the creation block in `cWinViewControl::Init`
(`r216-disasm-money-friend-block.txt`, 0x2b7240–0x2b7608):

- **The money gadget's own SetArea** (0x2b72f4, `vt+0x68`) is called as
  `(left=[+0x74], top=[+0x78], right=[+0x74]+W, bottom=[+0x78]+fontH)` where
  W = `font->vt[0x70]("$9,999,999")` (the worst-case measure) and
  fontH = `font->vt[0x60]()` (TSCharHeight). This PINS the gadget rect layout
  `{+0x74=left, +0x78=top, +0x7c=right, +0x80=bottom}` (cTSWin = Windows
  order, not QuickDraw) and the SetArea argument order
  `(left, top, right, bottom)`.
- **The anchor reposition** (0x2b733c) then writes
  `(178−W, 158, 178, 158+fontH)` from the plate-anchor globals — the r144
  "(178,158)" decode confirmed, now with the full box.
- **The friend gadget** (0x2b7520–0x2b75a8, `this+0x1a8`) loads
  r17 = money[+0x78] (= money TOP), r16 = money[+0x74] (= money LEFT), and
  calls the SAME font pair: `vt[0x60]()` → fontH (r14 = moneyTop+fontH — the
  friend gadget's own BOTTOM, not "moneyBottom"), `vt[0x70](caption)` → w.
  The final SetArea args:
  `(moneyLeft−w + this[0x1c], moneyTop + this[0x20], moneyLeft + this[0x1c],
  moneyTop+fontH + this[0x20])` — i.e.
  **SetArea(moneyLeft−w, moneyTop, moneyLeft, moneyTop+fontH)**.

So the engine paints the friend digit on the MONEY LINE, right-aligned to
the fixed worst-case money box's left edge (x = 178−W ≈ 90). R144's prose
"(moneyLeft−w, moneyBottom, …)" — inherited by R205 (`Y = 158 + LineHeight`)
and R215 — misread which register anchored the vertical pair. The drop was
pure port error: at LineHeight ≈ 13 the digit landed ~13px under the band
(the user-visible "too low off the screen").

PORT: `PositionFriendReadout` sets `Y = 158` (same line; X law unchanged
from R215 — the fixed worst-case anchor). The two plate hotspots are now
DISJOINT (every matching ListenForMouse rect fires, and R215's zones
overlapped in y 171..180: one click opened BOTH the budget and friend
dialogs): budget (90,144,92,36), friend (64,154,26,26).
Gate: `uifriend` geometry re-pinned to `Y == MoneyOriginal.Y`.

## 3 — the floor rim law belongs to the TOOL, not the primitive

R215 put `TileIsFloorable`'s rim rejection INSIDE
`VMArchitectureTools.FloorPatternRect`. Two consequences:

- **Wrong layer vs the engine**: `cNewFloorTool::TileIsFloorable` @0x17a6f0
  gates the tiles the TOOL accumulates — the architecture write primitive is
  unrestricted. In the port, `VMLotTerrainRestoreTools.RestoreTerrain` (the
  TS1 lot-setup path) writes FULL-LOT strips through FloorPatternRect with
  `DisableClip = true` — clearing roads/sea (0,0,w,5) / (w−7,0,7,h) / … and
  the water fill (1,1,w−3,h−3). R215's intersection clipped those writes: a
  1-tile stale border of road/water patterns survived every terrain restore
  — part of the user's "still seems odd".
- The preview and placement DID agree (both flow through the command list),
  so the visible residue was the stale rim + the geometric approximation.

PORT (FreeSO submodule):
- `FloorPatternRect` reverted to its pre-R215 bounds (rect mode:
  `DisableClip ? full lot : BuildableArea`; dot mode: OutsideClip only).
- New `ClipFloorRectToFloorable(target, rect)` — the TOOL-layer port of the
  rim law: TS1 clips to the terrain limit (the 1-tile inset, the port's
  standing geometric approximation of the per-tile flag table, disclosed),
  returns null when the rect lies entirely on the rim.
- `UIFloorPainter.Update` (FSO.UI) clips the pending RECT **before the
  command exists** — preview and placement agree, an all-rim drag shows
  nothing — and clamps/skips the SHIFT-fill seed (the fill walk itself stops
  at pattern changes; the rim carries no placeable floors, so the seed is
  the only lever needed).

Gate: `uibldt` floorRim re-pinned as the TWO-LAYER law — (a)
`ClipFloorRectToFloorable(whole-lot) == (1,1,w−3,h−3)`, (b) an UNCLIPPED
whole-lot rect through the real preview path paints the rim (the primitive
stays free — RestoreTerrain compatibility), (c) the clipped rect leaves the
rim untouched and paints the interior.

## Residuals disclosed

- The flag-table rim remains pinned geometrically (TerrainLimit inset); a
  lot with non-rectangular floorability data would need the table itself
  (not present in the port's lot format). Unchanged from R215.
- `CalculateThumbPosition` @0x52bff0 still not fully decoded — the linear
  thumb travel stays the disclosed model (unchanged; the thumb was never the
  complaint).

## Gate

Targeted soak (uiopts, uifriend, uibldt + corpus): 8/8 PASS first run.
Full gate: **passed=126 failed=0**, AUTOTEST_WRAPPER_EXIT=0.

Evidence: `r216-disasm-money-friend-block.txt` (the 0x2b7240–0x2b7608 block),
`targeted-soak.log`, `gate-run.log`. No proprietary payload.
