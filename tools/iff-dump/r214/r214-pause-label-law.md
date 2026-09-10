# R214 — the Paused label re-show cadence, decoded (R204 residual retired)

Binary: `game-data/The Sims/The Sims Complete` (PPC, addresses = file offsets).
Evidence: `r214-disasm-core.txt` (DrawPause / TSOnTimerMsg / CPState::Pause),
`r214-disasm-callers.txt` (UpdateView, the world-sync block, all five SetMode
arms + dispatch head). Prior art: `r204/r204-disasm-drawpause.txt`,
`r204/r204-callers.txt`, `r204-str-family-law.md`.

## What R204 left open

R204 decoded `DrawPause`/`TSOnTimerMsg` and knew the *callers* re-show the
label, but could not statically prove the re-show cadence, so the port shipped
a **symmetric blink** (3000 ms on / 3000 ms off toggle) with the cadence
disclosed. This round closes that.

## The machine

### `cDDDSimsView::DrawPause(bool show, bool subscribe)` @0x214000

1. If the timer flag (`this+0x181`) is set: **unsubscribe the timer**, clear
   the flag. — *Every* call first cancels any pending blink.
2. `this+0x180 = show` (the stored show flag).
3. If `show && subscribe`: `SubscribeTimer(this, this+0x188 /* 3000 ms from
   STR#148[1] */, 0)`; set `this+0x181`.
4. If the label (`this+0x17c`) exists: Show (vtable +0x9c) if the stored flag,
   else Hide (vtable +0xa0).

### `cDDDSimsView::TSOnTimerMsg()` @0x213830 — a ONE-SHOT

Clears the show flag, Hides the label, unsubscribes the timer, clears the
subscribe flag. **No resubscribe, no re-show** — the label stays hidden until
the next `DrawPause(show=1)` call. The symmetric blink therefore does not
exist in the engine.

## The call sites (all seven, with args and frequency)

| Site | Args | When it runs |
|---|---|---|
| `CPState::Pause(bool p)` @0x210118 | `(p, false)` | pause button, `sim_speed` cheat, app activate/deactivate, preferences, the neighborhood/expansion dialogs, `LoadGame` — **events only**, and only when `mode == 2` (LIVE) |
| `UpdateCPStateFromWorld` sync @0x21150c | `(worldPause, false)` | gated on flags word bit 0x00800000 at `this+0xc`, set by `Pause()` **and re-set by the block itself** (0x2114e4) — the bit is never cleared anywhere in the binary, so after the first `Pause()` (which `LoadGame` itself calls) the block re-runs on every sync; syncs happen from `UpdateView` (callers: `cSimsApp::DoCommand` ×5 — command events, **not per-frame**) and `EnteringHouse` |
| `SetMode` → BUILD(0) @0x2108e0 | `(1, 1)` | mode change into build |
| `SetMode` → BUY(1) @0x210ad0 | `(1, 1)` | mode change into buy |
| `SetMode` → LIVE(2) @0x210b68 | `(this->f30, 0)` | mode change back to live |
| `SetMode` → CAMERA(4) @0x210c50 | `(1, 1)` | mode change into camera |
| `SetMode` → OPTIONS(3) @0x210ca4 | `(1, 0)` | mode change into options |

Mode numbering (from the `cWinCPanel::UpdateViewFromCPState` dispatch @0x26ed00
and the `GetMode()` getter @0x211150 reading `this+8`): 0=arch/BUILD,
1=catalog/BUY, 2=people/LIVE, 3=options/OPTIONS, 4=campanel/CAMERA.

## The resulting visible law

- **LIVE mode: the label is a steady indicator.** Pause shows it, unpause
  hides it (`CPState::Pause` + the latched sync both pass `subscribe=false`,
  so no timer ever runs there). The R204 symmetric blink was behaviorally
  wrong in the common case.
- **Entering BUY/BUILD/CAMERA shows the label with a one-shot 3000 ms
  hide** (the clock-stopping modes): visible for one period, then hidden —
  and it *stays* hidden until the next `DrawPause` call, because the timer
  never resubscribes. Any subsequent `DrawPause` (e.g. a command-driven sync
  while paused, another mode change) cancels the pending timer and re-shows.
- **Entering OPTIONS shows it steady** (no timer).
- **Returning to LIVE shows it iff the pause flag is set.**

## The port

`UI/Controls/UIOriginalPauseLabel.cs` reworked to the law: a public
`DrawPause(show, subscribe)` implementing the unsubscribe-first / one-shot-arm
machine verbatim; `OnModeChanged(mode)` carrying the five SetMode arms keyed
by the port's 1:1 mode names; `Update` reduced to (a) the LIVE pause-edge
`DrawPause(paused, false)` — the engine drives the equivalent sites on command
events, the port reads the edge per frame, which is the identical visible
outcome because nothing else hides the label in LIVE — and (b) the one-shot
timer fire. Wiring: `TS1GameScreen.MountPauseLabel` subscribes the label to
`Frontend.MainPanel.ModeChanged` (re-wired per frontend mount, since each
remount builds a fresh `MainPanel`).

Probe counters: `OneShotArms`, `OneShotFires`, `TimerCancels` (the f181
accounting).

## Gate

- `uistrfam` re-pinned (stronger canon pin, R197-precedented replacement of
  the R204 symmetric-blink pin): steady show across a crossed period in LIVE
  (the old pin asserted the phase toggle — now asserted ABSENT), unpause
  hides, BUY entry shows + arms, the 3500 ms one-shot hides and stays hidden,
  CAMERA re-arm, OPTIONS cancel + steady, LIVE tracks the pause flag both
  ways, and the counter deltas pin the machine (arms +2, fires +1, cancels +1).
- `uitotal` folds the PAUSED state into the provenance walk: pause the real
  vm, let the label's Update edge show it, walk the same tree again — the
  paused HUD subtree enters the enforcement walk (previously never
  exercised). The label's text paints through the glyph-atlas pipeline (no
  `Texture` member), so the fold pins presence + zero violations.

## Residuals

- The event-vs-polling equivalence in LIVE (engine: command-event-driven
  re-assert through the permanently latched 0x00800000 sync bit; port:
  per-frame pause edge) — identical visible outcome, disclosed here.
- The style-3000 face stays the R175 palette-kinship slot (font_table[11])
  — unchanged from R204, still disclosed-grade.
