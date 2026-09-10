# R243 PIP transition contract — independent original recovery

All addresses are file offsets into the owned original executable, SHA256 `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`. PEF unpacking was in memory only. This directory contains derived disassembly, metadata and an offline fixture; no original binary/assets. No maintained code, build, app launch or git operation by this reviewer.

## Decision

The evidence supports implementing a **PIP-local original window transition state machine using existing TS1InterfaceFX**, including native no-capture paint threshold, logical visibility/hit eligibility until closing completes, exact repeated-request/reversal rules, and independent timer/target ownership. It does **not** support introducing a smooth image/child alpha fade: the normal Mac PIP path skips intermediate pixels instead of blending them. This is an original executable behavior, not a visual observation of a running original Mac application; managed scheduling remains an explicit adaptation.

## Existing option identity

`TS1InterfaceFX` is the existing fourth desktop Graphics checkbox and should be used; do not add a second BoboVision setting. Original Graphics builder `0x284ca0` builds caption slots6..9; popup resource table TOC `-0x5008` points to code `0x59b660`, whose index9 is resource3714 (`PopupOptTransUI.bmp`, r142 resource inventory). Its checkbox loop12..15 at `0x285770..7d4` stores four widgets at options offsets108/10c/110/114. UpdateViewFromCPState reads fourth checkbox +114 and supplies GetBoboVision (`0x281404..20`). TSOnCommand toggles Get/SetBoboVision at `0x281a38..4c`. The existing maintained checkbox uses STR145 indices23/24/25, that same popup member, and TS1InterfaceFX. Factory BoboVision is true (`0x238068..6c`).

## Fields, clock and ramp

Window fields: `+ac` signed phase (0 idle, +1 opening, -1 closing), RampGenerator `+b4` (target float +0, absolute slope +4, signed slope +8, end time +c = window+c0), `+c4` whether a private buffer existed before fresh transition, `+c5` force-paint-during-transition, `+c8` optional captured background. PIP owns its independent target+d0, duration+d4, zoom+d8, live/open flag+de.

Time is **milliseconds**, now independently resolved. `timeGetTime` file18220 calls GetTimebase, multiplies by the calibrated scale and converts to unsigned integer. Calibration18340 waits500000 units of imported **Microseconds**, then sets scale `500.0 / elapsedTimebaseTicks` (`0x183f8..18404`). Import proof: thunk59f7f0 loads TOC-7ab4 = data54c; initial PEF loader relocation operations `[4add,60df,4a51,6133,4ae2]` map that word to import341, whose name is Microseconds. The offline fixture checks this. TimeBase_Sims.SetNow14ec90 consumes timeGetTime and may cap its advancement using TimeBase max-delta state; a port monotonic-millisecond clock is a scheduling adaptation, not a literal emulation of that global clock.

`SetupConstantTimeRamp`14e890 stores target=end, slope=(end-start)/duration and endTime=now+duration. `GetVal`14e930 returns target once now>=endTime, otherwise target-(endTime-now)*slope. Float arithmetic is single precision. GetOpacity502770 returns0 when visibility bit1 is absent; with ordinary flags and an active phase it returns `byte(truncate(255*value+0.5))`; otherwise255. Constants `[0.5,255,1,0]` are at file5a50a0. The direct fixed-transparency flag paths precede ramp evaluation, but normal PIP construction does not set them.

## AsyncShowHideWin state machine

Entrypoint502e10 receives `(show, forcePaint, duration)`, substitutes333 when duration=0. Before deciding a request, it finishes any expired active transition even if no frame has yet completed it: closing calls virtual HideWindow, sends completion message27 to its parent with the signed phase, clears phase/capture, and removes a private buffer only if this transition originally created it (`+c4==0`). Opening completion leaves shown state intact. BlitPrivateBufferToParent also finishes transitions as part of drawing at `0x506330..6420`.

After that expiration cleanup:

| Prior state | Request | Original action |
|---|---|---|
| hidden, idle | hide | no-op |
| shown, idle | show | no-op |
| opening | show | no-op; no restart |
| closing | hide | no-op; no restart |
| hidden, idle | show | set forcePaint, preserve/create private buffer, phase+1, ramp0→1 for D, ShowWindow immediately |
| shown, idle | hide | set forcePaint, preserve/create private buffer, phase-1, ramp1→0 for D; keep visibility bit until completion |
| opening, current value v | hide | phase-1, ramp **v→0**, duration `truncate(0.5 + D*v)` |
| closing, current value v | show | phase+1, ramp **(1-v)→1**, duration `truncate(0.5 + D*(1-v))` |

The last row is an **asymmetric native quirk**, not a typo. At502fb4 GetVal supplies v, 502fd8 computes1-v for duration, then a second GetVal at503004 and `fsubs f1,f2,f1` at503018 supplies **1-v** as the new start, end1. Do not silently replace it with a smooth v→1 reversal. For v=.75,D333, reversal starts.25 with83ms duration. Opening v=.25 reversed to close starts.25 with83ms duration. The float-to-unsigned helper591720 truncates after the explicit +.5. A rounded zero duration has endTime==now and GetVal immediately returns the target.

Repeated/reversed requests return through branches before the fresh-transition `+c5` assignment at503110. **The old forcePaint flag is retained on reversal**, even when the new caller supplies a different flag.

## Normal Mac PIP drawing: no-capture threshold

Resolved PIP vtable data587d8, from constructor TOC-27044:

- +9c -> ShowWindow5029d0; +a0 -> PIP HideWindow298dc0.
- +b0 -> AsyncShowHideWin502e10; +b8 -> GetOpacity502770.
- +bc -> PIP GetWantsFadeCapture298c90, which returnsfalse.
- +d8 -> BlitPrivateBufferToParent506200; +17c -> PrivateBuffer505840.
- +180 -> Plot5065b0; +184 -> default PostChildDraw506570.

Flag/ownership trace closes the default case: base ctor509610 loads flags0x13 (data74208), initializes c8=null. SetParent508e20 only assigns parent fields48/50. PIP Init29a21c calls PrivateBuffer(true,false), which allocates/resizes buffer58 but does not set the window transparency flags. SetOverlapsScrollArea503820 adds only flag4000. Scene creation21946c..94dc adds the child, sets area, hides it; no transparency-flag override. Async show/hide changes visibility only. Normal PIP therefore has neither `(flag8000 && flag8)` nor flag80000, the fast-alpha conditions at506258..629c.

Plot calls GetWantsFadeCapture while phase is nonzero at50661c..6640. Returningfalse skips allocation/copy of capturebuffer+c8. It still paints the private window buffer, then children, before calling the whole-buffer blit at506c1c. This flag affects the ordinary window fade, contrary to the R241 helper's claim that it only concerns gamma transitions.

In the normal generic blit path:

- `alpha>>3 == 31` at506424..6430 copies the private buffer **opaque**.
- Otherwise intermediate blending requires nonnull captured buffer+c8 at5064a8..64b0. With PIP's null capture it skips output.
- At fade completion the blit enforces alpha255 for opening or alpha0 for closing and hides the closing window.

Thus the normal PIP paint predicate is **alpha>=248**. It applies to the composed image/caption/close-button together. There is no partial image/child-alpha output on that default path. Under a fully redrawn parent, an opening333ms transition first copies at integer elapsed324ms; a closing transition copies through9ms and stops at10ms. Logical visibility lasts the entire closing333ms interval. The fixture pins the instruction conditions and provides independent arithmetic examples; it does not claim a native GPU capture. Parent damage/retained pixels in the original can affect exactly when stale already-painted pixels disappear; the maintained redraw model should gate the complete PIP drawing, without mutating unrelated main-view rendering.

## Second argument / repaint

The second AsyncShowHideWin argument is written to+c5 at503110 for fresh transitions. Plot uses it at506618 when the normal private-buffer dirty predicate is false during a phase. This controls **repainting**, not hit-testing or final visibility. PIP passes its live/open byte+de in both Fade branches (2998bc /2998e0). Explicit Close clears that byte before starting the close; timer/ESC/recenter preserve it. TSPaint29a084..a4 itself rebuilds only when +de and LivePIP are true; it dirties the child close button and the PIP afterward. Preserve a frozen last image for explicit close; do not dispose the texture merely because target bookkeeping cleared. A repaint-only flag need not produce a visible result below the no-capture threshold.

## Input eligibility and timer ownership

Mouse hit-testing is now closed for the PIP window: GetWindowFromPoint51e480 calls recursive GetChildWindowFromPoint507b20. Child eligibility is GetFlag(1) at507b94..bac, independent of phase/opacity. PIP slot78 resolves screen-point testing cbb50 -> slot7c local rectangle testing cbc40. That rectangle method checks only bounds and optionally flag200 color-mask hit-testing, not the fade value. Normal PIP lacks flag200. Consequently its rectangle and close child remain eligible from immediate ShowWindow through actual closing completion, even when no pixels are copied. Keep registered visibility and paint eligibility separate.

HideWindow clears visibility and moves focus only if the PIP itself held it (`0x5028a0..2990`); before that completion, fade does not change focus. PIP's Escape handler299580 has no phase guard. This supports preserving existing key routing while shown; a new unconditional global Escape interceptor would exceed this finding. The full general focus/key-delivery policy is not reimplemented by this work.

Timer/ownership are independent of paint and registered visibility:

- Open rejects a different target whenever duration+d4!=0 (299aa0..ac0), with **no Visible condition**.
- Open installs duration at299b60 before skip-if-visible can return at299c38. A hidden skipped request can therefore hold duration ownership; a same-owner subsequent Open unsubscribes and clears old duration before replacing it.
- Open subscribes a timer only for positive duration; negative duration owns without a timer.
- Timer expiry clears duration first, then Fade(false). ESC/recenter only request the hide; their duration remains until actual PIP HideWindow298dc0 clears it. Explicit Close and close-button command clear target/live flag, request Fade(false), then unsubscribe/clear duration synchronously.
- PIP HideWindow clears duration/timer, **retains target**. Do not equate hide with full owner/target reset.

## Effects option changes during an active transition

Fade reads BoboVision/TS1InterfaceFX on each request. If false, it calls direct ShowWindow/HideWindow. Those routines do **not clear phase/ac or ramp fields**. Direct PIP HideWindow still clears duration/timer and visibility, retaining target. An already active ramp can therefore remain dormant while hidden. A later direct show can expose that old ramp state; the visible draw path then completes it if expired. A subsequent effects-on Async request first executes its expired-phase cleanup even while hidden.

Do not reset the ramp merely because the option becomes false: exact behavior is the direct operation plus existing active state. A hard lifetime reset on disposal is appropriate for the port; reusing a disposed window with stale transition state is not required. Merely toggling the option without a PIP show/hide request does not cancel an existing transition.

## Minimal implementation and verification

Use independent fields for logical visibility, transition phase/ramp timing, force-paint, current target, duration ownership and positive timer deadline. Update/finish visible transitions before UI input as a disclosed managed scheduling adaptation; process expired phases when an async request arrives even when hidden. Gate the entire Draw by the native alpha bucket, leaving logical Visible true until actual hide. Retain the last image until disposal/replacement. No new general UI opacity rule, shared fade framework change, setting, or wall-cutaway change is justified by this contract.

Required focused cases: normal show/close boundaries323/324 and9/10ms; hit eligibility while paint is skipped; same-direction no restart; both reversal rows including asymmetric .75→.25; positive expiry shorter than fade; negative ownership; hidden skip-if-visible ownership; ESC versus explicit Close timer/target differences; effects-off mid-ramp hide/show and reenabled async expired cleanup; disposal clears all port-owned state. Actual production captures must confirm that the gate covers image, caption and close button together without altering the main camera. No native Mac visual execution was performed; report that limit honestly.

`fixture_r243.py` passes20 instruction pins,10 resolved virtual-slot pins, constants/default flags, option popup identity, Microseconds import/calibration, and source-derived timing examples. Fixture evidence is distinct from later managed GPU tests and from the unexecuted original UI.

## First maintained implementation review

Read `UIOriginalPictureInPicture.cs` and `AutotestPIPFade243.cs` after root implemented the first version. Sent three concrete corrections before packaging:

- Timer expiry and explicit Close were retaining OwnerDuration until fade completion. Original timer expiry clears before Fade(false); explicit Close clears synchronously after it. Only ESC/recenter retain duration until actual hide. The first test incorrectly asserted timed ownership through the closing333ms interval, so that expectation also requires correction.
- Effects-off Hide can leave a hidden active-opening ramp. An effects-on Show before expiration must still take the native same-direction no-op at502f80..94, regardless visibility. The initial maintained guard only treated visible opening as a no-op and incorrectly restarted this hidden opening.
- Explicit Close immediately clears Request. The initial Update reissued Hide every frame because Target was null; turning effects off during that closing period then ended it immediately. Original changing the option alone does not cancel a ramp. Invalidation must not repeatedly request Hide while already closing.

Additional validation request: retain a distinct ESC-with-duration ownership fixture, immediate new-owner acceptance after timer/explicit Close, hidden same-direction effects toggle, and option-only toggle after explicit Close. The source-backed no-capture gate, FMA ramp arithmetic and child-inclusive GPU comparison otherwise follow the recovered contract. No implementation edit or test execution was performed by this reviewer.

## Final maintained source review

Re-read the final production diff and focused fixture after root applied all three corrections. Timer expiry now clears deadline and duration before starting the fade; explicit Close clears them synchronously; ESC and image activation preserve ownership until actual hiding. Same-direction opening requests now remain no-ops while an effects-off direct hide has left the opening ramp dormant. Invalid-target cleanup no longer reissues Hide during an existing close, so changing the option alone preserves that close's original deadline.

The added cases independently distinguish these routes and cover a hidden skipped request using a current-floor, single-member object with a validated nonempty damage rectangle. The first packaged run was reported by root as 16 passing checks and one failure in the earlier multi-part-object skip fixture; its 18 GPU comparisons and other transition assertions passed. The revised fixture explicitly establishes the single-object precondition and its packaged rerun is pending at this review. This is root-reported execution evidence; this reviewer performed no build or game launch.

No additional high-confidence production blocker was found within the bounded fade/ownership scope. The managed update/pre-draw/draw scheduling and full-parent-redraw behavior remain adaptations described above. The reflected close-button mouse invocation validates its registered handler, and `WillDraw` validates logical eligibility; together they do not constitute an end-to-end coordinate dispatch through the input manager. Original hit eligibility is independently supported by the native call chain. The implementation deliberately introduces no wall-cutaway correction, smooth per-child alpha, or generalized fade behavior.
