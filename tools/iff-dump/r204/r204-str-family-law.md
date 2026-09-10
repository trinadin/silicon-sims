# R204 — System string families: STR# 148 Pause + 152 DefaultDialogButtons (Tier B close-out round 1)

Charter note (user decision this round): the campaign switches from open-ended
"as close to 1:1 as possible" to **CLOSE IT OUT** — finish the enumerated Tier B/C
inventory (~8-10 rounds), then a final hunt round, then declare base-game 1.0 with
the disclosure list as the official delta document. Expansions stay an explicit
go/no-go after that.

## Canon tables (disk, byte-verbatim — dump_str_tables.py / r204-str-tables-full.txt)

| STR# | label | format | raw | English | full-chunk sha256 |
|---|---|---|---|---|---|
| 148 | Pause | −3 | 34 | `Paused`, `3000` | `1795b0e041b2ed6dd12ad990172d4837653cc235625d92cc404da86b021d6201` |
| 152 | DefaultDialogButtons | −3 | see dump | `OK`, `Cancel`, `Yes`, `No` | `d59f7a86e7ba10704d99fa7cbda0190c56ec192670e8fa3f8cd294bcb461a53b` |
| 162 | FriendCountDlg | −3 | see dump | title/body/OK (3) | `e86c02b0a5afa594880d32248285d8dddfb2eeca68d6fd8c623f6b9cc938c3f9` |
| 165 | SignPopups | −3 | see dump | 24 (12 × title+body) | `ec3b7d715a6aef0f0e5c4cf6ec823bcf36b2e1ecf87b182d51516cf39c826604` |

148's comments tag both entries `2 Game Screen: Text` — the entry pair is
{text, style/period}. 152 vs 142: **both** carry OK/Cancel/Yes/No in English, but
142 is 'ObjDialogs' (comments: "Appears in all OBJECT dialogs (example: find a job
in the newspaper)") while 152 is 'DefaultDialogButtons' — the SYSTEM dialog
defaults. Two tables, two dialog families.

## STR# 148 — the Paused label is a blink (engine decode)

- `cDDDSimsView::DrawPause(this, bool, bool)` @ **0x214000** (216 B):
  latch bytes this+0x180 (shown) / this+0x181 (subscribed). Both bools true →
  `cTSWinMgrW95::SubscribeTimerMsg(mgr, this, period = this+0x188, 0)` @0x51c500
  and latch; then toggles the label object this+0x17c through vtable +0x9c (show)
  / +0xa0 (hide) on the 0x180 latch. If already subscribed → `UnsubscribeTimerMsg`
  @0x51c160 first (re-arm).
- `cDDDSimsView::TSOnTimerMsg` @ **0x213830**: on timer fire → vtable+0xa0 (HIDE
  the label), 0x180=0, UnsubscribeTimerMsg, 0x181=0.
- `cDDDSimsView::Init` @ **0x218b70**: this+0x17c = `new(0xcc)` + `cTSWin::cTSWin()`
  @0x509610 — created HIDDEN, `SetOverlapsScrollArea(true)` @0x503820, **no SetArea
  → default window position (0,0) top-left of the game view**. A `cTSWinText`
  @0x538a50 is also created (this+0x18c), colored via `cTSWinText::SetTextColor`
  from the **InitSimsColors palette global `-0x6ca8(r2)`** (same global used by
  `cWinBudgetRow::TSPaint`, `cWinRelationship::TSPaint` — the shared game-UI text
  palette), also initially hidden.
- **Callers** (r204-callers.txt): `CPState::Pause(bool)` +0x48, `CPState::SetMode`
  ×5 sites, `CPState::UpdateCPStateFromWorld` +0x30c — the label re-evaluates on
  every pause/mode/world-state update.
- Net observable law: **while the simulator is paused, a 'Paused' text label sits
  top-left on the game screen, shows, hides when a 3000 ms timer fires, and is
  re-shown by the next CPState update while still paused** — a slow blink whose
  period is STR# 148[1] ('3000' ms). The re-show cadence (how often
  UpdateCPStateFromWorld runs) is undecoded from static disasm, so the port blinks
  symmetrically at the canon period (disclosed).
- Port face/color: the engine label is a cTSWinText tinted from the InitSimsColors
  palette — the same palette the R175 law decoded as the system VariableSans
  RGB (195,205,205) on system font slot 0 = font_table[11]. The port therefore
  uses `OriginalGlyphFont.LoadByIndex(11)` + `UIMobileDialog.OriginalSystemTextColor`
  (kinship, not a guess; the exact style-3000 face index is disclosed-grade).

## STR# 152 — system dialogs vs object dialogs (port law)

Engine-side exact read of 152 is not statically provable: `cTSWinMsgBox::Init`
@0x524150 builds its buttons through the ctrl-manager factory
(`cTSFrameWork_CtrlMgr` @0x512dd0 ×11, virtual dispatch ×46) — no direct
string-load site (same limitation class as R203's code-7 static-string case,
disclosed). The law rests on:

1. table identity: 142 'ObjDialogs' = OBJECT dialogs; 152 'DefaultDialogButtons'
   = the system defaults;
2. engine class split: `cTSWinMsgBox` (system message box, the R142 chrome law)
   vs `ObjectDialog::SetupDialog` @0xcc5d0 (BHAV primitive whose strings arrive
   from object data);
3. the port's own object-dialog surfaces were ALREADY correctly pinned to 142 by
   earlier rounds (R115 call-neighbor 'original ObjDialogs [1]').

**Port swap**: UIMobileAlert typed buttons (OK/Yes/No/Cancel — every system
dialog incl. the R197 native move-in AskDialogs) and FSO.UI UIAlert typed buttons
(now indexed — the old named keys "142"/"ok button" cannot resolve in a
format −3 TS1 table) read **152** [0]OK [1]Cancel [2]Yes [3]No. **Unchanged**:
UIOriginalPhoneBookDialog cancel (phone OBJECT dialog), UICallNeighborAlert
(R115), UISelectSkinAlert OK/Cancel (clothing/pet dialog, tables 220/221 family).
The uijob pin `GetString("142","1")=="Cancel"` stands (142 stays mounted for the
object dialogs) — no pin weakened; 152 pins are ADDED.

## STR# 162 — decoded this round, display ports next round (R205)

- `cWinViewControl::PostChildDraw(bool)` @ **0x2b57c0** (164 B): when the bool is
  set, `count = CPState::GetFamilyFriendCount(this+0xcc)` @ **0x20c770** (only two
  callers, both here) → `cTSString::FromInt` @0x4f4c90 → compare-then-set into the
  text gadget this+0x1a8 (caption slot +0x54; equal → skip, else vtable+0xa4 set).
  The family friend count is a live auto-updating readout on the view-control bar.
- Gadget creation in `cWinViewControl::Init` @0x2b5f30 around 0x2b74c0: factory
  `new(0x18c)` button-class alloc + `cTSWinBtn` ctor 0x50d720, text-measured
  (0x59cd30) auto-size, laid out relative to this+0x100's rect, +0x158=1, final
  SetRect +0x114..+0x120.
- UCP tooltip = STR# 138 'VCtlTips' [20] 'Family Friend Count' (r117 dump — the
  one r117 entry left unused); the About dialog = STR# 162 (title/body/OK).
- **R205 scope**: the readout + tooltip + About dialog on the desktop UCP.

## STR# 165 — ALREADY PORTED (ledger correction)

Tier B listed "165 SignPopups open" — stale. The personality subpanel's
`ToggleZodiacPopup` reads `GetString("165", 2*(code-1))` title/body and the gate's
`zodiacPopupLaw` (AutotestRunner ~7630) drives the REAL popup and pins
Title == signPopups[22], Body starts with signPopups[23] + 154[6]/[7]
compatibility lines. Retired in PARITY.md this round with this citation.

## Disasm evidence

- r204-disasm-drawpause.txt (0x214000), r204-disasm-dddsimsview-init.txt
  (0x218b70), r204-disasm-msgbox-ctor/init.txt (0x524b30/0x524150),
  r204-disasm-viewcontrol-init.txt (0x2b5f30), r204-disasm-postchilddraw.txt
  (0x2b57c0), r204-disasm-familyfriendcount.txt (0x20c770),
  r204-disasm-showwindow.txt (0x214250), r204-callers.txt (bl scan).

No proprietary payload; all analysis from the local engine binary + game data.
