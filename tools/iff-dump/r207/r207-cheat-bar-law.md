# R207 — The cheat bar on the engine law (+ two ledger retirements)

## Ledger retirements first (R159 census items, verified stale)

- "the r143 neighborhood navbar law never landed" — STALE: the navbar IS
  landed (`UINeighbourhoodSwitcher` mounts the kNghBarBkg 5420 banner +
  toolbar; `uinav` pins `NghUI\banner_neighborhood.bmp` 800x52 and
  `LastBannerMember`). Retired with citation, no code change.
- "modern select-sim overlay reachable on desktop" — RESOLVED by R199's
  same-mode law (StartSelect is desktop-unreachable: the same-mode no-op
  returns first and deskAuto covers every remaining desktop path). Retired.

## The cheat bar (cTSMainWindowW95 + cSimsApp)

- **Creation** — `cSimsApp::FinishSlowInit` @0x24d6a0, block 0x24d6f4-0x24d7fc:
  `new(0x198)` + `cTSWinTextEdit2::cTSWinTextEdit2()` (0x535470), then
  - `SetFont(fontTable[+0x28])` = **engine font index 10** (the funds/CAS-name
    face; r143 cas-layout-law §6) — 0x52fe20
  - `SetTextColor(BSS 0x92984 = WHITE)` — 0x52fdb0
  - `SetColors(*(BSS 0x9297c) = RGB(0x40,0x5D,0x5F), white, white)` — 0x530090
    (the InitSimsColors palette, r143 cas-layout-law §5)
  - `SetArea(0x14, 0x14, 0xdc, 0x29)` = **(20,20)-(220,41) = 200x21 top-left**
  - `SetCapacity(255)` — 0x52fd20
  - `SetLinesAllowed(1)` (0x52ff10), `SetTransparent(false)` = OPAQUE box
    (0x52ffc0), `SetDrawFrameOnFocus(false)` (0x52ff50), `SetMode(1)` (0x52fd60)
  - handed to the main window via vtable+0x1c4 (the +0xe4 widget slot; the ctor
    0x4d7940 nils it, Init stores the MANAGER at +0xe8 — the edit is the slow-init
    child).
- **Toggle keys** — `cTSMainWindowW95::TSOnKeyDown` @0x4d5af0, gated
  `app->0x64 == 0x10` and `(modifiers & 3) == 3` (ctrl+shift):
  **'c'/'C' (0x63/0x43) @0x4d5ba4 AND 'b'/'B' (0x62/0x42) @0x4d5b68** both call
  ShowCheatWidget(true) — TWO chords, not one.
- **Show/hide** — `ShowCheatWidget(bool)` @0x4d5ce0: show = vtable+0x9c (Show) +
  focus (TSSetFocus 0x51f850) + `SetOverlapsScrollArea(1)`; hide = vtable+0xa0
  (Hide) + `cTSWinTextEdit2::SetText("",false,true)` (0x52fbb0) + focus restore.
  The help window (this+0xec, `cTSWinCheatHelp` 0x50e8c0 via
  `SetupHelpList` 0x49e590) rides the same focus chain.

## Port

`UICheatTextbox` (the existing surface, now on the law):
- geometry **(20,20), 200x21** (was (10,10) 200x23); background the exact
  opaque `RGB(64,93,95)` (was the eyeballed (67,93,90)); text WHITE;
  `MaxChars 255`; `MaxLines 1` (already); toggle chord extended to
  **Ctrl+Shift+C OR Ctrl+Shift+B** (was C only).
- Disclosed: the edit's live glyphs render through the modern text renderer
  (the port's standing edit-box convention — original-glyph twins exist for
  readouts, not careted input); the engine's `SetMode(1)` edit mode and the
  cheat-HELP window (the '?' cheat list) are unported.

## Gate

`uicheat` (default suite): construct the production box and pin position
(20,20), size 200x21, background pixel RGB(64,93,95) sampled from the mounted
texture, text style WHITE, MaxChars 255, MaxLines 1; the chord law through the
real Update (manufactured key states: C and B both toggle, X does not); the
engine constants pinned as statics for the ledger.

Evidence: r207-disasm-finishslowinit.txt / tsonkeydown.txt / showcheatwidget.txt /
mainwindow-init.txt / initwin95.txt / windowproc.txt + the caller scans.
No proprietary payload.
