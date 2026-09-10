# R241 Assignment A — original secondary-camera (PIP) wall/cutaway selection and open/close fade

Evidence: the owner's original PowerPC executable
`game-data/The Sims/The Sims Complete`, SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f` (re-verified).
All addresses below are **raw file offsets**. The `r141/symbol-index.txt` uses a
+2 convention (its column is file offset + 2); every address here was used
directly with `ppc_decode.py`/`decode.py` on the file bytes, and the
decode-vs-file relationship is itself asserted by `fixture_r241.py`
(43/43 checks pass; see §4). Where a symbol name is cited, its index address
minus 2 equals the file offset used here.

New tooling in this directory (derived text/scripts only, no binaries):
`decode.py` (annotated PPC decode with symbol-index call resolution),
`fixture_r241.py` + `fixture-output.txt` (offline law verification),
plus saved decode dumps (`buildimage.txt`,
`pip-window-core.txt`, `pip-open-rest.txt`, `pip-open-tail.txt`,
`pip-tspaint.txt`, `pip-init.txt`, `pip-ctor.txt`, `pip-command.txt`,
`viewer-setrotation.txt`, `viewer-setlevel.txt`, `scrolltotile.txt`,
`wallmgr-setcutaway.txt`, `wallmgr-setrotation.txt`, `showtileinfo.txt`,
`asyncshowhide.txt`, `factory*.txt`, `capturebg.txt`, `colorfader-*.txt`).

## 1. Findings, ranked by confidence and user impact

### F1 (VERIFIED, highest impact) — the secondary camera renders the TARGET'S FLOOR by temporarily re-leveling the shared viewer through the standard wall-cutaway law; it is not "main-camera cutaway reused", and it is not "no cutaway"

The unnamed function previously decoded as framing evidence at `0x1c0c60` is
**`HouseViewer::DrawPictureInPicture(cTSBuffer*, const FTilePt&, int, int, int)`**
(traceback name at `0x1c17dd`; body `0x1c0c60..0x1c17c8`). Signature:
`(this, cTSBuffer* image, const FTilePt& centerTile, int verticalOffset, int level, int zoom)`.
The third and fourth int arguments were previously ambiguous; they are the
**floor (level)** and the **zoom**:

- `GetLevel__11HouseViewerCFv` (`0x1bfa80`) is `{ lwz r3,24(r3); blr }` — the
  viewer field `+0x18` is the **level**.
- `SetLevel__11HouseViewerFib` (`0x1d7110..0x1d71e8`), on a level change, runs
  exactly: `GetWallManager` → `WallManager::SetCutaway(0, level)` (`0x2c2f90`)
  → `cCutawaySet::clear(viewer->0x1fc)` (`0x1a9e60`) → `viewer->0x4e = 1`
  (dirty) → if `!viewer->0x49` `ResetDynamicCutaway` (`0x1cf240`) →
  `viewer->0x18 = level` (`stw r30,24(r29)` @`0x1d7178`) →
  `RoomManager::ComputeCutaway(level)` (`0x128ae0`) → if `!viewer->0x49`
  `DoDynamicCutaway(viewer, viewer->0x1f8)` (`0x1cf3b0`) → two virtual refresh
  calls → `PumpMouseMoveMsg` (`0x51bf10`).
- `DrawPictureInPicture` contains that same block **twice**, inlined:
  - enter pass `0x1c1048..0x1c10e8`: guard `lwz r21,24(r16)` (viewer level)
    vs arg4; `beq 0x1c10ec` skips when equal; then the SetLevel sequence with
    `ComputeCutaway` at `0x1c10a0` for the **target's floor**;
  - restore pass `0x1c15f8..0x1c169c`: guard vs the saved viewer level
    (stack `0x118`, saved at `0x1c0ea8`), `ComputeCutaway` at `0x1c1654` for
    the **main camera's floor**, then `viewer->0x18 = saved`
    (`0x1c163c`), `ScrollToTile(tile, level, 0, 1)` at `0x1c1188` receives the
    same level argument (`addi r6,r20,0` @`0x1c117c`, `li r7,0` @`0x1c1180`,
    `li r8,1` @`0x1c1184`).
- Rotation is NOT a parameter: `DrawPictureInPicture` calls
  `SetRotation(viewer, *(rotationGlobal+0xc))` (`0x1c1044`; the global is
  TOC`-0x28744`/`-0x7048`-adjacent camera state — `SetRotation` stores its
  argument there at `0x1d74e8` and `GetRotation__11HouseViewer` `0x1d1760`
  reads it back). So the PIP always renders at the **main camera's current
  rotation**, and the per-floor variation is purely the level/cutaway state.

Where the level comes from — `BuildImage__20cWinPictureInPictureFv`
(`0x2990c0..0x299540`), the only caller (`bl 0x1c0c60` @`0x2992c8`):

- `level = *(target + 0x110)` (`lwz r31,272(r3)` @`0x299278`, target =
  window+`0xd0`); a null target falls back to a virtual call on the viewer
  (`vtbl+64` @`0x29928c`). So `cXObject+0x110` is the object's **floor**.
  Cross-check: `TSOnMouseDownL` (`0x299730`) uses the identical
  `lwz r6,272(r4)` @`0x299794` to `ScrollToTile` the **main** camera to the
  target's tile *and floor* — i.e. clicking the PIP jumps the main view to
  that floor, exactly the port's `Center(target); Game.Level = target.Level`.
- The chain closes through the state layer: `ScrollToTile` calls
  `CPState::SetLevel(level, 1)` (`0x1d7d74` → `0x210170`), which clamps
  0→1, stores `CPState+0x2c` (`GetLevel__7CPStateFv` `0x210fe0` returns it)
  and forwards to `HouseViewer::SetLevel` (`0x2101b4`). `ComputeCutaway`'s
  per-room filter (`0x128b20..0x128b30`, a 2-bit room field +1 compared with
  the argument) confirms the int selects rooms of one floor.

Consequences for the wall question in the assignment:

- **Same floor** (`level == viewer->0x18`): the original performs **no**
  cutaway recomputation — the PIP renders with the main camera's current
  room-cutaway state (the shared `cCutawaySet`/room flags), and the wall
  style (walls up / down / cutaway mode) is inherently shared because there
  is only one global `HouseViewer`/`WallManager`.
- **Different floor**: the original shows the target floor under the result
  of the standard per-floor cutaway recomputation
  (`ComputeCutaway(targetLevel)` + `DoDynamicCutaway` when the dynamic
  cutaway viewer flag `0x49` is set) — not a blanket "no cutaway", and not a
  frozen copy of the main floor's cut set. After the image is drawn, the main
  floor's cutaway is recomputed so the main view is unchanged.

Maintained behavior: `WorldPictureInPictureRenderer.Render`
(`FreeSO/TSOClient/tso.world/WorldPictureInPictureRenderer.cs`) keeps
`Blueprint.Cutaway` as-is for the same floor (matches) and substitutes
`Blueprint.Cutaway = new bool[oldCutaway.Length]` when
`level != Main.Level` (line 110 — "Native secondary Render disables the old
floor's cutaway"), restoring in `finally` (lines 156–157). The port comment
is half right: clearing the old set matches `cCutawaySet::clear`, but the
original then **computes the new floor's cutaway**, which the port does not
do — its PIP view of another floor always draws with everything uncut
(walls-up appearance), whatever the main camera's wall mode is. Impact: a
PIP of an upstairs/downstairs target in cutaway wall mode shows the wrong
wall state for that floor. (In walls-down/up modes the substitution is
invisible, which is why R240 captures looked plausible.)

Also verified for the same-floor path: `DrawObjects`' skip
`(View.Level == Main.Level && obj.CutawayHidden)` (line 234) corresponds to
the original sharing one room-cut state; `View.SilentRotation = Main.Rotation`
(line 117) corresponds to `SetRotation(viewer, globalRotation)`; and
`View.Level = level` (line 120) corresponds to the viewer re-level.

### F2 (VERIFIED mechanics; default-value conclusion in §6) — original open/close is a window-system FADE (ramp-animated show/hide), not an instant visibility flip, under factory settings

The window class `cWinPictureInPicture` (`0x298c90..0x29a4d0`) routes every
open/close path through **`Fade(bool)`** (`0x299860..0x29996c`):

- `Open(target, open, durationMs, zoom, size, skipIfVisible, autoSnapshot, caption)`
  (`0x299960..0x299f58`): stores target+`0xd0`, open-flag byte +`0xde`,
  duration +`0xd4`, zoom +`0xd8` (`0x299b54..0x299b68`); enforces window
  ownership (nonzero duration + same target ⇒ re-open after unsubscribing;
  different target ⇒ return 0, `li r3,0` @`0x299abc` — R240's ownership law);
  skip-if-visible via `GetLastDamage(target,3)` vs the viewport rect;
  positions/sizes; then **subscribes a one-shot window timer with the
  duration** (`SubscribeTimerMsg` @`0x299f34`, delay = `+0xd4`) and calls
  `Fade(true)` (@`0x299f40`).
- Expiry: `TSOnTimerMsg` (`0x299600`) unsubscribes, clears `+0xd4`, calls
  `Fade(false)` (@`0x299634`). Duration is milliseconds via the window
  manager timer — **not frames**.
- ESC: `TSOnKeyDown` treats key 27 specially (`cmplwi r4,27` @`0x299584`) and
  calls `Fade(false)` (@`0x299598`).
- Close button: child button command (param==1) in `TSOnCommand`
  (`0x299fa0`) clears the target, clears `+0xde`, `Fade(false)`
  (@`0x299fdc`), unsubscribes the timer.
- `Close(cXObject*)` (`0x2999b0`): acts only when the argument equals the
  current target (`bne` over the body @`0x2999cc`), then the same
  clear + `Fade(false)` + unsubscribe. (Matches R240's close-on-same-target
  routing and the port's `if (!Open && Target == request.Target) Close()`.)
- Left-click recenter (`TSOnMouseDownL`, `0x299730`): rotates
  `GetCenterOnPt`'s tile into world axes, `StopTracking`,
  `ScrollToTile(mainViewer, centerTile, *(target+0x110), 1, 0)` (@`0x2997c8`)
  — the main camera follows the target across floors — then `Fade(false)`
  (@`0x2997f4`). So the recenter closes the PIP as well.

`Fade(bool open)` itself (`0x299860`) reads `GetBoboVision()`
(`0x23a870`, byte `cOptionsMgr+1`; setter `SetBoboVision` `0x23a830` also
byte +1) and branches:

- BoboVision OFF: instant window primitives — `Fade(true)` → vtable slot at
  byte offset **156** (`lwz r12,156(r12)` @`0x299934`), `Fade(false)` →
  offset **160** (@`0x29994c`). These two slots are the window-system
  Show/Hide pair (same offsets dominate `cWinCPanel::ShowWindow`/
  `HideWindow`, `SetMode`/`SetPanel` panel code across the binary).
- BoboVision ON: `Fade(open)` → vtable byte offset **176** (slot 22) with
  `(open ? 1 : 0, this->0xde, 0)` (@`0x2998cc`/`0x2998f0`) — the animated
  show/hide. `cDDDSimsView::ShowTileInfo(bool)` (`0x212ab0`) drives the same
  slot the same way on its tile-info window, gated by the same
  `GetBoboVision` reads (`0x212af0`, `0x212b5c`).

The animated route's engine is `cTSWin::AsyncShowHideWin(bool, bool, ulong)`
(`0x502e10..0x503210`): a **RampGenerator** at `window+0xb4`
(`SetupConstantTimeRamp` `0x14e890` ×3, `GetVal` `0x14e930`), clocked by
`TimeBase_Sims::now` (`0x2025c0`) and re-ticked via
`PostMessageToTarget(..., 27, ...)` (`0x51d930`), interpolating float
endpoints (TOC doubles at `-0x17428`) — a time-based value ramp, with
**default length 333** when the caller passes 0 (`li r29,333` @`0x502fa0`).
Units are the game timebase (milliseconds in original TimeBase use —
INFERENCE on the unit; the value 333 is verified).

**RestoreFactorySettings stores 1 into BoboVision** (`li r0,1` @`0x238068`,
`stb r0,1(r27)` @`0x23806c`; function body `0x237fc0..0x2381ee`, identified
by its traceback string at `0x2381ef`). So a factory-fresh installation runs
the **animated** route: the original PIP fades in on open and fades out on
timer/ESC/close/recenter, and the window remains interactive during the
transition (the command/timer routes are not gated on animation state).
`GetWantsFadeCapture` (`0x298c90` = `li r3,0; blr`) additionally opts the PIP
out of the screen-level fade-capture (base `cTSWin` `0x5023d0` returns 1),
i.e. the PIP does not participate in the frozen background captured for the
gamma fades (`CG_FadeGammaDown/Up` `0x575850`/`0x5754a0`, used at lot
transitions from `0xfc5c`/`0xfe88`).

Maintained behavior: `UIOriginalPictureInPicture`
(`Client/Simitone/Simitone.Client/UI/Panels/UIOriginalPictureInPicture.cs`)
has no fade at all — `Visible = true` on open (line 103), instant
`Hide()`/`Close()` (lines 231–232), click-recenter = `ActivateTarget()` =
Center+Hide (line 233, invoked from `OnMouse` line 236), `Update` hides on
deadline/ESC (lines 240–241). Event/timer/ownership semantics already match
the original (deadline at line 104 vs `SubscribeTimerMsg`; ownership guard
line 90 vs `0x299aac..0x299ac0`; close-route line 87 vs `0x2999cc`; close
button line 56→`Close()`). The gap is only the visual transition
(and, cosmetically, the window alpha during it).

### F3 (VERIFIED, no action needed) — live repaint gating and static-image mode match

Original `TSPaint(bool)` (`0x29a070..0x29a0ec`): rebuilds the image via
`BuildImage` only when the window is open (`+0xde`) and `GetLivePIP()` is on
(@`0x29a090`..`0x29a0a4`); with LivePIP off it keeps the last image (the
"camera snapshot" behavior). Port: `PreDraw` re-renders when
`NeedsImage || GlobalSettings.Default.TS1LivePIP` (line 251) — equivalent.
The original `BuildImage` also bails when `GetCenterOnPt` returns a negative
coordinate (`blt` guards @`0x299134`/`0x299140`); the port's `Valid()`
covers the same class of degenerate targets.

## 2. Maintained source references (exact)

- `FreeSO/TSOClient/tso.world/WorldPictureInPictureRenderer.cs`
  - lines 104–110: save `oldCutaway`; line 110 substitutes an empty
    `Blueprint.Cutaway` when `level != Main.Level` (the parity gap, F1).
  - lines 156–157: `finally` restore of cutaway and floor-alternate state.
  - lines 115–120: zoom handling, `View.SilentRotation = Main.Rotation`,
    `View.Level = level` (matches F1's rotation/level model).
  - line 234: same-floor `obj.CutawayHidden` skip (matches F1).
  - lines 88–97: `Render(targetTile, level, zoom, size, verticalOffset)`
    argument surface (matches the original
    `(buf, centerTile, vOffset, level, zoom)` after R240's framing work).
- `Client/Simitone/Simitone.Client/UI/Panels/UIOriginalPictureInPicture.cs`
  - lines 87, 90–91, 96, 103–104: close route, ownership, skip-if-visible,
    open + duration deadline (all match original; F2).
  - lines 231–241: instant `Hide`/`Close`/`ActivateTarget`, deadline/ESC
    handling (the fade gap, F2).
  - lines 56, 99: close button route (matches `TSOnCommand` param==1).
  - lines 251, 256: live/static repaint condition and render call (F3).
- `Client/Simitone/Simitone.Client/UI/Panels/UILotControl.cs`
  - lines 1493–1560 (`UpdateCutaway`): the main camera's wall-mode model —
    `WallsMode` 0/1/2, `VMArchitectureTools.GenerateRoomCut(architecture,
    level, cutRotation, finalRooms)` — the machinery a per-floor PIP cutaway
    should reuse for the proposed fix.

## 3. Original evidence index (all verified file offsets)

| Address (file) | Identity / role (symbol-index addr − 2 unless noted) |
|---|---|
| `0x1c0c60..0x1c17c8` | `HouseViewer::DrawPictureInPicture(cTSBuffer*, const FTilePt&, int vOffset, int level, int zoom)` — traceback name at `0x1c17dd` |
| `0x1c1048`/`0x1c1050` | enter guard `lwz r21,24(r16)` + `beq` skip |
| `0x1c1054..0x1c10a0` | enter cutaway block: GetWallManager, SetCutaway, clear set, Reset/DoDynamicCutaway, `ComputeCutaway(level)` |
| `0x1c15f8..0x1c169c` | restore cutaway block for the saved main level (`ComputeCutaway` @`0x1c1654`) |
| `0x1c1188` | `ScrollToTile(viewer, tile, level, 0, 1)` |
| `0x2990c0..0x299540` | `BuildImage__20cWinPictureInPictureFv`; level from `target+0x110` @`0x299278`; renders @`0x2992c8` |
| `0x298e50..0x299074` | `GetCenterOnPt__20cWinPictureInPictureFv` (R240) |
| `0x1bfa80` | `GetLevel__11HouseViewerCFv` = viewer+0x18 |
| `0x1d7110..0x1d71e8` | `SetLevel__11HouseViewerFib` — the cutaway law; `stw r30,24(r29)` @`0x1d7178` |
| `0x1d7c90..0x1d7fa8` | `ScrollToTile__11HouseViewerF7FTilePtibb` — same block inlined + `CPState::SetLevel(level,1)` @`0x1d7d74` |
| `0x210170..0x2101e8` | `SetLevel__7CPStateFiSynchron` → `HouseViewer::SetLevel`; `GetLevel__7CPStateFv` @`0x210fe0` = +0x2c |
| `0x128ae0` / `0x2c2f90` / `0x1a9e60` / `0x1cf240` / `0x1cf3b0` / `0x2c29f0` | `ComputeCutaway__11RoomManagerFi`, `SetCutaway__11WallManagerFbi`, `clear__11cCutawaySetFv`, `ResetDynamicCutaway`, `DoDynamicCutaway`, `GetWallManager` |
| `0x1d7230..0x1d7748` | `SetRotation__11HouseViewerFibb` — stores the rotation in global camera state @`0x1d74e8`; `GetRotation` @`0x1d1760` reads it |
| `0x299960..0x299f58` | `Open__20cWinPictureInPictureFP8cXObjectbiiibbPCc` — field stores @`0x299b54..68`; timer @`0x299f34`; `Fade(true)` @`0x299f40` |
| `0x299600..0x299648` | `TSOnTimerMsg` — unsubscribe, `+0xd4=0`, `Fade(false)` |
| `0x299580..0x2995b4` | `TSOnKeyDown` — ESC(27) → `Fade(false)` |
| `0x299730..0x299810` | `TSOnMouseDownL` — recenter via `target+0x110` floor, then `Fade(false)` |
| `0x2999b0..0x299a18` | `Close__20cWinPictureInPictureFP8cXObject` — same-target clear + `Fade(false)` |
| `0x299fa0..0x29a028` | `TSOnCommand` — close-button (param==1) clear + `Fade(false)` |
| `0x299860..0x29996c` | `Fade__20cWinPictureInPictureFb` — GetBoboVision branch; vtbl+156/160 (instant) vs vtbl+176 (animated, args `(open, this->0xde, 0)`) |
| `0x29a070..0x29a0ec` | `TSPaint__20cWinPictureInPictureFb` — rebuild iff open && LivePIP |
| `0x298c90` / `0x5023d0` | `GetWantsFadeCapture` PIP=false, cTSWin=true |
| `0x23a830` / `0x23a870` | `SetBoboVision`/`GetBoboVision` = byte `cOptionsMgr+1` |
| `0x237fc0..0x2381ee` | `RestoreFactorySettings` (traceback @`0x2381ef`); BoboVision default = 1 @`0x238068..0x23806c` |
| `0x502e10..0x503210` | `cTSWin::AsyncShowHideWin` — RampGenerator (window+0xb4), default ramp 333 @`0x502fa0`, tick message 27 |
| `0x212ab0..0x2131b4` | `ShowTileInfo__12cDDDSimsViewFb` — same animated slot under BoboVision |
| `0x5754a0` / `0x575850` | `CG_FadeGammaUp`/`Down` — screen gamma fades (lot transitions), called from `0xfe88`/`0xfc5c` |

Verification method: bytes decoded straight from the file with
`ppc_decode.py` (file offsets need no adjustment; the +2 convention applies
only to `r141/symbol-index.txt` numbers — e.g. index `00298e52` = file
`0x298e50`, re-confirmed against R240's published decode). Branch conditions
were read with the corrected PPC mapping (BO=12 branch-if-set → `beq` for BI=2,
etc.); `ppc_decode.py` prints these inverted, so every guard above was
validated against the trusted R240 `secondary-render.txt` decode and by
semantic direction. Call targets come from raw `bl` displacement math; virtual
slots from the `lwz r12,K(r12); bl thunk` pattern. The secondary render's name
comes from its embedded traceback table entry.

## 4. Reproduction / offline fixture

`fixture_r241.py` re-derives every law above from the executable in one run —
no emulator, no game, no assets:

```
cd tools/iff-dump/r241-helper-camera
python3 fixture_r241.py        # 43/43 checks pass
```

It asserts (among the 43): the SHA-256 of the executable; the exact instruction
pairs for both `GetLevel` forms; the ordered call sequence of
`HouseViewer::SetLevel`; the double inlined cutaway blocks inside
`DrawPictureInPicture` (enter + restore) with their guards and skip branches;
the `target+0x110` level loads in both `BuildImage` and `TSOnMouseDownL`;
`ScrollToTile`'s argument setup; the timer subscribe/expiry chain; the
open/close/ESC/command routes into `Fade`; the `Fade` vtable-slot branch table
(156/160/176); the BoboVision byte and its factory default; the 333-default
RampGenerator; and both `GetWantsFadeCapture` bodies. Captured output:
`fixture-output.txt`. A behavioral mini-model of the two fade routes and the
per-floor cutaway switch is embedded as the documented constants of the same
script header (no runtime needed).

## 5. Proposed minimal fix (described, NOT applied)

### 5a. Cutaway parity (F1)

File: `FreeSO/TSOClient/tso.world/WorldPictureInPictureRenderer.cs`, in
`Render`, lines 104–110 and 156–157.

Replace the unconditional empty-array substitution with a per-floor cutaway
array computed by the port's existing room-cut engine, mirroring
`ComputeCutaway(targetLevel)`:

- When `level == Main.Level`: keep today's behavior (share `Blueprint.Cutaway`
  unchanged) — verified identical to the original's skip.
- When `level != Main.Level`:
  - If the main camera's wall mode is walls-up or walls-down (mode 2/0),
    keep substituting the constant array (`false` for up, `true` for down) —
    the original's room-cut recomputation is invisible in those modes because
    the shared wall style dominates.
  - If the main camera is in cutaway mode (mode 1), build the array with
    `VMArchitectureTools.GenerateRoomCut(blueprint, level, mainRotation,
    finalRooms)` — the same function `UILotControl.UpdateCutaway` uses — with
    the target's room (and its enclosing cut propagation) as `finalRooms`,
    mirroring `ComputeCutaway(targetLevel)` + `DoDynamicCutaway` around the
    followed object. Practical plumbing: the renderer needs the wall mode and
    the cut-rotation (both live on the main `WorldState`/`UILotControl`);
    thread them through `Render`'s caller (`UIOriginalPictureInPicture.PreDraw`)
    or read them from `Main` like `Main.Rotation` already is.
- Keep the `finally` restore exactly as is (it already restores the main
  floor's array, corresponding to the original's restore pass).

Behavior after the fix: a PIP of a target on another floor shows that floor
with the wall treatment the main camera would use for it (including cutaway
cuts and dynamic cuts around the target), and the main view's cut state is
never mutated — the full original mechanism minus its global-singleton
aliasing, which the port deliberately avoids (null invalidation owner,
comment at lines 80–82).

Full affected behavior: secondary-view wall appearance for cross-floor
targets in cutaway wall mode; no change for same-floor targets, walls-up, or
walls-down modes.

### 5b. Open/close fade parity (F2)

File: `Client/Simitone/Simitone.Client/UI/Panels/UIOriginalPictureInPicture.cs`.

Add a PIP-local equivalent of the original's window fade (the port has no
window-manager ramp):

- Add `double FadeAlpha` (0..1) and an `open`/`closing` flag driven by the
  existing `Clock`.
- On open (`Handle`, after line 103's `Visible = true`): start a 333 ms ramp
  0→1 (the original's default ramp length) and draw in `Draw` (line 273) with
  `DrawLocalTexture(..., Ink * Opacity)`-style alpha = `FadeAlpha` applied to
  the image and caption (the class already multiplies caption ink by
  `Opacity`, line 368).
- On every close route (`Hide()` — timer expiry line 240, ESC line 241, close
  button via `Close()` line 232, click-recenter `ActivateTarget()` line 233):
  play the ramp 1→0 first and only then set `Visible = false`; keep the
  current semantics that the target/`Request` bookkeeping happens immediately
  (the original clears `+0xd0`/`+0xde` synchronously and lets the window
  system finish the ramp; re-open during a fade must win, as `Open`'s
  ownership path already does).
- Gate the fade on the BoboVision-equivalent setting only if the port adds
  that option; since the original's factory default is ON (verified), the
  faithful default is to fade.

Full affected behavior: PIP appear/disappear animation on open, timer expiry,
ESC, close button, and click-recenter; during the fade input still routes
(command routes were never gated in the original) and an arriving `Open` for
an owned window still re-targets.

### 5c. No change needed

- Live/static repaint gating (F3) and all event/timer/ownership/snapshot
  semantics — already at parity.
- `GetWantsFadeCapture=false`: relevant only to the original's screen-level
  gamma-fade capture; the port's screen switches dispose the PIP outright, so
  there is nothing to capture.

## 6. Remaining uncertainties

1. **BoboVision persistence vs factory default** — the factory default is
   verified ON (`RestoreFactorySettings`), but `cOptionsMgr` streams options
   to the save file (`DoStream` `0x2390e0`); whether a stock install's options
   file typically carries the byte, and whether the shipped options UI exposes
   the toggle under a friendly name, was not traced. If the owner's reference
   runs used a toggled-off file, the instant route would be the observed
   original behavior for them. The proposed fix therefore treats the fade as
   default-on but isolates it behind one flag.
2. **Exact room selection inside `ComputeCutaway(targetLevel)`** — the
   per-room iteration and 2-bit floor field are verified, but the inputs of
   `sub_125130` (per-room cut decision: line-of-sight from the view center,
   outside-ness, tracked room) were not fully derived. The proposed fix reuses
   the port's `GenerateRoomCut` with the target's room; if image review shows
   the original cuts a different room set on cross-floor PIPs, only that
   `finalRooms` choice needs adjusting.
3. **Ramp units and shape** — 333 is verified as the default ramp parameter;
   that TimeBase ticks are milliseconds, and that the ramp drives window
   alpha (vs a wipe), is inferred from the float interpolation endpoints and
   the window blit path, not from a rendered frame.
4. **Null-target virtual slot (`vtbl+64`) in `BuildImage`** — presumably the
   viewer's `GetLevel`, matching every other consumer, but the viewer
   vtable's slot 64 was not resolved to a named method (no-target PIP renders
   are an edge case the port rejects earlier via `Valid`).
5. **`this->0xde` as second argument of the animated route** — the PIP passes
   its open-flag byte where `ShowTileInfo` passes 0; the parameter's exact
   meaning in `cTSWin` (leave-visible vs style) is not derived and does not
   affect the proposed port behavior.
6. Virtual-slot identities (156/160/176) rest on the cross-class call-site
   statistics plus the two named consumers (`Fade`, `ShowTileInfo`), not on a
   loader-resolved cTSWin vtable (PEF vtable words are load-time relocated;
   the exploratory packed-data reconstruction used for this was removed from
   this directory to keep proprietary binaries out).
