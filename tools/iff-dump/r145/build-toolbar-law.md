# R145 — BUILD-MODE TOOLBAR COMPOSITION LAW (decoded from PPC binary)

Round target: the complete composition law of the build-mode toolbar
(cWinArch + cWinCatalogPopup + cWinRoofPanel), continuing/correcting r144's §4.
Binary: `game-data/The Sims/The Sims Complete` (PPC PEF). All addresses are FILE
offsets (== code-virtual). Memory laws used (re-validated):

- symbol-index addr == file offset + 2.
- TOC base = **DATA 0x8000** (new this round: found via the adjacent slot pair
  {0x55290, 0x92990}; all TOC resolutions below use it).
- initialized DATA = the unpacked sec1 bin (len 0x7bf80); BSS = 0x7bf80..0x989a4.
- CodeWarrior puts read-only constant pools **inside the code section**; a TOC
  slot holding a value > 0x5b9458-region (e.g. 0x59b808) is a pointer into the
  CODE section: code-addr P → file P+0x8E90. Several "BSS-looking" tables of
  this round are actually code literal pools.

DECODED = read directly from instruction immediates / static data.
INTERPRETED = semantic reading; immediates cited but behavior inferred.

Coordinate conventions:
- **screen**: game-window pixels, origin top-left.
- **arch-local**: cWinArch window-local. arch = cpanel (220, 50, W, 150) →
  screen (220, SH-100, W, SH); arch-local y 0..100 is the toolbar band,
  x 0..(W-220) (804 at 1024-wide).
- **roof-local**: cWinRoofPanel-local (panel at arch (314, 5, W, 100)).

---

## 0. Corrections to r144 (all re-derived this round)

| r144 claim | r145 correction |
|---|---|
| "12 build-tool buttons … flow layout origin (338,5) pitch 45" | The 12 tool buttons NEVER flow. They are placed from a **static 12-pair anchor table** by `cWinArch::TSPaint`. The (338,5)-pitch-45 flow belongs to the **subtool pattern row** (arch+0x11c list). |
| "undo/redo/page final positions live inside the catalog popup layout" | All four are positioned by `cWinArch::TSPaint`: undo/redo from static anchor pairs, page buttons from computed rects. The catalog popup never touches them. |
| "label strings at BSS 0x92990+0x30+4i" | BSS 0x92990+4i for i=0..11 = the TOOL tooltips; +0x30..+0x3c = undo/redo/pageL/pageR. All 16 are filled from **STR# table 139 'BldTips'** indices 0..15 (`LoadUIStrings(0x8b, 'UIText', …)` at 0x2622cc; count guard [TOC-0xae0]=16). |
| "cWinCatalogPopup area fields (201,0,W,100)" | arch+0x158..0x164 are NOT the popup's area. They are a mouse-guard rect, overwritten at Init end by the roof-panel SetArea args (314, 5, W, 100); TSOnMouseMove closes the popup when the mouse leaves them. The popup itself is parented to the MAIN view and floats above-left of the hovered button. |

---

## 1. Container + who builds what (DECODED)

`cWinArch::__ct` @0x262970 (base cTSWin ctor 0x509610; vtable via TOC -0x6af8):
+0xcc = CPState (ctor arg), +0xd4 = ctor's cTSBuffer arg (PanelBack buffer),
+0xd8 = 1 (**layout-on-next-paint flag** — all positioning happens in TSPaint),
+0x128..+0x148 = flow fields (0), +0x11c = std::vector<Product*> (subtool page
list; helpers: size 0x25f650, &at 0x25f6c0, clear 0x25f5e0).

`cWinArch::Init` @0x261f40:
1. vt+0x98(0x10, 0) — create 16-bit private surface (arch+0x5c).
2. **cWinCatalogPopup** = new(0x160) @0x591440 + ctor 0x26e620(CPState) →
   arch+0x154; popup->Init (vt+0x14) @0x261ff0.
3. arch+0x158..0x164 = (201, 0, GetW(arch), 100) (0x262010-0x262034) — the
   initial mouse-guard rect (superseded at step 9).
4. LoadBuffer(0x40=64 kToolBtnUndo) / (0x41=65 redo) / (0x64=100 prevPage) /
   (0x65=101 nextPage) and **4912/4913 kBuildToothPick1/2** (0x1330/0x1331) →
   arch+0x174/+0x178 (0x26205c-0x262078).
5. 4 chrome buttons: new cTSWinBtn(0x18c)+ctor 0x50d720, SetImage vt+0x1a4
   (buf, **4, 1**), btn+0x163=1, btn+0xec=1 (redo) / 0 (pageL), AddChild;
   stored arch +0xdc (undo), +0xe0 (redo), +0xe4 (pageL), +0xe8 (pageR).
   Tooltips: SetTooltipText 0x504ad0(btn, &BSS0x92990[12..15]) (0x262440-0x26246c).
6. **STR# 139 load** (0x2622a0-0x262378): if BSS 0x92990[0] still empty:
   `LoadUIStrings(139, 'UIText'–from 0x87f60(9), &set, 0)` @0x25f440; then
   `cTSString[i] = set[i]` for i < min(setCount, **16** = [TOC-0xae0]).
7. **12 tool buttons** (loop 0x26238c-0x26243c): res id = DATA **0x55290**[i]
   (read via TOC -0x6af4) = {66 Terrain, 68 Pool, 70 Wall, 73 Wallpaper,
   74 Stairs, 75 Fireplace, 67 Tree, 69 Floor, 71 Door, 72 Window, 76 Roof,
   77 Hand}; LoadBuffer(res, 4); new cTSWinBtn; SetImage(buf, **4, 1**);
   btn+0x163=1; btn+0xec=btn+0xf0=1; tooltip = BSS 0x92990+4i; AddChild;
   stored arch+0xec[0..11]. NO MoveTo anywhere.
8. Flow fields (0x262470-0x2624d4): +0x128/+0x12c = **(338, 5)**, +0x130 =
   +0x134 = +0x140 = +0x144 = **45**, +0x138 = archW−4, +0x13c = **100**;
   then a measurement loop (0x2624f0-0x262620) simulates the flow to count
   columns-per-row and stores a vector of that many NULL Product* slots into
   arch+0x11c (page slots). At 1024-wide: 10 columns fit (338..788).
9. **cWinRoofPanel** = new(0x128)+ctor 0x29e770(CPState) → arch+0x16c;
   arch+0x158..0x164 = (314, 5, archW, 100) (0x2624dc-0x2624ec);
   SetArea(roof, 314, 5, 804, 100) (0x262670-0x26268c); AddChild;
   HideWindow(roof); roof->SetCurPage(0); roof+0x11c = arch+0x154 (popup link).

## 2. THE 12 TOOL BUTTON POSITIONS — static anchor table (DECODED)

`cWinArch::TSPaint` @0x2615f0 (layout half, gated by arch+0xd8, cleared at
0x261a3c) does ALL chrome positioning:

- Reference conversion (0x261618-0x261654): the point arch-local (0, 50) is
  converted to the coordinate space of **CPState+0x44** (the main/DDD view) via
  WindowToScreenCoordinates 0x21a4d0 + ScreenToWindowCoordinates 0x261cc0 →
  dstX (=220 at any width, since arch x0 = cpanel x 220).
- Per button: SetArea(btn, anchor.x − dstX, anchor.y, +W, +H) with W/H =
  GetW/GetH of the button (= the SetImage cell = sheet/4 × 1). The same rect
  is hit rect AND art rect (cTSWinBtn draws its sheet into its window rect —
  INTERPRETED: no separate inset).

Anchor sources (sinit `__sinit_:WinArch.cpp` @0x263730, stores at
0x2637f4-0x263860):

- **tool pairs @BSS 0x92a90** (via TOC -0x6aec), 12 × {x,y}:
  (278,4) (325,6) (364,4) (408,3) (450,4) (502,8) / (279,57) (317,64)
  (367,55) (409,55) (449,55) (502,55).
- **undo @BSS 0x92af8** (TOC -0x5128) = (231, 21); **redo @BSS 0x92af0**
  (TOC -0x512c) = (231, 53).

Net arch-local positions (anchor.x − 220; anchor.y raw) — DECODED:

| i | tool (res) | arch-local (x, y) | | i | tool | arch-local |
|---|---|---|---|---|---|---|
| 0 | Terrain 66 | (58, 4) | | 6 | Tree 67 | (59, 57) |
| 1 | Pool 68 | (105, 6) | | 7 | Floor 69 | (97, 64) |
| 2 | Wall 70 | (144, 4) | | 8 | Door 71 | (147, 55) |
| 3 | Wallpaper 73 | (188, 3) | | 9 | Window 72 | (189, 55) |
| 4 | Stairs 74 | (230, 4) | | 10 | Roof 76 | (229, 55) |
| 5 | Fireplace 75 | (282, 8) | | 11 | Hand 77 | (282, 55) |

Two hand-tuned rows (y≈3..8 / 55..64, x 58..282), NOT a uniform 45 grid.
Undo (11, 21) and redo (11, 53), 30x24 cells (art 120x24 sheet, 4 cols).
Screen position = arch-local + (220, SH−100).

Exact tool-button cells, in tool/index order (UIGraphics art sheet width/4 ×
sheet height), are: **Terrain 39x39, Water 25x38, Wall 31x40, Wallpaper
32x40, Stairs 32x38, Fireplace 29x35, Tree 35x39, Floor 43x28, Door 27x41,
Window 29x39, Roof 39x33, Hand 30x35**. These dimensions are both the image
cell and the button/hit rectangle placed at the anchors above.

- **Page buttons** (TSPaint 0x2619a8-0x261a34): pageL = SetArea(l = **546−dstX**
  = arch-local 326, t = **26**, +W, +H); pageR = (l = **Width(arch)−14** = 790,
  t = 26). 26 = vertical center of the band for the 49-tall 9-wide cells
  (art 36x49 sheet → 9x49 cell, r144 sizes). They bracket the pattern row
  (338..788).

## 3. Tooltips / labels — STR# 139 'BldTips' (DECODED + data-verified)

- 16 global cTSStrings @BSS 0x92990+4i, default-constructed + registered for
  destruction by the sinit (0x263864-0x263a18; reg helper 0x590a30).
- Filled in Init from **table 0x8b = 139** of the 'UIText' string file
  (LoadUIStrings @0x25f440; filename from enum 9 = "UIText", cstring table at
  DATA 0x43ce0). Count guard [TOC-0xae0] = 0x10.
- `SetTooltipText(btn, str)` 0x504ad0 copies into btn+0x70 (vt+0x98(0x400,1)
  first — tooltip enable).
- **Verified against game data**: `GameData/UIText.iff` rsmp maps id 0x8b →
  'BldTips'; its string order is exactly: Terrain Tool, Water Tool, Wall &
  Fence Tool, Wallpaper Tool, Stair Tool, Fireplace Tool, Plant Tool, Floor
  Tool, Door Tool, Window Tool, Roof Tool, Hand Tool, Undo Last, Redo Last,
  Previous Page, Next Page → indices 0..11 = tools (in res-id order), 12=undo,
  13=redo, 14=pageL, 15=pageR. Port's "139 BldTips" CONFIRMED.

## 4. Click path + selection behavior (DECODED)

`cWinArch::TSOnCommand` @0x2612f0 (cmd r4, src r5):
- cmd 1 (click):
  - src==arch+0xdc → ArchCanUndo @0x20fc80 then **ArchUndo @0x20ee60**;
    src==+0xe0 → ArchCanRedo @0x20fc30 / **ArchRedo @0x20edc0**.
  - src==+0xe4 (pageL): arch+0x14c-- then CPState::SetDirty(0x10);
    src==+0xe8 (pageR): arch+0x14c++ then SetDirty(0x10).
  - src==tool[i]: all 12 SetState(0) except tool[i] SetState(1); then
    **0x20ef90(CPState, i) = CPState::ArchSetCurrentTool(i)** (unnamed symbol;
    guards CPState+0x58 — INTERPRETED name). Selected look = state frame
    (cell 2 of the 4-state sheet) — INTERPRETED from SetImage(_,4,1).
  - src==subtool product button: **ArchSetCurrentSubtool @0x20ef00**; if the
    catalog popup is enabled (popup+0xe0) → EnablePopup.
- cmd 0x13 (hover) from a subtool button: if popup enabled → EnablePopup.

`cWinArch::UpdateViewFromCPState` @0x25fe70 (guard byte TOC -0xaa8):
- newTool = ArchGetCurrentTool @0x20fd20 (arch+0xd0 cached).
- If CPState dirty & 0x110: SetState(0) on old tool's button, SetState(1) on
  new; if new==**10 (Roof)** → `SetRoofMode(true)` and done; if old==10 →
  SetRoofMode(false); then clear+hide all subtool buttons and rebuild the
  list: products of `GetCatalog` @0x211f20 where **TestArchTool(product, tool)**
  @0x20abd0 matches.
- Sort: per-tool key from jump table @DATA 0x55490 (via TOC -0x5124), applied
  by std::sort 0x262ba0 (comparator GetSortVal 0x174a30 / price 0x20b4c0):
  tool→key = 0:Terrain none, 1:Pool 9, 2:Wall 8, 3:Wallpaper none,
  4:Stairs 5, 5:Fireplace 7, 6:Tree 6, 7:Floor none-but-sorted, 8:Door 3,
  9:Window 4, 10:Roof none, 11:Hand none. (DECODED map; key semantics
  INTERPRETED.)
- Page fields: arch+0x150 = cols/page (recomputed by the same flow
  measurement), arch+0x14c = current page (clamped to (n−1)/cols).
- Selected subtool: if tool/subtool changed, find current subtool
  (ArchGetCurrentSubtool @0x20fcd0) in the list; set page = idx/cols.
- Show page buttons: +0xe4 shown iff page != 0; +0xe8 shown iff
  count > cols (0x260974-0x2609d8).
- For each subtool button: AddChild to arch (if needed), ShowWindow; the one
  at the selected index gets SetState(1).
- Undo/redo enable refresh (dirty & 4): ArchCanUndo/Redo → button+0x160
  (enabled byte) + Invalidate (vt+0x170).

`SetRoofMode` @0x25f870: true → hide every subtool button + both page
buttons, **ShowWindow(roof panel)**, roof UpdateViewFromCPState; false →
show subtool buttons, hide roof panel.

## 5. Subtool pattern row (DECODED)

- The subtool buttons are Product-owned (Product::GetButton 0x2099b0); arch
  re-parents and flows them: pt = (+0x128, +0x12c) = **(338, 5)** arch-local;
  MoveTo per visible button; x += 45; wrap when x+45 > archW−4; row pitch 45;
  bottom limit 100 (applied identically in UpdateViewFromCPState 0x2607f8-
  0x260964 and again in TSPaint 0x26176c-0x261998). The alternate +0x148≠0
  branch transposes the generic flow, but **the ctor zeros +0x148 and no
  cWinArch writer exists**: it is inactive in the shipped build-toolbar path,
  which always uses the horizontal-first layout above.
- At 1024: 10 columns x up to 2 rows; the row region (338..788) sits right of
  the tool cluster (58..322) with page arrows at (326,26)/(790,26).

## 6. cWinCatalogPopup (DECODED)

ctor @0x26e620: vtable TOC -0x6a20; +0xd8 product, +0xdc cTSString (name-price
line), +0xe0 enabled byte, +0xe4 RampGenerator, +0x110 cTSString[14],
+0x148 cTSString[5], +0x15c cTSString, +0x104 ctrl-mgr handle, +0x108 buffer,
+0x10c source window.

Init @0x26e2e0: private surface vt+0x98(0x10, 0); register with CtrlMgr (vt+0x18
arg 5); **SetArea(l, t, l+559, t+127)** (0x26e364-0x26e384) — a 560x128 window
at wherever +0x74/+0x78 currently are; fonts +0xd0/+0xd4 from static
[TOC-0x7178] (see below); **strings = STR# 160** (`LoadUIStrings(0xa0,
'UIText', …)` 0x26e3bc; rsmp name **'CatalogRatings'**): indices 1..14 →
+0x110[0..13] (rating row labels), 15..19 → +0x148[0..4] (audience suffixes),
17→+0x15c, 18→+0x158, 19→+0x150, 20→+0x154.

EnablePopup @0x26e000(product, srcWin): +0xe0=1, +0x10c=srcWin;
SetProductInfo @0x26e140 (name-price via catalog vt+0x84 → +0xdc;
BuildMyBuffer); **AddChild to the MAIN view** ([TOC-0x7790]→+0x80, vt+0x28) —
NOT to arch; SetOverlapsScrollArea(1); sound; slide-in ramp
SetupConstantTimeRamp(0.0, height−176.0, 250) (consts in code pool 0x59b500).

DisablePopup @0x26df40: +0xe0=0; RemoveChild from main view (vt+0x2c);
SetOverlapsScrollArea(0).

BuildMyBuffer @0x26d280 (the popup's entire look):
- Layout: left pane rect (0,0,168,127), right text pane (168,0,540,127) —
  two rects stored at r1+0x58/r1+0x68 (0x26d2ac-0x26d2c8); description
  word-wrapped with font +0xd4 (vt+0x60 measure, vt+0x68 draw), 14 rating
  lines via GetCatalogRating @0x209210 with labels +0x110[i]; audience
  suffix (AdultsOnly/PetsOnly/CatsOnly/DogsOnly/Kids selectors at 0x1041b0/
  0x103a40/0x103c10/0x103df0/0x103fd0 → +0x148[0..4]); can't-afford → red
  tint (0x26dccc-0x26dd94).
- Buffer: new cTSBuffer(100), init w=popup width, h=max(needed, **127**),
  16bpp + zbuffer; product icon DrawIcon @0x20a930 scaled 1/3
  (mulhw 0x55555556) into the left pane.
- **Placement** (0x26dd98-0x26dea4): walk srcWin's parent chain summing
  +0x74/+0x78 (absolute x/y of the hovered button), then
  SetArea(popup, chainX − popupW, chainY − popupH, +W, +H) in main-view
  coords → the popup appears **above-left of the hovered button, over the 3D
  view**. It never overlaps the toolbar band.
- kCatalogPopupBack 3006 / Tiles 3008 are NOT used here (no LoadBuffer of
  3006/3008 in this class — those belong elsewhere; see residuals).

Show/hide law: popup enabled on subtool-button click/hover (arch TSOnCommand
1/0x13) and on roof-panel pattern hover (roof TSOnCommand 0x29cc00 → 0x29cecc/
0x29d08c/0x29d120/0x29d204); disabled on LowerPopup @0x25fa10, mouse leaving
the guard rect (TSOnMouseMove 0x25fd90: outside arch+0x158..0x164 =
(314,5,W,100) → DisablePopup), mouse exit of arch/child windows
(0x25fa70/0x25fc00).

## 7. cWinRoofPanel (DECODED)

ctor @0x29e770: vtable TOC -0x6970; +0xcc = button array (6), +0xe4 =
vector<btn*> pattern buttons, +0xf0 = vector<name> pattern names, +0xf4 =
count, +0x108 = page, +0x10c = per-page, +0x110 = CPState, +0x114/+0x118 =
grid origin, +0x120/+0x124 = two pseudo-Products.

Init @0x29e4f0 → BuildButtons @0x29df20:
- **Strings = STR# 147** (`LoadUIStrings(0x93, 'UIText', …)` 0x29df60; rsmp
  name **'roofpanelstrs'**). Verified content (English block, in order):
  '(10;50)' 'Shallow Roof Pitch' / '(10;30)' 'Medium Roof Pitch' /
  '(10;1)' 'Steep Roof Pitch' / '(62;20)' 'Previous Page' / '(256;20)'
  '(460;20)' 'Next Page' / '(10;71)' 'Flat Roof' / '(74;3)' 'Roof Pitch' …
  'Roof Patterns' — positions parsed by StringPosToPoint 0x25f110.
- 6 buttons, res ids from code literal pool @code 0x59b808 (file 0x5a4698;
  TOC -0x4f2c): **{3900 kBldSbTlRoofShallow, 3901 Medium, 3902 Steep,
  100 kCatalogPrevPage, 101 kCatalogNextPage, 3904 kBldSbTlRoofFlat}**;
  SetImage(buf, 4, 1); MoveTo(x−4, y) → shallow (6,50), medium (6,30),
  steep (6,1), prevPage (58,20), nextPage (253,20)@800w or (433,20),
  flat (6,71) — roof-local; screen = + (314+220, 5+SH−100).
- Screen-width switch (0x29dfec): if DDD width == 800 → **8 patterns/page**,
  nextPage from '(256;20)' then x−3 → **x=253**; at every other width →
  **16/page**, from '(460;20)' then x−27 → **x=433**.
- Pattern grid origin = '(74;3)' − (4,2) = **(70, 1)** roof-local (+0x114/
  +0x118, 0x29e280-0x29e298).
- TraverseRoofDirectory @0x29ddd0 then BuildPatternButtons @0x29d660:
  template buffer **3903 kRoofPatternTemplate**; per roof texture: thumb
  36x36 (inset 4 in a 40 cell, or centered if smaller), drawn into the
  template's 4 state cells spaced **45** px; button SetImage(template, 4, 1);
  names stripped of path/extension; appended to +0xe4.
- SetCurPage @0x29c730: clamp page to count/perPage; hide off-page buttons;
  the first pattern button measures **45x45**, and 0x29c890+ constructs a
  flow bound at `originX + (perPage/2)*45`, `originY + 2*45`. From origin
  **(70,1)** with 45px pitch this is an exact two-row grid:
  - width == 800: **4 columns × 2 rows**, slots 0..3 at y=1/x=70..205 and
    slots 4..7 at y=46/x=70..205;
  - every other width: **8 columns × 2 rows**, slots 0..7 at y=1/x=70..385
    and slots 8..15 at y=46/x=70..385.
  Equivalently, slot `s` is `(70 + (s % columns)*45,
  1 + (s / columns)*45)` with columns = perPage/2.
- UpdateViewFromCPState @0x29c420: pitch = cRoofLayer::GetPitch 0x2dcac0;
  |p−0.4|≤1e−6 → Shallow btn state 1; |p−0.6|≤1e−6 → Medium; p<0.1 → Flat;
  else Steep (consts: 0.4/0.6 are in-TOC floats [TOC-0x68c/-0x688], 0.1 and
  1e-6 in code pool 0x59b820/0x59bf3c). Pattern match by name vs
  GetRoofPattern 0x2dc950. prevPage shown iff page≠0; nextPage iff
  (page+1)·perPage < count.
- Two pseudo-Products for the popup: +0x120 'Roof Pitch' desc 'The pitch…'
  bitmap **3605 kBldPopupRoofPitch**; +0x124 'Roof Patterns' desc 'A roof
  pattern…' bitmap **3606 kBldPopupRoofPattern** (SetBitmap @0x2093e0) —
  shown via the SAME cWinCatalogPopup on hover.

## 8. Toothpicks 4912/4913 (DECODED)

Buffers arch+0x174/+0x178 = LoadBuffer(0x1330=4912, 4) / (0x1331=4913, 4)
(0x26205c-0x262078). Drawn ONLY in `cWinArch::TSPaint` 0x261ae4-0x261c1c into
arch's private surface (window at arch+0x5c): two vt+0xa0 calls whose dest
rects are computed from the buffer's own src rect {buf+0x14, buf+0x18,
buf+0x1c, buf+0x20} plus arch's rect (+0x1c/+0x20):
- toothpick1 dests: {srcX+archR+**0x31**, srcY+archB+**6**, …} and
  {srcX+archR, srcY+archB, …}
- toothpick2 dests: same but x-offset **0x13f** instead of 0x31.
INTERPRETED: the two 2x89 strips are the bottom-edge "tabs" of the build
toolbar blitted at arch-relative x-offsets 49 and 319, y+6 — the visual seam
where the toolbar band meets the UCP plate. vt+0xa0 on the surface class =
multi-rect tile blit; +0x2c(0x10) after = flush/commit.

## 9. Coexistence law: popup vs tool row vs roof panel (DECODED)

- The 12 tool buttons + undo/redo never move or hide in build mode (only
  SetState frames change; enable byte for undo/redo).
- The pattern row (338..788, y5..95) and the tool cluster (58..322) are
  disjoint columns of the same band; page arrows sit in the 4px gutters
  (326 / 790).
- Roof tool active: pattern row + page arrows hidden, roof panel (314,5,W,100)
  shown in their place — the panel's own pitch/page/pattern widgets replace
  them (SetRoofMode @0x25f870).
- cWinCatalogPopup is parented to the MAIN view, positioned above-left of the
  hovered subtool button, i.e. over the 3D view; it can never cover the tool
  row. It closes on mouse exit of the arch band region (guard rect
  arch+0x158.. = (314,5,W,100) after Init).

## 10. Static map (for the port)

- TOC base = DATA 0x8000. TOC slots used here: -0x6af8 arch vt, -0x6af4 →
  DATA 0x55290 (tool res ids), -0x6af0 → BSS 0x92990 (16 tooltips),
  -0x6aec → BSS 0x92a90 (12 tool anchors), -0x5128/-0x512c → BSS 0x92af8/
  0x92af0 (undo/redo anchors), -0x5124 → DATA 0x55490 (tool→sort-key jump
  table), -0x4f2c → code 0x59b808 (roof res ids), -0x68c/-0x688 = floats
  0.4/0.6 in-TOC, -0x7178 → fonts static, -0xae0 = 16 (tooltip count).
- STR# tables (UIText file): **139 BldTips** (toolbar tooltips 0..15),
  **147 roofpanelstrs** (roof layout+labels), **160 CatalogRatings**
  (catalog popup), plus 134 'NghRollover' (not this round).

## Residuals (honest)

- The 12 kToolBtn cell sizes are now exact from the UIGraphics art headers
  and are listed in §2; this residual is closed.
- The +0x148 branch is ctor-zero-only with no cWinArch writer and is inactive
  in the shipped toolbar path; this residual is closed.
- SetCurPage's bound arithmetic is now decoded to the exact 4x2 / 8x2 grid
  in §7; this residual is closed.
- Roof panel TSOnCommand (0x29cc00) internals not fully disassembled (pitch
  setters presumably call cRoofLayer::SetPitch) — only its EnablePopup calls
  verified.
- kCatalogPopupBack 3006 / kCatalogPopupBackTiles 3008: no reference found in
  cWinCatalogPopup; they likely belong to cWinCatalog (buy-mode catalog
  window), out of this round's scope.
- CPState+0x44 assumed = main/DDD view (used only as an x-reference frame;
  the net arch-local math is independent of it).
