# R206 — The control panel composes PERMANENTLY at lot entry (the R199 residual closed)

R199 disclosed: "the original composes its control panel permanently at lot entry
(the port reveals on first click — a separate lot-entry round)." This is that round.

## Engine

- `cSimsApp::RebuildControlPanel` @ **0x24de40** (276 B): destroys the old panel's
  windows, then `new(0x108)` + **cWinCPanel ctor 0x270d40** stored at app+0x84,
  positioned via window SetRect (vtable+0x68) from the app window's rect. The
  panel is BUILT as part of the game's window setup — not revealed on demand.
- `CPState::EnteringHouse` @ **0x211c80** (96 B): flag word this+0xc |= −1 →
  0x211200(this) → **SetMode(this, mode, force=0)** immediately — the mode (and
  its panel) is set as part of entering the house, from the first frame.
- Callers scanned (r206-callers-setmode.txt): EnteringHouse, SetControlPanel,
  ComputeUpDownState, RebuildControlPanel, the transit dialogs, AppCheatCallback.
- Net law: at lot entry the control panel EXISTS, composed in the entry mode —
  there is no hidden-then-reveal state in the original.

## Port

- `UIMainPanel.ComposeAtLotEntry()` — Open()'s END state, untweened: Visible,
  CurWidth = ScreenWidth − X, fadeables at Opacity 1, PanelActive = true.
- `UISimitoneFrontend` ctor, LIVE branch (global 32 clear): the old flow ended
  with `MainPanel.Visible = false` and no compose — the hidden-until-first-click
  reveal. Desktop now calls ComposeAtLotEntry there (touch keeps the mobile
  reveal; the BUY global-32 entry keeps its tweened Open).
- The R199 no-op comment updated in place (the "one port artifact" paragraph is
  resolved by this round).

## Gate

`uipanelentry` (default suite, 119→120): LIVE — the real frontend's panel is
PanelActive+Visible+composed-width (>700; exact width is maintained per-frame by
UIMainPanel.Update and a same-frame X shift by an earlier check can lag one
frame — the deterministic exact pin is the ctor probe); CONSTRUCTION — a fresh
desktop frontend composes its panel in the ctor with the exact full width
(804 = 1024 − 220, the engine's UCP(220)+PanelBack(804) composition).

One honest first full-gate FAIL on the exact-width live pin (width 764 vs 804 —
the one-frame X-drift race above) fixed by the range pin; the ctor pin keeps the
exact-width law. uilive's same-mode/reveal/cross/back probes UNREGRESSED (the
probe force-manufactures the closed state it tests). Soak
uipanelentry+uilive+uifriend+corpus 8/0; FULL DEFAULT GATE **120 passed, 0
failed**; clean exit probes; WRAPPER_EXIT=0; dist byte-match 5/5 (Simitone.Client
66397436, FSO.UI 9f799351, FSO.Client 9c138b72, FSO.SimAntics 4cfa2c18,
FSO.Common 5c8d3066).

No proprietary payload.
