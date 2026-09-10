# R245: tutorial lesson-highlight and UI event-poll restoration

The port's tutorial can now do what the original's tutorial does when a lesson
wants the player to act: flash the original orange arrow over the exact control
to press, and detect — every view tick, against the player's real UI state —
when the awaited action happens. The unimplemented "UI effect" primitive
(opcode 34) now drives the original three-frame `cpanel\TutHigh.bmp` highlight
window (167 ms ping-pong, never auto-hiding, hiding during modal dialogs and
restoring after), and the original poll machine runs the owner's real
"query wait event"/"got wait event" trees each tick against a 16-entry
condition table (control-panel mode, five person-panel page captions, button
press/mouse-over by original image id, shown portrait, rotation/scroll edge
latches sharing one native-style latch, page-absent) so lessons advance on
real input.

## Decode phase (decode → skeptic → repair provenance)

[`decode.md`](decode.md) + [`skeptic-corrections.md`](skeptic-corrections.md)
resolve the whole family from the owner executable (SHA-256
`33c76da2…06a5f`): the single `Tut_CheckForEvents` call site in
`cDDDSimsView::Simulate` (0x21715c, unconditional per tick, global-12
last-button mirror before the owner guard), the 16-entry jump tables
(0x4abb8/0x4ab78), the flash helpers as the primitive-34 handler
(`cXObject::TryElement` 0xf0410 → sub-op 0/1/2 →
`Tut_FlashButtonByImageID`/`FlashPersonPanelButton`/`FlashRelButton`,
target vtable slot 0x168 `HiliteForTutorial(bool)`), the
`cTSWinTutorialHighlight` lifecycle (created only in view Init, winmgr +0x20c
slot, `SetBuffer(buf, 3, 6.0f)` → trunc(1000/6+0.5) = 167 ms), and the read
side: owner s16 temps +0x3a/+0x3c carry the armed event code/param written by
the lesson script's attribute mailbox. `verify.py` pins 118 instruction words,
11 data pins and 7 fixture groups.

The skeptic re-derived everything and issued FIX FIRST; all six corrections
held and are applied ([skeptic-response.md](skeptic-response.md)):
event captions 9="rel"/11="job" were transposed in prose; the highlighter
field's full accessor set (modal auto-hide + restore in `DoModalWin`, clear on
window destroy without release, release+null at shutdown) supersedes the
"two readers" claim; the native poll is genuinely re-entrancy-prone (poll runs
before the simulator gates; `TryDialog` can open a nested modal loop) so the
port adds an explicit guard; `FindRelButton` returns the first
relationship-panel window (person id is existence-only); the primitive-34
opcode index is `(s16)opcode & 0x7fff`; two verify fixtures were corrected
(centering truncation law, flip guard).

The data side (parallel analysis, read-only): lesson scripts arm owner
attributes `myAttr[2]`=event code (BCON "ui events" = [1,15,4,5,2,6,7,8,9,10,
11,12,13,14]) and `myAttr[3]`=param, with `myAttr[7]` as the poll heartbeat;
primitive 34 is issued in symmetric on/off pairs bracketing every wait. The
repair pass corrected one detail against the on-disk IFF: the game-visible
BCON id is 4097 (big-endian chunk id), matching the decode.

## Implementation

Engine (nested `FreeSO`, branch `mac-port-rel`):
* NEW `Primitives/VMUiEffect.cs` — opcode 34 ("ui_effect"; the `//TODO` at
  `VMContext.cs:345` is retired): operand law byte0 sub-op / scope-byte1 +
  s16 id / `byte4 & 1` on-flag; sub-op>2 no-op-true; TSO no-op-true. Raises
  `vm.OnTutorialUIEffect(subOp, id, on)`.
* NEW `Model/TutorialUIState.cs` — the UI→engine mirror (cp-mode, person-panel
  page caption token or absent, last-button/hover image ids, shown portrait
  neighbor, raw rotation/scroll for the engine's −1-initialized shared edge
  latch).
* NEW `Model/TutorialEventPoller.cs` — per-tick poll running the owner's REAL
  named trees (nothing reimplemented, so the attribute mailbox and heartbeat
  semantics are the IFF's own): query via an EvaluateCheck-faithful clone
  thread whose temps are read afterwards, jump-table dispatch with the
  skeptic-corrected captions, found → "got wait event" once. `InPoll`
  try/finally plus `ModalDialogTracked` implement the port-side re-entrancy
  guard the native lacks.

Client (parent, branch `mac-port`):
* NEW `UI/Panels/UIOriginalTutorialHighlight.cs` — the highlight window port:
  art via `EnsureResolvedByID(4980)`, 3×50x50 frames, 167 ms ping-pong
  (strict up-flip), anchor `x+(W−50)/2`, `y−53`, lot-button +50, modal
  hide/restore with fresh phase, membership-based target-destroy clear
  (this framework never nulls `Parent` on removal — found by review),
  key-consistent transparent-footprint census (5892/7500, matches the raw
  BMP).
* NEW `UI/Panels/TutorialControlMap.cs` — original-image-id → control
  registry with per-entry provenance (19 UCP ids + 7 people-chrome tabs
  verified line-by-line against `r142/rt-inventory.txt`), hover resolution,
  last-activated mirror, person/rel resolvers.
* `UILotControl.Tutorial.cs`/`UILotControl.cs` — per-tick mirror publication +
  `MirrorLastButton` + guarded `Poll()`; `OnTutorialUIEffect` handler
  (flash by control/person/rel; off = native unconditional-hide when the
  target resolves, no-op when it does not); full unbind in `Removed()`.
* `UIDesktopUCP.cs`/`UIMainPanel.cs`/`UIOriginalPeopleChrome.cs` — additive
  registrations and second click handlers (existing routes untouched).
* NEW `AutotestTutorialHighlight245.cs` + additive default check
  `uitutorial-highlight` (141 → 142).

## Review and repair record

The adversarial review verdict was FIX-FIRST: one P1 (the dead `Parent==null`
target-destroy test — replaced with membership detection; also turned the
battery's red assert green before packaging) and P2 refinements implemented:
native unconditional hide on `off`, the ev-5 fires-early disclosure, a
de-tautologized rel-panel expectation, and battery self-healing. While
iterating, the repair pass also fixed real battery defects (missing
`vm.Init()`, the BCON endianness misread, an unpassable invisible-target
assert, a redundant re-key). The review separately confirmed: the poller's
clone-thread is instruction-for-instruction `EvaluateCheck`; the real IFF
ground truth is that the mailbox clear lives in tree 4122 (not 4121) and the
port matches the actual data; click hooks and all runner edits are purely
additive; threading is single-game-thread safe; and the opcode-mask
divergence for raw opcodes ≥0x100 with 0x8000 set is unreachable in real
content (documented).

## Validation

* Baseline before work: 141/0 (includes R244's uicutaway).
* Focused packaged gate `lot,corpus,uitutorial,uitutorial-render,
  uitutorial-action,uitutorial-highlight`: **PASS 10/0** at 2026-09-06
  03:56 CDT (`focused-final.log`).
* Full packaged default suite: **PASS 142/0** at 2026-09-06 04:01 CDT, clean
  exit-probe chain (`default-final.log`).
* Publish/package carry the same `Simitone.Client.dll` SHA-256
  `2a6ae25cbb26e99ca49185118b15acaf14faa149c3cffb064dc9c9132087d4d3`.
* Visual evidence `<UserDir>/ui-audit/r245/flash-phase{0,1,2}.png`: the
  original orange TutHigh arrow renders at the anchored 50x50 position and
  the three animation frames are pairwise distinct.

## Disclosed deviations and residuals

* Poll/heartbeat freeze while a tracked modal dialog is up (the port-side
  re-entrancy guard; native polls re-entrantly inside modal loops).
* Event 5 fires on the selected avatar without the native "portrait was
  last-clicked" conjunction (fires-early only).
* The lot-button +50 override has no live caller yet (the native
  `cWinLotBtn` is the neighborhood house-lot button — future neighborhood
  round); implemented and exercised via the map's lot-button flag.
* Native cursor ids (0/4) and `PumpMouseMoveMsg` are not ported (no cursor
  machinery on these controls).
* The native "skill" page caption is unreachable from the port (no skill
  tab); house/interest/gift tabs publish a non-native empty caption so
  event 14 semantics hold.
* Raw opcodes ≥0x100 with bit 0x8000 route to gosub in FSO rather than
  masking to a tutorial primitive — unreachable in real TS1 content.
* The tutorial setup/reset/completion family (`ResetTutorial`,
  `TutorialCompleted`, `CancelTutorial`, `Tutorial.FAM`) remains out of
  scope; fixtures never touch `nhoodData[1]`.
