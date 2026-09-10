# R142 — Dialog / Message-Box / Non-CPanel UI Chrome Canon

Binary: `game-data/The Sims/The Sims Complete` (PPC, file offsets; symbol addresses are
Metrowerks-style +2 — **always decode from `symaddr - 2`**). Art: `UIGraphics/UIGraphics.far`
(794 members; manifest: u32@12 → count u32@manifest, entries {len, d2, off, namelen, name},
names contain literal backslashes, case-insensitive lookup).

---

## 1. RT resource-script inventory (the whole FAR)

`UIGraphics.far` contains exactly **4 RT/.h pairs** plus one shared include. Identical loose
copies sit in `game-data/The Sims/UIGraphics/` next to the FAR (verified byte-equal).

| Pair | Size RT/h | Governs |
|---|---|---|
| `SMCtrlMgrRes.RT/.h` | 779 / 804 | **System control-manager chrome: ALL dialogs, buttons, checkboxes, sliders, scrollbars, list boxes, PIE MENUS, close/minimize boxes** |
| `Res_Other.RT/.h` | 1422 / 506 | Misc in-game UI: speech balloons, logos, queue-cancel overlay, tragedy mask |
| `Res_Nbhd.RT/.h` | 12863 / 8530 | Neighborhood screen + all sub-neighborhoods (Downtown, Vacation, Studiotown, Magictown), family/character-creation screens, lot popups |
| `Res_CPanel.RT/.h` | 27327 / 15800 | Live/BUY/BUILD control panel (r141's domain) |
| `Res_Languages.h` | 1243 | shared include: kRes_Language* 0..20 |

Extracted copies + all referenced art (minus cpanel\*, fonts\*) are in `r142/art/`
(349 files). Full machine-readable inventory with dims: `r142/rt-inventory.txt`.

FAR directory census: cpanel 393, Shared 73, Nbhd 66, Fonts 52, Magicland 43, Studiotown 40,
NghUI 37, Other 22, Community 21, Downtown 20, VIsland 18.

**Dead references** — RT entries whose art exists in NO .far in the install (verified by
scanning every FAR manifest): `CPanel/Backgrounds/{BuyBack,OptionsBack,CamBack,DisposeBack,BuildBack,LiveBack}.bmp`,
`CPanel/Buttons/IconTemp.bmp`, `Other/MaxisLogo.bmp` (kMaxisLogo=9002), `Nbhd/SimEstates.bmp`
(kPickAFamily=5015), `Nbhd/Marquee.bmp` (kMarquee=5016). Port must treat these ids as
"art missing" and follow the engine's fallback path.

---

## 2. SMCtrlMgrRes — the system chrome table (THE dialog canon)

Header comment literally says "Someone's resources". Parsed by `cGZResMgrRTParser`.
All entries are `{BITMAP,id,"shared/...bmp"}` — plain bitmaps (not RLE), loaded with
colortype 4 (`cTSBufferColorType` value 4).

| id | const | art | dims | sheet math |
|---:|---|---|---|---|
| 17 | kSMSystemBtn | shared/sys/WinBtn.bmp | 260x33 | 4 button states of 65x33 (normal/hilite/pressed/disabled) |
| 18 | kSMSystemCheck | shared/sys/WinChk.bmp | 168x28 | 6 cells of 28x28 (2 check states x 3 widget states) |
| 19 | kSMSystemSliderH | shared/sys/WinSlH.bmp | 48x12 | horizontal slider thumb+track |
| 20 | kSMSystemSliderV | shared/sys/WinSlV.bmp | 12x48 | vertical |
| 21 | kSMSystemScrollbar | shared/sys/WinScrol.bmp | 204x20 | scrollbar arrow/page strips |
| 22 | kSMSystemGenDlgEdge | shared/dlg/GenDlg.bmp | **30x30** | nine-slice tile for EVERY generic dialog frame |
| 24 | kSMSystemComboDown | shared/sys/DropDown.bmp | 16x16 | combobox arrow |
| 26 | kSMSystemListBackground | shared/sys/ListBack.BMP | 136x22 | list box row background |
| 27 | kSMSystemListLineItemBackground | shared/sys/ListBack.BMP | 136x22 | same art, second id |
| 28 | kSMSystemPieButton | shared/sys/PieButt.bmp | 17x17 | pie menu disc tile |
| 29 | kSMSystemPieBackground | shared/sys/PieBack.bmp | 195x195 | pie menu backdrop (see §5) |
| 30 | kSMSystemDlgCloseBox | shared/sys/CloseBox.bmp | 48x11 | 4 states of 12x11 |
| 31 | kSMSystemDlgMinimizeBox | shared/sys/MinimizeBox.bmp | 48x11 | 4 states of 12x11 |

Extra chrome present in `Shared\Sys\` but NOT RT-referenced (legacy/alt, port optional):
WinClose.bmp 1734 B, WinlbEdg.BMP 198 B, WinRadioBtn.BMP 2408 B, CheckBtnNew.BMP 14168 B,
ListBack136.bmp 9030 B, ListToggle.bmp 2958 B, TogglePushBtn.bmp 26046 B, FLDRBTNS.BMP 4662 B,
SPINBTNS.BMP 1782 B; `Shared\Dlg\LBoxTile.bmp` 23790 B.

### 2.1 cTSWinCtrlMgr slot table (from `cSimsApp::SetupWinCtrlMgr` @ **0x259570**, 6596 B)

The engine does NOT look ids up by RT id; `SetupWinCtrlMgr` loads each via
`nsSMResFac::LoadBuffer(rtId, &buf, colortype=4)` (0x3b6190) and stores it into
`cTSWinCtrlMgr::SetSystemBMP(slot, buf, false)` (vtable+28):

| ctrl-mgr slot | RT id | art |
|---:|---:|---|
| 0 | 17 | WinBtn (system push button) |
| 1 | 18 | WinChk |
| 2 | 19 | WinSlH |
| 3 | 20 | WinSlV |
| 4 | 21 | WinScrol |
| 5 | 3008 | kCatalogPopupBackTiles (CPanel popup tiles — the ONLY non-SM id) |
| 9 | 22 | **GenDlg tile — fetched by cTSWinGenDlg::Init** |
| 11 | 24 | DropDown |
| 13 | 27 | ListBack (line-item) |
| 14 | 26 | ListBack (background) |
| 15 | 28 | **PieButt — default cTSPieMenu buffer** |
| 16 | 29 | PieBack |

`SetupWinCtrlMgr` also registers **46 cursors** via `cTSCursorManager::AddCursor` (0x497160)
from `UIGraphics/shared/cursors/*.cur` with explicit ids (in registration order):
0, 3, 2, 4, 6, 7, 8, 10, 11, 9, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25,
26, 27, 29, 28, 42, 43, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 44, 45, 46, 47.
The 53 .cur files in the FAR include 7 not registered here (arrow/hourglass/edit/nodrop/
help/hand/finger are the first four TOC blocks' likely names — exact id↔file pairing is a
residual, see §7). First two registrations are preceded by RemoveCursor calls (ids 1 and 5
are never re-added — reserved/unused).

`SetupWinCtrlMgr` also sets default control colors via `MakeColor` (0x505780) +
`SetDefaultColor(slot, color)`; the RGB sequence observed (misaligned extraction, values
reliable, slot pairing partial): (166,180,180), (255,0,255), (189,188,188), (255,180,180),
(189,188,255), (255,255,188), (0,0,255), (128,0,0), (0,128,0), (128,128,0), (0,0,64),
(128,0,128), (0,128,128), (128,128,128), (192,192,128), (255,0,192), (0,255,0),
(255,255,0), (0,0,0), (0,255,255), (255,255,255).

### 2.2 cTSWinGenDlg — the generic dialog window (GenDlg consumer)

Key symbols (addr = file offset of function start):

| function | addr | size |
|---|---|---|
| cTSWinGenDlg::__ct(cITSWinGenDlg::tWindowBackgroundStyle) | 0x51a910 | 124 |
| cTSWinGenDlg::Init | 0x51a5e0 | 428 |
| cTSWinGenDlg::GetBorderWidths | 0x212580 | 36 |
| cTSWinGenDlg::AddText(rect, string, ulong, bool) | 0x519610 | 388 |
| cTSWinGenDlg::AddCheck(rect, string) | 0x519520 | 160 |
| cTSWinGenDlg::AddSlider / Scrollbar | 0x519970 / 0x5197e0 | 332 each |
| cTSWinGenDlg::SetBackground(cTSBuffer*) | 0x519be0 | 120 |
| cTSWinGenDlg::Shutdown | 0x51a480 | 296 |

Init behavior: if a background style is set and this+220 (background buffer) is null, it
fetches **`cTSFrameWork_CtrlMgr()->SystemBMP(9)`** = the GenDlg 30x30 tile; copies four
border widths from a TOC global into this+228..240 (the nine-slice insets — values live in
a data global not statically resolvable; GetBorderWidths just returns them). Text color
fallback RGB(255,255,255) at 0x51a710.

---

## 3. Message box / alert canon

### 3.1 Entry point — `cSimsApp::MessageDialog`

Two overloads:
- `MessageDialog(const cTSString& title, const cTSString& msg, unsigned long flags, bool)` @ **0x253cf0** (444 B) — decoded fully.
- `MessageDialog(const char*, const char*, ulong, bool)` @ 0x253f00 (164 B) — wraps the above.

Flow (0x253cf0):
1. `operator new(276)` → `sizeof(cTSWinMsgBox) == 276`.
2. `cTSWinMsgBox::__ct(title, msg, flags, Timeout, bool)` @ **0x524b30** with
   **Timeout = 40000 ms** (r8 = 0x10000-25536 = 40000; enum param `cTSWinMsgBox::Timeout`).
3. `SetMainFillColor(r, g, b)` @ 0x523ed0 with bytes from `cSimsApp::GetDialogFillColor`
   @ **0x253fe0** which returns hard-coded **RGB(0, 0, 82)** (deep blue dialog fill).
4. `SetOverlapsScrollArea(true)` (0x503820), then the modal run virtuals.

### 3.2 `cTSWinMsgBox` class (window base cTSWinDlg @ 0x5193f0)

| function | addr | size |
|---|---|---|
| __ct(title, msg, ulong flags, Timeout, ulong) | 0x524b30 | 344 |
| Init | **0x524150** | 2304 |
| PositionButtons | **0x5234b0** | 532 |
| SetMainFillColor(uc,uc,uc) | 0x523ed0 | 348 |
| TSBeginModal / TSEndModal | 0x523780 / 0x523700 | 148/72 |
| TSOnCommand / TSOnCharacter / TSOnKeyDown / TSOnTimerMsg | 0x523970 / 0x523a40 / 0x523bd0 / 0x523850 | |
| Set/GetKeyboardShortcut, Get/SetButtonLabel | 0x524cf0 / 0x524d50 / 0x524dc0 / 0x524e20 | |
| Shutdown / __dt | 0x524070 / 0x524a80 | |

Object layout (offsets from PositionButtons/Init):
- +0xD4 (212): flags ulong; **low 16 bits select button count** (switch at 0x523544:
  value 3 → 3 buttons at +0xE8/+0xEC/+0xF0; other paths 1 or 2 buttons — branch senses
  in that window are ambiguous due to the decoder's eq/ne bug, verify at runtime).
- +0xE8/0xEC/0xF0 (232/236/240): button pointers (cTSWinBtn).
- own window rect at +116/+120/+124/+128 (l/t/r/b).
- +248: title static, +252: message static, +256: third gadget.

**PositionButtons algorithm** (0x5234b0):
- maxW = max over buttons of (btn.right - btn.left) (btn rect at +116/+124).
- n = button count; buttons array on stack.
- spacing = (dlgWidth - 32 - n*maxW) / (n+1), **clamped to minimum 16** (cmpwi/branch @ 0x5235d8).
- Buttons placed left-to-right starting x = 16+spacing (first button left edge), each
  advanced by (spacing + maxW); vertical: bottom-aligned against the box bottom with a
  32 px inset (dialog height used as `right-left-32` work area; per-button centered via
  `cTSRect::Height` 0x266720 and moved through the window vtable+104 SetRect virtual).
- Window width auto-sizing in Init: content height + 32, minimum width 200 (0x5242ac).

Message-box buttons are cTSWinBtn using system slot 0 art (WinBtn.bmp 260x33 = 4x65x33
states). Dialog frame = GenDlg nine-slice (slot 9). Fill = RGB(0,0,82).

### 3.3 `ObjectDialog` — the BHAV "dialog" primitive (classic Sim messages)

| function | addr | size |
|---|---|---|
| ObjectDialog::SetupDialog | **0xcc5d0** | 1092 |
| ObjectDialog::SetParams(StackElem*, DialogParam*) | 0xcca50 | 3916 |
| ObjectDialog::BtnPressed(int) | 0xcc220 | 360 |
| ObjectDialog::Show / ResultProcessed / GetString | 0xcc4a0 / 0xcc1c0 / 0xcd9f0 | |
| ObjectModule::EnqueueObjectDialog | 0xe3710 / 0xe3800 | |

SetupDialog builds a **cWinPictureDialog** (this+64 holds it):
1. Fill color again from `GetDialogFillColor` → dialog+100 = RGB(0,0,82).
2. If param byte +61: `SetTitle(cTSString@this+8)` (0x2974b0).
3. Icon: string@+36; if its measured rect is empty → `SetImage(buffer, point)` (0x296b60)
   else `SetImage(buffer, bool)` (0x296c20) — the dialog icon/portrait.
4. If param byte +57 (close-box mode): byte +60 selects `AddCloseBox(2, 31)` (0x296f20 —
   two-arg) vs `AddCloseBox(2)` (0x297030).
   else: `AddButton(cTSString@this+12, id=2)` (0x297230) — the OK button.
5. Param +32 (dialog style): paths add `AddButton(str@+16, id=3)` and `AddButton(str@+20, ...)`.
So Sim-message dialogs have up to 2 buttons with gadget ids 2 and 3 plus optional close box id 2.

### 3.4 cWinPictureDialog family (picture dialogs)

Class methods @ 0x295d50+: DoLayout (0x295d50, 1276 B — all rects computed relative to the
GenDlg border insets via window vtable+104 SetRect; no magic constants beyond computed
margins), Init 0x2977c0, AddButton 0x297230, AddEditBox 0x297090, AddCloseBox 0x296f20/0x297030,
SetTitle 0x2974b0, SetImage 0x296b60/0x296c20, SetKey 0x296cd0, SetPositioning 0x297620,
TSBeginModal 0x2962b0 (1980 B). Sibling classes: `cWinRadioDialogBox` (Init 0x45aed0,
PositionButtons 0x459d50), `cWinProgressPictureDialog` (Init 0x4056f0), `cWinPictureSplashDialog`.
Neighborhood transit dialogs: cSimsApp::ShowNbhdDialog 0x254e00, ShowDowntownDialog 0x254bb0,
ShowVacationDialog 0x254960, ShowStudiotownDialog 0x254710, ShowMagiclandDialog 0x2544c0,
DoSimsModalDialog 0x253910, CreateTaxiDialog 0x24bb50.

---

## 4. Res_Other — misc in-game UI

| id | const | art | dims | notes |
|---:|---|---|---|---|
| 9000 | kSpeechMediumBmp | Other/SpeechMedium.bmp | 16x16 | speech balloon template (heads) |
| 9001 | kSpeechLargeBmp | Other/SpeechLarge.bmp | 32x32 | large balloon |
| 9002 | kMaxisLogo | Other/MaxisLogo.bmp | — | **MISSING from all FARs** |
| 9003 | kSimsLogo | Other/setup{,uk,fr,de,du,sw,it,jp,th,tc,sp,pg,sc,po,ko,dn,no,fi}.bmp | 800x600 each | 18 language title screens |
| 9004 | kQueueCancelOverlay | Other/QueueCancel.bmp | 45x45 | red X over queue icons |
| 9005 | kTragedyUnhappyMask | Other/TragedyMask.bmp | 133x103 | death/unhappy overlay mask |

---

## 5. Pie menu canon

Class `cTSPieMenu` (0x526c10–0x52a634) + `PieMenuItem` (dtor 0x5284d0) + `PopupHead`
(the animated Sim head in the pie center: Start/StartSubmenu @ 0x11b130/0x11b040,
Render 0x119a10). Consumers: `cDDDSimsView::CancelPieMenu` 0x2125e0,
`cObjPickerTool::{MenuItemChanged,MenuItemSelected,DoMenu}` 0x1839f0/0x183eb0/0x184080,
`SAnimator::StartPopupHead*` 0x362940/0x362a10.

### 5.1 Field map (setters decoded, offsets in object)

| off | field (setter addr) | default (ctor 0x52a4f0) |
|---:|---|---|
| +100 | label color (written by Init + game) | 0 → Init: RGB(118,118,118) |
| +208 | lowlight color (0x529e50) | Init: RGB(185,185,208) |
| +212 | highlight color (0x529e10) | Init: RGB(0,255,255) |
| +216 | selected color (0x529dd0) | Init: RGB(255,255,255) |
| +220/+224 | title icon buffer / owns (SetTitleIcon 0x529e90) | — |
| +228/+232 | **popup background buffer / owns** (SetPopupBackgroundBuffer 0x529f50) | Init: SystemBMP(15) = PieButt |
| +260/+264 | background w/h (from buffer+28/+32) | — |
| +300/+304/+308/+312 | center/anchor points | — |
| +328 | slice count (set by Layout quantizer) | 0 |
| +332 | first direction (SetFirstDirection 0x529d90) | 0 |
| +336 | inactive radius (SetInactiveRadius 0x527410) | 0 |
| +340/+344 | float thresholds used by CalcItem | TOC float |
| +348 | spoke radius (SetSpokeRadius 0x529d50) | **8** |
| +352 | abbreviated (SetAbbreviated 0x529d10) | **1** |
| +356 | **max radius (SetMaxRadius 0x529c90)** | 0 |
| +360 | auto cancel (0x529c10) | 0 |
| +364 | click sounds (0x529b90) | 1 |
| +372/+376 | geometry params (unnamed; see below) | 40 / 20 |
| +384/+388 | item array / item count | — |

### 5.2 The two shipped pies

**Main interaction pie** — built in `cDDDSimsView::Init` @ 0x218b70 (ctor call @ 0x218df0):
- Background: `SetPopupBackgroundBuffer(LoadBuffer(825), false)` — RT id **825 =
  kViewMenuBackground = `CPanel/ViewMenuBackground.bmp`, 17x17** (a disc tile, scaled).
- vtable+488 arg **90** (outer/max radius 90 px).
- vtable+476 arg 6; direct stores +372 = 6, +376 = 0.
- Label color store: +100 = RGB(187,187,187).
- Title-button inserted via string literal + vtable+432.

**People-panel pie + sub-pie** — built in `cWinPeople::Init` @ 0x28ee40 (ctors @ 0x28f038,
0x28f140):
- Background: `SetPopupBackgroundBuffer(cWinPeople+208)` where +208 = `LoadBuffer(800)` =
  RT **800 = kPieFaceBkg = `CPanel/PieFace1.bmp`, 201x201** (the Sim-face pie backdrop).
- vtable+488 arg **150** (radius 150 px); vtable+476 arg 35; +372 = 60, +376 = 30.
- GetPeoplePie 0x28b3d0 / GetPeopleSubPie 0x28b370 / GetPieBackBuffer 0x28b310.

### 5.3 Geometry laws

- **CalcItem(x,y)** @ 0x527590: classic radial hit test — dx/dy from popup center (+284/+288),
  dist = sqrt(dx²+dy²) (helper 0x5a1b78), rejected beyond the float threshold (+340/+344 and
  TOC float consts), then atan2 (0x5a1d40) → item index = (angle − firstDirection(+332))
  scaled by slice count (+328) — index cross-checked against per-item rects
  (PieMenuItem +16/+20/+24/+28 = l/t/w/h) and the inactive radius (+336).
- **Layout** @ 0x5290c0: centers popup on its background buffer size; measures the title
  label through the font vtable+88 with ±3 px insets; then the **slice-count quantizer**
  @ 0x5292b8 (branch senses corrected from raw opcodes):
  - count == 0 → slices = 1
  - 1 ≤ count ≤ 2 → slices = count + 2
  - 3 ≤ count ≤ 4 → slices = count + 4
  - count ≥ 5 → slices = count + 8
  (the +N guarantee keeps item 0 on the first-direction axis with symmetric neighbors).
- Other layout/paint: ReLayout 0x529000, TSPaint 0x5288b0 (720 B), DrawFramedLabel
  0x528bb0, DrawLabelFrame 0x528d10 (696 B), InsertString 0x5283f0, SetSelection 0x527a70.
- `PieBack.bmp` (RT 29, 195x195) IS loaded into ctrl-mgr slot 16 but none of the three
  shipped cTSPieMenu instances fetches slot 16 statically — likely a Mac-build leftover or
  fetched via an untraceable virtual path; treat as available chrome, not canon-critical.

---

## 6. Neighborhood screen chrome (Res_Nbhd)

Full table in `r142/rt-inventory.txt` (ids 5000-6338). Highlights (all RLEBMP unless noted):

**Base screens**: kNbhdBck 5000 `Nbhd/NScreen.BMP` 800x600 (US) / kNbhdBckNonUS 5308
`NScreen_Loc.bmp` (EU/Asia); wave deltas 5302-5304 + non-US 5309-5311
(`Nbhd/DiffN1-N{2,3,4}_8{,_Loc_8}.bmp`). Unleashed community: kNbhdBck_UL 6000
`Community/NScreen_unleashed{,_nonus}.bmp` 800x600 + 6 wave frames each
(6302-6304,6330,6331,6332 US; 6309-6311,6336-6338 non-US).

**Sub-neighborhood screens**: Downtown 5320 `Downtown/DScreen.bmp`, Studiotown 5321,
Magicland 5322 (all 800x600). Water animation: Downtown 5400-5405 (6 frames), Vacation
5410-5414 (5), Magictown 5720-5725 (6). Studiotown car animation 5660-5679 (20 frames,
odd/even = two directions). Magictown balloons 5701-5716 (16) + fog 5717-5719
(`Magicland/DScreen_mist{A,B,C}.tga`). Nessie cameo 5050-5052 (106x75 x3).

**Dialogs/popups**: kDTDlgBkg 5423 `Downtown/dlgframe.bmp` + kDTDlgBkg2 5432 `dlgframe2.bmp`
(downtown dialog frames), 1024x768 masks 5001/5002, lot-status icons 5070-5074 (133x103),
family-pick popup suite 5015-5043 (PickBkg 800x600, item bkg 690x204, scrollbar 300x30,
PAF buttons), hotspot popups 5101/5102 (200x166) + tiler 5107 (`HotSpotPopupTiler.tga` 111x111),
create-a-character full-screen 5042 800x600 + LED/buttons, design-family 5022 800x600.

**Navbar**: kNghBarBkg 5420 `NghUI/banner_neighborhood.bmp`, kDTBarBkg 5422
`banner_downtown.bmp`, localized Sims logos 5421/5427-5430. Toolbar buttons 212x52
(MoveIn 5200, Bulldoze 5201, Exit 5202, Import 5207, Rezone 5215, prev/next 5204/5205
104x52, current 5206 23x30). Vacation filter buttons 5060-5076 (200x52) + filter
toolbars 5045/5046 (800x72).

**Transit load screens** (800x600 + 1024x768 pairs): bus 5600/5601, taxi 5424/5425,
vacation 5500/5501 + 5502/5503, studiotown 5650/5651 + fan 5652/5653, magicland
5654/5655 + hole 5656/5657. Phone icons: dtphone 5426, viphone 5431, NeighborhoodPhone
5433, stphone 5434, mtphone 5435, mtclownhole 5436. Credits buttons 5307 + localized
5312-5316; Downtown/Studiotown credits 5210/5220 (`TheSimsLogo.bmp` 70x52).

---

## 7. Residuals / honest failures

1. **GenDlg nine-slice border-width values**: `cTSWinGenDlg::Init` copies 4 longs from a
   TOC-referenced global (`lwz r31, -17352(r2)`). The TOC base is runtime state; the values
   could not be recovered statically. Recover at runtime or from a Mac data-section dump.
2. **Cursor id → .cur file pairing**: names load through r2-TOC / r29-relative pools;
   no static pointer table exists (verified). The registration id order (§2.1) and the
   53-file set are exact; the pairing is provisional.
3. **cTSPieMenu vtable+476 / +488 / +504 / +560 / +572..596**: slot semantics inferred
   (+488 = SetMaxRadius-family, +560 = SetPopupBackgroundBuffer confirmed by behavior).
   No static vtable layout available to pin the rest.
4. **PieBack.bmp (slot 16) consumer** not statically traceable (all virtual dispatch).
5. **kSMSystemDlgCloseBox(30) / MinimizeBox(31)**: RT entries exist but SetupWinCtrlMgr
   never binds them; cWinPictureDialog::AddCloseBox may use different art paths.
6. **PositionButtons flags→button-count switch** (0x523544): branch-sense ambiguity in the
   decoder for that window; count is 1/2/3 from low 16 bits of flags, exact value mapping
   needs a runtime check.
7. **Default control color table** (§2.1): RGB values exact, slot pairing misaligned.
8. Fields +372/+376 of cTSPieMenu (values: default 40/20, interaction pie 6/0, people pie
   60/30) — purpose unidentified (likely label-frame / title geometry).

## 8. Files in this round

- `r142/dialog-chrome-law.md` — this document.
- `r142/art/` — 4 RT pairs + Res_Languages.h + 340 extracted art files (all surfaces
  except cpanel\* and fonts\*, which are r141/asset-repo territory).
- `r142/rt-inventory.txt` — machine-readable id→const→art→dims table for all 4 RT pairs.
- `r142/manifest.txt` — full FAR manifest (794 entries).
- Scripts: `far_manifest.py`, `extract_res.py`, `extract_art.py`, `dims_inventory.py`.
- Decode logs: `msgbox-init.txt`, `setupwinctrlmgr.txt`, `piemenu-layout.txt`,
  `piemenu-drawlabelframe.txt`.
