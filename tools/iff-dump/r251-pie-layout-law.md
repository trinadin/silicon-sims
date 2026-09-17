# R251 — cTSPieMenu::Layout decoded: the 8-case anchor table, the item record, and the polar hit-test (UI-14)

Date: 2026-09-17. Binary: `game-data/The Sims/The Sims Complete` (PPC, addresses =
file offsets). Regenerable with `python3 tools/iff-dump/ppc_decode.py "<binary>" 0xSTART 0xEND`.
This round closes the r191 residual ("Layout's 8-case anchor table with the geometry
pads — a multi-hour derivation") and rewrites the people-pie ring on decoded law.

Decoder caveats used throughout (both pre-known and re-proven this round):

- `.long 7cXX00d0` renderings are `neg rD, rX` (the tool's XO gap; verified by hand:
  0x7C0000D0 = XO 104 = neg, and the signed-halving idiom `rlwinm sign; add; srawi 1`
  that always precedes it).
- BO=12/BI=2 conditionals print as `bne` but are `beq` (r140 note); the same swap
  applies to the LT/GT forms used here: printed `bne(0)` with a 0x41 first byte =
  `blt`, printed `beq(0)` with 0x40 = `bge`, printed `beq(1)` with 0x40 = `ble`,
  printed `bne(1)` with 0x41 = `bgt`. Rule: invert the printed sense (0x41-prefixed
  words are the "branch if true" family).
- All raw words cited below were verified against the file bytes, not just the tool.

## Method inventory (symbol trailers, 0x526c00–0x52c000)

Every cTSPieMenu method is named by its CW trailer. The load-bearing ones:
Layout 0x5290c0 (`Layout__10cTSPieMenuFv`), ReLayout 0x529060-ish, TSPaint 0x5288b0,
DrawFramedLabel 0x528bb0, DrawLabelFrame 0x528d10, CalcItem 0x5275b0
(`CalcItem__10cTSPieMenuFll`), SetSelection 0x527a70, TSOnMouseMove 0x527c90,
InsertString 0x52832c (`InsertString__10cTSPieMenuFRC9cTSStringP9cTSWinBtn`),
Clear 0x528600, GetItem 0x526d90, GetCurItemCenterX/Y 0x529950/0x529890
(r191's 0x529882 citation was inside the trailer strings — wrong), the setter
family at 0x529c90–0x529ee0, ctor 0x52a4f0.

## The PieMenuItem record (GetItem 0x526d90 + vector trailers)

`GetItem(pie, i)` = `[vector+8] + i*32` — a `std::vector<PieMenuItem>` at pie+384,
**stride 32**. Record:

| offset | field | proof |
|---|---|---|
| +0 | label (cTSStringC ref; Strlen 0x4f4690 = `[[x]+0]`, Data 0x4f46d0 = `[[x]+12]`) | measured by Layout, drawn by TSPaint via vt+424 |
| +4 | `cTSWinBtn*` sub-button (0 = plain text item) | InsertString trailer `...P9cTSWinBtn`; Layout branches on it |
| +8 | enabled byte | EnableItem 0x527510 `stb r31, 8(r3)`; TSPaint/ClearItem skip disabled |
| +12 | angle, float, DEGREES on a 45° grid | Layout `stfs f1, 12(r21)` |
| +16 | box left (pie-center-relative) | Layout store; GetCurItemCenterX adds +24/2 |
| +20 | box top | Layout store; GetCurItemCenterY adds +28/2 |
| +24 | box width | sub-rect path or measured+6 |
| +28 | box height | sub-rect path or measured+6 |

`Clear` (0x528600) destroys items and resets selection +204 = −1.

## Layout (0x5290c0–0x529730), step by step

1. **Background size**: vt+20 → +228 bg buffer; +260 = buf width [+28], +264 = height
   [+32]. +252 = −(w/2), +256 = −(h/2) (negated halves, round-toward-zero).
   +336 (the inactive-radius field, see setter map) initialized: 8 if no bg buffer,
   else half of max(w,h) (w==h → w/2 else h/2).
2. **Title measure** (this+240 cTSString): font(+220) vt+88 measure → +276 = −(w+6),
   +280 = −(h+6), +268 = right/2, +272 = right/2 (title box fields, tinted/drawn by
   the vt+424 title call in TSPaint 0x5289b0–0x5289d0).
3. **Bounds**: with m = +348 (spoke radius): left = −w/2 − m, right = w/2 + m,
   top = −h/2 − m, bottom = h/2 + m (registers r27/r28/r29/r30).
4. **Per item i** (loop bound +388 = count; items via GetItem):
   - r7 = item.+4; **r7 == 0 → text path**: font vt+88 measure → r19 = (right+3)−(left−3)
     = w+6, r20 = (bottom+3)−(top−3) = h+6 — the UI-11 ±3px/side law again.
     **r7 != 0 → button path**: r19 = [btn+124]−[btn+116], r20 = [btn+128]−[btn+120] —
     the button's current rect {left,top,right,bottom} at +116..+128 (the same fields
     `cTSWin::MoveTo` 0x260ba0 writes via vt+104).
   - r4 = +328, then r3 = r0 = r6 = 0 (three `li 0`, raw-verified).
   - `if (i < +328)` → anchor path; else → stacking paths (below). +328 is computed
     at function entry from the count: count 0 → 1; 1–2 → count+2; 3–4 → count+4;
     ≥5 → count+8. That is count + nextPow2(count) (pad ∈ {1,2,4,8}), so **+328 > count
     for every reachable count and every item takes the anchor path**; the stacking
     paths (x = −w6/2, y = item[i−2].y ± box heights — a vertical stack for the
     overflow-list configuration) are dead code unless +328 is written elsewhere.
5. **Angle** (0x5294d8–0x529524): idx = (r6 + [+332]) & 7 with r6 = the per-item
   direction carrier; f1 = [r25+8] × ((2 − idx) − [r24+16]) with the int→double
   conversion via the 2^52 magic at [r24+16] (pool resolved in UI-15: r24 → file
   0x5A50F8, r25 → file 0x5A50E0): **[r25+8] = π/4 (0.785398)** and the normalize
   loop `while f1 < [r24+8] (= 0.0d): f1 += [r25+12] (= 2π, 6.28319)` — **the
   angle field is RADIANS on a π/4 (45°) grid, not degrees**; angle(0) = π/2
   (north), decreasing by π/4 per index (clockwise-by-index in the engine's
   convention; CalcItem's inverse formula subtracts: `fnmsubs f1, f4, f1, f2` =   direction − π/4×(firstDir − D1), i.e. index ∝ −angle).
6. **Anchor table** (0x52948c–0x5295ec): a binary search sends +328 ∈ {1,2,4,8} to
   no-op `or` merges (r22/r23/r31 |= r6, all provably-0 operands — CW noise), then
   `idx = (r6 + [+332]) & 7; if idx ≤ 7 → jump table[TOC−17284][idx]`. The eight case
   bodies, with w6 = item w, h6 = item h, padA = +372, padB = +376
   (**people pie 60/30 per cWinPeople::Init 0x28f0e4/0x28f0f4 and 0x28f1ec/0x28f1fc;
   interaction pie 6/40 per cDDDSimsView::Init; ctor defaults 40/20**):

   | case | body | box left | box top | compass role |
   |---|---|---|---|---|
   | 0x529540 | `-(w6/2)`, `-(padA+h6)` (both neg-idioms) | −w6/2 | −(60+h6) | N — centered above, 60 gap |
   | 0x529560 | `lwz r3,376`; `-(padB+h6)` | +30 | −(30+h6) | NE — up-right, 30 gaps |
   | 0x529570 | `lwz r3,372`; `-(h6/2)` | +60 | −h6/2 | E — right, vcentered, 60 gap |
   | 0x529588 | `lwz r0,376`; `or r0,r3,r0` | **0** (r3 stays 0, raw-verified 0x7c030378 = `or r0,r3,r0`) | +30 | SE — raw decode puts the box's left edge at center-x (asymmetry disclosed below) |
   | 0x529594 | `-(w6/2)`; `lwz r0,372` | −w6/2 | +60 | S — centered below, 60 gap |
   | 0x5295ac | `-(padB+w6)`; `lwz r0,376` | −(30+w6) | +30 | SW — down-left |
   | 0x5295bc | `-(padA+w6)`; `-(h6/2)` | −(60+w6) | −h6/2 | W — left, vcentered, 60 gap |
   | 0x5295dc | `-(padB+w6)`; `-(padB+h6)` | −(30+w6) | −(30+h6) | NW — up-left |

   The compass reading is forced by the geometry (four cardinals at the padA gap, two
   corner cases at padB, two close cases), by the code order matching a clockwise
   ring from N, and by the port's independently-validated interaction-pie dirConfig
   model (i==0 top, i==dirConfig/2 bottom, sides) running the SAME Layout and table.
7. **Store + clamp** (0x5295f0–0x52963c): item+16 = left, +20 = top, +24 = w6,
   +28 = h6, +12 = angle; then
   `left = clamp(left, −w/2−m, w/2+m)`, `top = clamp(top, −h/2−m, h/2+m)`
   (m = +348 = spoke radius, default **8**, ctor 0x52a5c8). For the people pie
   (bg 201) the clamp window is ±108 — inert for normal label sizes.

**The geometry this produces**: labels and portraits live INSIDE the pie disc, at
60 px (cardinal) / 30 px (diagonal) anchor gaps from the pie center — the r150/r90
"radii" never position anything; they bound only the polar hit-fallback. That also
explains the interaction pie's 6/40 pads: its labels hug the cursor point. The old
port model (portraits r=75, labels on the r=150 ring) was an outside-the-disc guess.

## Selected-item accessors

- GetCurItemCenterX 0x529950 / Y 0x529890: guard (+388 != 0 && +204 >= 0), then
  GetItem(+204) twice: return x + w/2 (resp. y + h/2) — **the center of the
  SELECTED item's box**. This is the portrait-mount law: cWinPeople's slot buttons
  ride at the item box centers.
- GetPopupCenterX/Y = +292/+296; GetPopupDownX/Y = +288/+…; the mouse handlers use
  +300/+304 as the polar origin.
- CalcItem 0x5275b0 (the vt+456 hit-test used by TSOnMouseMove 0x527c90 /
  TSOnMouseDownL): (1) linear scan — first item whose box contains (dx,dy) wins;
  (2) else boundary checks against +336 (inactive radius) and +356 (max radius);
  (3) else the polar fallback: index = trunc(D0 + (direction(+344) − 45×(firstDir(+332)
  − D1)) × ((dirbits(+328) − D1)/360)) normalized by ±360 steps, −1 if out of range
  or equal to count; (4) disabled items (byte +8) return −1. **Rect first, angles
  only as fallback.**

## Color law (TSPaint 0x528a74–0x528aa0 + setter bodies) — CORRECTED in UI-15

- SetSelectedColor → **+216** (0x529dd0), SetHighlightColor → **+212**,
  SetLowlightColor → **+208** (0x529e50) — raw-verified setter bodies.
- Per item (raw words 0x528a74–0x528a9c; 0x528a80 `0x4082001c` = **BNE**,
  BO=4 — this section's first version misread it as beq): `i != +204`
  (**untracked**) → +208; `i == +204` (tracked) && press-flag clear → +212;
  tracked && press-flag set → +216. So for the people pie: **the untracked
  labels draw the LOWLIGHT color (0,255,255 cyan); the tracked Sim's label
  draws (165,195,214) idle and flips WHITE (255,255,255) while the press is
  held on it.** (This inverts this section's original reading and restores
  the intuitive mapping: the majority of labels are muted, the pointed-at
  Sim is highlighted.) Frame tint for every item = +100 (people pie:
  0,40,140 base blue; interaction pie: 187,187,187).
- DrawFramedLabel draws the text inset at (left+3, top+3) — top-left, no centering.

## cWinPeople portrait plumbing (re-verified)

- Init 0x28eee0–0x28f244: both pies get SetMaxRadius(150) (vt+488 → +356),
  SetInactiveRadius(35) (→ +336), SetFont(1), the four colors, +372=60/+376=30,
  owner +80, bg buffer +208 with the magenta key. **No +332/+348 writes** →
  firstDirection 0, spoke radius 8 for both pies.
- The 8 `this->240[]` slot buttons (loop bound `cmpwi 8` @0x28f340) get flags
  (+236/+240 = 1), a callback field, and a window mount — **no positions anywhere in
  the loop** (r159 confirmed; the port positions them from the item boxes).
- The pictured `this->340[]` buttons (7 iterations, `cmpwi 7` @0x28f45c) load
  pictures from a TOC-indirected resource table and `cTSWin::MoveTo` from a
  TOC-indirected 7×(x,y) static table — the gauge portrait strip, not pie content;
  the table bytes are not recoverable statically (PEF-relocated pointer chain).

## Disclosed residuals — RESOLUTION STATUS (UI-15)

1. **Jump-table order — RESOLVED, assumption CONFIRMED.** The PEF container was
   fully parsed (container header, section table, loader section, pattern-data
   expansion, relocation simulation — `tools/iff-dump/pef_load.py`, format from
   the archived Apple "MacOS Runtime Architectures" ch. 8). The TOC lives at
   data offset 0x8000; the TOC slot at −17284 holds the jump-table pointer
   0x75F78; the eight table words at data 0x75F78 = 0x5206B0, 0x5206D0,
   0x5206E0, 0x5206F8, 0x520704, 0x52071C, 0x52072C, 0x52074C = the case
   bodies at files 0x529540, 0x529560, 0x529570, 0x529588, 0x529594, 0x5295AC,
   0x5295BC, 0x5295DC **in table order = code order** — the port's
   (N, NE, E, SE, S, SW, W, NW) mapping is byte-exact, no longer just
   corroborated. The angle math (π/4 × (2 − idx)) triple-confirms it.
2. **SE asymmetry — VERIFIED at store level.** The SE case (0x529588:
   `lwz r0,376(r26)`; `or r0,r3,r0`; `b 0x5295f0`) never writes r3; the common
   store (0x5295f0 `stw r3,16(r21)` = LEFT, 0x5295f8 `stw r0,20(r21)` = TOP)
   therefore records left = 0, top = +30. Genuine engine behavior, ported as
   decoded.
3. **Item sizing — RESOLVED: text law is engine-exact.** The pie-fill loop
   (0x183c28, second site 0x183b94) calls the InsertString virtual (vt+560)
   with **r5 = 0** (`addi r5,r0,0` at 0x183b98/0x183c30) — people-pie items
   carry NO sub-button, so Layout's text path (measured name + 6) is the law
   the engine itself takes for this pie.
4. **Float-pool bases — RESOLVED.** r24 = TOC−17280 → 0x59C268 (file 0x5A50F8),
   r25 = TOC−17276 → 0x59C250 (file 0x5A50E0): [r25+8] = π/4, [r25+12] = 2π,
   [r24+8] = 0.0d (normalize bound), [r24+16]/[r24+24] = the 2^52 int→double
   conversion constants. The r251 first reading ("45.0f/360.0f") was wrong —
   the angle is radians (see step 5).
5. **Gauge-strip MoveTo table — CLOSED as runtime data.** cWinPeople::Init
   loads the table pointer from TOC−20416 → data 0x933E8, which is in the BSS
   (beyond unpackedLength 0x7BF80): the 7×(x,y) pairs are computed at runtime
   (panel layout), not stored constants — and the strip is cosmetic, not pie
   content.
6. **Color law correction (new in UI-15).** This doc's first color section
   misread branch 0x528a80 (0x4082001c = BNE, not beq): the correct law is
   untracked → +208, tracked idle → +212, tracked press → +216. Both pie
   ports were fixed accordingly.

## Port

- `UIOriginalPeoplePie.cs` rewritten on this law: `SlotBox(k, w, h)` = the decoded
  case table (60/30 pads, raw SE), label boxes = measured name + 6, portraits
  centered at box centers, state colors (tracked cyan / idle 165,195,214 /
  press-white), base-blue frames, (3,3) text inset, rect hit-test, clamp ±108.
- `uipie` gate refreshed: anchor-box pins replace the modeled 45°-ring position
  pins (engine submodule AutotestRunner.cs — coordinator review required).
