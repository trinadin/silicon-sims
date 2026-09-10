# R145 — BUY-MODE CATALOG LAW (cWinCatalog, decoded from PPC binary)

Round target: the composition of the BUY-mode toolbar band child `cWinCatalog`
(cpanel+0xd8, window = cpanel (220,50,W,150) → screen (220, SH-100, W, SH)),
continuing r144 (toolbar container law). All addresses are FILE offsets.
Binary: `game-data/The Sims/The Sims Complete` (PPC PEF). Symbol-index addr =
file offset + 2 (re-validated: `.__ct__11cWinCatalog` sym 0x26c492 = file 0x26c490).
TOC base = sec1 0x8000 (data-sec1-unpacked.bin addressing; r2+offset → sec1 word).
`nsSMResFac::LoadBuffer` @0x3b6190 is static-style: **r3 = res id, r4 = &cTSBuffer* out,
r5 = colorType (4)** — the res facility singleton is fetched from a global inside.

DECODED = read directly from instruction immediates.
INTERPRETED = semantic reading; immediates cited but behavior inferred.

Coordinate conventions:
- **screen**: game-window pixels, origin top-left. SH = screen height, W = width.
- **catalog-local**: cWinCatalog-local. Catalog occupies screen (220, SH-100, W, SH),
  so catalog-local x 0..W-220, y 0..100. Add (220, SH-100) for screen.
- **anchor-table space**: the plaque/arrow anchor tables are stored in a space where
  the catalog's left edge = 220 (i.e. cpanel/screen coords at any width, because the
  catalog's left is fixed at 220). Every consumer computes `x_local = anchor.x - r17`
  where r17 = catalog absolute left = 220 (parent-chain walk of +0x74 fields — DECODED
  walk at 0x267d00/0x2690d8; cpanel left = 0, DDD left = 0). So effectively
  **catalog-local = anchor − (220, 0)**; anchor.y is used as-is (catalog-local y).

Class symbols (symbol index → file):
ctor 0x26c490 · Init 0x26b400 (4004 B) · Shutdown 0x26ae20 · TSPaint 0x26ace0 ·
TSOnCommand 0x26a3e0 · TSOnMouseDownL 0x26a820 · TSOnMouseEnterChild 0x26a9a0 ·
TSOnMouseExitChild 0x26a8c0 · UpdateViewFromCPState 0x267930 (3352 B) ·
LoadBooks 0x2690a0 (4232 B) · SetProduct 0x26a230 · PageLeft 0x267890 ·
PageRight 0x2677f0 · Get{Room,Function,SubFunction,…}Mask 0x268970/0x268830/0x2686f0… ·
unnamed static-init (anchor writer) **0x26c7d0**.
Companion: cWinCatalogPopup ctor 0x26e620, Init 0x26e2e0, BuildMyBuffer 0x26d280,
SetProductInfo 0x26e140, EnablePopup 0x26e000, DisablePopup 0x26df40.

---

## 0. Member map of cWinCatalog (DECODED from ctor 0x26c490 + Init 0x26b400)

| member | meaning |
|---|---|
| +0xcc | CPState* (ctor arg r4) |
| +0x64 | tooltip color = RGB(31,124,31)→565 (call 0x505780 @0x26b458, args 0x1f,0x7c,0x1f) |
| +0xd4 / +0xd8 | cTSBuffer* of res **102 kCatalogBuyModePrevPage** / res **101 kCatalogNextPage** (table @0x55d00, loop 0x26b50c-0x26b5b0) |
| +0xdc / +0xe0 | the two page-arrow cTSWinBtn (ctor 0x50d720, SetImage(buf,4,1), children of catalog) |
| +0xe4..+0x204 | **56 plaque buffers, 7 rows x 8 cols** (table @0x55ea8; member = +0xe4 + 0x20*row + 4*col) |
| +0x1c4 | the big LEFT toggle plaque cTSWinBtn; initial image = cat+0xe4 = res 180 kBuyBackRLiving |
| +0x1c8+4i | res 158..165 kBuyRLiving..Misc (selected room plaques), i=0..7 |
| +0x1e8+4i | res 210..217 kBuyRSubSortSeating..Misc (function subsort plaques in room mode) |
| +0x208+4i / +0x228 / +0x248 / +0x268 / +0x288 | res 200..204 kBuyDDining.. / 230..234 kBuyVOne.. / 250..254 kBuyCOne.. / 273..277 kBuySTfood.. / 1705..1709 kBuyMTfood.. (selected plaques for downtown/vacation/community/studio/magic; i≠4,5,6) |
| +0x2a8 / +0x2c8 / +0x2e8 | res 220..227 kBuyDSubSort* x1, res 240..247 kBuyVSubSort*, res 260..267 kBuyCSubSort* (+0x308, +0x328 = two more copies of 220..227) |
| +0x348+4i | res 150..157 kBuyFSeating..Misc (selected function plaques) — doubles as sub-sort art base |
| +0x368+16i+4j | res T13[i]*10+j = **1500..1573 kBuyF&lt;Sort&gt;SubSort1..4** (function sub-sort plaques, 4 per sort) |
| +0x3e8/+0x3ec/+0x3f0/+0x3f4 | res 1574 kBuyFMiscSubSortPets, 1575 …Magic, 1580 kBuyFSubSortOther, 1581 kBuyFSubSortAll |
| +0x3f8+4i | **the 8 tab-plaque buttons** (i=0..7), flags +0xec=+0xf0=1, children of catalog (loop 0x26ba30-0x26bbac) |
| +0x418/+0x41c/+0x420 | product TVector / count / array (filtered items on current page) |
| +0x424 | cWinCatalogPopup* (new 0x160 @0x26b488, ctor 0x26e620) |
| +0x428..+0x434 | grid/popup area rect — Init writes {201,0,W-220,100} (0x26b4d8-0x26b508); Update OVERWRITES to **{195, 5, W-220, 95}** (0x267d88-0x267df4; = (415−220−20, 5, width, 0x5f)) |
| +0x438/+0x43c | grid flow origin (x,y) = (415−r17, 5) → **catalog-local (195, 5)** |
| +0x440 / +0x444 | x pitch / y pitch = **45 / 45** (0x2d) |
| +0x448 / +0x44c | wrap limits = **width−12** (=792 @1024) / **95** (0x5f) |
| +0x450 / +0x454 | item advance = 45 / 45 |
| +0x458 | orientation: 0 = row-major (horizontal) layout |
| +0x45c | items per page (capacity = 26 @1024, see §3) |
| +0x460 / +0x464 | CPState+0x90 NewCatalogSort / +0x94 eWinCatalogSortState (cached in Update 0x267948-58) |
| +0x468/+0x46c | current sub-catalog index (per-sort, from CPState+0x98/0xa0/… via Get*Mask helpers) |

The 7 plaque rows (table @0x55ea8, DECODED values + Res_CPanel.h names):

| row | res ids | meaning |
|---|---|---|
| 0 | 180..187 kBuyBackRLiving..Misc | room-sort toggle-plaque art (unselected backs) |
| 1 | 170..177 kBuyBackFSeating..Misc | function-sort toggle-plaque art |
| 2 | 190..193,0,0,0,194 kBuyBackD* | downtown toggle art (5 used) |
| 3 | 195..198,0,0,0,199 kBuyBackV* | vacation |
| 4 | 255..258,0,0,0,259 kBuyBackC* | community/old town |
| 5 | 268..271,0,0,0,272 kBuyBackS* | studio |
| 6 | 1700..1703,0,0,0,1704 kBuyBackM* | magic town |

**Art naming law (DECODED): `BuyBack*` sheets are the art of the big LEFT toggle
plaque only; the 8 tab buttons use `BuyR*` (rooms), `BuyF*` (functions) and the
`…SubSort…` sets.** All plaque SetImage calls are (_, **4, 1**) — 4 frames wide,
1 row (matches the 144x36 port sheets → 36px cells).

## 0.1 The unnamed static-init writer @0x26c7d0 (anchor tables — DECODED)

Lives between the ctor and the popup code (no symbol; starts `stmw r13,-0x4c(r1)`).
Writes 14 BSS tables (8 x {x,y} each) + the tooltip string array (BSS 0x92d90,
56 cTSString) + numeric_limits float globals. Values:

| table | BSS | TOC | used by | pairs (anchor space; subtract 220 for catalog-local x) |
|---|---|---|---|---|
| room main | 0x931c0 | −0x6a24 | sort 0, state 0 | (242,7)(281,3)(323,7)(361,4) / (239,53)(284,59)(322,55)(364,64) |
| room sub | 0x93180 | −0x6a2c | sort 0, state≠0 | (251,10)(289,10)(327,10)(365,10) / (251,58)(289,58)(327,58)(365,58) |
| function main | 0x93140 | −0x6a30 | sort 1, state 0 | (242,8)(279,16)(322,1)(369,4) / (241,61)(285,59)(322,55)(364,64) |
| function sub | 0x93100 | −0x6a34 | sort 1, state≠0 | same grid as room sub |
| downtown main/sub | 0x930c0 / 0x92f80 | −0x6a38/−0x6a3c | sort 2 | main (242,8)(284,12)(318,12)(369,14) / bottom row standard |
| vacation main/sub | 0x93040 / 0x92ec0 | −0x6a40/−0x6a44 | sort 3 | main = downtown pattern |
| community main/sub | 0x93000 / 0x92e80 | −0x6a48/−0x6a4c | sort 4 | main = downtown pattern |
| studio main/sub | 0x93080 / 0x92f40 | −0x6a50/−0x6a54 | sort 5 | main (242,8)(284,12)(318,12)**(358,14)** / bottom standard |
| magic main/sub | 0x92fc0 / 0x92f00 | −0x6a58/−0x6a5c | sort 6 | main = downtown pattern |
| page arrows | 0x93200 | −0x6a90 | always | {404, 26} for prev (idx 0/1), {404, 26} for next y (idx 3) |

("bottom row standard" = (241,61)(285,59)(322,55)(364,64).)

LoadBooks case→table pairing verified in code: case0 r19/r20, case1 r21/r23,
case2 r24/r25, case5 r30/r14, case6 r13/[r1+0x4c] (0x269d20/0x269db0/0x269e88);
case3/4 by the same register sequence.

---

## 1. Tab plaque row (DECODED)

`LoadBooks` @0x2690a0 (called from UpdateViewFromCPState 0x268570 with
arg = CPState+0x90 NewCatalogSort) re-lays the **8 plaque buttons** (+0x3f8[0..7])
every sort change, in a loop i=0..7 (0x269380-0x26a02c) that dispatches on
NewCatalogSort (jump table @data 0x562c4) and sortState:

```
SetImage(btn[i], buffer_for(sort, sortState, i), 4, 1)      // vt+0x1a4
SetArea(btn[i], ax - 220, ay, ax - 220 + btnW, ay + btnH)   // vt+0x68; keeps image size
SetToolTipLong(btn[i], tooltip[i])                          // 0x504ad0
btn[i]+0x160 = enable-mask (repaint vt+0x170 if changed)    // subsort modes only
```

- **sortState 0 (main)**: images = kBuyR<i> (room sort) or kBuyF<i> (function sort);
  anchors = the irregular "hanging plaque" main tables (§0.1) → catalog-local
  top row x≈22,61,103,141 / bottom x≈19,64,102,144, y per table.
- **sortState ≠ 0 (sub-sort)**: images = kBuyRSubSort<i> (room mode — i.e. the tab
  row becomes FUNCTION sub-sorts within the selected room) or kBuyF&lt;cur&gt;SubSort1..4
  (function mode, buffers +0x368+16*cur+4j; plaque 4/5/6/7 special-cased at
  0x269654/0x26970c/0x2697c4/0x269830 for the Misc slots Pets/Magic/Other/All using
  res 1574/1575/1580/1581); anchors = the regular sub grids
  **catalog-local (31,10),(69,10),(107,10),(145,10) / (31,58),(69,58),(107,58),(145,58)**
  (38 px x-pitch, 48 px y-pitch).
- Enable masks: byte matrices built in Init 0x26bd3c-0x26c374 from the Catalog product
  list — `roomFuncMask[room*8+function]` (BSS via TOC−0x6a60) and one 8x8 matrix per
  expansion sub-catalog (TOC −0x6a7c/−0x6a80/−0x6a84/−0x6a88/−0x6a8c/−0x6a68), filled
  from Product flag tests (0x20b0f0/0x20b070/0x20aff0/0x20af60/0x20aee0/0x20b1e0).
  Selected-state: the plaque for the current sub-catalog gets `SetState(1)`
  (0x268544-0x268564, index from CPState+0xbc).

The **left toggle plaque** (btn +0x1c4): image = one of the 56 `BuyBack*` buffers
(row = NewCatalogSort, col = current sub-catalog index — jump table @0x562e0,
cases 0x2691b8..0x2692f0); hidden iff sortState == 0 (0x269160-0x269184:
vt+0xa0 Hide vs vt+0x9c Show); placed ONCE at LoadBooks end (0x26a0c0-0x26a10c):
`SetArea(l = -2, t = (catH - btnH)/2, r = btnW - 2, b = t + btnH)` — flush to the
catalog's LEFT edge, sticking out 2 px, vertically centered in the 100-tall band.

## 2. Function vs room switching (DECODED)

- `cWinViewControl::TSOnCommand` 0x2b4b24-0x2b4bac (source == VC+0xf0, the UCP
  **kObjects/buy button**): after SetCPMode/GetMode()==1 (buy active):
  `sort==0 → ShopSetSort(1)`; `sort==1 → ShopSetSort(0)`; sort 2 kept; 3+ kept;
  then ResetPreviousPages. **The Objects button TOGGLES room↔function on every
  activation.** `ShopSetSort` @0x20e9c0: CPState+0x90 = sort; **CPState+0x94 = 0**
  (always resets to main-plaque state); UpdateShopItem.
- Plaque click (`cWinCatalog::TSOnCommand` cmd 1, 0x26a4fc-0x26a710):
  - sortState 0 → `ShopSetSortState(1)` then `ShopSet{Room,Function,Downtown,…}Catalog(i)`
    + `ShopSet…SubCatalog(0)` (jump table @TOC−0x50e8) → tab row switches to sub-sorts;
    all 8 plaques SetState(0), clicked one SetState(1).
  - sortState 1 → `ShopSet{Room,Function,…}SubCatalog(i)` (jump table @TOC−0x50ec) —
    picks the sub-sort within.
- Toggle-plaque click (0x26a48c-0x26a4f8): if sortState==1 → `ShopSetSortState(0)`,
  all plaques SetState(0) — "back to main sorts".
- Keyboard: `cDDDSimsView::ComputeUpDownState` 0x217c94-0x217cdc calls ShopSetSort
  7x (hotkeys cycling sorts — INTERPRETED).
- CPState ctor defaults (0x21228c-0x21229c): **+0x90 = 1 (function), +0x94 = 0,
  +0x98/+0xa0 = 0 (Seating/Living)** → with the toggle law, the FIRST activation of
  the Objects button flips 1→0: **first-ever buy mode shows the ROOM sort, main
  state, room 0 (Living Room) selected** (INTERPRETED composition of the two laws).

## 3. Object/item grid (DECODED)

`UpdateViewFromCPState` 0x267d88-0x267f2c writes the flow params, then lays out the
page in 0x2682bc-0x268494:

```
origin = (415 - 220, 5) = catalog-local (195, 5)      // +0x438/+0x43c
for each product on page (btn = Product::GetButton() 0x2099b0, +0x10):
    TSWinMoveTo(btn, pt)                              // 0x260ba0
    pt.x += 45; if pt.x + 45 > width-12: pt.x = 195; pt.y = 5 + row*45
```

- **Cells 45x45, pitch 45/45** (members +0x440/+0x444/+0x450/+0x454 = 0x2d).
- Wrap limits: x ≤ width−12 (=792 @1024 → x = 195,240,…,735 = **13 columns**),
  y ≤ 95 (rows y = 5, 50 = **2 rows**). Capacity +0x45c = **26 items/page @1024**.
- Screen rect: x 415..1000, y SH-95..SH-5.
- Item buttons are `ProductButton` (0x190 B, ctor → cTSWinBtn 0x50d720; vtable data
  0x4fed8; product back-ptr +0x18c) created by `Product::CreateButton` 0x2099f0:
  SetImage(thumbnail sheet, cols, 1), flags +0xec/+0xf0 = 1, custom-draw type
  **+0x14c = 6** with template buffer global *(DATA 0x74460) at +0x150 when present;
  `ProductButton::ForegroundBlt` 0x20b940 composes the thumbnail (centered, half-scale
  variants; SetForeground 0x509d50). **No text is drawn on grid items** — name/price
  live in the hover popup (§4).
- **Page arrows = the two cTSWinBtn +0xdc/+0xe0** (res 102 kCatalogBuyModePrevPage /
  101 kCatalogNextPage, SetImage(_,4,1)): click → `ShopSetCatalogPage(page∓1)`
  (0x26a444/0x26a460). Placement (LoadBooks 0x26a030-0x26a0b8):
  prev at anchor (404,26) → **catalog-local (184, 26)**; next right-aligned
  `x = width − 24 − btnW + btnL`, y = 26. Prev arrow is HIDDEN when page == 0
  (0x26857c-0x2685c8); PageLeft/PageRight 0x267890/0x2677f0 synthesize clicks.
- No scrollbar: paging via the two arrows only (26 items per page).

## 4. Buy-mode chrome / children of cWinCatalog (DECODED)

1. 8 tab-plaque buttons +0x3f8[0..7] (§1).
2. Left toggle plaque +0x1c4 (§1).
3. Page arrows +0xdc/+0xe0 (§3).
4. `cWinCatalogPopup` +0x424 — the item-info popup (NOT a band child visually;
   added to the window manager, SetOverlapsScrollArea(1), fades in via
   RampGenerator SetupConstantTimeRamp duration 0xfa — EnablePopup 0x26e000).
   - popup Init 0x26e2e0: SetArea(l, t, l+**559**, t+**127**) — 559x127;
     **name font = font[10]** (BSS 0x92900+0x28 → popup+0xd0), **description font =
     font[8]** (0x92900+0x20 → popup+0xd4); UI strings table **160** (0xa0),
     14+ labels at +0x110[0..13], +0x148….
   - SetProductInfo 0x26e140: popup+0xd8 = product; price string built at popup+0xdc
     (Catalog vt+0x84 format of `0x20b4c0(product)` = price/description value).
   - BuildMyBuffer 0x26d280: local rects (0,0,168,127) and (168,0,540,127);
     text origin = TopLeft(rect2) + **(10, 12)**; draws **name — price** with
     font[10] as one advancing line (measure vt+0x70, +4 px cursor advance),
     description below with font[8] (word-wrap measure loop 0x26d3e8-0x26d42c).
   - Wiring: TSOnCommand cmd 0x13 (hover) → EnablePopup(product, catalog)
     (0x26a76c-0x26a7b4); TSOnMouseExitChild → DisablePopup when the exited child
     is not the popup (0x26a8c0-0x26a920). Clicking a grid item → SetProduct 0x26a230.
5. `cWinCatalog::TSPaint` 0x26ace0 paints NO backdrop — only tooltip-rect
   invalidation (this+0x5c child, this+0xd0 tooltip win) and SetCursor per +0x470.
   The visible band background is the shared cpanel PanelBack (4910 kUniversalCPBkg,
   r144 §0). **kCatalogBck 48 is NEVER loaded in this binary** (scan: no
   `li r3,48; bl 0x3b6190` anywhere) — INTERPRETED: unused/leftover on Mac.

## 5. Strings (DECODED ids; STR# numbering per port r122)

| table id | loaded at | contents |
|---|---|---|
| **150** (0x96) | Init 0x26bbb0-0x26bc08, writer 0x26cc24 | **56** entries → tooltip array BSS 0x92d90. Tab tooltips: room main plaques use [i] (0..7), room-subsort/function-main plaques use [i+8] (8..15) — exactly matches port r122 (STR# 150 'BuyModeCatalogSortTips': [0..7] rooms, [8..15] functions). Remaining 40 = expansion plaque tips (INTERPRETED). |
| 154 (0x9a) | Init 0x26b6a0-0x26b76c | page-arrow tooltips (prev/next) |
| 210 (0xd2) | Init 0x26bc7c-0x26bd0c | idx 2,3,4,5 → 4 globals (TOC −0x6a74/−0x6a78/−0x6a6c/−0x6a70) = Misc sub-sort plaque tips (Pets/Magic/Other/All — INTERPRETED) |
| 200..207 (0xc8+i) | Init 0x26bc10-0x26bc78, writer 0x26ccf4 | 4 strings each → 32 sub-sort names (BSS via TOC−0x6a64): sub-sort tooltips for function sort i |
| 160 (0xa0) | popup Init 0x26e3a4-0x26e45c | 14+ popup labels (stats/cost lines) |

## 6. Visibility law (DECODED)

`cWinCPanel::SetPanel` @0x26eee0 (arg = PanelID): stores cpanel+0xd0 = id; id 0 →
Show [cpanel+0xdc] (cWinPeople); **id 1 → Show [cpanel+0xd8] = cWinCatalog**
(0x26efc8-0x26f078 path); other ids → arch/options/etc. The previous child is
hidden first (each block hides r31 = previous). Callers: `cWinViewControl::
UpdateViewFromCPState` 0x2b3ea0 (mode switch) and ShowDisposePopup. BoboVision
variant uses vt+0xb0 with (1/0,0,0). On show, the catalog renders from current
CPState sort fields (§2 default: room main, Living selected — first ever entry).

## 7. Port-relevant corrections vs r144 residuals

1. r144 said "cWinCatalogPopup area fields (201,0,W,100)" — that is only Init's
   initial write; UpdateViewFromCPState overwrites to **(175, 5, W-220, 95)** and
   the grid origin is (195,5) with 45x45 cells (26/page @1024), not derived from
   the popup.
2. The plaque row is **8 buttons in 2 rows of 4** (left column of the band,
   catalog-local x≈19..149), NOT a single horizontal strip; sub-sort mode uses a
   regular 38/48 grid at (31,10).
3. res 102/101 are the ITEM-GRID page arrows at catalog-local (184,26) /
   right-edge−24; res 100 kCatalogPrevPage belongs to cWinArch (r144 §4 stands).
4. `BuyBack*` art = the left toggle plaque only; tab plaques = `BuyR*`/`BuyF*`/
   sub-sort sets — resolves the BuyF*/BuyR* "paging" question: there is no paging
   between art sets; the SET choice is (NewCatalogSort, sortState) and the toggle
   happens via the UCP Objects button (§2).
5. Item name/price are NOT drawn on grid cells; they appear in the 559x127 hover
   popup (name+price on one line at +(10,12) rel. to its text rect, font[10];
   description font[8]).

## Residuals (honest)

- Exact STR# 150 entries 16..55 and STR# 200..210/160 contents not extracted from
  the binary (string data lives in IFF UI files; port r122 covers 150).
- The special-case blocks for function-sub plaques 4..7 (0x269654-0x269830) were
  skimmed: which of Pets/Magic/Other/All appears in which slot depends on
  expansion-install flags (INTERPRETED from buffer sets +0x3e8..+0x3f4).
- ProductButton custom-draw type 6 internals (thumbnail scaling variants in
  ForegroundBlt) traced only to the SetForeground call; exact source-rect math
  half-verified.
- The tooltip child (catalog+0x5c) and 0x505780 color plumbing verified only at
  the RGB(31,124,31) level.
- Magic main anchors verified; the two extra copies of kBuyDSubSort* buffers at
  +0x308/+0x328 (three total) — purpose not traced (INTERPRETED: shared downtown
  sub-sort art reused by other expansions' sub modes).
- The catalog's Shutdown 0x26ae20 not fully walked (deletion order only).
