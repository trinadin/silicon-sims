# R142 — Main-game UI/UX round 2: dialogs, pie menu, bar composition on original law

Round mandate (user): "Continue with the next recommended course of action. I'd love to
flesh out UI/UX in its entirety. Utilize Hermes."

Three Hermes agents ran concurrently:
- **A** — TSUI template database decode → **PREMISE BUSTED, REAL LAW RECOVERED** (§5):
  0x591440 is MSL `operator new` (100/380/480 = sizeof cTSBuffer/cWinArch/cWinViewControl);
  the actual layout law is a hardcoded {x,y} anchor table written by layout-init
  **0x2b8230** and applied through cTSWinBtn::SetImage(art,cols,rows)+SetArea. The
  UCP's every button position is now ENGINE-EXACT (`tsui-template-law.md`,
  `tsui/ucp-layout.txt`) — and it CORRECTS R141's interpretation: wall row at y77-110
  (not the bottom recess), options at (193,125) bottom-right, camera diamond centered
  ~(35,127), and pause (15x15, 4x2 grid!) + kSpeed1/2/3 live ON THE PLATE at y=135 —
  the toolbar never carried them. Bonus: agent A also fixed a real ppc_decode.py bug
  (X-form `or` operand order), wrote a capstone decoder, and recovered the PEF
  packed-data format + loader relocations (18 CFM vtables + the TOC base).
- **B** — dialog/message/pie chrome canon → `dialog-chrome-law.md`, `rt-inventory.txt`,
  `art/` (4 RT pairs + 340 art files). The round's canon backbone.
- **C** — client artifact survey → `client-ui-survey.md` (P0: dialog family; P1: pie,
  queue icons, switcher chrome, relationship/inventory, CAS, neighborhood).

## Canon recovered this round (agent B; binary-verified)

### 1. SMCtrlMgrRes — the SYSTEM chrome table (all dialogs, buttons, pie menus)
`UIGraphics.far` holds exactly **4 RT/.h pairs**: `SMCtrlMgrRes` (system chrome),
`Res_Other`, `Res_Nbhd`, `Res_CPanel` (+shared `Res_Languages.h`). SMCtrlMgrRes maps:

| id | const | art | dims |
|----|-------|-----|------|
| 17 | kSMSystemBtn | shared\sys\WinBtn.bmp | 260x33 = 4x 65x33 states |
| 22 | kSMSystemGenDlgEdge | shared\dlg\GenDlg.bmp | 30x30 nine-slice (every generic dialog frame) |
| 28 | kSMSystemPieButton | shared\sys\PieButt.bmp | 17x17 pie disc tile |
| 29 | kSMSystemPieBackground | shared\sys\PieBack.bmp | 195x195 |
| 30/31 | kSMSystemDlgCloseBox/MinimizeBox | CloseBox/MinimizeBox.bmp | 48x11 = 4x 12x11 |

Full table (663 rows, all 4 pairs) in `rt-inventory.txt`; ctrl-mgr slot table decoded
from `cSimsApp::SetupWinCtrlMgr` @ 0x259570 (slots 0=WinBtn, 9=GenDlg, 15=PieButt,
16=PieBack + 46 cursor registrations).

### 2. Message-box law (fully decoded)
- Entry `cSimsApp::MessageDialog` @ 0x253cf0 → `cTSWinMsgBox` (ctor 0x524b30, 276 B),
  **default timeout 40000 ms**, fill from `GetDialogFillColor` @ 0x253fe0 =
  **hard-coded RGB(0,0,82)**.
- `Init` @ 0x524150: auto-size — content height + 32, **min width 200**.
- `PositionButtons` @ 0x5234b0: maxW = widest button; **spacing = (width − 32 − n·maxW)
  / (n+1), clamped ≥ 16**; first button at x = 16 + spacing; bottom-aligned,
  **32 px inset**. Buttons = cTSWinBtn on slot-0 WinBtn art (one state buffer
  stretched to the button rect).
- BHAV Sim-message dialogs: `ObjectDialog::SetupDialog` @ 0xcc5d0 → cWinPictureDialog
  (title + icon/SetImage + buttons gadget ids 2/3 + optional close box id 2), same
  RGB(0,0,82) fill.

### 3. Pie-menu law
- `cTSPieMenu` @ 0x526c10–0x52a634 field map + geometry decoded.
- **Interaction pie** (cDDDSimsView::Init @ 0x218df0): background = RT 825
  `cpanel\ViewMenuBackground.bmp` 17x17 (scaled disc), **max radius 90**, label color
  RGB(187,187,187), highlight RGB(0,255,255), selected RGB(255,255,255), lowlight
  RGB(185,185,208).
- **People pie** (cWinPeople::Init @ 0x28f038): `cpanel\PieFace1.bmp` 201x201, radius 150.
- **Slice-count quantizer** (Layout @ 0x5290c0, band logic @ 0x5292b8, branch senses
  corrected): count==0 → 1 slice; 1–2 items → count+2; 3–4 → count+4; ≥5 → count+8.
- `PieMenuItem` rect layout + radial hit-test (CalcItem @ 0x527590) decoded.

### 4. Queue overlay
- Res_Other id 9004 `Other/QueueCancel.bmp` **45x45** — the red X over queued
  interactions. TS1 queue icons are bare interaction icons (no pill/rim chrome).

## Ported this round

### A. The bottom-bar composition (the R141 residuals, tree-verified)
- `UIMainPanel` at **(220, SH−100)** desktop — flush against the UCP's right edge,
  flush bottom. `OriginalPanelBack` at **natural 804x100** (was stretched 1024x128,
  252 px past the edge). Original composition: UCP(220) + PanelBack(804) = **1024
  exactly** at the original resolution.
- `UISubpanel` desktop: **100 tall** (PanelBack's height), width = SW−220−263 → the
  subpanel ENDS exactly at the right screen edge (was 49 px past).
- Motive grid compacted: rows at 28/72 (pitch 44; labels 12/56) — both rows inside
  the 100 px bar (was 60px pitch spilling row 2).
- Floor readout `FloorOriginal` X was never set (rendered at the plate's far
  bottom-left, tree ABS 0,752) → anchored (156,167) under the Lev column.
- `uimpanel` pin corrected with disclosure: bar height 128 → 100 (the PanelBack
  law; flush bottom Y = SH−100 exactly) — a strengthening, not a weakening.

### B. The dialog family (Hermes C's P0 — every modal in the game)
`UIOriginalDialogChrome` (new): GenDlg nine-slice (10/10/10 split of the 30x30 tile —
measured; the engine's border widths live in an unrecoverable TOC global, disclosed)
+ RGB(0,0,82) fill + WinBtn system buttons + the PositionButtons/auto-size laws.
`UIMobileDialog`/`UIMobileAlert`: desktop branch renders the original windowed chrome
(9-slice frame, fill, glyph title/body twins, WinBtn 3-slice-stretched buttons in the
decoded row law, content-sized box min-width 200, compact 420px wrap — disclosed);
the mobile stripe wipe is preserved for touch builds. Sim-message dialogs (UILotControl),
quit/evict/save (GameController) inherit automatically.

### C. The pie menu
- Background: `cpanel\ViewMenuBackground.bmp` (was TextureGenerator radial blue).
- Buttons: `shared\sys\PieButt.bmp` 17x17 tile stretched per label (was generated pill).
- Palette: label (187,187,187) / highlight cyan (0,255,255) / lowlight (185,185,208)
  (was TSO blue/yellow).
- Geometry: **radius 90**, disc grows to 180; **slice quantizer = the decoded band law**
  (was fixed 2/4/8). `UIPieMenu.SliceCount()` extracted for the gate.

### D. Queue icons
- Cancel overlay = `Other\QueueCancel.bmp` 45x45 (was int_cancel.png).
- Desktop: bare icons — Simitone pill background, blue tint (104,164,184) and yellow
  rim removed.

### E. Category switcher chrome
- Desktop: no diagonal stripe, no rotated gradient, and the modern round plaque never
  draws behind original plaque art (`OriginalStyle` owned by the switcher, not
  per-mode).

### F. Survey harness (measurement fixes, found by this round's survey)
- AbsWalk prunes invisible/transparent subtrees (touch helpers and the cheat box
  polluted trees with never-drawn elements), prints **captions + texture dims** so
  trees name their own elements.
- State C detaches killed subpanels before dumping (`UISubpanel.Kill()` removes after
  a 300ms tween; the harness's opacity-forcing resurrected the dying LIVE panel —
  the "motive panel in buy mode" reading was a HARNESS artifact, not a game defect).
- Cheat box inner textbox starts hidden like its container.
- `uisurvey-dialog.png` evidence dump: the gate constructs a real alert, attaches it,
  completes the fade, renders it (dumps/dialog-r142.png, visually verified: beveled
  slate frame + deep-blue interior + glyph title/body + two metallic WinBtns).

## Gate (new checks)

- `uidlgchrome` — GenDlg 30x30 + WinBtn 260x33 resolve; FillColor == RGB(0,0,82); a
  real 2-button alert obeys OriginalChrome/BoxWidth≥200/WinBtn sheets/uniform maxW,
  button lefts == ButtonLefts(w,2,maxW) and top == ButtonTop(h,33) ±1; title twin
  mounts; PieButt + ViewMenuBackground resolve at 17x17; slice law pinned for inputs
  {0,1,2,3,4,5,8,12} → {1,3,4,7,8,13,16,20}.
- `uibargeom` — MainPanel == (220, SH−100); PanelBack == min(804,SW−220)x100; subpanel
  right edge == SW and height 100; FloorOriginal == (156,167).

## Disclosed interpretations / residuals

1. **GenDlg border widths** = 10/10/10 measured off the tile (engine values in a TOC
   data global; dialog-chrome-law.md §7.1).
2. **Dialog wrap width 420** (canon: content-sized auto-wrap; engine measure not
   recovered) and title inset (16,8).
3. **Button caption width heuristic** (label chars × 9 + 24, min 65) — the engine
   measures through the font vtable; per-dialog uniform maxW IS canon.
4. **People pie** (PieFace1 201x201, radius 150) not yet ported — the port's people
   panel is a different surface; next round candidate.
5. The 804x100 toolbar's own button row is built inside cWinPeople/cWinArch children
   (undecoded — but proven NOT template-driven); mode-patch compose positions live in
   paint code; kHelpBtn (5,164) 10x15 and kCameraBtn (175,95) 29x29 are engine canon
   not yet ported (no handler surfaces in the port yet).
6. CAS screen + neighborhood chrome (Hermes C P0/P1; art now extracted in art/) —
   next round candidates.
7. Relationship/Inventory subpanels still mobile art (Hermes C P1).
