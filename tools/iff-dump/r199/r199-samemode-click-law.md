# R199 — the same-mode click law (desktop live-mode dual mount)

User report: *"when I clicked Live Mode, it shows the relatively traditional
panel, but then a Simitone family member selector and a background appear on
top of it. As if it triggers both."*

## Root cause (port)

`UISimitoneFrontend.LiveButtonClicked` routed the desktop same-mode click into
the mobile family-selector flow:

- Fresh lot entry (desktop): `MainPanel` is constructed `Visible=false` /
  `PanelActive=false` with `Mode = LIVE` (enum default; only global-32 lots
  force BUY+Open at entry). Clicking Live Mode hit the terminal `else`:
  `MainPanel.Open(); StartSelect();` → **both** the traditional panel
  (Open slides it in) **and** `UISwitchAvatarPanel` + its `pswitch_bg.png`
  strip (ShowSelect) — the reported dual mount.
- Steady state (already LIVE, panel up): the same-mode click fell into the
  mobile `StartSelect()` branch → selector over the traditional panel.

`UIMainPanel.SetMode` already no-ops same-mode requests; the bug was purely
the frontend routing.

## The engine law

`CPState::SetMode(CPState::Mode mode, bool force)` @ **0x210790** (symbol
0x210792, 1380B). Prologue:

```
lwz   r3, 8(r3)        ; r3 = this->mode
cmpw  r3, r27          ; vs requested mode
bne   0x2107cc         ; different -> proceed with the switch
clrlwi. r0, r5, 0x18   ; same -> test the bool arg (force)
beq   0x210ce0         ; force clear -> straight to the epilogue (blr)
```

Every `cWinViewControl::TSOnCommand` mode-button case passes force **0**
(`0x2b4b24` region): the +0xf4 slot (LIVE) is `SetMode(0, 0)` at 0x2b4bec,
+0xf0 (BUY/objects — the R145 room<->function toggle ladder runs first)
`SetMode(1, 0)` at 0x2b4bb4, +0xf8 `SetMode(3, 0)`. **Clicking the
already-active mode button is therefore a complete no-op** — no selector, no
panel re-run, no fade. The BUY button is the one special case: its handler
does extra work (the R145 toggle) *before* the no-op'ing SetMode call.

CPState::Mode enum (confirmed by the body): 0=live 1=buy 2=build
(0x2107e4 special-cases enter/leave of 2) 3=options 4=camera (0x2107cc) —
identical to the port's `UIMainPanelMode`.

Adjacent law disclosed, not ported: each mode-button case first runs
`if (window->m_subMode(+0x188) == 1) CPState::SetCPMode(0, 0)` — a sub-state
reset the port has no counterpart for on this path (its CPMode surface is
the R145 buy-sort toggle, already implemented).

## The port

`UISimitoneFrontend.LiveButtonClicked` gains the engine guard after the R145
BUY-toggle case (which stays first, matching the engine's order):

```csharp
if (Game.Desktop && mode == MainPanel.Mode)
{
    if (!MainPanel.PanelActive) MainPanel.Open();
    MainPanel.SwitchAvatar?.Kill();
    return false;
}
```

- Same-mode desktop click → no-op. The one port artifact is the lot-entry
  hidden panel (the original composes its control panel permanently), so the
  no-op still reveals it — the user's click now shows ONLY the traditional
  panel, never the mobile strip.
- `UISwitchAvatarPanel` (pswitch_bg + elastic icon buttons) is now
  unreachable on desktop in every path; mobile keeps it by design (touch).
- Cross-mode clicks (deskAuto path) unchanged.

Sim selection on desktop remains the original's surfaces: click a sim in the
world, Space cycles (`frontend.Update`), and the people-window portraits
(the R131/R144 follow-sim visibility round is the next Tier B item).

## Gate

`uilive` EXTENDED (still 115 checks — no pins weakened): `desktopSameModeLaw`
drives the real private click handler by reflection and pins
`steady=True` (same-mode click with panel up: not consumed, mode retained,
no `ShowingSelect`, no `SwitchAvatar`, zero mounted `UISwitchAvatarPanel`
children), `reveal=True` (the reported repro: `PanelActive=false` → click
reveals the panel with zero selectors), `cross=True` / `back=True`
(LIVE→BUY→LIVE still switches and never mounts a selector). Targeted soak
`uilive,corpus` PASS (`sameMode=True steady=True reveal=True cross=True
back=True`); FULL DEFAULT GATE **115 passed, 0 failed** first run, carseek
PASS, clean after Run/after Dispose exit-probe chain, WRAPPER_EXIT=0; dist
DLLs byte-match publish (Simitone.Client ebe77386, FSO.SimAntics 6b8d6403,
FSO.Client 2b83283d).

## Residuals

- The engine's permanent control panel at lot entry is only approximated
  (reveal-on-first-click); making the panel composed at entry like the
  original is a separate lot-entry round (it interacts with the CAS→lot
  transition and the R197 move-in flow).
- `+0x188 -> SetCPMode(0,0)` pre-reset disclosed above, unported (no port
  state to reset).
- No proprietary payload in this round's evidence.
