# R159 — Engine decode: relationship / inventory / report card / people pie / help+camera / build toolbar

Source: `game-data/The Sims/The Sims Complete` (PPC PEF, sec0 code at file 0x8e90,
sec1 data packed at 0x5c22f0, unpacked size 0x7bf80). Method per r90 recipe +
r153 symbol map (14,578 function starts, decimal offsets = file offsets).
sec1 regenerated at `/tmp/r159-sec1-unpacked.bin` (r104-corrected unpacker,
exact 0x7bf80 match). TOC convention: `[TOC - X]` = entry at sec1[0x8000 - X].
The gadget factory is `nsSMResFac::LoadBuffer(id, &slot, 4)` at **0x3b6190**;
`0x591440` = operator new (alloc sizes identify classes: cTSWinBtn 396,
cWinDeltaMeter 496, cWinGift 420, cWinPictureDialog 320, cWinBudgetDlg 312,
cWinHelp 384, cTSPieMenu 396, cWinCamPanel 288).

All art ids resolved against `game-data/The Sims/UIGraphics/Res_CPanel.h`.

---

## 1. cWinRelationship — the relationship grid window

Class cluster 0x29a5c0-0x29c2e5 (r153 symbols):

| method | body | notes |
|---|---|---|
| SetRelationship(int,int) | 0x29a5c0-0x29a759 | full decode below |
| SetupClient | 0x29a790-0x29aaa9 | builds label strings (GetStringConstant + STR# via 0x59cd30/0x843b0 chain) |
| TSOnMouseDownR/L | 0x29aad0 / 0x29ab50 | |
| TSPaint | 0x29abb0-0x29bb25 | the big compositor (see below) |
| Shutdown / Init / dt / ct | 0x29bb50 / 0x29bbb0 / 0x29be50 / 0x29bf80 | ctor args (cWinLivePopupClient&, StringSetIter) |

### Init (0x29bbb0) — gadget inventory
* Enlarges itself by **(+45, +110)** English / **(+45, +90)** double-byte UI
  (branch on `IsDoubleByteUI` 0x20c670) — vtable+104 SetArea(0,0,w+45,h+110).
* Creates a **cWinDeltaMeter** (496 B alloc, ctor side-effect via 0x591440 +
  failure path 0x275da0) stored at **this+416**; anchor via
  `cWinDeltaMeter::SetTopLefts` (0x275af0) with points from the global at
  **[TOC-20288]** offset {0,+6}..{+20,+6} — i.e. a 20-wide meter pinned at the
  global anchor, 6 px down. [TOC-20288] is BSS (runtime point) — static cap.
* Three gated marker gadgets (the COMPOSITION HIGHLIGHTS):
  * **kRelSmiley 4105** mounted iff `*[TOC-27020] != 0`
  * **kRelHeart 4106** iff `*[TOC-27028] != 0`
  * **kRelHeartDeep 4107** iff `*[TOC-27024] != 0`
  All three TOC slots are BSS singletons (runtime expansion/mode managers) —
  the gates' identity resists static decode; ids + call shape are canon.

### Field layout (ctor 0x29bf80 + SetRelationship)
| field | init | meaning |
|---|---|---|
| +396, +400 | -1 | the two relationship ints (SetRelationship args) |
| +404, +408 | 0 | display scores (from PersonFinder::GetRelation 0x241ba0 out +72/+76) |
| +412/+413/+414 (bytes) | 0 | marker-select flags (GetRelation out bytes +69/+68/+70) |
| +416 | 0 | cWinDeltaMeter |
| +420 | 0 | client window ref |
| +424/+428/+432 | 0 | spare |
| +436/+440 | — | two cTSString (0x4f4e70) |

SetRelationship(0x29a5c0) behavior: store +396/+400 (dirty if changed →
`cWinDeltaMeter::ResetHistory` 0x274730); when both are -1, pull
`PersonFinder::GetRelation(i,j,rel)` (0x241ba0) — rel layout {+64 score,
+68/+69/+70 flag bytes, +72/+76 two scores} — refresh +404/+408 (triggering
SetupClient when the client window differs), flags +413/+414/+412, then
`cWinDeltaMeter::PumpSample(score)` (0x2751d0); any change → vtable+368
Invalidate.

### TSPaint composition highlights (0x29b918-0x29ba60)
Priority chain per row, drawing the buffer loaded by Init:
1. `if (this+414 == 0 && [TOC-27024])` → draw **kRelHeartDeep** buffer
2. `else if (this+413 == 0 && [TOC-27028])` → draw **kRelHeart** buffer
3. (symmetric earlier branch at 0x29b8b8-0x29b8c4 for **kRelSmiley** via [TOC-27020])
Draw shape: src rect {0,0,buf.w,buf.h} from buffer+28/+32; dst x = textX+1
(row rect +140 +1), dst y = rowTop(+128) + buf.w + 3. Row band is centered:
`x = [this+28] + ([this+124]-[this+116] + 40)/2` (0x29b4c4-0x29b4e8).
The number/band transform near 0x29b578 uses the 0x43300000 int→double idiom
against the float pools below.

### Sort buttons — cWinPeople::InitRelationshipSorts (0x28e910-0x28ee15)
`factory(id) → new cTSWinBtn(396) → SetState(0)`, toggle byte +355=1,
child of cWinPeople+260 container:
| art id | name | stored at (cWinPeople) |
|---|---|---|
| 4108 | kRelFamilySort | +404 |
| 4109 | kRelFriendSort | +408 |
| 4111 | kRelFamousSort | +412 |
| 4110 | kRelAllSort | +416 |
Positions come from the RT buffers (vtable+420 SetArea(buf,4,1)) — no code
coordinates.

### BuildRelationships (0x289880-0x28a4e1)
Enumerates persons (0x243ea0/0x242360 pairs), creates **TWO** cWinRelationship
objects (0x2899b4, 0x289a68 — one per relationship direction), calls
SetRelationship 0x29a5c0, then string composition per row.
`MakeRelationshipBtnImage(person, ImageHandle&)` 0x23ccc0-0x23cf9d: 100-byte
image, **40x40 portrait** (r6=r7=40 into 0x23b8a0), constants 200/40/4/16 and
RGB-ish 45/122/125/76 nearby.

### TOC constants resolved
* [TOC-20280] → sec1+0x58b40 = string fragments `" ( )  -- \n\n %d"` — the
  relationship row text format family (real data).
* [TOC-20276] → code-rel file 0x5a4674 = float pool {-100.0, 40.0, 0.5, 255.0}.
* [TOC-20272] → code-rel file 0x5a4688 = double pool {0.5, 0.0, 0x43300000 magic}.
* [TOC-20288], [TOC-27020/-27024/-27028], [TOC-29180/-29192] = BSS (runtime).

---

## 2. The INVENTORY window (cWinGift) — EXPANSION-ERA, flagged

There is no `cWinInventory`; the inventory tab is the **gift token system**:

* **Data model**: `SimInventory` (0x400760-0x40365d, `__sinit_:Inventory_cpp`
  0x403600) — token vector with types; `Neighborhood::GetInventoryByNeighbourID`
  0xac680; `cXObject::TryInventoryAction` 0xebcc0 (BHAV primitive);
  `CatManageInventoryParam` 0x167690 (catalog string).
* **Window class**: `cWinGift` cluster 0x407e30-0x408591 (+ std::sort helpers
  0x292480-0x292bf5 using `cWinGift_LessThan` 0x2891b0):
  | method | body |
  |---|---|
  | TSOnMouseDownL / TSPaint / SetupClient / Shutdown / Init / dt / ct | 0x407e30 / 0x407e90 / 0x4081f0 / 0x408270 / 0x4082c0 / 0x4083b0 / 0x408460 |
* **cWinGift ctor** (0x408460, 420 B, `cWinGift(cWinLivePopupClient&, long)`):
  `factory(kItemUnknown 598, ...)` when the item icon pointer is null — the
  placeholder inventory icon. No other art.
* **cWinGift::Init** (0x4082c0): enlarges by **(+42, +60)**; gadget at +404
  (0x504ad0 = `cTSWin::SetToolTipsText`).
* **cWinGift::TSPaint** (0x407e90): draws item name (string via [TOC-27868])
  and count (`lha this+400` — S16 count), +42 right margin.
* **cWinPeople::BuildGifts** (0x2887d0-0x289191): reads neighbour inventory,
  5x `CountTokensOfType`, creates **three** cWinGift windows (0x408460 ctor
  at 0x2889c4/0x2889ae0/0x288c24) — one per sort — then std::sort.
* **Sort buttons** — `cWinPeople::InitInventorySorts` (0x28e2b0-0x28e8dd),
  same cTSWinBtn pattern, GetStringConstant(9) + `LoadUIStrings(241, ...)`:
  | art id | name | stored at |
  |---|---|---|
  | 4112 | kInventoryGiftSort | +424 |
  | 4113 | kInventoryMagicSort | +420 |
  | 4114 | kInventoryOtherSort | +428 |
  String table id 241 (via LoadUIStrings 0x25f440), constant 9 via
  GetStringConstant 0x87f60.
* **Tab strip entry**: **kGiftBtn 4507** is the 7th tab (see item 4 table).
* **ERA FLAG**: gift tokens + the gift tab are expansion content (Downtown/Hot
  Date gift system; kInventoryMagicSort 4113 is Makin' Magic). In this Complete
  binary `InitInventorySorts` is called unconditionally from cWinPeople::Init
  (0x28fed0, no gate). The port's base-game parity target should treat the
  inventory tab as Complete-era, like the Magic sort.

---

## 3. cWinSubpanelReportCard — children's school report

Cluster 0x2a6180-0x2a6d1d (`__sinit_:WinSubpanelReportCard_cpp` 0x2a6cc0):

| method | body |
|---|---|
| InvalidateSelf / SetQuickTips | 0x2a6180 / 0x2a6230 |
| TSOnCommand | 0x2a62c0-0x2a639d |
| TSPaint | 0x2a63d0-0x2a65cd |
| Shutdown / Init / dt / ct | 0x2a66c0 / 0x2a6780-0x2a6af1 / 0x2a6b20 / 0x2a6bf0 (`ct(cWinPeople*, cWinSubpanelJob*)`) |

* **Init** (0x2a6780): `LoadUIStrings(139, [TOC-20104] file)` (string table
  **139** — same table cWinArch uses); builds 4 grade-caption strings
  (StringSetIter pairs); a 420-B subpanel window (cTSWinBtn-family ctor
  0x50d720); SetFont via 0x50c610; **NO art factory calls of its own** — it
  borrows the Job subpanel's popup art through
  `cWinSubpanelJob::GetJobPopupComposite(i, cTSRect&, ImageHandle&)` 0x2a2a40
  and mounts it via `cWinLivePopupClient::SetBitmap(int, cTSBuffer*, cTSRect&)`
  0x279e90. Rect math via cTSRect::Width 0x261c80 / Height 0x266720,
  cTSWin::GetH/GetW 0x21a5b0/0x21a5f0.
* **TSPaint** (0x2a63d0): grade lookup helper 0x5a7c0 with the **10 → A+**
  special case (`cmpwi r30, 10` @0x2a64d0); popup client at this+216; quicktip
  toggle byte +413; flash values **(333, 333)** passed to 0x51c500/0x51c160
  (show/hide helpers) — 333 is engine-literal (color/tick constant,
  semantics unlabeled); grade text drawn by local 0x2a6600 at this+84.
* **TSOnCommand** (0x2a62c0): pure popup machinery —
  `cWinLivePopup::EnablePopup(client, win)` 0x278ee0 /
  `SetClient` 0x278d30, then base `cTSWin::TSOnCommand` 0x504bf0.
* Port note: report card = a **live-popup overlay on the Job subpanel** with
  Job composite art + STR 139 text — no dedicated bitmap family exists.

---

## 4. The PEOPLE PIE — construction, radius, slot layout

Built in **cWinPeople::Init** (0x28ee40-0x291829), block 0x28eee0-0x28f244:

```
factory(kLiveModeGauge 4911, this+548, 4)
this+208 = 0; factory(kPieFaceBkg 800, this+208, 4)      ; PieFace1.bmp 201x201
0x48e380(buf,255,0,255)                                   ; mask/color-key setup
font r14 = 0x512dd0(...)+vtable+16 (type 1)
pie  = new cTSPieMenu (396 B, ctor 0x52a4f0) -> this+212  ; GetPeoplePie 0x28b3d0
sub  = new cTSPieMenu -> this+216                         ; GetPeopleSubPie 0x28b370
; identical config for BOTH:
SetFont(r14)                    vtable+572
field+100 = color {0,40,140}                 (dark blue)
SetSelectedColor  {255,255,255}              vtable+580
SetHighlightColor {165,195,214}              vtable+576
SetLowlightColor  {0,255,255}                vtable+584
SetMaxRadius(150)                            vtable+488   <- @0x28f0ac / 0x28f1b4
SetInactiveRadius(35)                        vtable+476   <- @0x28f0d4 / 0x28f1cc
pie+80 = cWinPeople (back-pointer)
pie+372 = 60; pie+376 = 30                   (two literal slots)
SetPopupBackgroundBuffer(this+208, 0)        vtable+560   <- PieFace1 background
SetPerson(this, -1)                          0x28b420
```

* The four pie colors are built by vtable+36/68 MakeColor calls with literal
  RGB components — engine canon.
* **Radius 150 confirmed** at both 0x28f0ac (main) and 0x28f1b4 (sub-pie);
  **inactive radius 35** (the dead disc where the portrait sits).
* The interaction pie (cDDDSimsView::Init, r142) uses radius 90 with
  ViewMenuBackground — the people pie is a distinct surface as suspected.
* Portrait slot layout: the family portrait buttons are **8** cTSWinBtn
  (396 B) in the loop 0x28f280-0x2b4 (bound `cmpwi 8` @0x28f340) stored at
  `this->240[0..7]`; each gets +236/+240=1, vtable+156, AddChild(this).
  The pie surfaces reference these portrait gadgets as slices (TrackPerson
  0x28bf00 mounts them, per r132).
* **The People-panel tab strip** (also in Init, loop 0x28f38c-0x28f468, 7
  iterations) — art ids from table **[TOC-20412]** (V=0x59b708, read code-rel
  file 0x5a4598 = clean data, engine canon):

  | i | id | name | point ([TOC-20416] table, written by `__sinit_:WinPeople_cpp` 0x292c70) |
  |---|---|---|---|
  | 0 | 4500 | kMoodBtn | (202, 81) |
  | 1 | 4504 | kRelationshipBtn | (269, 54) |
  | 2 | 4503 | kJobBtn | (269, 85) |
  | 3 | 4502 | kHouseBtn | (269, 116) |
  | 4 | 4501 | kPersonalityBtn | (237, 85) |
  | 5 | 4505 | kInterestBtn | (237, 54) |
  | 6 | 4507 | kGiftBtn | (237, 116) |

  (points traced register-exact from the sinit stores at 0x292e70-0x292f20;
  values r3=202, r28=81/237, r4=269, r0=54, r30=85, r29=116.)
  The same sinit also writes: [TOC-20400] = {205, 123}; [TOC-20344] rect
  {0, 50, 280, 150}; [TOC-20340] rect {0, 0, 280, 150}; [TOC-20356] {10, 5};
  [TOC-20352] {300, ...}; personality anchors {12,72,38,96}, {108,61,167,93},
  {108,38,167,61}, {108,70,167,9} into [TOC-20436..-20420]; {13,16} sizes
  into [TOC-20464/-20468/-20472]; a 16-word constant table into [TOC-20424]
  (packed 0x85E00942-style words — attribution open).
* Other art in cWinPeople::Init: 4506 kGreenbars / 4510 kRedBars (motive bars),
  4601 kTrackingTarget (follow crosshair), 3750 kSocialPopupIcon, 100/101
  catalog pagers, plus a 132-B cWinLivePopupClient (ctor 0x27a770) at this+560.

---

## 5. kHelpBtn / kCameraBtn — what they DO

### Buttons (cWinViewControl::Init 0x2b5f30-0x2b7fc0)
Full UCP inventory loaded by Init (factory calls):
2037 kUniversalBkgTGA; 2023 kLevel1 / 2024 kLevel2 / 2025 kLevelRoof;
2012 kRotLeft / 2011 kRotRight / 2013 kZoomIn / 2014 kZoomOut; 2006 kNoCut /
2004 kDynaCut / 2007 kNoWall; 2010 kPeople / 2008 kObjects / 2003 kArch /
2009 kOptions; 2026 kPause; **kCameraBtn 2034 → this+352**;
2035 kCameraPatch; **kHelpBtn 4993** (0x2b7158) → button stored **this+288**,
toggle +355=1, +328=0, position offset from global **[TOC-20044]** {x,y}
(BSS). Plus 16 cTSWinBtn slots and 4 more factory calls whose ids are
table/computed (speed cluster).

### TSOnCommand (0x2b48c0-0x2b5460) — dispatches by SENDER gadget
(cmpwi cmd,1 gate at entry; then ~20 `cmpl sender, this+N` branches):
* Level/wall/mode buttons → `CPState::SetLevel` 0x210170 /
  `SetWallState` 0x20fe40 / `SetCPMode` 0x20c900 (with GetMode 0x211150 checks).
* sender == this+256 → **cWinBudgetDlg** (ctor 0x264d90, 312 B), guarded by
  `[TOC-30608 app]->28->+80 != 0` and `Neighborhood::GetZoningType` 0xaaa20;
  centered rect (avg/2 math 0x2b5094/98), dialog+80=this, then
  `SetBlockSimulator(view,true)` → `cSimsApp::DoSimsModalDialog(app,dlg,1,0)`
  0x253910 → `SetBlockSimulator(view,false)` → DestroyWindow 0x51dfa0.
* sender == this+424 → **cWinPictureDialog** (ctor 0x297a20, 320 B):
  GetStringConstant(9) + LoadUIStrings, `SetTitle` 0x2974b0, `AddText` 0x296ab0
  x2, `AddButton` 0x297230, same modal pause/resume pattern.
* **sender == this+288 (kHelpBtn) @0x2b5354**: creates (once) a
  **cWinHelp(cITSFont*)** (ctor 0x278470, 384 B) cached at **this+448**,
  title string from **[TOC-29048]+48**; `cWinHelp::Init` (vtable+20);
  `SetSelected(GetSelected())` preserves last help page (0x2777c0/0x277820);
  `SetBlockSimulator(true)` → `DoSimsModalDialog` → `SetBlockSimulator(false)`;
  stores `GetSelected()` back to this+448; finally `cTSWinBtn::SetState(0)`
  (momentary button). **kHelpBtn = opens the modal help browser, pausing the
  sim, remembering the last page.**
* The **camera button (this+352)** itself has no creation branch here —
  `cWinCamPanel` (ctor 0x267310, 288 B, `ct(CPState*, cTSBuffer*)`) is
  constructed ONLY inside **cWinCPanel::Init** (call site 0x270914, args from
  cWinCPanel+244/+252, stored +236). The view-control camera button toggles
  CP state; the panel lifecycle belongs to cWinCPanel (0x270170-0x270c89) —
  its TSOnCommand (0x266310) + SetupPopup (0x266190) + SetScrapbookOpen
  (0x265ce0) are the camera/scrapbook surface. Honest limit: the exact
  sender-match for the camera click inside cWinCPanel was not traced this
  round (it lives in cWinCPanel::TSOnCommand, not decoded).

---

## 6. cWinArch::Init — the build-mode toolbar row (R142 residual CLOSED)

`cWinArch::Init` 0x261f40-0x2628c0:

* Base rect fields {this+344..356} initialized {0,0,352,100} — the arch
  toolbar row occupies **352 x 100**.
* `factory(kToolBtnUndo 64)` and `factory(kToolBtnRedo 65)` — cTSWinBtn
  (396 B) children (undo at this+220).
* **kBuildToothPick1 4912 → this+372, kBuildToothPick2 4913 → this+376**
  (the build plaque pointer picks).
* `factory(kCatalogPrevPage 100)` / `factory(kCatalogNextPage 101)` — pager.
* GetStringConstant(9) + `LoadUIStrings(139, ...)` — tooltips + captions.
* **THE 12-BUTTON BUILD TOOL ROW** (loop 0x26238c-0x26243c, bound
  `cmpwi r25, 12`): ids read from table **[TOC-27380]** = sec1+0x55290
  (REAL sec1 data, engine canon), one cTSWinBtn per id stored at
  **this+236+4*i**, each with SetToolTipsText (0x504ad0):

  | i | id | name |
  |---|---|---|
  | 0 | 66 | kToolBtnTerrain |
  | 1 | 68 | kToolBtnPool |
  | 2 | 70 | kToolBtnWall |
  | 3 | 73 | kToolBtnWallpaper |
  | 4 | 74 | kToolBtnStairs |
  | 5 | 75 | kToolBtnFireplace |
  | 6 | 67 | kToolBtnTree |
  | 7 | 69 | kToolBtnFloor |
  | 8 | 71 | kToolBtnDoor |
  | 9 | 72 | kToolBtnWindow |
  | 10 | 76 | kToolBtnRoof |
  | 11 | 77 | kToolBtnHand |

  Button geometry is carried by the RT buffers (vtable+420 SetArea(buf,4,1));
  the engine contributes the ORDER and the loop count only.
* Trailing geometry: `cTSPoint(338, 5)` (0x260b60) + a `TSWinMoveTo` cluster;
  rect fields {this+296..324} = {0,·,45,45,·,100,45,45} and
  {this+344..356} = {318, 5, 318, 100} — pager/undo cluster anchors.
* A 296-B alloc at 0x26264c (cWinCamPanel-sized — unconfirmed identity).

---

## Honest residuals (what resisted static decode)

1. **BSS gates/anchors** (proven runtime-only, r135 signature — code-rel read
   lands mid-instruction): [TOC-27020/-27024/-27028] (smiley/heart/deep-heart
   gates + sort singletons), [TOC-20288] (delta-meter anchor point),
   [TOC-20044] (help button anchor), [TOC-29180/-29192]. The INITIALIZERS that
   fill them run before main (their write sites use non-TOC addressing).
2. **People tab points table** — recovered via the __sinit literal trace
   (done); the analogous personality anchor constants {12,72,38,96} etc. are
   dumped but their semantic frame (window-relative vs pie-relative) is
   unlabeled.
3. **cWinCPanel::TSOnCommand** (camera click routing) and the speed-cluster
   factory ids (table/computed) — not traced this round.
4. ReportCard flash constants (333,333) and grade-lookup internals — literals
   pinned, semantics unlabeled.
5. The 16 packed words written to [TOC-20424] (0x85E00942 family) — likely
   colors/fixed-point; attribution open.
6. cWinGift SetupClient string composition (0x4081f0) — boilerplate
   StringSetIter, not itemized.

## Regenerated artifacts (in /tmp, per licensing hygiene)

* /tmp/r159-sec1-unpacked.bin — sec1 unpacked (0x7bf80 exact).
* /tmp/r159-*.txt — raw ppc_decode dumps for every function cited above.
