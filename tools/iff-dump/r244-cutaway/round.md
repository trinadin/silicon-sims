# R244: view-specific cutaway — native projected wall masks, dynamic composition, PIP mask ownership

The maintained port now computes wall cutaway the way the original Mac
executable does: per-room masks are projected wall-extent overlap geometry in
the rotated view frame, and the drawn mask is the dynamic composition of the
three-room hover history, the live-mode selected/tracked person's room (with
the decoded outside-person rectangle), and the r26-gated cursor vicinity.
Cross-floor picture-in-picture renders draw a real composition for the target
floor — walls up everywhere except the watched sim's room — replacing both the
old all-false mask and a rejected all-rooms intermediate. Same-floor PIP still
shares the main mask, exactly as the native secondary render sequence implies.

## Decode phase (with decode → skeptic → repair provenance)

Two independent read-only decodes resolve every item R243 left open. All
addresses are file offsets into the owner executable
(`game-data/The Sims/The Sims Complete`, SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`).

* [Geometry](../r244-cutaway-geom/decode.md) — `Room::ComputeCutawayMatrix`
  0x125130 (direction table at BSS 0x7F040 from static init 0x610B0; sweep
  candidates origin+{(0,+1),(+1,0),(+1,+1)} with origin +=(+1,+1);
  out-of-bounds-only termination counter; rect R0..R3 = px−h+1, py+1, px+h−2,
  py+h−2 with h = signed(viewer+0x58)/2; sides 1/3 → R2−=h / R0+=h; strict
  overlap `R2≥W0+1 ∧ W2≥R0+1 ∧ R3≥W1+1 ∧ W3≥R1+1`), `TileToPoint` 0x1D3850,
  `GetTheoreticalWallExtent` 0x1CEF30 (`W = px±V5C−1, py−F..py+V60`),
  `GetPackedAlt`/`HasWalls`, `ResolveDiagonal`, and the room+0x34 gate named
  with writer evidence ("room is live/in-use", values only 0/1).
  `verify.py` pins 132 instruction words + 9 fixture groups.
* [State](../r244-cutaway-state/decode.md) — the history driver (exactly two
  `cCutawaySet::insert` call sites, both in `HouseViewer::MouseTrack` 0x1D0310
  at phase==0, inserting the cursor room plus the behind-room probe walk;
  off-lot cursor = `ResetDynamicCutaway` **and return**; dup-preserving FIFO
  capacity 3), all 16 `clear` call sites with enclosing functions, the
  suppression predicate (`EdgeDetectScroller::IsWaitingForClick`, vtable +0x5c),
  the matrix callback being the CURRENT TOOL's secondary-interface slot 18
  (`AdjustCutawayForTool`; corrects R243's "strategy virtual" wording), the
  cursor vicinity (Y += 32<<zoom; corner = tile+(−4,−4); both-direction
  walk-ins; the r26 gate — cursor rect marked ONLY when ≥1 ORed room was
  outside; half-open [corner,tile) rect), the outside-person k=4..0 probe
  rectangle, CPState mode 2 = LIVE (binary-proven mode map), and person fields
  +0xAAC (room id) / +0x110 (floor). `verify.py` pins 72 words + 13 vtable
  chain links + 6 fixture groups.
* Skeptics re-derived every headline claim from the binary. The geometry
  skeptic's two material corrections (zoom-rescale direction C1; non-persistent
  global state C2) were applied; its third proposal (a 32-bit mirrored-bit
  "aliasing" law) was REJECTED by full simulation of the now-pinned helper
  0x591B10 body — it is a faithful 64-bit shift. Provenance:
  [skeptic-corrections](../r244-cutaway-geom/skeptic-corrections.md) +
  [skeptic-response](../r244-cutaway-geom/skeptic-response.md) (geometry) and
  [skeptic-corrections](../r244-cutaway-state/skeptic-corrections.md) (state).

## Implementation

Engine (nested `FreeSO`, branch `mac-port-rel`):

* NEW `TSOClient/tso.world/Utils/CutawayMatrix.cs` — `ComputeRoomMask`
  (native mask law in the port's own projection; per-zoom metrics
  h = V5C = V60 = half projected tile width — confirmed against native
  DoCommand 0xf6's `0x10/8/8/4<<zoom` resets — and story height
  `2.95·OneUnitDistance·cos(π/6)`), `ComposeDynamic` (history ∪ live-gated
  person branch ∪ r26-gated cursor vicinity), `ComputeCutawayRooms`
  (floor-filtered mask union), `CutawayMaskCache`, `Differs`,
  `NativeZoomIndex` (port PreciseZoom 0.5 @ Far = native zoom 0).
* `ObjectComponent.cs` — the cutaway tile test extracted into the static
  `ComputeCutawayHidden` (behavior-identical; PIP reuses it).
* `WorldPictureInPictureRenderer.cs` — new optional `CutawayViewInputs
  pipInputs` on `Render`; when present the PIP draws
  `ComposeDynamic` at the PIP's own view state (history cleared, tracked
  object null ⇒ selected person, native draw-input law: the per-room matrices
  are intermediates, never the draw base). Scoped swap + finally
  reference-restore preserved; no `WALL_CUT_CHANGED` from inside the draw;
  null-inputs paths byte-identical to R243.

Client (parent, branch `mac-port`):

* `UILotControl.cs` — `UpdateCutaway` rewritten onto the native laws: phase-0
  history driver with the behind-room probe walk (`dv = 4<<zoom` px, bound
  `29<<(zoom−1)` px), off-lot reset **and return**, floor-change clear
  (`NoteCutawayViewChange`; rotation alone does not clear — native DoCommand
  0xe4), live-mode person resolution (tracked = ScrollAnchor avatar, else the
  selected sim), commit-on-`Differs`, WallsMode 0/2/3 shortcuts byte-identical,
  touch path unchanged.
* `UIOriginalPictureInPicture.cs` — builds real PIP inputs (target floor, main
  rotation/zoom, empty history, live-gated selected person with tile, cursor +
  logical-unit buffer bounds).
* `AutotestRunner.cs` — purely additive: default check `uicutaway`
  (139 → 140).
* NEW `AutotestCutaway244.cs` — the `uicutaway` battery: per-room mask
  non-emptiness/localization, floor-change production lifecycle, history
  (insert/dup-no-recency/cap-3 evict/off-lot reset keeps history), person
  (indoor OR == room mask; other floor ORs nothing; outside k-probe exact 16),
  cursor (r26 half-open rect == roomMask ∪ rect; indoor/other marks nothing;
  outside-buffer/suppressed nothing), PIP (same-floor drives + restored;
  cross-floor no-live-person walls-up; person-room-localized; main mask
  reference+content restored; exception-path restore), and a localized
  world-grab pixel diff (31817/31817 changed px in band).

## Adversarial review and repair record

1. First code review (skeptic, read-only): verdict FIX-FIRST — 2 P0
   (battery asserted an engine floor-filter native does not have; client missed
   the native clear-history-on-floor-change idiom) and 5 P1 (probe step half of
   native `4<<zoom`; `PersonTile` never set so the outside-person rectangle was
   dead code; PIP person branch not live-gated; battery composed at the wrong
   default view; PIP mask base contradicted the draw-input law). All fixed.
2. First focused gate run caught 9 failures that review had missed. Root
   causes: a genuine engine bug (`Room.Floor` is the port's 0-BASED story
   index — `VMArchitecture.cs:333` passes the loop index straight into
   `GenerateMap`; `IsIndoorsPrecise` indexes `RoomMap[floor]` directly — so
   every ground-floor mask was empty), a genuine production bug (the off-lot
   reset was immediately defeated by recomposition), plus five battery bugs
   (floor-convention mix-ups, style-0 fixture walls, cached-render-target
   self-comparison, camera-coupled screen probes, an outside person where an
   indoor one was required). All fixed; two regression assertions added
   (`floor-change-*`, exact `roomMask ∪ rect` cursor law).
3. Second focused gate run: **PASS, 0 failed** (`focused-final.log`), and the
   full default suite follows in `default-final.log`.

## Validation

* Focused packaged gate `lot,corpus,uicutaway`:
  **AUTOTEST RESULT PASS passed=8 failed=0** at 2026-09-05 22:04 CDT,
  clean `after Run`/`after Dispose`, wrapper exit 0. Log: `focused-final.log`
  (288 lines).
* Packaged default suite: **AUTOTEST RESULT PASS passed=141 failed=0** at
  2026-09-05 22:12 CDT, wrapper exit 0, clean `after Run`/`after Dispose`,
  zero `SimAnticsExc`, zero `bad-routine-frame` (`default-final.log`), only
  the known self-recovering `splashprogress mount` startup exception.
* Baseline honesty note: the first pre-work baseline run showed the known
  `mood` timing-boundary flake (storedMood 72 vs computed 76 at minute 41);
  an unchanged rerun passed 139/139 before any R244 change was made (same
  pattern as R168).
* Publish/package carry the same `Simitone.Client.dll` SHA-256:
  `062e1f939affa5449d1623bbb02e2873421d9524adeadf2817a3415b3a7adc93`
  (`package-assemblies.sha256`). Visual evidence:
  `<UserDir>/ui-audit/r244/uicutaway-*.png` — cross-floor PIP without a live
  person keeps walls up; with the watched sim on the target floor exactly
  their room band is cut.

## Blast radius

Production changes are confined to `CutawayMatrix.cs` (new),
`ObjectComponent.cs` (extraction), `WorldPictureInPictureRenderer.cs`,
`UILotControl.cs`, `UIOriginalPictureInPicture.cs`, `AutotestRunner.cs`
(additive), `AutotestCutaway244.cs` (new). Wall/floor/terrain build tools,
`WallComponent`/`WallComponentRC` read paths, `BlueprintChanges` invalidation,
thumbnail/facade swaps, and the CAS cutaway fallback are untouched. No
proprietary asset is committed; original data stays read-only.

## Disclosed deviations and residuals

* The native tool callback (`AdjustCutawayForTool`, slot 18) is an unwired
  extension point: the port has no current-tool object during live play, where
  dynamic cutaway applies; the base implementation's grab-offset behavior is
  documented in the decode and remains unported.
* No `BuildMaxAltsTable` remap in the outside-person rectangle (identity; the
  port has no packed-altitude table).
* While RMB-scrolling or mouse-off, the port recomposes with the cursor
  suppressed (person/history masks stay live); native freezes the standing
  matrix including its last cursor rect.
* History clears on wall architecture changes (native clears only on
  DoCommand 0x105/0xf6/0xf0); conservative, avoids stale ids across
  re-partitioning.
* Tracked-nonperson falls back to the selected sim; native uses none.
* PIP cursor vicinity maps screen→tile through the main view's picker;
  native resolves in the target view frame.
* Diagonal half-room side-distinct masks and the strict-overlap exact-touch
  boundary have no constructible headless fixture (room-map flood merges
  diagonal-split interiors; integer projection cannot produce exact touches);
  both are decode-proven, opt-in residuals. The synthetic floor-2 diagonal
  fixture is verbose-only.
* Rotation uses the port's own rotated-frame mapping (the native LUT generator
  at BSS 0x7F0B8 remains UNRESOLVED); mask bits are consumed in world space
  with the native 64-bit shift's aliasing wart not ported.
