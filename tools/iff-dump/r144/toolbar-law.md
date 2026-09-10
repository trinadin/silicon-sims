# R144 — IN-LOT TOOLBAR CONTENT LAW (decoded from PPC binary)

Round target: the exact content layout of the 804x100 in-lot toolbar (PanelBack.bmp
4910 kUniversalCPBkg) and its relation to the UCP plate, continuing r142/r143.
Binary: `game-data/The Sims/The Sims Complete` (PPC PEF). All addresses are FILE
offsets (== code-virtual; data-stored code pointer P → file P+0x8E90; vtable slot
rawcode+0x8E90 = file address — both laws re-validated this round).

DECODED = read directly from instruction immediates.
INTERPRETED = semantic reading; immediates cited but behavior inferred.

Coordinate conventions used below:
- **screen**: game-window pixels, origin top-left. SH = screen height.
- **cpanel**: cWinCPanel-local (origin = (0, SH-150)).
- **people-local / arch-local**: child-panel-local; PanelBack pixel x == people-local x
  (people window starts exactly at the PanelBack's left edge = screen x 220).

---

## 0. The container stack (DECODED)

`cSimsApp::WindowSetup` @0x255bf0 (one-time creation path):

- DDD view (app+0x80) = `SetArea(mainWin->0x74..0x80)` = full game window.
- `cWinCPanel` created (ctor @0x270d40, vtable data 0x56748) and
  **`SetArea(cpanel, dddL, dddB-0x96, dddR, dddB)` = (0, SH-150, W, SH)** — 150 tall
  (0x255d2c-0x255d4c), added as child of the DDD view, then `PostInit` @0x270000.
- NOTE: `RebuildControlPanel` @0x24de40 (re-create path, only caller = a cheat
  callback 0x24fb1c) uses bottom-**100** instead of bottom-**150** — that path would
  push the toolbar 50px off-screen; the canon creation is WindowSetup's -150.

`cWinCPanel::Init` @0x270170 builds eight child panels, every one anchored from the
ONE static pair at **BSS 0x93230 = {220, 50}** written by `__sinit_:WinCPanel_cpp`
@0x270e10 (store at 0x270e94/98). Per child (identical code shape, e.g. arch at
0x2704fc):

```
W   = cpanel width
SetArea#1(child, l, t, l+(W-220), t+H0)        ; H0 = 100 (people: 150)
SetArea#2(child, 220, 50[, or 50-50=0 people], 220+GetW, 50+GetH)
Init(vt+0x14); HideWindow(vt+0xa0); AddChild(vt+0x28)
```

| member | class (ctor) | cpanel rect | screen rect (W=1024) |
|---|---|---|---|
| +0xd4 | cWinViewControl 0x2b80a0 | set in PostInit | (0, SH-183, 220, SH) — see §6 |
| +0xe0 | cWinArch 0x262970 | (220, 50, W, 150) | (220, SH-100, W, SH) |
| +0xdc | cWinPeople 0x292220 | (220, 0, W, 150) | (220, SH-150, W, SH) |
| +0xd8 | cWinCatalog 0x26c490 | (220, 50, W, 150) | (220, SH-100, W, SH) |
| +0xe4 | cWinOptions 0x2866c0 | (220, 50, W, 150) | (220, SH-100, W, SH) |
| +0xec | cWinCamPanel 0x267310 | (220, 50, W, 150) | (220, SH-100, W, SH) |
| +0xf0 | cWinSweepMeter 0x2a9360 | (220, 50, W, 150) | (220, SH-100, W, SH) |
| +0xe8 | cWinDisposePopup 0x2769d0 | (220, 50, W, 150) | (220, SH-100, W, SH) |

Buffers: +0xfc = 4910 kUniversalCPBkg (PanelBack 804x100, shared by all children
via ctor arg); +0xf8 = 2036 kBackPatch (52x100); +0x104 = 4918 (double-byte locales
only). `cWinCPanel::TSPaint` @0x26f510 blits kBackPatch at cpanel-local **(168, 50)**
(anchor BSS 0x93220 = {168,50}, 0x26f5a8-0x26f5c4) — i.e. screen (168, SH-100) — the
52px strip that visually bridges UCP plate and toolbar (168+52 = 220).
`cWinCPanel::SetPanel` @0x26eee0 switches child per mode via vt+0xb0/0x9c/0xa0.

**So: the toolbar band (804x100) = screen y SH-100..SH, x 220..W; cWinPeople owns an
extra 50px strip above it (y SH-150..SH-100) for its sub-panel chrome.**

vtable slots re-validated (cpanel vt @data 0x56748): +0x14 Init, +0x18 Shutdown,
+0x28 AddChild, +0x68 SetArea, +0x9c ShowWindow, +0xa0 HideWindow, +0x16c TSPaint.

---

## 1. LIVE-MODE TOOLBAR (cWinPeople) — the primary target

`cWinPeople::Init` @0x28ee40 (10712 B). All coords people-local (PanelBack pixels;
y=50 is the toolbar's top edge). Static tables from `__sinit_:WinPeople_cpp`
@0x292c70 (ends 0x2930a8).

### 1.1 Webcam strip (family member buttons) — DECODED
Built 0x28f274-0x28f344: **8 x cTSWinBtn** (0x18c bytes each) in array +0xf0[i],
AddChild to people. No SetImage at build time — each gets `btn+0x14c = 2` and
`btn+0x150 = [DATA 0x74460]` (the shared thumbnail template buffer, kPeopleTemplate
4600 = PeopleTemplate.bmp 180x45 loaded by `PersonFinder::Init` @0x245f78) — i.e.
custom-drawn webcam thumbnails (INTERPRETED: type-2 custom draw = live sim portrait +
template frame; kPeopleBtnUnknown 107 = UnknownFace.bmp 180x135 is the empty state).

Layout (flow, 0x28f550-0x28f738, params BSS 0x93420 = {9, 55, 189, 150}):
- origin (9, 55), button 45x45 (const 0x2d at 0x28f5a0), x-pitch 45,
  wrap when next x+45 > 189, row pitch 45, bottom limit 150.
- Effective grid: **4 columns x 2 rows** — cells (9,55),(54,55),(99,55),(144,55) /
  (9,100),(54,100),(99,100),(144,100). Screen: add (220, SH-150).

### 1.2 LiveGadget backdrop — DECODED
4911 kLiveModeGauge (LiveGadget.BMP 108x100) → people+0x224 (0x28eee4).
`cWinPeople::TSPaint` @0x28d510 blits it at people-local anchor
**BSS 0x93480 = {193, 50}** (0x28d644-0x28d6bc) → covers x 193..301, y 50..150:
the plaque behind the category tab cluster (INTERPRETED: it is the tab-column
backdrop, not a gauge).

### 1.3 Category switcher (7 tab buttons) — DECODED
Loop 0x28f38c-0x28f468; res-id array at code 0x5a4598 =
{4500, 4504, 4503, 4502, 4501, 4505, 4507}; anchors BSS 0x933e8+8i (pairs).
Per button: LoadBuffer(res); `SetImage(buf, 4, 1)`; flag 0x163=1; `TSWinMoveTo(ax, ay)`;
AddChild. Tooltips are `Live.iff` STR# 134 `ModeTips` entries `[0..6]` in
button order: `Mood`, `Relationships`, `Job`, `House`, `Personality`,
`Interests`, `Inventory`.

| i | res | art (sheet -> cell) | anchor (people-local) | RECT |
|---|---|---|---|---|
| 0 | 4500 kMoodBtn | Mood.bmp 128x39 -> 32x39 | (202, 81) | (202,81,234,120) |
| 1 | 4504 kRelationshipBtn | Relationship.bmp 108x30 -> 27x30 | (269, 54) | (269,54,296,84) |
| 2 | 4503 kJobBtn | Job.bmp 108x30 -> 27x30 | (269, 85) | (269,85,296,115) |
| 3 | 4502 kHouseBtn | House.bmp 108x30 -> 27x30 | (269, 116) | (269,116,296,146) |
| 4 | 4501 kPersonalityBtn | Personality.bmp 108x30 -> 27x30 | (237, 85) | (237,85,264,115) |
| 5 | 4505 kInterestBtn | Interest.bmp 108x30 -> 27x30 | (237, 54) | (237,54,264,84) |
| 6 | 4507 kGiftBtn | Gift.bmp 108x30 -> 27x30 | (237, 116) | (237,116,264,146) |

Geometry law: three columns x=237/269 (27 wide, 31 px pitch) + the wider Mood button
at x=202; all inside the LiveGadget plaque (193..301).

### 1.4 Mood trend arrows — DECODED
Buffers: people+0x174 = 4506 kGreenbars (Greenbars.bmp 27x25), people+0x170 = 4510
kRedBars (Redbars.bmp 27x25) (0x28f530-0x28f54c). TSPaint 0x28d7a8-0x28da64:
- trend = clamp(round(overall-mood delta, weights at code 0x5a45ec float pool), -5, +5)
- trend < 0: kRedBars at people-local **{205, 123}** (BSS 0x934a0), src rect reveals
  5 px per |trend| unit (partial arrow).
- trend > 0: kGreenBars at **{205, 53}** (BSS 0x93498), same 5px/unit reveal.
- trend == 0: both drawn full with 2px-inset src (INTERPRETED steady-state art).

### 1.5 The 8 motive gauges — DECODED (the "2x4 grid" verified)
Host window people+0xf8 (plain cTSWin 0xcc): `SetArea(0,50,280,150)` (rect BSS
0x934c8 single-byte / 0x934b8 = (0,0,280,150) double-byte) then `TSWinMoveTo(300,50)`
(anchor BSS 0x93490/0x93488) → final rect **(300, 50, 580, 150)** people-local =
screen (520, SH-100, 800, SH).

Gauges: 8 x `cWinMotive` (ctor @0x27fa50, vtable data 0x57948, cTSWinBtn subclass)
in people+0x128[i], motive ids from code 0x5a45b8[0..7] = {7,5,6,15,8,14,9,13};
each ctor takes two icon GUIDs from BSS 0x93390+8i (see §1.6) + two scale ints from
BSS 0x93350+8i ({0,2} for i=0, else 0).

Flow layout (0x2905ac-0x290800), single-byte UI:
- start pt (0, 21), extent (200, 105); DB+Japanese: (0,28)/(200,145); DB other:
  (0,40)/(200,155).
- gauge size from cWinMotive::Init @0x27f620: **SetArea(l, t, l+100, t+H)** with
  H = 0x14 (20) single-byte / 0x19 (25) DB / 0x1e (30) DB-Japanese
  (0x27f6d0/0x27f710/0x27f748).
- x += 100 per gauge; wrap when x+100 > 200 → next row, row pitch = H-1 (19).

**Effective single-byte grid (host-local): (0,21),(100,21) / (0,40),(100,40) /
(0,59),(100,59) / (0,78),(100,78) — 2 columns x 4 rows of 100x20.**
Screen (host at (300,50) people + (220,SH-150)): gauge(i) = (520+100*col, SH-129+19*row).
Pet motives: 8 more cWinMotive in people+0x134 (ids 0x5a45b8[8..12]={7,6,3,5,2}+...),
GUID pairs from BSS 0x93310, same flow at pt (0,21)/(200,105) — laid out in host+0xfc.

Gauge internals (cWinMotive):
- label font = **font[8]** (BSS 0x92900+0x20, SetFont 0x27f75c); label y-offset
  (fontH-10)/2 (0x27ea90).
- a cWinDeltaMeter (ctor 0x275da0, arg 10) at +0x1a8, full gauge size, delta arrows
  at SetTopLefts({W-20, H}, {W+61, H}) (0x27f808-0x27f834) (INTERPRETED: up/down
  trend arrows right of the bar).
- TSPaint @0x27ea50: two fills into the private surface: track color over the whole
  100xH rect, then the value fill `clamp(round(raw*60/200), 0, 60)` px wide
  (0x27eb64-0x27ebc8; constants 60@code 0x5a4478+0x3c, 200@+0x40) in a color lerped
  between two RGB endpoints (vec3 ramp, RampGenerator at +0x1dc; endpoint pair per
  motive from the ctor GUID table colors — INTERPRETED) + 1px-inset highlight strip
  (0x27f2a8-0x27f2f4).

### 1.6 Motive icon GUID table (BSS 0x93390, 8 pairs) — DECODED values
In grid order (Hunger, Energy, Comfort, Fun, Hygiene, Social, Bladder, Room):

| row | human GUID 0 | human GUID 1 | pet GUID 0 | pet GUID 1 |
|---:|---:|---:|---:|---:|
| 0 | `0xb94755df` | `0xff9f18e0` | `0x8eec19f7` | `0xffc0ea58` |
| 1 | `0x82e04c5b` | `0xbf137195` | `0xbc537c52` | `0x96cb67ae` |
| 2 | `0xd97681db` | `0x7cb11019` | `0xe23f9020` | `0x2b4502a2` |
| 3 | `0x5cdc712f` | `0x481a74ec` | `0x914e5a74` | `0xb26ab256` |
| 4 | `0x85e00942` | `0x85e02a40` | `0x2ecdb068` | `0` |
| 5 | `0` | `0` | `0` | `0` |
| 6 | `0x84e0774c` | `0` | `0x39ccf441` | `0xbedd7b26` |
| 7 | `0x7f907075` | `0xbc3ba088` | `0x7f907075` | `0xbc3ba088` |

These are the selectors passed to `ObjectFolder::GetSelectorByGUID` at 0x27fb84.
The scale tables at BSS 0x93350/0x932d0 contain `{0,2}` for row 0 and zeroes for
every remaining row. `cWinMotive` therefore invokes `SetSpecialScale(1,2)` only
for the second Hunger selector (0x27fc14-0x27fc40). The corrected values above
apply signed `addi` carry to the preceding `lis`; for example `lis d977` +
`addi -0x7e25` is `0xd97681db`, not `0xd97781db`.

### 1.7 Sub-panel hosts and sub-panels — DECODED
Four host cTSWins, all `SetArea(0,50,280,150)` (or DB (0,0,280,150)) + MoveTo(300,50):
people +0xf8 (motives), +0xfc (interests/gifts/pet motives), +0x100 (selected-sim
name + relationship list), +0x104 (relationships + page arrows).

Sub-panels (`SetArea(0,50,280,150)` +224 right at 1024-wide screens —
`GetBuffDims()==0x400` check 0x28f9f4 — then MoveTo(300,50) → rect (300,50,804,150)
people-local = the whole toolbar band right of the tab column):
+0x108 cWinSubpanelJob, +0x10c DogSkills, +0x110 CatSkills, +0x114 Fame,
+0x118 House, +0x11c ReportCard. In-band category panels, NOT popups above the bar
(corr. of r142 residual: the TallSubPanel 4918 600x150 art is used only as the
double-byte composite source, loaded in cWinCPanel::Init 0x270240).

Misc people members:
- +0x18c/+0x190: two hidden cTSWinBtn (SetImage(100/101 = ScrollLeft/ScrollRight
  36x49 -> 9x49, cols 4 rows 1) in host +0x104 — sub-panel page arrows.
- +0x1bc: selected-sim NAME button — cTSWinBtn, SetFont(**font[12]**
  BSS 0x92900+0x30), SetText(name), SetImage(NULL,4,1), child of host +0x100
  (0x290e74-0x290f08).
- +0x150: cWinLivePopup (ctor 0x279790); its backdrop = **4601 kTrackingTarget**
  (TrackingTarget.bmp 45x45) at people+0x240 with magenta color-key (0x291134).
  This is the entire use of 4601 — the track-camera follow popup, not a toolbar
  button (port's UIOriginalTrackButton 45x45 = this art).
- Pie menus: two cTSPieMenu (+0xd4, +0xd8), colors (0,0x28,0x8c)/(0xff,0xff,0xff)/
  (0xa5,0xc3,0xd6)/(0,0xff,0xff), radius 150, 60/30 params — context pies, not
  toolbar geometry.
- Texts (cTSWinText, font **font[11]** BSS 0x92900+0x2c; anchors {5,0} single-byte /
  {10,5} DB from BSS 0x934b0/0x934a8): one per host (titles "Motives"/etc);
  font[9]/font[10] for langs 4/7 (0x2913a0); a 4th with **font[6]** (0x92900+0x18).

---

## 2. CLOCK + MONEY — they live on the UCP PLATE, not the toolbar (DECODED)

Both are children of cWinViewControl and updated in
`cWinViewControl::UpdateViewFromCPState` @0x2b3dc0:

- **Money**: VC+0x100 = cTSWinBtn; text = `CPState::GetFunds()` (a formatted
  cTSString) via SetText at 0x2b4280; flash-on-change via SetState(1)/(0) +
  repaint (0x2b4314-0x2b4390). Built in VC::Init 0x2b7234-0x2b73c0:
  SetImage(NULL,4,0), SetCaptureBkg(1), SetFont(**font[10]**, BSS 0x92900+0x28),
  sized to the worst-case string **"$9,999,999"** (data 0x5b11e), then
  right-aligned: final rect = **(178-w, 158, 178, 158+h)** — anchor BSS 0x93b58 =
  {178, 158} (0x2b72fc-0x2b733c). UCP-plate coords (plate origin = screen (0,SH-183)).
- **Clock**: VC+0x1a8 = cTSWinBtn (font[10]) whose button chrome is the clock face;
  the DIGITS are painted directly in `cWinViewControl::TSPaint` @0x2b58a0:
  string = VC+0x168 (from `CPState::GetTime()`, 0x2b4440), font = VC+0x1dc or
  **font[8]**, rect BSS 0x93b40 = **(95, 118, 151, 125)** (writer 0x2b8428-34),
  right edge grows by measured width, text CENTERED horizontally in the rect
  (0x2b5a34-0x2b5a58), drawn via font vt+0x80 (0x2b5b94). Width sized for "999".
  The clock button rect = (moneyLeft-w', moneyBottom, moneyLeft, moneyBottom+h')
  (0x2b7520-0x2b75a8) — directly BELOW the money readout, left-aligned to it.

So the plate's bottom-right = money over clock; both right-anchored at x<=178,
y>=158. (In the shipped game the time string includes day info — INTERPRETED.)

---

## 3. MODE PATCHES 4803-4806 (+2035) compose law — DECODED

Loaded in VC::Init 0x2b7e74-0x2b7f64: VC+0x1c4=4803 kLiveModePatch (213x153),
+0x1c8=4804 kBuyModePatch (156x148), +0x1cc=4805 kBuildModePatch (107x132),
+0x1d0=4806 kOptionsModePatch (37x100), +0x1d4=2035 kCameraPatch (55x100); all
magenta-keyed. Composited in `cWinViewControl::BlitPrivateBufferToParent` @0x2b5460
(on the mode-cascade backbuffer), switch on `CPState::GetMode()` (0x2b5590):

| mode | patch | art size | plate-local offset (DECODED) | covered rect |
|---|---|---|---|---|
| 0 build | 4805 | 107x132 | **(113, 51)** | (113,51,220,183) |
| 1 buy | 4804 | 156x148 | **(64, 35)** | (64,35,220,183) |
| 2 live | 4803 | 213x153 | **(7, 30)** | (7,30,220,183) |
| 3 options | 4806 | 37x100 | **(183, 83)** | (183,83,220,183) |
| 4 camera | 2035 | 55x100 | **(165, 83)** | (165,83,220,183) |

All right-aligned to the plate's right edge (220) and bottom (183) — the waterfall
backdrops behind the mode cascade; composed whenever the plate's private buffer is
blitted (every repaint of the plate, not only on switch — INTERPRETED from call
site). In "bobo vision" only the alpha is drawn (0x2b5490).

---

## 4. BUILD/BUY TOOLBAR (cWinArch) — top-level chrome — DECODED

`cWinArch::Init` @0x261f40. Arch-local coords (window = cpanel (220,50,W,150); the
100-tall band = arch-local y 0..100).

- 12 build-tool buttons (array +0xec[0..11], loop 0x26238c-0x26243c): res ids at
  DATA 0x55290 = **{66 Terrain, 68 Pool, 70 Wall, 73 Wallpaper, 74 Stairs, 75
  Fireplace, 67 Tree, 69 Floor, 71 Door, 72 Window, 76 Roof, 77 Hand}**; each
  SetImage(res, 4, 1) (per-state cell = sheet/4 wide); label strings at BSS
  0x92990+0x30+4i.
- Flow layout (fields +0x128.. written 0x262470-0x2624d4, applied in
  UpdateViewFromCPState 0x2607f8-0x260964): origin **(338, 5)**, x-pitch 45,
  wrap when x+45 > archW-4, row pitch 45, bottom limit 100 → 2 rows of ~10.
- 4 chrome buttons: +0xdc undo (64 kToolBtnUndo 120x24 -> 30x24), +0xe0 redo
  (65), +0xe4 page-left (100 kCatalogPrevPage 36x49 -> 9x49), +0xe8 page-right
  (101) — all SetImage(_,4,1); enable state per ArchCanUndo/Redo (0x2609f0+).
  Positioned by the catalog popup (not re-anchored in Init — INTERPRETED: they sit
  on the catalog's left edge).
- Buffers +0x174/+0x178 = 4912/4913 kBuildToothPick1/2 (2x89 strips).
- cWinCatalogPopup +0x154 (ctor 0x26e620); area fields set to (201, 0, W, 100).
- cWinRoofPanel +0x16c (ctor 0x29e770): `SetArea(314, 5, W, 100)` (0x262670).

(The object-catalog grid internals belong to cWinCatalog/cWinCatalogPopup — prior
rounds; not re-derived here.)

---

## 5. UCP PLATE — corrected anchor map (DECODED, r142 values re-validated)

The r142 TOC-slot -> BSS-address mapping was skewed (e.g. -0x4E3C is BSS 0x93B18,
not 0x93B88 — verified from the unpacked data directly). Re-tracing
`__sinit_:WinCtrlPanel`-equivalent writer @0x2b8230 with the correct mapping
confirms ALL r142 anchor VALUES:

| TOC | BSS | value | button |
|---|---|---|---|
| -0x4E3C | 0x93B18 | (85,135) | kPause |
| -0x4E34* | 0x93BC0 | (105,135) | kSpeed1 |
| -0x4E44* | 0x93B08 | (120,135) | kSpeed2 |
| -0x4E48* | 0x93B00 | (141,135) | kSpeed3 |
| -0x4E58* | 0x93AF8 | (5,164) | kHelpBtn 4993 |
| -0x4E38 | 0x93B68 | (175,95) | kCameraBtn 2034 |
| -0x4E2C | 0x93B70 | (69,20) | kObjects |
| -0x4E30* | 0x93B78 | (117,45) | kArch |
(*stored via writer registers r25/r24/r23/r22 etc. at 0x2b8458-0x2b847c)

SetImage sheets re-confirmed from VC::Init: every plate button cols=4, rows=1
EXCEPT **kPause = SetImage(_, 4, 2)** (0x2b6e20-0x2b6e34) → pause cell 15x15.
kHelpBtn/kCameraBtn full law = r142 table (4993: 40x15 sheet -> 10x15 at (5,164);
2034: 116x29 -> 29x29 at (175,95)) — stands.

Speed cluster is PLATE-relative (ViewControl children), i.e. screen
(85,SH-48)...(173,SH-33) — NOT toolbar-relative.

---

## 6. PostInit / UCP placement — DECODED
`cWinCPanel::PostInit` @0x270000: ViewControl is re-parented to the container
returned by cpanel vt+0x20 (the DDD/main window) and `SetArea(0, containerH-183,
220, containerH)` (0x270068-0x270098) — flush bottom-left of the screen, exactly
the r142 (0, SH-183) law.

---

## 7. Fonts & colors (r143 law applied: font[i] at BSS 0x92900+4i)

| element | font | evidence |
|---|---|---|
| motive gauge labels | font[8] | 0x27f664-0x27f67c |
| clock digits | font[8] (or custom VC+0x1dc) | 0x2b5978-0x2b5988 |
| money readout | font[10] | 0x2b7278-0x2b7284 |
| clock button chrome | font[10] | 0x2b74f8 |
| sub-panel title texts | font[11] (font[9]/[10] langs 4/7) | 0x2911c4, 0x2913a0 |
| relationship title | font[6] | 0x2914e4 |
| selected-sim name | font[12] | 0x290e94 |

Money/clock flash colors: SetCustomHiliteColors with the four 565 words at VC+0x18c
(0x2b7410-0x2b74a4). Toolbar transparent key = 0xff00ff magenta (BSS 0x92968).

## 8. Port-relevant corrections (vs the r141-era interpretation)
1. Motive gauges are **2 columns x 4 rows** of 100x20 (flow-verified), not a 2x4
   (2 rows) grid; pitch 19; origin host-local (0,21).
2. The gauge area starts at people-local x=300 (screen 520) — the whole left half
   of the toolbar (webcams + tab plaque) contains NO gauges.
3. Clock + money are on the UCP plate bottom-right (money (<=178,158), clock below
   it, clock digits centered in (95,118,151,125)) — nothing time/money-related is
   drawn on the 804x100 panel.
4. The mood trend arrows are Greenbars/RedBars at (205,53)/(205,123) people-local.
5. Speed cluster, pause, help, camera = plate children (§5); kBackPatch 2036 at
   cpanel (168,50) bridges plate->toolbar.
6. cWinPeople is 150 tall (extends 50px above the toolbar band); all other children
   are 100 tall exactly in-band.

## Residuals (honest)
- Webcam thumbnail DRAW internals (type-2 custom draw in cTSWinBtn::TSPaint) not
  disassembled; only placement + template buffer decoded.
- Money/clock exact runtime strings (GetFunds/GetTime formatters) not decoded.
- cWinCatalog/cWinCatalogPopup internals (tab row, scrollbar) not re-derived this
  round (prior rounds cover the catalog grid; the popup area fields are (201,0,W,100)).
- Arch chrome (undo/redo/page) final positions live inside the catalog popup layout.
- The motive color-ramp endpoints per motive: GUID table decoded; RGB endpoint
  pairs are in the cWinMotive ctor float/vec3 setup — partially traced.
