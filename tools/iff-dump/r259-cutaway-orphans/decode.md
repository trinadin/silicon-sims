# UI-25 — the four cutaway orphan residuals: decoded laws + bounded fix design

Desk decode (read-only; no source edits, no game launches). Owner executable
`game-data/The Sims/The Sims Complete`, SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f` (same binary as
R244). All addresses are executable FILE OFFSETS; TOC base = data+0x8000 (r143);
symbol addr = file offset + 2 (r145). Pins: `verify_ui25.py` (all pass,
`verify_ui25.out`); excerpts: `build-rotation-lookup.txt`, `build-max-alts-table.txt`,
`setsize-ctx.txt`, `setrotation-ctx.txt`, `pip-enter.txt`, `pip-restore.txt`;
scans: `scan_lut_users.py/.txt`, `scan_maxalt.py/.txt`.

---

## Item 1 — RESOLVED: the BSS 0x7F0B8 generator is `BuildRotationLookup__7CTilePtFv`

`BuildRotationLookup` (file 0x60f20..0x61070, symbol 0x60f22) is the long-sought
generator of the 4-page table at BSS 0x7F0B8 (TOC slot -0x73E4, r244-geom §A.6
UNRESOLVED 1). Instruction-exact law:

* Three pages (`r11` = 0..2, `cmpwi r11,3` at 0x61064), page pointer advancing
  `+= 0x2000` (0x61060). Page p holds the map for world rotation `rot = p+1`.
* Per page, a 64x64 double loop (`r10` = x outer, `r9` = y inner, unrolled 2x
  under `mtctr 0x20`), storing 2 bytes per entry at `x*0x80 + y*2`
  (`stb r8,0(r4)` / `stb r7,1(r4)` at 0x60fa8/0x60fac).
* Entry law (byte0 = x', byte1 = y'), branch tree at 0x60f5c..0x60fa4:
  * rot 1 (page 0): `(63-y, x)`
  * rot 2 (page 1): `(63-x, 63-y)`
  * rot 3 (page 2): `(y, 63-x)`
  * rot 0 or >= 4 would store identity (unreached: only 3 pages)
  * x >= 0x40 or y >= 0x40: `(0xFF, 0xFF)` (0x60fb4)
* Contents are a PURE function of the loop constants — no world data, no
  viewer state, no rand. Same output every run.
* Sole call site: `bl 0x60f20` at 0x164910, inside the world sizing path
  (`cFixedWorld::SetSize(int)` region 0x1648a0..; symbol index). Built once per
  world (re)build — matching r244's "built at runtime (world load)".
* Consumer addressing (r244-geom §A.6) now closes cleanly: forward map =
  effective page `rot-1` (ComputeCutawayMatrix 0x1251F8; outside-person remap
  0x1cf7d0); inverse map = effective page `((4-rot)&3)-1` (HasWalls 0x1259e4,
  GetPackedAlt 0x161e0c, and the other 80 accessor users in
  `scan_lut_users.txt`). With page p = rot(p+1): inverse of rot r is page
  3-r = map for rotation 4-r — the exact inverse rotation. Simulated:
  rot2 is an involution, rot1 ∘ rot3 = identity on 0..63 (verify_ui25.py).

The native matrix is indexed in the ROTATED frame; every world accessor rotates
its tile argument through this one LUT. The port's `RotMap`/`InvRotMap`
(CutawayMatrix.cs) are the same rotation family modulo the [0..63] translation:
native rot1 = (x,y)->(-y,x)+ (63,0) = port TopRight; rot2 = BottomRight; rot3 =
BottomLeft; rot0 = TopLeft. The port's world-space mask convention makes the
LUT itself unnecessary — only the comment "generator UNRESOLVED" is now wrong.

### Fix design (item 1) — documentation only, no behavior change

File `FreeSO/TSOClient/tso.world/Utils/CutawayMatrix.cs`, class-doc deviation
list (lines ~208-213) and `RotMap` doc (~552-561): replace "The native rotation
LUT generator is UNRESOLVED" with the decoded law above and the enum
correspondence (native rot 1/2/3 = TopRight/BottomRight/BottomLeft). No code
change: the port's mapping is proven same-family and self-consistent with its
projection. Evidence links: tools/iff-dump/r259-cutaway-orphans/decode.md.

---

## Item 3 — RESOLVED (identity corrected): there is no altitude table in the cutaway path

R244-state §4 conflated two distinct artifacts; the instruction evidence
separates them:

* **The outside-person rectangle remap (0x1cf7bc..0x1cf800) reads the ROTATION
  LUT, not a max-alt table.** Base `r22 = TOC-0x73E4` (loaded 0x1cf3c0),
  address = `base + (world+0x84 << 13) + (x << 7) + (y << 1) - 0x2000`
  (`slwi 0xd` at 0x1cf7d0). world+0x84 is the world ROTATION (0..3) — the same
  field r244-geom proved drives the LUT page and side-rotation terms — NOT an
  "altitude level count" (r244-state wording). The remap converts the person's
  world-frame tile (+0xfc/+0xfd) into the rotated frame with the FORWARD map
  (page rot-1), exactly like ComputeCutawayMatrix, because the native matrix is
  rotated-frame indexed.
* **`BuildMaxAltsTable__11HouseViewerFv` (0x1c6b70..0x1c6eb0) builds two
  unrelated 128-byte arrays at viewer+0x1EC / +0x1F0.** Law (fully traced in
  `build-max-alts-table.txt`): init arr1[..] = 0, arr2[..] = 64 (0x1c6b84..
  0x1c6c40); for A = 0..63, B = 0..63 gated to `0 <= coord < *(floorGlobal)->+0x10`
  (0x1c6c58..0x1c6cac): tile = {B + (byte)(world+0x70), A + (byte)(world+0x78), 0};
  `rec = GetPackedAlt(world, tile)` (bl 0x1c6d04); for each of the four record
  bytes: `arr[i] = max(arr[i], rec[k])` with **i = (sbyte)x + (sbyte)y — the
  DIAGONAL index** (0x1c6d28), first into arr1 (four maxima), then arr2 (four
  maxima). So: per-diagonal maximum of the four packed-altitude record bytes
  (the record is the tile's 4 corner heights; GetPackedAlt byte 1 is one
  corner, per r244-geom §C.2's permutation). Rebuilt by DoCommand 0x105
  (0x1d66a8/0x1d670c) and by SetRotation (bl at 0x1d74f4) — view-dependent
  render helpers. Consumers live in the HouseViewer draw/SetTileObjectID/CPState
  regions (0x1d325c..0x1d3404, 0x1cc7f8/0x1ccb04, 0x219140); **no cutaway
  function reads +0x1EC/+0x1F0** (negative scan in verify_ui25.py over
  ComputeCutawayMatrix, GetTheoreticalWallExtent, DoDynamicCutaway, MouseTrack).

### Verdict and fix design (item 3)

The port's identity remap in `AddOutsidePersonRectangle` (CutawayMatrix.cs
L524) is **behaviorally correct**, but for the right-by-accident reason: the
native remap is the rotation-frame conversion, which the port's world-space
mask subsumes — not a missing altitude table. Design: update the DEVIATION
comment (CutawayMatrix.cs ~L520-522) to state the rotation-LUT identity; no
code change. Do NOT add a BuildMaxAltsTable port — it is not a cutaway input.

---

## Item 2 — decoded (R244 base + this round's confirmation); bounded wiring design

Instruction law re-pinned (`verify_ui25.py` item-2 block; excerpts in
`../r244-cutaway-state/tool-callback.txt`):

* Dispatch: DoDynamicCutaway 0x1cffcc: if current tool (`*(TOC-0x6fb8)`) != 0
  and corner in world bounds: `tool->vt+0x28 table slot 18(corner, viewer+0x204)`
  (0x1d0038 `lwz r12,0x28(r3)`, 0x1d0044 `lwz r12,0x48(r12)`). Slot 19
  ModifyCursorPos: base = `blr` (0x1579d0) — pass-through.
* Base `cTool::AdjustCutawayForTool` 0x191910: gate `tool+0x24` (grab active);
  corner bounds [x0+1..x1-1]x[y0+1..y1-1]; `wall = GetWall(tile)`; SET
  mask bit(tile); if `wall&1`: CLEAR bit(tile + {tool+6, tool+7}); if `wall&2`:
  CLEAR bit(tile + {tool+0, tool+1}) — offsets are signed-byte runtime drag
  state (still unnameable statically; only the read sites are pinned).
* `cMoveTool::AdjustCutawayForTool` 0x174100 (808 bytes): gates tool+0x24 AND
  tool+0x3c (picked object); also loads the LUT global (0x174108) and
  floor-table (0x174114); via GetCurrentLevel + dynamic_cast type 0x28 it
  iterates the picked-object list: tile = {+0xf4>>4, +0xf8>>4, floor +0x110},
  replaced by the dragged object's {+0xfc,+0xfd,+0xfe} when tool+0x79 and the
  drag object exist; bounds [1..size-2]; MARKS those tiles into the matrix.
  This is the leg with portable semantics: cut the dragged object's footprint.

### Fix design (item 2) — wire the existing hook; cMoveTool leg only

* Engine `FreeSO/TSOClient/tso.world/Utils/CutawayMatrix.cs`: NO structural
  change. `CutawayViewInputs.AdjustCutawayForTool` (L115, `Action<Point,
  bool[]>`) is already invoked at the exact native point (ComposeDynamic L446:
  after the cursor rectangle, gated on corner-in-world, inside the same
  suppressed/bounds region as native). Optional additive helper
  `CutawayMatrix.AdjustForDrag(bp, mask, IEnumerable<Point> tiles)` marking
  [1..W-2]x[1..H-2] tiles, so both callers share the bounds law.
* Client `Client/Simitone/Simitone.Client/UI/Panels/UILotControl.cs`, in
  `BuildCutawayInputs` (after `AppendPersonInputs(view)`, ~L1660):

  ```csharp
  var holding = ObjectHolder?.Holding;           // native current-tool + grab flag
  if (holding != null)
      view.AdjustCutawayForTool = (corner, mask) =>
          CutawayMatrix.AdjustForDrag(vm.Context.Blueprint, mask,
              holding.CursorTiles.Select(t => new Point(
                  (int)t.Position.x, (int)t.Position.Y)).Where(inBounds));
  ```

  Mapping: `UIObjectHolder.Holding != null` = native tool+0x24 gate;
  `UIObjectSelection.CursorTiles` (the multitile phantom entities) = the native
  picked/dragged-object tile list (+0xf4/+0xf8>>4 / drag +0xfc..). Floor-filter
  to `World.State.Level`.
* PIP (`UIOriginalPictureInPicture.BuildPipCutawayInputs`): leave the hook
  NULL — native save-and-NULLs the tool global around offscreen rendering
  (enter save 0x1c0e48, restore 0x1c1714..0x1c171c), so no tool callback ever
  runs inside a native PIP mask.
* Base-leg grab-offset clears (tool+0/+1/+6/+7): WALL — the offsets are native
  runtime drag state with no static values and no port equivalent field. A
  runtime capture must record the base tool's +0/+1/+6/+7 (and wall flag at
  the corner) during a native drag with the non-move tool. Defer: every port
  grab flow carries a multitile group (CursorTiles), so the cMoveTool leg
  covers the observable port behavior; document the deferral in the hook doc.

---

## Item 4 — RESOLVED: PIP state restoration law; two parity gaps found

`DrawPictureInPicture__11HouseViewerFP9cTSBufferRC7FTilePtii` (0x1c0c64...),
per-render save/restore skeleton (`pip-enter.txt`, `pip-restore.txt`):

ENTER (per render): save to stack frame — zoom (global viewer+8), +0xc, floor
(viewer+0x18), view rect +0x28..+0x34, flags 0x48/0x49/0x1f8, viewport dims
0x10/0x14, **current-tool global** (`*(TOC-0x6fb8)` -> stack+0x164, 0x1c0e48),
animator tracked object (dynamic_cast cXPerson 0x1c0e54) and picking flag
(0x1c0e28); clear tracked/picking (0x1c0ed4/0x1c0edc); SetScale/SetRotation
(0x1c1030/0x1c1044) into the target view; **floor-diff branch** (`cmpw
viewer+0x18, target` 0x1c104c, `beq 0x1c10ec`): WallManager.SetCutaway(false),
**clear history** (0x1c1068), dirty, ResetDynamicCutaway, set floor (0x1c1088),
ComputeCutaway + DoDynamicCutaway.

RESTORE: SetScale/SetRotation back (0x1c15e0/0x1c15f4); **same floor-diff
branch** (0x1c15fc..0x1c1604): SetCutaway(false), **clear history**
(0x1c161c), dirty, ResetDynamicCutaway, restore floor (0x1c163c),
ComputeCutaway(savedFloor) (0x1c1654), DoDynamicCutaway (0x1c166c); restore
scroll/origin (0x1d6d10), SetCameraPosition, SetTrackedObject/SetPickingObject
(0x1c16fc/0x1c1700); **restore tool global** (0x1c1714..0x1c171c).

Consequences (each instruction-pinned):

1. **Same-floor PIP** (target == viewer floor): the whole block is skipped —
   the PIP renders the STANDING main matrix untouched (history included), and
   the main view's state is never recomputed.
2. **Cross-floor PIP**: the live history set is cleared on ENTER and AGAIN on
   RESTORE, and the main mask is recomposed WITHOUT it (person + cursor only)
   — i.e. **every cross-floor PIP render wipes the hover history**. During a
   live PIP this happens per frame.
3. The tool global is NULL for the whole PIP mask (no slot-18 callback —
   matches item 2's PIP design).

Port parity (`UIOriginalPictureInPicture.cs`, `WorldPictureInPictureRenderer.cs`):
statelessness already gives the tool-NULL and restore semantics; the renderer's
`level != Main.Level` all-false base matches the native cross-floor enter;
`(2)` and `(1)` are the gaps: the port's `CutRooms` history survives cross-floor
PIPs (stale rooms keep cutting in the main mask), and the same-floor PIP
composes an empty-history mask instead of sharing the standing main mask.

### Fix design (item 4) — three bounded client-side changes

All in `Client/Simitone/Simitone.Client/UI/Panels/`:

1. Same-floor sharing: in `UIOriginalPictureInPicture.PreDraw` (~L367), pass
   `pipInputs: null` when `Target.Position.Level == World.State.Level` — the
   renderer's existing null + `level != Main.Level` path then draws the main
   `Blueprint.Cutaway` unchanged (= native standing-matrix share).
2. History wipe: add `internal void ClearCutHistory() => CutRooms.Clear();` to
   `UILotControl` (`CutRooms` is private, L128) and call it from
   `UIOriginalPictureInPicture` when building cross-floor inputs
   (`targetLevel != World.State.Level`). The main loop's next
   `UpdateCutawayDynamic` commit then recomposes history-less — native's
   restore law. (Cross-floor only; same-floor PIP must not clear.)
3. Cursor frame (R244 disclosed deviation): in `BuildPipCutawayInputs`
   (~L225), change the picker to
   `World.EstTileAtPosWithScroll(new Vector2(pos.X, pos.Y), targetLevel)` —
   the method already takes a level (World.cs L831); native resolves the
   cursor tile through the PIP-swapped viewer (target floor).
   Update the L223-225 comment accordingly.

Validation sketch for the implementer: extend the `uicutaway` battery —
(a) same-floor PIP keeps main mask reference AND leaves history rooms cutting
in the PIP image; (b) cross-floor PIP render empties `CutRooms` and the next
main commit contains no pre-PIP history room; (c) PIP cursor tile maps through
the target-floor picker (offset the target floor's terrain and assert the
marked rect moves).

---

## Round ledger

| item | verdict | port action |
|---|---|---|
| 1 rotation LUT generator | RESOLVED: `BuildRotationLookup` 0x60f20, pure 3-page 64x64 map, sole caller SetSize 0x164910 | doc-only comment update in CutawayMatrix.cs |
| 2 AdjustCutawayForTool | decoded (base + cMoveTool); base grab offsets are runtime drag state (wall) | wire `CutawayViewInputs.AdjustCutawayForTool` from `ObjectHolder.Holding.CursorTiles` in UILotControl.BuildCutawayInputs; null in PIP |
| 3 packed-alt identity | RESOLVED: remap = rotation LUT (world+0x84 = rotation); BuildMaxAltsTable = unrelated diagonal max-corner-alt arrays (+0x1EC/+0x1F0), never read by cutaway | keep identity remap; fix its rationale comment |
| 4 PIP state restoration | RESOLVED: per-render save/restore; cross-floor PIP wipes history each render; same-floor PIP shares standing matrix; tool NULL throughout | pass null inputs on same-floor PIP; clear CutRooms on cross-floor PIP; target-floor picker line |

UNRESOLVED (with reason): base-cTool grab-offset field values (tool+0/+1/+6/+7)
— native runtime drag state, only read sites pinned; needs a native runtime
capture if the non-move-tool drag path is ever ported. viewer+0x200 flag
readers (r244-state) — unchanged, nothing depends on it.
