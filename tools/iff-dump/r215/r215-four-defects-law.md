# R215 — the user-reported defect round: four UI defects on decoded engine law

User report (4 screenshots): (1) the SOUND options sliders look stretched;
(2) the GRAPHICS options controls misaligned/overlapping; (3) an artifact "0
coming in from the bottom" near the money readout + a question about the
smiley face's placement; (4) floor-tool previews "still go too far to the
edge". All four analyzed with LOCAL pixel tooling only (PIL/numpy ASCII +
component maps — no external image services), per the standing constraint.

Evidence: `r215-disasm-slider.txt`, `r215-disasm-graphics.txt`,
`r215-disasm-floor.txt`. Art analyzed in-place from UIGraphics.far
(`/tmp/r215-art/`, ASCII maps in the round transcript).

## 1 — the volume slider is a 4-PIECE strip, not a stretch

`cTSWinSlider::SetImage(buffer, bool)` @0x52bbf0: stores the buffer at
+0x100, computes the PIECE width `w/4` (srawi 2) = 11px and the piece rect
{0, 0, w/4, h} at +0x104, then RESIZES the slider's area to the art's
natural HEIGHT while keeping the existing WIDTH — so the 130 from the
STR#145[53] directive stays the logical width and 14 the height.

`cTSWinSlider::TSPaint` @0x52c7c0 (horizontal branch, with the rect helpers
OffsetRect 0x26f7c0 / CopyRect 0x26f820 / RectWidth 0x261c80 decoded):
1. draws piece 1 (src {0,0,11,14}) at the area's left — the beveled LEFT CAP;
2. src becomes {11,0,22,14} and the destination spans to `right − 11` —
   the MIDDLE, drawn through a different port call (vtable +0xc4, the
   stretch/tile variant; only the caps/thumb use the plain blit +0xa0);
3. src {22,0,33,14} at {right−11..right} — the RIGHT CAP at natural size;
4. src {33,0,44,14} — the THUMB — drawn at the value position.

The art confirms it: `optslideron/off.bmp` are 44×14 sheets whose ONLY
difference is piece 4 (bright marker vs dim slot); the track pieces are
identical between the sheets. There is NO fractional fill — the marker IS
the value indicator. The port had uniformly scaled the whole sheet ×2.95
horizontally (the reported stretch).

PORT: `UIOptSlider.Draw` now paints cap / stretched middle / cap / thumb
(piece = `w/4`), the thumb at a linear position across the free width —
`CalculateThumbPosition` @0x52bff0 was not fully decoded; the linear thumb
travel is the disclosed model. Gate: `PieceWidthForProbe == 11` added to
`uiopts`.

## 2 — ONE shared Low/Med/High header, not one per radio

The graphics builder (unnamed, ~0x284c48–0x285e12) constructs
`cWinTriRadio` gadgets and exactly ONE `cWinTriText` (the binary's only
`cWinTriText::__ct` call site is 0x285c64 — a whole-binary caller scan).
Its SetArea tail (0x285d98–0x285da0):
`left = radio.left − 8`, `right = radio.right + 5`,
`top = radio.top − fontMetric − 7`, height `fontMetric − 2`, and
`cWinTriText::SetArea` @0x2b2910 splits the area into THREE equal cells
(the 0x55555556 ÷3 idiom) — 31px cells for the 93px area. So the engine
draws ONE column header row ("Low Med High") above the FIRST tri-radio
(y 7..21 for the radio at y=30); the second radio row (y=56) has NO label
row of its own.

The port had called `AddRadioValueLabels` per radio — the second row's
labels (y 33..47) landed exactly on top of the first radio (y 30..50),
which is also geometrically forced: a text-above-radio pair needs ≥ 34px
pitch, the radio pitch is 26. (The tri-radio itself was already right:
`cWinTriRadio::SetArea` @0x2b22e0 places three cells at pitch
`(area_w − cell_w)/2` = 30 for the 80×20 area, and `Init` @0x2b2500 builds
each cell as a cTSWinBtn with `SetImage(sheet, 6 frames, 1 row)` — the
120×20 sheet is six 20px state cells, matching the port's 6-frame crops.)

PORT: `AddRadioGroup` adds the value labels only for the FIRST radio; the
row comment cites the single-ctor-site decode. Gate: `uiopts` re-pinned —
`RadioValueLabels.Count == 3`, all at y=7 (the y=33 row pin retired).

## 3 — the "0" is the friend count; the smiley is correct plate art

Local pixel analysis of the plate twin (`orig_ucp_back.png`) places the
baked smiley at plate pixels (67,168)–(76,177) — the LEFT end of the money
band's second line. It is original `UniversalBack` art, correctly placed;
the engine expects the friend-count DIGITS right beside it.

Engine law (r144 toolbar-law §2 + r205 friend-count law): the money gadget
is sized once for the worst-case `"$9,999,999"` (data 0x5b11e) and
right-anchored at x≤178 — so its LEFT edge is a fixed anchor — and the
friend gadget's rect `(moneyLeft−w, moneyBottom, moneyLeft, moneyBottom+h)`
is computed ONCE at Init against that fixed left edge (creation block
0x2b7520–0x2b75a8). The port's R205 approximation re-derived the anchor
from the LIVE money string every pass, so with short money strings the
digit floated under the money text instead of sitting beside the smiley —
exactly the reported "0 coming in from the bottom".

PORT: `UIDesktopUCP.PositionFriendReadout` now anchors to
`FixedMoneyLeftForProbe` = `178 − Measure("$9,999,999")` (computed once).
Gate: `uifriend` geometry re-pinned to the fixed anchor (plus the
derivation check and an in-band bound).

## 4 — floors obey the unfloorable rim

`cNewFloorTool::TileIsFloorable` @0x17a6f0 decodes as a PER-TILE law, not
a geometric guess: after the lot's tile-index transform, it (1) returns
true outright when a global edit-anywhere flag is set; (2) reads the lot's
per-tile FLAG table (`lot->0x34` rows, byte per tile) and rejects tiles
whose bit 5 (0x20) is set — the unfloorable mark carried by the outer
ring/road tiles; (3) requires the tile's four floor quarter-slots to agree
(no half floors); (4) recurses for support below (0x17a930). For the
standard base-game lots the flag rim equals the same 1-tile inset the
terrain tools already decode to (`TerrainLimit = (1,1,w−2,h−2)` in TS1
mode) — and the port's `FloorPatternRect` was the ONE architecture writer
still clamping to the FULL lot in TS1 mode (`DisableClip`), painting
preview and placement across the rim.

PORT (FreeSO submodule, `VMArchitectureTools.FloorPatternRect`): the
rect-mode bounds now intersect `TerrainLimit` when `DisableClip`, and dot
mode rejects rim tiles — preview AND placement share the path. Gate: a new
live section in `uibldt` drives the REAL preview path
(`SimulateCommands` → `VisFloors`) with a WHOLE-LOT rect using a real
catalog floor style (the dispatch drops unknown patterns — first soak run
caught my synthetic sentinel): the rim tiles must stay untouched while an
interior tile takes the pattern.

## Residuals disclosed

- The slider thumb's travel formula (`CalculateThumbPosition` @0x52bff0)
  not fully decoded — the linear mapping across the free width is the
  model; the middle's stretch-vs-tile is visually equivalent (the 11px
  middle sample is horizontally uniform in the art).
- The flags-table rim is pinned geometrically (the 1-tile inset); a lot
  with non-rectangular floorability data would need the table itself
  (not present in the port's lot format).
- The graphics captions' right-aligned x=480 anchor keeps the 1–2px box
  kiss with the fourth checkbox caption at y=69 (no INK overlap; engine
  anchors as decoded).
