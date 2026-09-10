# R242 independent camera handoff and CAS transparency review

Scope: read-only maintained-source review plus offline inspection of the owned original executable. No build, game launch, asset modification, staging, or production changes by this reviewer. Root owns the CAS correction. This report contains derived findings only; `r241-helper-camera/sec1.bin` and `data-secs.bin` are proprietary intermediates and must not be included in a source commit/package.

Handoff timing: `r241-helper-camera/report.md` was absent at the initial review and appeared afterward. The final handoff was then read in full; its assessment is appended below. Both proprietary intermediates were present at initial inspection and absent on the follow-up check. This reviewer did not delete or stage them.

## CAS: approve the bounded RGB/alpha correction

The existing `UICASOriginalScreen.CutHole` wrote only alpha zero. The production UI uses premultiplied `BlendState.AlphaBlend`: source RGB factor One, destination factor InverseSourceAlpha (mounted `FreeSO/Other/libs/FSOMonoGame/MonoGame.Framework/Graphics/States/BlendState.cs:236`; `FSO.UI/UILayer.cs` uses this state). Consequently pixels with nonzero RGB and zero alpha still add their old background RGB over the 3D view. Replacing the existing assignment with `Color.Transparent` correctly zeros all four channels. It also removes RGB contamination when fractional-DPI linear sampling interpolates the hole boundary.

Blast radius traced:

- Only production call is `UIOriginalDesignChar` in `UI/Panels/CAS/UIOriginalCAS.cs`, guarded by background dimensions 800x600, using unchanged `VITA_RECT = (618,145,100,220)`.
- This is **not a private clone**: `UICASOriginalScreen` obtains `UIOriginal.ResolveOrPng` -> cached `EnsureResolved` `ITextureRef` -> `AbstractTextureRef.Get`, which returns its cached `_Instance`. `CutHole` calls `GetData`/`SetData` on that texture in place. All concurrently retained/repeated DesignChar screens therefore share the correction. Searches found no other production consumer of `CreateACharBack.BMP`; the other direct name references are audit/catalog metadata.
- The correction remains idempotent, creates/disposes no textures, changes no source BMP bytes, and leaves every pixel outside the existing rectangle and all control geometry unchanged. No additional clone or ownership change is necessary for this one-caller fix. Future reuse of the raw background must account for this pre-existing cache mutation.
- The rectangle is positive and in bounds. Generic negative-rectangle handling in the helper is outside this change.

Recommended root verification: actual background readback verifies RGBA=(0,0,0,0) throughout the hole, exact preservation outside it, and a Create-a-Character GPU capture over a known contrasting avatar/backdrop at both 1x and fractional DPI. This reviewer did not run these GPU checks.

## Camera handoff reliability

Independently reran `python3 tools/iff-dump/r241-helper-camera/fixture_r241.py`: **43/43 PASS**, including original executable SHA-256 `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`. These are byte/call-location assertions; they are not renderer, timing, visual, or complete behavior tests.

Two handoff defects matter when using its text:

- Its custom decoder labels conditional branches incorrectly: raw `4182` is `beq`, although generated text says `bne(2)`; raw `4082` is `bne`, although generated text says `beq(2)`. Critical level/owner guards below were independently decoded with Capstone through `r145/cdis.py`. Read instruction bytes, not those branch labels.
- The fixture's unused address-map entry for `Open` is `0x299960`; actual symbol start is **file `0x299a60`** (`r141/symbol-index.txt` symbol address `0x299a62`, minus two). Labels saying “tail-calls” overstate ordinary `bl` call checks. No failed byte assertions resulted, but neither description should be copied as precise disassembly.

All addresses below are original executable file offsets.

## Settled camera contracts and maintained gaps

### Floor and cutaway

`HouseViewer+0x18` is the **floor/level**, not rotation: `GetLevel` at `0x1bfa80` returns that field, and `SetLevel` writes it at `0x1d7178`. `CPState+0x2c` similarly stores level (`0x210190`, getter `0x210fe0`). Therefore the DrawPictureInPicture comparison at `0x1c1048..50` is a level-change guard.

When requested level differs, native `DrawPictureInPicture`:

1. `0x1c1054..68`: gets WallManager, calls `SetCutaway(false, oldLevel)`, clears viewer's cCutawaySet.
2. `0x1c1074..84`: conditionally calls `ResetDynamicCutaway` when viewer byte `+0x49` is nonzero.
3. `0x1c1088..a0`: installs requested level then calls `RoomManager.ComputeCutaway(requestedLevel)`.
4. `0x1c10a4..b8`: under the same `+0x49` condition calls `DoDynamicCutaway(viewer+0x1f8)`.
5. `0x1c15fc..166c`: repeats the corresponding clear/recompute sequence when restoring the saved level, with the saved level passed to ComputeCutaway at `0x1c1654`.

The level-change block is skipped when equal (`beq 0x1c10ec` at `0x1c1050`). Earlier/later rotation-setting calls are separate and must not be confused with this condition. Native `WallManager.SetCutaway` also recomputes wall vertices/second-pass cutaway; it is not just a boolean array assignment (`wallmgr-setcutaway.txt`, `0x2c2f90..3090`).

Maintained `WorldPictureInPictureRenderer.Render` instead temporarily replaces `Blueprint.Cutaway` with a fresh all-false array for another floor, then restores the old array in `finally`. This protects main-view state, but does **not** implement the native room/dynamic recomputation. Same-floor views reuse the main cutaway; different-floor objects bypass the main `CutawayHidden` filter. The helper establishes this residual conclusively; it does not establish a correct managed substitute. Do not change this to guessed blanket walls-up/down, or mutate the main room/wall state without independent restoration checks. A restoration requires mapping native static/dynamic cutaway inputs to maintained wall/room algorithms and validating same/different floors, all rotations, wall modes, and main-view state before/after.

`BuildImage` loads target level from `target+0x110` at `0x299278`, forwards requested zoom from `window+0xd8` at `0x2992b0`, then calls DrawPictureInPicture at `0x2992c8`. Its null-target fallback is a virtual call at `vtable+0x40`; this review has not resolved that object's exact interface. Do not assert it is a particular getter solely from the slot number. `ScrollToTile(tile, level, false, true)` at `0x1c1188` is separately pinned.

### Fade and lifecycle

`cWinPictureInPicture.Fade` (`0x299860`) reads `GetBoboVision` at `0x299880`. BoboVision is options byte `+1` (`0x23a830/0x23a870`), initialized true by RestoreFactorySettings at `0x238068..6c`.

- Enabled: `0x2998b8..d0` / `0x2998dc..f4` call `AsyncShowHideWin(show, window+0xde, 0)`. The final zero selects its **333 ms default** (`0x502f98..a0`). It drives window RampGenerator `+0xb4`; the full helper body has interruption/reversal handling, not just independent one-shot delays.
- Disabled: direct ShowWindow/HideWindow virtual calls (`0x299934` / `0x29994c`).
- PIP `GetWantsFadeCapture` returns false at `0x298c90`, versus base true at `0x5023d0`. It therefore must not inherit a guessed frozen-background capture effect. This getter alone does not prove every detail of its private-buffer compositing or per-frame input eligibility.
- On open, a non-live image can be built before showing (`0x299908..28`, and equivalent animated path). Ongoing TSPaint image rebuilding is gated by `+0xde` and GetLivePIP (`0x29a084..a4`). This window-level ability does not by itself remove the separate original primitive/dispatcher LivePIP eligibility law recovered in R240.

Maintained `UIOriginalPictureInPicture` sets `Visible=true` immediately, and `Hide` sets false/clears Deadline immediately. No BoboVision setting or fade state exists in that class/current settings search. **Missing native fade is verified.** The 333 ms routing is sufficient to justify continued bounded implementation work, but this handoff is not a complete alpha/interruption/input contract. Before claiming exact fade parity, derive opacity evaluation, initial/terminal visibility, reversal and buffered-composite behavior from the cTSWin Plot/GetOpacity path; reuse existing tutorial compositing only after confirming its semantics apply.

Other lifecycle claims are substantially supported: duration nonzero rejects a different owner at `0x299aa0..c0` (no visibility test in this native guard); the same owner unsubscribes/clears the old duration at `0x299ac4..d4`. Native HideWindow override `0x298dc0..e0c` unsubscribes and clears duration. Timer (`0x299600`), Escape (`0x299580`), and image click (`0x299730`, main recenter then Fade(false)) are hide/fade routes. Explicit owner-matching Close (`0x2999b0`) and close-button command (`0x299fa0`, guarded by `+0xe4` control presence) also clear target and live flag. The maintained Hide versus Close distinction broadly preserves that ownership distinction; animation timing remains absent. Do not remove the maintained Visible guard in isolation without matching the native duration clearing at completed hide, particularly once a fade is introduced.

## Disposition

Approve the one-line CAS Color.Transparent correction with root's focused visual/readback validation. Camera helper is useful, but incomplete: it proves floor-dependent cutaway recomputation and an enabled-by-default 333 ms fade are outstanding; it supplies no visual comparison or ready managed implementation. Keep both residuals explicit until separately implemented and verified. No camera production change was made by this reviewer.

## Follow-up: final helper report received and independently checked

The final `r241-helper-camera/report.md` supports the two main residuals above. Its proposed implementation is **not yet an original-backed patch contract**. Specific corrections and limitations:

1. **Dynamic-cutaway branch orientation is contradictory.** F1's initial SetLevel sequence says `if !viewer->0x49`; its later paragraph says the flag is set. The latter is correct. Raw `4182000c` at `0x1d716c` branches over ResetDynamicCutaway when the byte is zero; raw `41820010` at `0x1d719c` similarly skips DoDynamicCutaway. The identical PIP guards at `0x1c107c`/`0x1c10ac` are also `beq` skips. Both calls run only for **nonzero +0x49**. These guards were independently decoded with Capstone, not inferred from the helper's mislabeled branch strings. Also, restore stores the saved level at `0x1c163c` **before** ComputeCutaway at `0x1c1654`, contrary to the ordering of one sentence in F1.

2. **Target-room cut selection is unproved.** Proposed section 5a selects `finalRooms={targetRoom}` and propagation, but its own uncertainty 6.2 says the native per-room cut decision is not derived. The verified input to ComputeCutaway is the floor, not a target-room argument. The maintained desktop main-camera algorithm uses recent hovered rooms, a mouse cut rectangle, and CutRotation; target room is not interchangeable with those inputs. Its example call also takes `VMArchitecture`, not `Blueprint`, and the maintained call uses `World.State.CutRotation`, not simply `Main.Rotation`. Reusing GenerateRoomCut is a possible implementation mechanism after native inputs are recovered; selecting target room now would be a guess. The report's statement that all-false cross-floor cuts are invisible in walls-down mode is likewise not established: maintained walls-down is explicitly encoded as an all-true Blueprint.Cutaway array by UILotControl.UpdateCutaway. There is no independent wall-style dominance proof in this handoff that makes replacing it with all-false harmless.

3. **Per-child fade is not demonstrated and can produce the wrong composite.** PIP Init calls PrivateBuffer(true,false) at `0x29a21c..30`. AsyncShowHideWin also creates a private buffer when needed (`0x503114..140`). The existing independently recovered cTSWin Plot law paints the window and children before BlitPrivateBufferToParent (`r241-tutorial-evidence/window-plot.txt`, `0x506890`, `0x5069d8..a10`, `0x506c0c..1c`). Applying alpha separately to the image and caption is not equivalent to applying alpha once to the composed window; caption pixels overlapping the image would have a different background contribution. Proposed 5b also omits how the close-button pixels receive the same fade. Require a native buffer/composite contract and an overlapping-pixel GPU oracle before adopting that proposal.

4. **GetWantsFadeCapture is not limited to gamma transitions.** The final report asserts it is relevant only to screen-level gamma fades; that claim is contradicted by the normal window Plot path. PIP's actual vtable was independently resolved from unpacked PEF data in memory (no binary written): TOC `-27044` points to data vtable `0x587d8`. Slot `+0xbc` resolves through descriptor `0xfc78` to file `0x298c90`, the PIP GetWantsFadeCapture override. cTSWin Plot checks nonzero window fade state `+0xac` at `0x50661c..24`, calls this slot at `0x506630..34`, and skips the local capture-buffer allocation/copy block if false (`beq 0x506804` at `0x506640`; buffer `+0xc8` thereafter). Thus this flag directly affects ordinary animated-window painting. Its exact no-capture intermediate blend behavior still needs tracing; it cannot be dismissed because the port disposes PIP at screen changes.

5. **Second AsyncShowHideWin argument is materially used.** The helper uncertainty 6.5 says its unresolved meaning does not affect the proposal. The argument is saved to window `+0xc5` at `0x503110`; during animation, Plot uses it at `0x506618` when its ordinary repaint predicate is false, immediately before the image/child drawing decision. It therefore influences repaint scheduling during the fade. It is not evidence for an input mode, and cannot simply be ignored for a faithful live/static fade.

6. **Input during transitions remains unproved.** Ungated TSOnCommand/TSOnKeyDown handlers do not establish that the window manager hit-tests or dispatches to them throughout animation. The helper did not trace those upstream paths. Its proposed unconditional interactive fading and claim that the gap is only cosmetic are too strong. Opening uses ShowWindow immediately (`0x503170..17c`) and closing calls HideWindow at completion (`0x5063c8..dc`); those are useful visibility facts, but insufficient to prove full mouse/key routing eligibility. Reversal also needs current ramp value/remaining duration, rather than resetting every open to zero and every close to one; AsyncShowHideWin contains explicit in-flight branches (`0x502fa4..3094`).

7. **Vtable identities can now be considered closed.** The same independent lookup gives `+0x9c -> 0x5029d0` ShowWindow, `+0xa0 -> 0x298dc0` PIP HideWindow, `+0xb0 -> 0x502e10` AsyncShowHideWin, `+0xb8 -> 0x502770` GetOpacity, `+0xd8 -> 0x506200` BlitPrivateBufferToParent, and `+0x17c -> 0x505840` PrivateBuffer. These are actual descriptor resolutions, closing helper uncertainty 6.6 rather than relying on call-site statistics. The data can be rederived with `r176/r176-expansion-category-decode.py`'s in-memory unpacker; code descriptor values add file base `0x8e90`.

8. **The ramp does feed opacity.** GetOpacity reads RampGenerator at `0x502828..34`, then multiplies by 255 and adds 0.5 (`0x502838..44`, truncation to integer). Constants at file `0x5a50a0`, reached through TOC `-0x4414`, are `[0.5,255.0,1.0,0.0]`. Opening setup uses 0-to-1, closing uses 1-to-0 (`0x503154..1a4`). Thus an opacity ramp is stronger than the helper's “alpha versus wipe” uncertainty suggests. This does not on its own settle the full no-capture/compositing/input behavior noted above.

The final report still labels Open start `0x299960` rather than `0x299a60`, and overstates the 43 instruction fixtures as rederiving “every law.” In particular those fixtures do not establish target-room selection, window-manager input, per-child alpha, or screenshot parity. Its broad “all event/timer/ownership/snapshot semantics already at parity” statement exceeds this assignment's evidence and should not be imported into project release claims. Final disposition is unchanged: implement the bounded CAS correction, preserve the camera residuals, and continue native contract recovery before approving either proposed camera patch as exact restoration.

## CAS bounded signoff after root's packaged checks

Root reported the R242 focused package passed **16 checks / 0 failures**. The new GPU oracle draws the actual production background over a varied underlay and finds **0 / 22,000 changed underlay pixels** inside the existing 100x220 preview hole. Root's before/after comparison (`cas-before-after.json`) reports **21,960 changed pixels inside the hole and zero changed pixels outside it within the 800x600 CAS artboard**. These results directly address the premultiplied RGB defect and the proposed change's bounded visual footprint.

On that evidence and the independent source/blend-state/cache review above, this reviewer signs off the one-line CAS transparency correction. The figures in this section are root-reported; this reviewer did not execute the GPU checks. The capture is an isolated survey rendering over another screen, **not a live Create-a-Character Sim interaction test**. It therefore does not establish complete CAS rendering/interaction parity. No camera patch is included or approved by this signoff; its unresolved contracts remain explicit above.
