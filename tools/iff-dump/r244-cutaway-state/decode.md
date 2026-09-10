# R244 — dynamic-cutaway STATE machinery: history lifecycle, strategy virtuals, cursor vicinity, outside-person rectangle

Resolves the STATE items left open by r243 (`review.md` items: insert callers,
CPState mode 2, strategy virtual slots, cursor marking, outside-person rectangle,
room+110/+aac fields). All addresses are executable FILE OFFSETS (code section
starts at file 0x8e90; stored data pointers are section-relative, so a vtable
code word + 0x8e90 = file offset). Nothing outside this directory was written.

Tooling additions used this round (all regenerable, none committed as bulk):
PEF section-1 packed-data unpack (r104 law, reimplemented inline in `verify.py`)
plus the r142 laws: TOC base = data+0x8000, vtable words point at 8-byte
TVectors {code, toc}, code stored section-relative. Function names/boundaries
came from the executable's own function records (`00092041` marker, layout
`[pad][marker][size u32][00][len u8][name]`, function start = size_addr - size - 8).

Corrections to r243 wording (no contradiction of its instruction evidence):

* The "secondary Render 1c0c60" is `DrawPictureInPicture__11HouseViewerFP9cTSBufferRC7FTilePtii` (0x1c0c64).
* `GetRoom` at 0x1cf62c uses the RoomManager held at CPState-app global; the
  world singleton used for bounds is `*(TOC-0x7350)` (cRotatableWorld), see below.

## 1. The three-room history: who inserts, who clears

### 1.1 Insert — HouseViewer::MouseTrack(EventRecord*, unsigned char) 0x1d0310 (mousetrack.txt)

`cCutawaySet::insert` (0x1aa030) has exactly TWO call sites in the whole
executable: 0x1d0628 and 0x1d063c, both inside `MouseTrack`. The set lives at
viewer+0x1fc; capacity 3 (0x1d9640).

Event flow (instruction-exact):

1. `XViewer::MouseTrack(event, phase)` super-call at 0x1d0334. r30 = event+0xc
   (h), r28 = event+0xa (v) (Mac `EventRecord.where` = Point {v, h}).
2. If the mouse is OUTSIDE the view port (h in [viewer+0x28, viewer+0x30],
   v in [viewer+0x2c, viewer+0x34]): if `CPState::GetMode()==4` clear
   viewer+0x4bc; return (no insert). If INSIDE and mode==4: viewer+0x4bc=1,
   viewer+0x4c0=h, viewer+0x4c4=v (camera-mode mouse bookkeeping).
3. `phase != 0` → exit (0x1d03d0/0x1d03d4). `XViewer::MouseDown` (0x157a10)
   calls `MouseTrack(..., phase=1)` — so mouse-DOWN never inserts. phase==0 is
   the tracking/move call.
4. viewer+0x49 (dynamic cutaway enabled) == 0 → exit (0x1d03d8/0x1d03e0).
5. screen→world: viewer virtual vt2 slot +0xc (index 3) =
   `HouseViewer::PointToTile(Point, bool)` with r5 = packed {v,h}, r6=1 → two
   ints; tile = {(int1>>4), (int0>>4), floor=viewer+0x18}. If the tile is
   outside world bounds expanded by 1 (x in [x0+1, x1-1], y in [y0+1, y1-1]
   against `*(TOC-0x7350)` +0x70..0x7c) → **ResetDynamicCutaway(viewer)** at
   0x1d0650 and return (0x1d048c). So hovering off-lot resets instead of inserting.
6. `r26 = room = cRotatableWorld::GetRoom(tile)` (0x17a3c0); r31 = room & 0xffff.
7. Probe walk (0x1d04b4-0x1d0614): starting at the event point, add
   `viewer+0x60/2` (signed) to v, re-project, and take the room at each probed
   tile (same floor). Loop while the probed room (r27) equals the cursor room
   AND the accumulated offset r28 satisfies the SIGNED test `r28 < viewer+0x124[(3 - *(zoomCfg+8))*4] / 2`
   (the per-story pixel height table also read by GetTheoreticalWallExtent;
   the loop therefore runs while the accumulated offset stays below half the
   story height in pixels). Out-of-world probe tiles are skipped (treated as
   "same room", continue).
8. Insert r26 if `< 0xfffb` (0x1d0618-0x1d0628), insert r27 if `< 0xfffb`
   (0x1d062c-0x1d063c) — i.e. **the room under the cursor AND the room found
   directly "behind/above" it on screen**.
9. `DoDynamicCutaway(viewer, true)` at 0x1d0648.

PORTABLE PSEUDOCODE (history driver):

```
onMouseTrack(event, phase):
    if phase != 0: return                  # only the move/track phase
    if not pointInViewport(event.where): updateCameraBookkeeping(); return
    if not viewer.dynamicCutawayEnabled: return
    tile = pointToTile(event.where, viewer.floor)        # vt2 slot 3, >>4 per int
    if not worldBounds.insetBy(1).contains(tile):        # [x0+1..x1-1]x[y0+1..y1-1]
        viewer.resetDynamicCutaway(); return
    frontRoom = world.getRoom(tile)
    behindRoom = frontRoom
    dv = viewer.storyPixelStep / 2                       # viewer+0x60/2 (signed)
    off = dv
    while behindRoom == frontRoom and off < storyPixelHeight(viewer.zoom) / 2:
        behindRoom = world.getRoom(pointToTile(shiftV(event.where, off), viewer.floor))
        off += dv
    if frontRoom < 0xfffb: history.insert(frontRoom)
    if behindRoom < 0xfffb: history.insert(behindRoom)
    viewer.doDynamicCutaway(true)
```

### 1.2 Clear — all 16 call sites of clear__11cCutawaySetFv (0x1a9e60) (clear-calls.txt)

Every site is one of two idioms:

A. Floor-change idiom (disable old floor's wall cutaway, clear, dirty, reset,
   swap floor, recompute — identical shape at all of these):
   * `SetLevel__11HouseViewerFib` 0x1d7158 (+0x44)
   * `ScrollToTile__11HouseViewerF7FTilePtibb` 0x1d7cf0 (+0x5c, floor-changing scroll)
   * `SetGraphicsContext__11HouseViewerFPC9HVContext` 0x1c0a7c
   * `GenerateSnapshot__11HouseViewerF7CTilePtP9cTSBufferiiii` 0x1c01ac, 0x1c0814
   * `DrawPictureInPicture__11HouseViewerF...` 0x1c1068 (entering target floor),
     0x1c161c (restoring)
   * `MakeThumbnail__11HouseViewerF...` 0x1c1dd8, 0x1c2094, 0x1c24c8
   * `DoCommand__11HouseViewerFs` 0x1d6904 (command 0xf6 floor-reset leg)
B. Other:
   * `SetDynamicCutaway__11HouseViewerFb` 0x1d6fbc — any toggle of the flag.
   * `GenerateSnapshot` 0x1c0020 — copies the set into the snapshot state
     (`cCutawaySet(const cCutawaySet&)` at 0x1c0018) then clears the live set.
     GenerateSnapshot also NULLs the current-tool global (0x1c0068) and calls
     MouseTrack (0x1c03d8) to rebuild tracking for the snapshot view.
   * `DoCommand` 0x1d66c0 — command **0x105**: `BuildMaxAltsTable()` then
     memset(viewer+0x204, 0, 0x200), clear, dirty, SetRotation(...) — the
     terrain/altitude rebuild command.
   * `DoCommand` 0x1d68b8 and 0x1d6904 — command **0xf6** (restore view
     zoom/rotation/floor; viewer+0x58/0x5c/0x60/0x64 = 0x10/8/8/4<<zoom), clears
     twice, then full cutaway recompute.
   * `DoCommand` 0x1d6b78 — command **0xf0**: `viewer+0x200 = 1; history.clear()`.
     (viewer+0x200 is a byte flag next to +0x1f8; its readers were not chased
     further — UNRESOLVED minor.)
   * For reference, command **0xe4** (0x1d65ac) does NOT clear: it re-runs
     `DoDynamicCutaway(viewer+0x1f8)` only.

### 1.3 insert/cutaway-set semantics (set-insert.txt + fixture)

`insert(v)`: linear search for v; if found, return (duplicate left in place —
0x1aa0a8/0x1aa0ac compare search-end vs end). Otherwise if `size == capacity`
(+0x10, =3) erase the FRONT element (0x1aa2e0 call at 0x1aa0c8) and push_back.
So the history is FIFO with capacity 3. Proven by fixture in verify.py.

## 2. Strategy virtuals in DoDynamicCutaway

Two distinct virtual receiver objects are involved (dynamic.txt 0x1cfa64,
0x1cffcc). Identification of the second one corrects the r243 wording
("strategy virtual callback"): it is dispatched on the CURRENT TOOL, not the
strategy.

### 2.1 Suppression predicate — vtable byte offset +0x5c (slot index 23)

`GetCurrentStrategy__Fv` (0x21cc50) = `appView->GetScrollingStrategy()`
(view+0x16c, lazily created by `CreateEdgeDetectStrategy__Fv` 0x21cb70 — the
ONLY strategy factory in the binary; 11 call sites all cDDDSimsView code).
The only concrete class is **EdgeDetectScroller** (0x94 bytes; base
ScrollingStrategy). Its primary vptr (TOC slot -0x6c94, verified in unpacked
data) = 0x50910; slot at byte +0x5c resolves through TVector 0xf198 to
**`IsWaitingForClick__18EdgeDetectScrollerFv`** at 0x21a7e0 =
`return this->b0x93` (bool, ctor-initialized 0; base ScrollingStrategy impl at
0x1d02c0 is `li r3,0; blr`).

Contract: `if (strategy && strategy->IsWaitingForClick()) skip the whole
mouse/cursor section` (0x1cfa6c-0x1cfa84). For the live default strategy the
predicate is effectively always false.

Vtable layout of EdgeDetectScroller (vptr 0x50910, 4-byte slot words → TVector
data offsets), for reference: +0x8 dtor, +0xc Init, +0x10 OnMouseMove,
+0x14..0x28 mouse-down/up/double L/R, +0x2c OnMouseWheel, +0x30/0x34 OnKeyDown/
KeyUp, +0x38 PreDraw, +0x3c PostDraw, +0x40 StopScrolling, +0x44 IsEdgeDetect,
+0x48 GetMargin, +0x4c/0x50 Enable/DisableScroller, +0x54/0x58
Is/SetEdgeDetectEnabled, **+0x5c IsWaitingForClick**, +0x60 SetMiniCP.

### 2.2 Screen→tile and matrix-modifying callback — the CURRENT TOOL's secondary interface (+0x28)

The object loaded from global `*(TOC-0x6fb8)` receives `lwz r12, 0x28(r3)`
vtable dispatches throughout input code (XViewer::MouseDown/MouseTrack,
DoCommand, DoDynamicCutaway). Evidence chain:

* `IssueToolSelection__7XViewerFP5cTool` (0x158138, +60) stores its cTool*
  argument INTO `*(TOC-0x6fb8)`; GenerateSnapshot/DrawPictureInPicture/
  MakeThumbnail save-and-NULL it around offscreen rendering (0x1c0068,
  0x1c08fc, 0x1c1718, restore from viewer+0x164).
* `cTool`'s destructor (0x172690) writes the vptr at `this+0x28` from
  TOC slot -0x6f48 → data 0x4d280: a secondary-base vtable whose slots are
  (slot = byte offset/4): 3 RapidFire, 4 AutoScroll, 5 CursorNumber,
  6 NeedsObject, 7 Select, 8 Deselect, 9 Float(FTilePt,s,EventRecord*),
  10 Click(...), 11 DoubleClick(...), 12 Drag(...), 13 Release(...),
  14/15 Set/ResetParameter, 16 GetString, 17 ResetGrabPoint,
  **18 AdjustCutawayForTool(const CTilePt&, BitMatrix64&)**,
  **19 ModifyCursorPos(Point*)**, 20/21 OnKeyDown/KeyUp, 22 OnChar,
  23 SupportsUndo, 24-26 OnEnterWorld/OnExitWorld/Cancel, 27 CannotAfford,
  28 ControlsOwnToolTips, 29 DeletingObject, 30 ZoomChanged(i), 31 RotationChanged(i).

So in DoDynamicCutaway (0x1cffcc-0x1d004c):

```
if (currentTool != null && worldBounds.contains(corner)):
    currentTool->adjustCutawayForTool(cornerTile, viewer.matrix0x204)   # slot 18
```

Implementations:
* Base **cTool::AdjustCutawayForTool** 0x191910 (tool-callback.txt): returns
  immediately if `tool+0x24 == 0` (grab/active flag); bounds-checks the tile in
  [x0+1..x1-1]x[y0+1..y1-1]; `wall = world.GetWall(tile)`; SETS the matrix bit
  for the tile; then if `wall & 1`: tile2 = tile + CTilePt{tool+6, tool+7, 0}
  and CLEARS bit(tile2); if `wall & 2`: tile3 = tile + CTilePt{tool+0, tool+1, 0}
  and CLEARS bit(tile3). (The 64-bit set/clear bit ops use helper 0x591b10,
  see §3.)
* **cMoveTool::AdjustCutawayForTool** 0x174100 (override, vtable word 0x4c598):
  same +0x24 gate plus tool+0x3c (picked object); gets GetCurrentLevel(),
  iterates the picked-object list (each entry: tile from +0xf4/+0xf8 >>4 with
  floor +0x110; when tool+0x79 and the dragged object exist, uses the drag
  object's +0xfc/+0xfd/+0xfe tile instead — the SAME person-tile fields as
  §4), bounds-checks [1..size-2], and marks those tiles into the matrix.
* **cTool::ModifyCursorPos(Point\*)** 0x1579d0 (slot 19; called at 0x1cfb44 and
  inside XViewer::MouseTrack/MouseDown BEFORE PointToTile): mutates the screen
  point in place (base implementation is `blr`-only per its 4-byte size — the
  Point passes through unless a tool overrides).

## 3. Cursor-vicinity marking (cursor-vicinity.txt, ctilept-dir.txt)

Sequence in DoDynamicCutaway after the mode-2 branch (0x1cfa64..0x1cffc8):

1. **Predicate gate**: `strategy == null || strategy->IsWaitingForClick()` →
   jump to the XOR step (0x1d0050).
2. Query mouse into stack object at r1+0x88 (0x37630 init + GetMainframeWND
   0x3bc760 + fill 0x374b0). **Main-animation-buffer bounds test**: mouse.x >= 0,
   mouse.x < GetBuffDims().x (0x3bc960 word 0), mouse.y >= 0, mouse.y <
   GetBuffDims().y (word 4); else skip to XOR.
3. **Y adjustment**: `z = viewer->vt2.slot7()` (byte offset 0x1c, index 7 =
   `GetScale__7GViewerFv`/zoom, HouseViewer impl 0x1d17a0); `mouse.y += (8 << z) << 2`
   = **32 << zoom** (0x1cfb0c-0x1cfb30). Point at r1+0x48 = {v = adjusted y, h = x}.
4. `currentTool->modifyCursorPos(&point)` (vt +0x28 table, slot 0x4c/4 = 19).
5. `tile = viewer->vt2.slot3(point, true)` (`HouseViewer::PointToTile`,
   0x1d3b00) → ints {A@0x90, B@0x94}; CTilePt tile@0x9b = {B>>4, A>>4, 0}.
6. **Direction constants** come from the global at TOC-0x73e8 (BSS, filled once
   by `__sinit__:ctilept_cp` 0x610d0): dir1 = CTilePt at +0xc = **(-1,-1,0)**,
   dir2 = at +0xf = **(+1,+1,0)** (also +0x3 {0,1,0}, +0x6 {-1,0,0}, +0x9 {1,0,0},
   +0x12 {0,-1,0}, +0x15 {-1,1,0}). corner@0x98 = tile + 4*dir1 =
   **tile + (-4,-4)** (operator* by 4 at 0x19abf0, operator+ 0x118ef0).
7. Phase 1 (0x1cfc30): while tile outside world bounds [x0..x1]x[y0..y1] AND
   tile != corner: tile += dir1. (Pulls an off-lot cursor back toward the lot
   along the diagonal, max 4 steps.)
8. Phase 2 (0x1cfd10): while corner outside world AND corner != tile:
   corner += dir2. (Pulls the corner back inside.)
9. **r26 gate** (0x1cfdc4): r26 = "at least one ORed room was outside" (set at
   0x1cf5a8 when a history room IsOutside, 0x1cf75c when the mode-2 person's
   room IsOutside). If r26 == 0 the cursor rectangle is NOT marked at all.
10. If corner in world: mark every tile of CTileRect{corner, tile}. CTileRect
    is 8 bytes {a.x,a.y,a.z, b.x,b.y,b.z}; `end()` (0x1183d0) requires
    a.x < b.x AND a.y < b.y (else begin==end → empty); iteration is row-major
    over **[a.x, b.x) x [a.y, b.y)** (half-open; the cursor tile itself is NOT
    marked). Each tile is bounds-checked against the world before setting its
    bit (0x1cfe74-0x1cfed0). Typical result: the 4x4 block below-left of the
    cursor tile.
11. Then §2.2's tool callback with the final corner tile.

Bit addressing (helper 0x591b10): bit index = tile.x, row = tile.y; the 512-byte
matrix at viewer+0x204 (and room+0x7c) is 64 rows x 8 bytes; word0 = bits 0-31,
word1 = bits 32-63; helper computes the 64-bit (1<<x) as two words
(r3=hi, r4=lo from inputs r3=0, r4=1).

PORTABLE PSEUDOCODE (cursor vicinity):

```
if strategy is null or strategy.isWaitingForClick: goto xorStep
p = queryMouse()
if p.x < 0 or p.x >= buffDims.x or p.y < 0 or p.y >= buffDims.y: goto xorStep
p.y += 32 << viewer.zoom
tool?.modifyCursorPos(&p)                       # slot 19, no-op in base
tile = viewer.pointToTile(p, true)              # (b>>4, a>>4)
dir1 = (-1,-1); dir2 = (+1,+1)                  # ctilept globals +0xc / +0xf
corner = tile + 4*dir1
while not worldBounds.contains(tile) and tile != corner: tile += dir1
while not worldBounds.contains(corner) and corner != tile: corner += dir2
if anyOredRoomWasOutside and worldBounds.contains(corner):
    for y in [corner.y, tile.y):                # half-open
        for x in [corner.x, tile.x):
            if worldBounds.contains((x,y)): matrix.set(x, y)
if currentTool and worldBounds.contains(corner):
    currentTool.adjustCutawayForTool(corner, matrix)   # slot 18
```

UNRESOLVED (minor, not needed for the contract): the exact values the base
cTool reads from tool+0/+1/+6/+7 (grab offsets, runtime state), and viewer+0x200's
full reader set.

## 4. Outside-person bounded rectangle (mode-2 branch, task C)

Gate chain (dynamic.txt 0x1cf5c8-0x1cf7b8): mode==2; person = tracked object
dynamically cast from cXObject to cXPerson (0x591230, type id 0x28) or, when
tracked global (TOC-0x70b8) is null, `ObjectModule::GetSelectedPerson`
(0xe2060); person nonnull; **person+0x110 == viewer+0x18** (floor, see §6);
**roomID = lhz person+0xaac < 0xfffb** (see §6). Then OR the room's 512-byte
matrix (room+0x7c) into viewer+0x204. `IsOutside(room)` (0x124fe0): if inside →
done; if outside → r26=1 and build the rectangle:

1. tile = {extsb(person+0xfc), extsb(person+0xfd), person+0xfe} (three bytes).
2. Gate: tile must satisfy **1 <= x <= worldSize-2 AND 1 <= y <= worldSize-2**
   (worldSize = `*(TOC-0x7354)->+0x10`); otherwise skip the rectangle entirely.
3. **Altitude-table adjustment** (0x1cf7bc): if `world->+0x84 != 0` (altitude
   level count), recompute x,y from the packed-alt table built by
   `BuildMaxAltsTable__11HouseViewerFv` (0x1c6b70; base pointer at TOC-0x73e4):
   `entry = tableBase + (alt<<13) + (x<<7) + (y<<1) - 0x2000` — i.e.
   2-byte entries indexed [alt][x][y] (64 rows x 2 bytes, one page of 0x2000 per
   altitude level); x = entry[0], y = entry[1].
4. Probe k = 4, 3, 2, 1, 0 (ctr=5, 0x1cf810-0x1cf8b0): candidate =
   (x+k, y+k); the FIRST k whose candidate is inside world bounds
   [x0..x1]x[y0..y1] inclusive wins and is stored as the rect's second corner;
   if none inside (candidate k=0 is the tile itself, always the last tried) the
   stored second corner = the tile.
5. Rectangle = CTileRect{a = tile, b = tile+(k,k)} → validity a < b requires
   **k >= 1**; marks **[x, x+k) x [y, y+k)** with per-tile world bounds checks.
   So the outside-person contribution is at most a 4x4 block up-right of the
   person's tile, never including the tile itself at k=0.

PORTABLE PSEUDOCODE:

```
if personRoom.isOutside:
    t = (person.tileX, person.tileY)            # +0xfc/+0xfd
    if 1 <= t.x <= size-2 and 1 <= t.y <= size-2:
        if world.altLevels != 0:
            e = altTable[world.altLevels][t.x][t.y]   # 2-byte entries
            t = (e[0], e[1])
        for k in (4,3,2,1,0):
            if worldBounds.contains((t.x+k, t.y+k)): break
        if k >= 1:
            for yy in [t.y, t.y+k):
                for xx in [t.x, t.x+k):
                    if worldBounds.contains((xx,yy)): matrix.set(xx, yy)
```

## 5. CPState mode 2 = LIVE mode (task D)

Evidence (setmode.txt, viewcontrol-modes.txt):

1. `cWinViewControl::TSOnCommand` (0x2b48bc) — the on-screen mode buttons —
   calls `SetMode__7CPStateFQ27CPState4Modeb` with r4 = 2 / 1 / 0 / 3 / 4
   (0x2b4b1c, 0x2b4bbc, 0x2b4bf8, 0x2b4c34, 0x2b4c70).
2. Inside `SetMode` (0x210790), entering mode 2 calls **`EnterLiveMode__5HouseFv`**
   (0x210800) plus ObjectFolder/VBAnim cleanup, and emits the hook message at
   string table +0x3d = **"Entering Live Mode"**. Mode 0 → "Entering Build
   Mode" (+0x17), mode 4 → "Entering Camera Mode" (+0x50), mode 3 → "Entering
   Options Mode" (+0x65); mode 1 runs the shop-catalog setup (zoning types,
   UpdateShopItem) = Buy. Strings verified from the unpacked data section at
   0x50258.
3. `cDDDSimsView::TSOnKeyDown` (0x21758c): the key guarded by
   `IsLiveModeDisabled` sets mode 4 (camera); the buy key sets 1 with
   ResetPreviousPages; another key toggles to 2 (0x217bd0).
4. `CPState::EnteringHouse` (0x211c7c) sets mode 2 (a house is entered in live
   mode). LoadGame sets mode 1 for a saved-while-buying game (0x25922c).
5. Mode 4 has the special mouse bookkeeping at viewer+0x4bc/0x4c0/0x4c4 that
   MouseTrack performs (cleared when leaving mode 4 at 0x2107cc).

∴ **DoDynamicCutaway's `GetMode()==2` gate = live mode** — numeric identity is
no longer merely numeric. Mode map: 0=Build, 1=Buy, 2=Live, 3=Options,
4=Camera.

## 6. person+0x110 and person+0xaac semantics (task E)

* **+0xaac (u16) = the person's current room ID**, maintained by
  `cXPerson::UpdateCurrentRoom` (0x109450): compares `lha +0x84 & 0xffff` (the
  position-derived room) against +0xaac; on change stores the new value
  (0x1094b4), updates Room overhead lights via `GetPeopleCount` (which itself
  scans people whose +0xaac equals the room id, 0x1249dc), and dispatches
  0xf7 when the flag at +0x114 bit29 is set or the person is the tracked
  global. `cXPerson::Simulate` (0x10bfec) uses +0xaac for room ambient light.
  DoDynamicCutaway's `< 0xfffb` gate = "room id valid" (0xfffb+ = invalid/
  outside markers, same constant in UpdateCurrentRoom).
* **+0x110 (u32) = the object's floor/level**, word-compared directly against
  the viewer floor: `CenterHouseViewOnMe__8cXObjectF` (0xc496c) does
  `lwz r27, 0x110(r26); bl GetLevel__7GViewerFv; cmpw r27, r3` (0xc49e8-0xc49f0),
  and passes it as the floor argument to TileToPoint (0xc4a04). DoDynamicCutaway's
  gate is exactly `person.floor == viewer+0x18` (viewer floor word).
* (Near-miss documented: +0x114 is a cXPerson FLAGS word — bit29 tested in
  UpdateCurrentRoom — not the floor.)

## 7. What is safe to base a C# reimplementation on

Instruction-proven and fixture-backed (verify.py passes):

* History driver: MouseTrack phase==0 + in-viewport + dynamic-cutaway-on;
  insert(cursorRoom) + insert(behindRoom); off-lot cursor → ResetDynamicCutaway;
  DoDynamicCutaway(true) after inserts. Insert = dup-preserving, FIFO, cap 3.
* Clear lifecycle: every floor/view change (SetLevel, ScrollToTile cross-floor,
  SetGraphicsContext, PIP/thumbnail/snapshot enter+restore), SetDynamicCutaway
  toggle, DoCommand 0x105/0xf6/0xf0; snapshot saves then clears the set and
  nulls the tool global.
* Mode 2 = live mode; modes 0=Build, 1=Buy, 3=Options, 4=Camera.
* Predicate = EdgeDetectScroller::IsWaitingForClick (strategy vtable slot byte
  +0x5c), effectively false in live play.
* Cursor vicinity: buffer-bounds test, Y += 32<<zoom, ModifyCursorPos,
  PointToTile, corner = tile+4*(-1,-1), both-direction walk-ins, r26 gate,
  half-open [corner,tile) rectangle, per-tile bounds check.
* Tool callback = current cTool secondary-interface slot 18
  AdjustCutawayForTool(corner, matrix); base marks the corner tile and clears
  per wall-flag neighbors; cMoveTool marks dragged-object tiles.
* Outside-person rectangle: exact tile source +0xfc/+0xfd/+0xfe, [1,size-2]
  gate, altitude-table remap, k=4..0 probe, k×k block up-right, k=0 → nothing.
* Person fields: +0xaac room id (u16, <0xfffb valid), +0x110 floor word equal
  to viewer floor.

Deliberately NOT re-derived here (geometry agent's scope): PointToTile (0x1d3b00)
internals, ComputeCutawayMatrix (0x125130), TileToPoint, GetTheoreticalWallExtent
(0x1cef30), and the construction of the altitude tables beyond their read
convention (BuildMaxAltsTable 0x1c6b70).

## Files

* `verify.py` — 72 code pins + packed-data vtable-chain proof + 6 fixture groups; PASS.
* `verified-state.json` — its output.
* `mousetrack.txt`, `docommand.txt`, `setmode.txt`, `viewcontrol-modes.txt`,
  `strategy-predicate.txt`, `tool-callback.txt`, `cursor-vicinity.txt`,
  `person-fields.txt`, `ctilept-dir.txt`, `clear-calls.txt` — disassembly
  excerpts produced with r145/capdis2.py at the addresses named above.

## UNRESOLVED (with reason)

* viewer+0x200 flag readers (set by DoCommand 0xf0): not chased; only its
  writer is pinned. No reimplementation decision should depend on it.
* Base-cTool grab-offset fields tool+0/+1/+6/+7 (runtime values): only the
  read sites are pinned; they are drag-state, not constants.
* EdgeDetectScroller::b0x93's setters beyond the ctor zero: IsWaitingForClick is
  the only named accessor found; if some UI sets it before modal clicks, the
  predicate could fire — the slot identity, not its every writer, is what the
  port needs (callers of +0x5c are DoDynamicCutaway only).
