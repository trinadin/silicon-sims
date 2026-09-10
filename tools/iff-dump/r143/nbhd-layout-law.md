# R143 — NEIGHBORHOOD SCREEN + PICK-A-FAMILY layout law (The Sims Complete, PPC Mac)

Round mandate: decode the exact UI layout law of the neighborhood screen
(NScreen.BMP chrome) and the Pick-A-Family family-selection screens, r142-style
(anchor tables, SetImage cols/rows, every claim citing a binary address).

Binary: `/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete`
(PPC PEF, addresses == raw file offsets, code [0x8E90, 0x5C22E8)).
Reuse of r142 methodology: capstone (`capdis.py`), TOC slot resolution through
`r142/tsui/data-sec1-unpacked.bin` (image = data section, TOC base = img+0x8000),
C-kind code pointers resolve as `file = stored + 0x8E90`, D-kind data pointers
resolve directly as image offsets.

Key engine facts reused from r142 (tsui-template-law.md):
- `cTSWinBtn::SetImage(art, cols, rows)` @0x50c0f0: W = art->w(+0x1C)/cols,
  H = art->h(+0x20)/rows, then re-SetArea around top-left.
- `cTSWin::SetArea(l,t,r,b)` @0x504860: stores +0x74/+0x78/+0x7c/+0x80.
- `cTSWin::TSWinMoveTo(x,y)` @0x260ba0: keeps size, virtual SetArea.
- Virtual dispatch glue @0x5a29e0 (`vt = *(this); fn = *(vt+slot)`).

---

## 1. SCREEN-SIZE LAW (both screens) — DECODED

Both screens support windows LARGER than 800x600 by **centering the 800x600
artboard** and loading a 1024x768 mask when width > 800:

- `cWinPickFamily::Init` @0x2d99f0:
  ```
  0x2d9aa8-0x2d9af8:  offX = this+0x11c = max(0, (winW - 800)) / 2      (win rect = this+0x74..0x80)
                      offY = this+0x120 = max(0, (winH - 600)) / 2
                      (subf/subfe/srwi sequence = ((a-b)>>1) & (a>=b ? -1 : 0))
  ```
  i.e. `offX = (winW>800) ? (winW-800)/2 : 0`, same for Y. All screen content
  anchors below are `(anchor + offX, anchor + offY)`.
- `cWinNeighborhoodVC::Init` @0x473b20: same computation at 0x474034-0x474084,
  stored at this+0xe4 (offX) / this+0xe8 (offY).
- `cWinNeighborhoodUL::Init` @0x46b6a0: same at 0x46bbc4-0x46bbe4, this+0x130/0x134.
- Large-window mask: kLargeMask 5001 is loaded ONLY when `winW > 0x320 (800)`:
  VC @0x473fa0-0x473fc0 (`cmpwi r0, 0x320; ble`), UL @0x46bb00-0x46bb20,
  stored this+0xd4 (VC) / this+0x118 (UL). UL additionally makes it a 1x1
  cTSWinBtn covering the mask when `!community && winW > 800` @0x46bc74-0x46bcb8.
- Toolbar anchors are passed to SetArea as banner-local values, then every
  normal entry is attached with `banner->AddChild(button)` (VC
  @0x474614-0x474624; UL @0x46c468-0x46c478). `cTSWin::CalcAbsoluteArea`
  @0x509030-0x5090a4 adds the parent top-left, so the complete top strip follows
  offX/offY. Credits are the one root child: their special path adds banner
  left/top explicitly before SetArea.
- `cWinPickFamily::TSPaint` @0x2d96c0 only re-blits the background itself when
  offX>0 || offY>0 (0x2d96d4-0x2d96e8); at exactly 800x600 the base class paint
  path draws it.

---

## 2. PICK-A-FAMILY SCREEN (cWinPickFamily) — DECODED

Class symbols (r141 symbol-index.txt; function start = symbol addr - 2):

| function | addr | size |
|---|---|---|
| `cWinPickFamily::CreateButton(i, name)` | 0x2d9480 | 508 |
| `cWinPickFamily::TSPaint` | 0x2d96c0 | 436 |
| `cWinPickFamily::Init` | 0x2d99f0 | 2080 |
| PAF anchor writer (un-symbolized, after `.__ct__14cWinPickFamilyFv` trailer @0x2da3d0) | 0x2da3f0 | ~220 |
| `cPickFamilyItem::TSPaint` | 0x2c6810 | 280 |
| `cPickFamilyItem::CalcRowCol` | 0x2c6960 | 644 |
| `cPickFamilyItem::SetFamilyID` | 0x2c6c20 | 1016 |
| `cPickFamilyItem::Init` | 0x2c70e0 | 2188 |
| item anchor writer (un-symbolized) | 0x2c7e90 | ~228 |
| `cPickFamilyList::Init` (un-symbolized) | 0x2c9c10 | 1184 |
| `cPickFamilyList::TileItems` | 0x2c9340 | 652 |
| `cPickFamilyList` ctor | 0x2ca590 | 144 |
| `cPickFamilyPersonInfo::Layout` | 0x2ca780 | 1592 |
| `cPickFamilyPersonInfo::Init` | 0x2cb670 | 1252 |

### 2.1 Background & window

- Background art: `LoadBuffer(5041 kPickFamilyBkg PickBkg.bmp 800x600)` at
  0x2d9a80, or `5043 kPickFamilyBkgMagic PickBkgMagic.bmp` at 0x2d9a8c when
  `this+0x141 != 0` (Makin' Magic town); buffer at this+0x124.
- Default-family tile art: `LoadBuffer(5301 kNghDefaultBtn NbhdTileBtn.bmp
  364x62)` at 0x2d9a44 → this+0x128.
- Window/strings: `LoadUIStrings(0x80=128, ...)` @0x2d9b34 (needs >= 8 strings,
  checked @0x2d9b60).

### 2.2 PAF button anchor writer @0x2da3f0 — DECODED

BSS anchor pairs written @0x2da45c-0x2da498 (writer disasm:
`nbhd-disasm-paf-anchorwriter.txt`):

| TOC slot | BSS ptr | value | used for |
|---|---|---|---|
| -0x4cfc | 0x946c8 | (568, 10) | write-only in this binary (not read back) |
| -0x4d00 | 0x946c0 | (668, 10) | write-only |
| -0x4cf8 | 0x946b8 | (41, 76) | family-list top-left |
| -0x4cf0 | 0x94698 | see table below | PAF button anchors |

PAF button anchor table @BSS 0x94698 (8-byte {x,y} entries):

| idx | offset | anchor |
|---|---|---|
| 0 | +0x00 | (322, 530) |
| 1 | +0x08 | (506, 530) |
| 2 | +0x10 | (414, 530) |
| 3 | +0x18 | (200, 529) |

### 2.3 The four PAF buttons — `CreateButton` @0x2d9480 — DECODED

Resource table is a code literal pool @0x5a4820 = `[5103, 5106, 5105, 5104]`
(TOC-0x4cf4 → stored 0x59b990, file = 0x59b990 + 0x8E90).

- idx 0..2: `new cTSWinBtn(396)` (ctor 0x50d720 @0x2d95bc),
  `LoadBuffer(tbl[idx])`, `SetImage(art, cols=4, rows=1)` @0x2d95e0-0x2d95f8,
  then `TSWinMoveTo(anchor.x+offX, anchor.y+offY)` @0x2d9600-0x2d9634,
  btn+0x163 = 1.
- idx 3: `new(0x1a4=420)` ctor 0x500880 (`cTSSystemButton`)
  @0x2d94a8-0x2d94bc; `SetArea(l,t,l+200,t+62)` @0x2d94c8-0x2d94ec
  (62 = NbhdTileBtn sheet height), then `CenterOnPoint` (0x4ffc70, decoded =
  x-pt.W/2, y-pt.H/2) @0x2d94f4-0x2d953c with point
  (anchor.x+offX, anchor.y+offY + 62/2). This 200x62 area is provisional:
  assigning STR#128[5] `Cancel` in font[12] changes the final width to 121px
  and recenters it. STR#128[15] `Back to Neighborhood` is its tooltip. Its
  91x62 state source is painted with the native 45/1/45 law; see
  `../r183/r183-cas-system-button-law.md`.

| idx | res | art (sheet) | cols,rows | per-state | anchor (+offX/offY) | final RECT |
|---|---|---|---|---|---|---|
| 0 | 5103 kPickFamilyAdd | PAFAddFamily.bmp 368x62 | 4,1 | **92x62** | (322,530) | **(322,530,414,592)** |
| 1 | 5106 kPickFamilyMoveIn | PAFMoveIn.bmp 656x62 | 4,1 | **164x62** | (506,530) | **(506,530,670,592)** |
| 2 | 5105 kPickFamilyDelete | PAFDeleteFamily.bmp 368x62 | 4,1 | **92x62** | (414,530) | **(414,530,506,592)** |
| 3 | 5301 kNghDefaultBtn (`cTSSystemButton`) | NbhdTileBtn.bmp 364x62 | 4,1; 45/1/45 paint | **121x62 final** | centered on (200,529+31) | **(139,529,260,591)** |

- 5104 kPickFamilyCancel (PAFCancel.bmp 744x49): **present in the literal pool
  (0x5a482c) but never loaded by any code path found** — DEAD/unused art in
  this binary. The live Cancel/back control is button 3, rendered as text over
  RT 5301 rather than from PAFCancel.bmp.
- Bottom row law: Add (322..414) + Delete (414..506) adjacent, MoveIn
  (506..670), all y=530, bottom 592 = 600-8. The Cancel system button is
  (139,529)..(260,591).

### 2.4 Top text bar + scroll arrows — DECODED (cWinPickFamily::Init)

- Top marquee text: `new cTSWinText(264)` ctor 0x538a50 @0x2d9c40-0x2d9c54 →
  this+0x138; font = [TOC-0x7178]+0x30; string id 9 tooltip;
  `SetArea(0, offY+6, winW, offY+6+fontH)` @0x2d9c7c-0x2d9ccc
  (r27=0, r6=winW, r7=GetH(font)+6+offY); vt+0x1f4(2), vt+0x1ec(0).
- Three transparent hotspot windows (`new cTSWin(204)` ctor 0x509610 — no art,
  tooltips only), each `SetArea(ax, ay, ax+0x1a, ay+0x17)` = **26x23**:

| window | anchor (+offX/offY) | RECT | tooltip string |
|---|---|---|---|
| arrow up/left | (0x24a=586, 0x2a=42) or (563, 42) if magic | (586,42,612,65) | 11 |
| arrow down/right | (0x296=662, 42) or (637, 42) if magic | (662,42,688,65) | 12 |
| magic 3rd | (0x2b2=690, 42), magic only | (690,42,716,65) | 18 |

  (@0x2d9e64-0x2d9ec4, 0x2d9f34-0x2d9f94, 0x2da010-0x2da054; the magic x-shift
  comes from `(-23/-25) & (magic ? -1 : 0)` @0x2d9e68-0x2d9e90.)
  INTERPRETED: these are click zones over arrow graphics painted in
  PickBkg.bmp's top strip (no art is attached at runtime).
- Two label strings (CTGString) at this+0x110 / this+0x114 @0x2d9dd0-0x2d9e2c.

### 2.5 Family list geometry — DECODED

`cPickFamilyList` (284 bytes, ctor 0x2ca590; ctor sets visible-items=6 at
+0xf4, topRow=+0xf8, itemH=+0xfc, originY=+0x100=0, selected=+0x104=-1):

- Position: `TSWinMoveTo(list, 41+offX, 76+offY)` @0x2d9bc0-0x2d9bec
  (anchor pair TOC-0x4cf8 = (41,76) from writer @0x2da474).
- `cPickFamilyList::Init` @0x2c9c10:
  - creates one `cPickFamilyItem` (488 bytes, ctor 0x2c7ad0) per family,
    `SetFamilyID` each, AddChild (loop @0x2c9cd0-0x2c9d94).
  - item width/height taken from item 0's rect @0x2c9d9c-0x2c9dd4; if the
    neighborhood is empty: defaults W=0x2bc (700), H=0x4b (75) @0x2c9ddc-0x2c9de4.
  - scrollbar: `new(0x148=328)` ctor 0x5269f0 @0x2c9de8-0x2c9e04 → list+0xdc;
    initial `SetArea(sb.x, sb.y, sb.x+1, sb.y+6*itemH)` @0x2c9e08-0x2c9e2c
    (vertical 1px track, 6 visible items);
    `LoadBuffer(5018 kPickScrollbar ScrollBar.bmp 300x30)` @0x2c9e58 →
    list+0xe0, then `SetThumbImage(buf, 0)` via vt+0x1c0 @0x2c9e84-0x2c9e9c
    (the 328-byte scrollbar class derives its pieces from the sheet —
    internal slicing not decoded, INTERPRETED);
    final `SetArea(W+32, 0, W+33, 6*itemH)` @0x2c9ea0-0x2c9edc in list space
    (W = item width; sb sits at x = W+32, right of the cards);
    if item count > 6 @0x2c9ef8: enable, vt+0x1b4(0)=min, vt+0x1b8(count-6)=max,
    vt+0x1b0(topRow)=value, vt+0x1fc(6)=pagesize.
  - list width = `sbWidth + itemW + 32` @0x2c9fa0-0x2c9fd4 (SetArea keeps
    current top/bottom; the list is a logical container, children unclipped —
    INTERPRETED from ctor default zero-size + member strip overflow, see 2.6).
  - `LoadBuffer(5019 kPickFamilyItemBkg, list+0xf0)` @0x2ca070-0x2ca07c
    (list's own copy, for empty-list placeholder painting).
- `TileItems` @0x2c9340: single-column vertical tiling —
  point (0, -topRow*itemH + originY), each visible item `MoveTo(0, y)`,
  `y += itemH` (0x2c9500-0x2c95a8; per-item advance (0, itemH) @0x2c9538-0x2c9550,
  wrap resets to start; x-advance slot is 0 → never wraps horizontally).

**List absolute geometry (800x600): cards at (41,76) .. (41+690, 76+6*68) =
(41,76,731,484); scrollbar track at x=763, y=76..484.**

### 2.6 Family card (cPickFamilyItem, 488 bytes) — DECODED

`Init` @0x2c70e0:

- Static sheet `LoadBuffer(5019 kPickFamilyItemBkg FamilyPickItemBkg.bmp
  690x204)` (once, into BSS via TOC-0x68fc) @0x2c7144-0x2c715c;
  `SetImage(art, cols=1, rows=3)` @0x2c71bc-0x2c71f4 → **card = 690 x 68**
  (three stacked states: normal/hilite/selected); cardH = 204/3 = 68 stored at
  item+0x1d0 @0x2c71c0-0x2c71e0 (mulhw 0x55555556 signed /3).
- Smiley `LoadBuffer(5021 kPickFamilySmiley Smiley.bmp 20x23)` (static,
  TOC-0x68f8) only if `0x37380(16) < 0` @0x2c7160-0x2c71b8; drawn by TSPaint
  @0x2c6838-0x2c6900 at anchor **(657, 19)** (TOC-0x4d98, writer @0x2c7f3c)
  when child +0x5c visible — marks the "default/current" family.
- 8 member slots @0x2c7214-0x2c72d8: `new cTSWinBtn(400)` ctor 0x50d720 +
  vtable patched to [TOC-0x6900]; each `SetArea(0,0,45,68)` → **45x68**.
- Member strip packing @0x2c72dc-0x2c74bc: start point **(110, 14)** (anchor
  TOC-0x4da4 = (110,14), writer @0x2c7f08), x-step **48**, wrap when
  `x > 494` (bound @+0x70 = 110+0x180), row step 48:
  `member_i = (110 + i*48, 14)`, i = 0..7 — 8 portraits, last ends 446+45=491.
- Four `cTSWinText` windows (ctor 0x538a50), font [TOC-0x7178]+0x28/+0x30,
  color via 0x537bc0(text, [TOC-0x7204]); placeholder strings from the packed
  blob at img 0x5b990 (TOC-0x4d9c): "The Symptoms\0%d\0$999,999,999\0 0\0 12\0".

| member | content | anchor (card coords) | source |
|---|---|---|---|
| item+0x1d4 | family name ("The Symptoms") | **(20, 0)** (y+4 for languages 0x11,0x12,0x14,0xf) | SetFamilyID @0x2c6d14-0x2d6d74; anchor TOC-0x4da0=(20,0) writer @0x2c7f14 |
| item+0x1d8 | funds ("$999,999,999") | **(526, 21)** or (494,21) magic | anchors TOC-0x4dac/-0x4da8, writer @0x2c7f1c-0x2c7f30 |
| item+0x1dc | magic-only counter ("0") | **(649, 21)**, hidden unless magic | TOC-0x4db0, writer @0x2c7f44; vt+0xa0 hide @0x2c7820 |
| item+0x1e0 | member count ("12") | **(620, 21)** or (606,21) magic | TOC-0x4db8/-0x4db4, writer @0x2c7f24-0x2c7f38 |

`SetFamilyID` @0x2c6c20 fills real values: name (0x2c6cac-0x2c6cec),
funds=|item+0x1c0| (0x2c6d8c-0x2c6dc0), [item+0x1cc] (0x2c6dd4-0x2c6df8),
[item+0x1c4] (0x2c6e00-0x2c6e34); per member (loop 0x2c6e44-0x2c6fa0):
`SetImage(personSheet, cols=6, rows=3)` — cols=**4** if child (age test
@0x2c6ee0-0x2c6f08; r28=6 default / 4 child, passed as cols @0x2c6f1c-0x2c6f28) —
then `SetArea(l,t,r,t+68)` and `(l,t,l+45,b)` → final **45x68**; extras hidden
via vt+0xa0.

Text colors (`CalcRowCol` @0x2c6960, colors made by the un-symbolized
`InitSimsColors` @0x25d600):

| state | color | RGB | address |
|---|---|---|---|
| selected card text | [TOC-0x6b0c] | **(0, 255, 255)** cyan | created @0x25d6ec-0x25d710 |
| unselected + hover | [TOC-0x6b38] | **(255, 255, 255)** | @0x25d6b4-0x25d6d8 |
| unselected | [TOC-0x6d68] | **(195, 205, 205)** | @0x25d67c-0x25d6a0 |

(also created there: (166,180,180) → TOC-0x6b08 @0x25d65c; (64,93,95) →
TOC-0x6b3c @0x25d728; (0,0,82)/(0,0,74)/(209,0,0)/(0,202,…) follow.)
Colors are stored as 16-bit values (`clrlwi r3, 0x10`).

### 2.7 Person info popup (hover on member) — DECODED

`cPickFamilyPersonInfo::Init` @0x2cb670: `SetArea(0, 0, 0x71, 0x7a)` →
**113 x 122** @0x2cb708-0x2cb724; art `LoadBuffer(5300
kPickFamilyPersonInfoBkg PersonInfoBkg.bmp 36x36)` → this+0xdc @0x2cb6ec-0x2cb704.
Contains a cWinPeople personality panel at this+0xd0 (0x291bf0 ctor).
`Layout` @0x2ca780: margins **(15, 10)** (this+0xe8/+0xec @0x2ca838-0x2ca844),
name sized to text, personality rows at stride = person row height
(0x2ca994-0x2ca9d8), tooltip rect width 0xc8 (200) @0x2ca790.

---

## 3. NEIGHBORHOOD SCREEN — DECODED

Two variants: `cWinNeighborhoodVC` (classic; symbols `…VCF…`) and
`cWinNeighborhoodUL` (Unleashed/community). Same layout law, different art.

| function | addr | size |
|---|---|---|
| `cWinNeighborhoodVC::Init` | 0x473b20 | 3948 |
| `cWinNeighborhoodVC` ctor | 0x474bb0 | ~380 |
| neighborhood anchor writer (un-symbolized, after VC ctor) | 0x474d10 | ~380 |
| resource table loader (un-symbolized, called from VC ctor) | 0x46f960 | 1036 |
| `cWinNeighborhoodUL::Init` | 0x46b6a0 | 5312 |
| `cWinNeighborhoodUL` ctor | 0x46ccb0 | ~520 |
| `cWinLotBtn` ctor | 0x2d5290 | 208 |
| `cWinLotPopup::Init` (un-symbolized, trailer @0x2d7518) | 0x2d753c | ~524 |

### 3.1 Anchor writer @0x474d10 — DECODED

Writes {x,y} pairs to the BSS anchor array [TOC-0x46ec] @0x474dcc-0x474e38
(disasm: `nbhd-disasm-nbhd-anchorwriter.txt`):

| off | x | y | consumer |
|---|---|---|---|
| +0x00 | 200 | 0 | toolbar btn 0 (MoveIn) |
| +0x08 | 300 | 0 | toolbar btn 1 (Bulldoze) |
| +0x10 | 600 | 0 | toolbar btn 2 (Exit) |
| +0x18 | 762 | 0 | toolbar btn 3 (Internet) |
| +0x20 | 195 | 540 | toolbar btn 4 (Credits) — parked offscreen, see below |
| +0x28 | 106 | 0 | toolbar btn 5 (Previous) |
| +0x30 | 141 | 0 | toolbar btn 6 (Next) |
| +0x38 | 250 | 0 | toolbar btn 7 (Import) |
| +0x40 | 400 | 0 | toolbar btn 8 (Downtown) |
| +0x48 | 5 | 0 | toolbar btn 9 (SimsLogo, 1x1) |
| +0x50 | 450 | 0 | toolbar btn 10 (Vacation) |
| +0x58 | 350 | 0 | toolbar btn 11 (Rezone) |
| +0x60 | 500 | 0 | toolbar btn 12 (Studiotown) |
| +0x68 | 550 | 0 | toolbar btn 13 (Magicland) |

The language switch in VC::Init (@0x474240-0x474314, jump table [TOC-0x46f4])
overwrites **+0x20/+0x24** with the bottom credits anchor: (41,543)
Fr/De/Sp/En, (19,524) Jp. A second pair {10, 140} is written to TOC-0x46f8
@0x474e3c.

### 3.2 Toolbar — the 14 buttons — DECODED

Loop @0x474488-0x4748e4 (r29 = 0..0xd), resources from the static data table at
img 0x6ce40 ([TOC-0x46e8]): `[5200, 5201, 5202, 5306, 5307, 5204, 5205, 5207,
5208, 5210, 5213, 5215, 5216, 5221]`. Entry +0x10 (idx 4) is replaced at
runtime with 5312..5316 (localized CreditBtn), entry +0x24 is not a button res.

For entries other than credits, `anchor` and `RECT` below are **banner-local**;
their final window coordinates add `(offX,offY)` through the banner parent.
Credits already show their explicit final calculation.

| i | res | const | sheet | cols,rows | per-state | anchor | LOCAL RECT |
|---|---|---|---|---|---|---|---|
| 0 | 5200 | kNghMoveIn | NghUI/MoveIn.bmp 212x52 | 4,1 | **53x52** | (200,0) | (200,0,253,52) |
| 1 | 5201 | kNghBulldoze | Bulldoze.bmp 212x52 | 4,1 | 53x52 | (300,0) | (300,0,353,52) |
| 2 | 5202 | kNghExit | Exit.bmp 212x52 | 4,1 | 53x52 | (600,0) | (600,0,653,52) |
| 3 | 5306 | kNghInet | InetBtn.bmp 156x52 | 4,1 | **39x52** | (762,0) | (762,0,801,52) |
| 4 | 5307→531x | kNghCredits (loc) | CreditBtn.bmp 196x58 (232x58 loc, 168x35 Jp) | 4,1 | 49x58 (58x58, 42x35 Jp) | **(41+offX,543+offY)** Fr/De/Sp/En; **(19+offX,524+offY)** Jp | localized bottom control |
| 5 | 5204 | kNghPrevious | Previous.bmp 104x52 | 4,1 | **26x52** | (106,0) | (106,0,132,52) |
| 6 | 5205 | kNghNext | Next.bmp 104x52 | 4,1 | 26x52 | (141,0) | (141,0,167,52) |
| 7 | 5207 | kNghImport | Import.bmp 212x52 | 4,1 | 53x52 | (250,0) | (250,0,303,52) |
| 8 | 5208 | kNghExitToDowntown | Downtown.bmp 212x52 | 4,1 | 53x52 | (400,0) | (400,0,453,52) |
| 9 | 5210→5421/27-30 | SimsLogo (per lang) | TheSimsLogo.bmp 70x52 | **1,1** | **70x52** | (5,0) | (5,0,75,52) |
| 10 | 5213 | kNghExitToVacation | Vacation.bmp 212x52 | 4,1 | 53x52 | (450,0) | (450,0,503,52) |
| 11 | 5215 | kNghRezone | Rezone.bmp 212x52 | 4,1 | 53x52 | (350,0) | (350,0,403,52) |
| 12 | 5216 | kNghExitToStudiotown | Studiotown.bmp 212x52 | 4,1 | 53x52 | (500,0) | (500,0,553,52) |
| 13 | 5221 | kNghExitToMagicland | Magicland.bmp 212x52 | 4,1 | 53x52 | (550,0) | (550,0,603,52) |

Evidence: res load @0x4744a8-0x4744b4 (r27 advances +4/iter @0x4748d4);
SetImage @0x4744e0 (1,1) for i==9 else (4,1) @0x474504; normal SetArea
@0x4745d8-0x47460c uses the local anchors, then the button is added to the
banner @0x474614-0x474624. The i==4 special path @0x474874-0x4748b0 adds the
banner left/top loaded into r24/r23 @0x474424-0x474430 to the localized anchor
and adds credits to the root @0x4748b8-0x4748c8. (The earlier interpretation
that normal anchors were raw window coordinates was incorrect.)
Main row x-grid: 200,250,300,350,400,450,500,550,600 → **exact 50px pitch**;
Previous/Next pair at 106/141; top Sims logo at 5; inet at 762. All y=0
inside the centered top strip (52 tall). btn+0x163=1 @0x474528.

### 3.3 Banner / credits / current-hood indicator — DECODED

- Banner art `LoadBuffer(5420 kNghBarBkg banner_neighborhood.bmp 800x52)` →
  this+0xd8 @0x473f84-0x473f9c.
- Banner **is a button**: `new cTSWinBtn(396)` @0x474318-0x47432c → this+0x14c;
  `SetImage(bannerBuffer, cols=1, rows=1)` @0x474330-0x474348;
  `SetArea(offX, offY, offX+800, offY+52)` @0x474350-0x474384 — the full top
  strip is clickable (opens neighborhood picker/credits), vt+0x98(0x400,0).
- "Current neighborhood" template `LoadBuffer(5206 kNghCurrent
  NghUI/Current.bmp 23x30)` @0x474088-0x4740a0 → this+0xdc, with rect
  **(124+offX, offY+3, 124+23+offX, offY+3+30)** — x = 0x7c=124 read from the
  TOC scratch word at TOC+0x1fbc (img word verified = 0x7c), built
  @0x4740a4-0x4740f4 into the [TOC-0x46a0] global. (UL uses the identical law
  with the word at TOC+0x1f80, also 0x7c, @0x46bc0c-0x46bc54.)
- The template pixels are **not drawn**. VC `PostChildDraw`
  @0x46e650-0x46e7c8 and UL @0x463ce0-0x463e54 read its width/height, format
  `CurrentNeighborhoodNumber` as base-10 text, measure it, and draw that white
  number centered in the 23x30 rect. Exact top-left:
  `x=left+floor(23/2)-1-floor(textW/2)`,
  `y=top+floor(30/2)-1-floor(textH/2)`.
- That formula produces the engine text origin `(131,6)` for neighborhood `1`,
  but `InitBitmapped` normalizes the runtime glyph records before drawing:
  it finds the minimum glyph T2 at `0x4b4554-0x4b4634`, subtracts that minimum
  from every runtime T2 at `0x4b4668-0x4b47dc`, and expands the font height at
  `0x4b464c-0x4b4660`. `variablesans_12.ffn` has `minT2=-4`, so the engine's
  digit ink starts four pixels below the raw-FFN renderer's ink. The port keeps
  `UIOriginalText` raw globally and therefore mounts this number at y=`6+4=10`
  (ink y=11), an isolated equivalent of the engine's font normalization.
- Credits/logo language law: jump [TOC-0x46f0] @0x473f2c; per language:
  Fr→credits 5312, De→5313, Sp→5314, Jp→5316, default→5315 (stored [r27+0x10]
  @0x473f40-0x473f74); second table [TOC-0x46f4] @0x474254 sets the top-logo
  resource (5428 Fr / 5429 De / 5427 Sp / 5430 Jp / 5421 En) and the bottom
  credits anchor (41,543)/(19,524) @0x474268-0x474314. The top-logo's
  banner-local idx9 anchor remains (5,0).

### 3.4 Background & lots — DECODED

- VC background: 5000 kNbhdBck (NScreen.BMP 800x600) if lang==1 else 5308
  kNbhdBckNonUS @0x473e70-0x473ea0 (this+0xd0); wave deltas 5302-5304 (lang1)
  / 5309-5311 @0x473eb4-0x473f14 (this+0x198/0x19c/0x1a0).
- UL background: 6000 / 6308 @0x46b8d8-0x46b908 (this+0x114); waves 6302-6304
  + 6330-6332 (lang1) / 6309-6311 + 6336-6338 @0x46b91c-0x46b978
  (this+0x1e4..0x1f8).
- UL banner law: `this+0x111 != 0` (classic) → banner 5420; `== 0` (community)
  → **5045 kFilterToolbar (800x72)** @0x46ba7c-0x46ba9c (this+0x11c) plus
  **5432 kDTDlgBkg2 dlgframe2 800x600** @0x46bac4-0x46bad0 (this+0x120).
- Lots: 10 `cWinLotBtn` (604 bytes, ctor 0x2d5290) @0x474134-0x47423c loop,
  this+0xec+4*i; per-lot position from lot data struct (+0x18c → x=+0x6c,
  y=+0x70), `MoveTo(offX+lotX, offY+lotY)` via 0x2d3cc0 @0x4741c4-0x4741e4;
  lotBtn+0x220..0x22c = **(offX, offY, offX+800, offY+600)** background
  bounds @0x4741f4-0x474200.
- Lot construction preserves the neighborhood vector/STR order; Init walks it
  directly with no Y-sort. UL's hierarchy is base → water/wave layer → lots →
  banner, so waves paint below lot thumbnails. `cWinLotBtn::ImageBlt` passes
  the +0x220 bounds to every composite blit (@0x2d31d8, 0x2d31f8,
  0x2d3228, 0x2d3830) and intersects candidate rectangles against them
  @0x2d35fc onward: terrain/house composites are clipped to the 800x600 map.
- Vacation has an additional recovered post-child phase. Its water frame is
  painted by `TSPaint__12cWinVacation` below lot children
  (`0x441678-0x4417c8`), while `PostChildDraw` blits
  `kNghVacationPort` (`this+0x130`, `0x43e134-0x43e1a4`) and then
  `kNghVacationTrees` (`this+0x134`, `0x43e1ac-0x43e21c`) above those lots.
  The port therefore keeps water in its under-lot animation collection and
  mounts the two static overlays in that exact post-lot order.
- `cWinLotPopup::Init` @0x2d753c: popup `SetArea(l, t, l+0xc8, t+0x64)` =
  **200x100** @0x2d7594-0x2d75b4; `LoadBuffer(5107 kHotspotPopupTiler
  HotSpotPopupTiler.tga 111x111)` @0x2d75c0-0x2d75c8; 39 hotspot photos loaded
  from loose FILES (string-set 0x86=134 → names, `0x591570` loads) into
  this+0xe4 array @0x2d7650-0x2d76e8.

### 3.5 Hotspot popup 5101/5102 + SimEstates/Marquee 5015/5016 — NOT RECOVERABLE

`li/addi r?, 5101`, `5102`, `5015`, `5016` never appear in the code section
(scan: `resid-hits.txt` + targeted scans), and no data pool contains them.
Both 5015/5016 BMPs are missing from the FAR (rt-inventory: "3 MISSING"),
consistent with **dead resource slots** in The Sims Complete. The popup chrome
in this port is the 200x100 window + 5107 tiler (see 3.4). INTERPRETED: the
alpha-blended HotSpotPopupBkg screens were dropped in the Mac/Complete build.

---

## 4. Screen positioning law (summary for reimplementation)

```
offX = max(0, (winW - 800) / 2)     # integer division
offY = max(0, (winH - 600) / 2)
```
- Pick-A-Family: everything (bkg, list@ (41,76), PAF buttons @ y=530, name
  tile, arrows @ y=42, top text @ y+6) is anchored at (ax+offX, ay+offY).
- Neighborhood: background/banner/waves/lots/current-number/bottom credits and
  every top toolbar button are anchored at `(ax+offX, ay+offY)`. Normal toolbar
  controls inherit the offset from their banner parent; credits add it explicitly.
- If winW > 800: kLargeMask 5001 (1024x768) is loaded as surround mask (and
  in UL becomes a dismiss-button).

---

## 5. Work log / search path

1. Read r142 law + tools; copied `capdis.py` (capstone venv /tmp/capvenv).
2. `scan_resids.py`: scanned all `addi rD, r0, imm` (opcode 14, rA=0) for
   Res_Nbhd ids → load-site map (`resid-hits.txt`); + rA!=0 pass (noise,
   stack offsets).
3. Symbol grep (r141 symbol-index) → cWinPickFamily / cPickFamilyItem /
   cPickFamilyList / cPickFamilyPersonInfo / cWinNeighborhoodUL/VC / cWinLotBtn /
   cWinLotPopup function addresses.
4. Decoded cWinPickFamily::Init + CreateButton; found the anchor writer by
   scanning `lwz rD, slot(r2)` for TOC -0x4cf0..-0x4d00; res table found by
   searching binary for BE word pool (5103,5106,5105,5104) @0x5a4820;
   C-kind pointer law confirmed (0x5a4820 = 0x59b990 + 0x8E90).
5. Decoded the un-symbolized cPickFamilyList::Init (found via 5018 load,
   prologue backscan @0x2c9c10), TileItems, cPickFamilyItem::Init +
   SetFamilyID + CalcRowCol + TSPaint; item anchor writer @0x2c7e90;
   resolved string blob (img 0x5b990) + FLT/DBL sentinel constants (they are
   MSL limits, not layout data — red herring documented).
6. Colors: found CalcRowCol color globals → traced writers into
   `InitSimsColors` @0x25d600 (after ShutdownSimsColors trailer).
7. Decoded VC::Init fully; anchor writer found after VC ctor @0x474d10;
   toolbar res table found as STATIC initialized data (img 0x6ce40).
   Confirmed r27/r28 advance +4/+8 inside the 14-button loop.
8. UL::Init diffed (community/classic banner law, 6000/6308 + 6 wave sets).
9. cWinLotPopup::Init (200x100, 5107, 39 file photos); cPickFamilyPersonInfo
   (113x122, 5300, margins 15/10).
10. Failures / not recovered:
    - 5015 kPickAFamily, 5016 kMarquee, 5101/5102 kHotspotPopup{Alpha}Bkg:
      no code refs, no pools → dead in this binary.
    - 5104 PAFCancel in pool but unused.
    - Internal slicing of scrollbar sheet 300x30 by the 328-byte class
      (ctor 0x5269f0) not decoded (only SetThumbImage(vt+0x1c0) call seen).
    - SetMode @0x46fd70 only changes visibility for the credits control;
      its final position is established by the i==4 Init path above.
    - The UL community filter toolbar (BuildFilterToolbar @0x4629a2) and
      phone art 5433-5436 not decoded this round.
    - 0x87f60(9) helper (used before LoadUIStrings) not identified.

## 6. Evidence files (this directory)

- `nbhd-disasm-winpickfamily-init.txt` — cWinPickFamily::Init 0x2d99f0-0x2da210
- `nbhd-disasm-winpickfamily-tspaint.txt` — TSPaint 0x2d96c0
- `nbhd-disasm-createbutton.txt` — CreateButton 0x2d9480
- `nbhd-disasm-paf-anchorwriter.txt` — PAF anchor writer 0x2da3f0
- `nbhd-disasm-pickfamilylist-init.txt` — list Init 0x2c9c10
- `nbhd-disasm-tileitems.txt` — TileItems 0x2c9340
- `nbhd-disasm-pickfamilyitem-init.txt` — item Init 0x2c70e0
- `nbhd-disasm-setfamilyid.txt` — SetFamilyID 0x2c6c20
- `nbhd-disasm-item-calcrowcol.txt`, `nbhd-disasm-item-tspaint.txt`,
  `nbhd-disasm-item-anchorwriter.txt` (0x2c7e90)
- `nbhd-disasm-personinfo-init.txt`, `nbhd-disasm-personinfo-layout.txt`
- `nbhd-disasm-winneighborhoodvc-init.txt` — VC Init 0x473b20-0x474a9e
- `nbhd-disasm-winneighborhoodul-init.txt` — UL Init 0x46b6a0-0x46cb42
- `nbhd-disasm-nbhd-anchorwriter.txt` — neighborhood anchors 0x474d10
- `nbhd-disasm-vc-anchorwriter.txt` — resource table loader 0x46f960
- `nbhd-disasm-lotbtn-ctor.txt`, `nbhd-disasm-lotbtn-setpos.txt`,
  `nbhd-disasm-lotbtn-init.txt`
- `nbhd-disasm-lotpopup-init.txt`, `nbhd-disasm-lotpopup-paint.txt`
- `resid-hits.txt` — resource immediate scan
- `scan_resids.py`, `capdis.py` — tools
