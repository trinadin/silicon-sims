# R256 — NATIVE ARCHITECTURE UNDO/REDO MACHINERY LAW (decoded from PPC binary)

UI-22 decode round. Binary: `game-data/The Sims/The Sims Complete` (PPC PEF, 6.5 MB).
Addresses are FILE offsets (== code-virtual; symbol-index addr − 2). Memory laws per
r145: TOC base = DATA 0x8000; initialized DATA len 0x7bf80; BSS 0x7bf80..0x989a4.
Companion tools (this dir): `scan_undo_calls.py` (full-binary bl-target caller scan),
`ppc_decode_bo_correction.md` (decoder polarity fix this round relies on).

DECODED = from instruction immediates / static data / call graph.
INTERPRETED = semantic reading, behavior inferred and labeled.

## 0. DECODER CORRECTION (load-bearing for all branch polarity below)

`ppc_decode.py` prints BO 12/4 branches with SWAPPED mnemonics: op-16 word
`0x4182xxxx` (BO=12) is **beq** per PPC spec and per radare2 (`rasm2 -a ppc -b 32 -e
-d 41820014` → `beq 0x14`), but the decoder prints `bne`; `0x4082xxxx` (BO=4) is
**bne**, printed `beq`. r129 fixed branch TARGETS; condition polarity was never
validated. Every conditional reading below uses the CORRECTED polarity (verified
against radare2 ground truth + behavioral coherence, e.g. the SetMode flush guards
and the SubmitUndoable commit path only make sense corrected). Earlier rounds that
read `beq/bne` off ppc_decode output may need re-verification.

## 1. Ownership + construction (DECODED)

- ONE `UndoManager` per game, embedded in `cGameTools`:
  - `cGameTools::__ct(House*)` @0x172700: +0x04 = House*; **+0x08 = UndoManager**
    (ctor 0x194990 called with `this+8`); every tool ctor then receives
    `&tools->mUndoManager` (cNewWallTool 0x181470, cNewWallpaperTool 0x17f7d0,
    cNewFloorTool 0x17c390, cPoolTool 0x18b8b0, cMoveTool, cDirtTool, cWaterTool,
    cSimpleAltTool).
  - `cGameTools` is constructed in `InitGame(...)` @0x088ad0 (right after
    `House::__ct` @0x088a5c). The UI reaches it through the game global G
    (TOC slot −0x778C → data offset 0x874, referenced ~600× binary-wide):
    `H = *(G+0x18)`, manager at `H+8`. G is an unnamed init-game global; only its
    +0x18 field is pinned here (residual §8).
- `class UndoManager` (0x1C bytes, DECODED):
  - +0x00: vtable ptr (polymorphic dtor; value from TOC −0x6EBC → data 0x1144)
  - +0x04: `std::vector_imp<Undoable*>` **mUndoList**  { +4: size, +0xC: data }
  - +0x10: `std::vector_imp<Undoable*>` **mRedoList**  { +0x14: size, +0x18: data }
  - CW vector_imp layout verified from `back()` @0x1944d0 (+4 = count, +8 = data)
    and `size()` @0x194610.
- `class Undoable` base (0x24 bytes, DECODED — __ct @0x193c50):
  - +0x00 vtable, +0x04 `state` (int), +0x08 refcount, +0x0C preSubs vector_imp,
    +0x18 postSubs vector_imp.

## 2. Command/undo stack law (DECODED)

`SubmitUndoable(Undoable* u)` @0x194680 — the single entry used by all tools
(caller list in §6):
1. `GetState(u)` (= `*(int*)(u+4)`, @0x1929f0). If state != 1:
   `Commit(u)` @0x192bc0 → virtual vt+0x20 (**CommitSelf**).
   - ret == 0 (success) → state = 1 → push path.
   - ret != 0 (**REFUSED, e.g. cannot afford**) → state = 3 →
     **FlushCommandQueues(mgr) and DO NOT push** — a failed transaction discards
     the whole undo/redo history (0x1946c0).
2. Push path: re-check state == 1; push_back onto **mUndoList**
   (u->+0x08 refcount++ at 0x1947d8; push_back @0x193460); then
   **ReleaseRedoList(mgr)** @0x193f20 — any newly submitted command CLEARS the
   redo list. Sound `GlobalDispatch(238, 0)` (@0x60ca0) on every submit.
3. Depth limit: the trim branch (0x1946c8-0x1947b8: drop oldest entry, refcount−−
   at u+8, virtual delete via vt+8 when it hits 0, erase front) fires only when
   `undoList.size == vector.max_size()` (@0x194850 → 0x36850/0x368a0 = constant
   **0x3FFFFFFF**). **The native undo stack is effectively UNBOUNDED** — no
   artificial depth cap; bounded only by memory.

State enum (corrected-polarity reading, INTERPRETED labels):
0 = new (created by tool, not committed), 1 = committed/applied, 2 = undone,
3 = failed/invalid.

## 3. Undo / redo replay (DECODED)

`UndoLastCommand(mgr)` @0x194550:
1. helper 0x194610 on both lists (= `size()` reads; no side effect)
2. `back(mUndoList)` @0x1944d0 → `UndoIt(u)` @0x192ab0
3. UndoIt returns the raw virtual result (r3); **0 = success**:
   - state==0 (fresh): call vt+0x20 CommitSelf; success → also call vt+0x24;
     non-fresh: call vt+0x24 (**UndoSelf**) directly. state ← 2 on success, 3 on fail.
4. success (ret == 0) → pop mUndoList (size−− @0x194370), push onto mRedoList;
   failure → **ReleaseUndoList(mgr)** @0x194180 (rest of the undo stack is dropped —
   a failed undo breaks the chain, mirroring submit).
5. `GlobalDispatch(238, 0)`.

`RedoLastCommand(mgr)` @0x1942c0: `back(mRedoList)` → `RedoIt(u)` @0x192a30 →
virtual vt+0x28 (**RedoSelf**); success (ret==0, state←1) → pop mRedoList, push
mUndoList; failure (state←3) → **ReleaseRedoList(mgr)**. `GlobalDispatch(238,0)`.

`CanUndo` = `mUndoList.size != 0` (@0x20fc80; `neg/or/bit31` idiom on *(mgr+8)),
`CanRedo` = `mRedoList.size != 0` (@0x20fc30, *(mgr+0x14)).

Sub-undoables: `UndoPreSubs/RedoPreSubs/Flush*` @0x193250/0x192c40/0x1935e0/0x1937d0
iterate the base vectors. WallUndoable::RedoSelf calls RedoPreSubs (@0x19c734) and
UndoSelf calls UndoPreSubs (@0x19cab4) — **wallpaper changes ride as sub-undoables
of the wall operation** (INTERPRETED: wallpaper re-evaluation on wall edits).

## 4. Per-domain payload laws (DECODED unless noted)

- **cWinArch buttons → CPState** (r145 §4 + this round):
  `cWinArch::TSOnCommand` cmd 1: undo btn (arch+0xdc) → `ArchCanUndo` @0x20fc80 →
  `ArchUndo` @0x20ee60; redo btn (+0xe0) → `ArchCanRedo` @0x20fc30 → `ArchRedo`
  @0x20edc0. Both then: if BSS latch byte *(data 0xFD8) == 0 →
  `GlobalDispatch(145, 0)` (undo zap) and clear latch; set `CPState+0xC |= 4`
  (dirty bit 4 = the "refresh undo/redo buttons" bit per r145 UpdateViewFromCPState).
  Floor/Wall undoables SET the 0xFD8 latch when they mutate the world
  (stb 1 @0x17cc44/0x17cd1c) — the zap only sounds after a real world change.
- **FloorUndoable** (RedoSelf @0x17cbd0 / UndoSelf @0x17cca0): an ARRAY of 60-byte
  change records (+0x28 count, +0x2C data ptr, +0x2C field cTool* at +0x30).
  Redo iterates FORWARD, Undo iterates BACKWARD (classic inverse order). Per change:
  `UndoOne(change)` @0x17c8e0 applies it; then `cTool::Purchase(cost)` @0x192630
  (redo) / `cTool::Refund(cost)` @0x1925f0 (undo) — **money is part of the undo
  law, charged/refunded per change**. When `GetState(this)==0` after replay →
  `cTool::FinalizePurchase()` @0x192570 + set the 0xFD8 latch.
  Records appended by `AddChange(CTilePt&, FloorPattern, TileWalls&, bool, int)`
  @0x17c6c0 (452 B; also a 5-arg overload @0x17c480).
- **WallUndoable**: the wall tool's edit engine AND the undo record (CanChangeTile
  @0x198570 5744 B, ChangeTile @0x197c32 2264 B, DoLine/DoRectangle, CleanupWallpaper
  @0x19ae40 6248 B). Large embedded grid (fields reached at this+0xE02E region —
  INTERPRETED: snapshot-scale state). Undo/Redo propagate to wallpaper sub-undoables
  (§3).
- **WallpaperUndoable** @0x195890+: packet list; `AboutToChange(CTilePt&, int)`
  @0x195250 snapshots the tile's current wallpaper before the first change to it;
  `UndoOne(WallpaperChangePacket&)` @0x195650, per-tile inverse records.
- **TerrainUndoable** @0x190580: **full-grid snapshot**. `TakeSnapshot()` @0x18fe70
  walks the terrain grid from the House (+0x14/+0x1C chains) copying per-tile
  4-corner heights (bytes) + a second per-tile value into internal arrays
  (+0x2C.. onward); `SetCost(int)` @0x18fe30 stores cost at +0x24; tool ptr +0x28.
  UndoSelf @0x190120 and RedoSelf @0x190090 are 84 B wrappers around
  `UndoRedo()` @0x1901b0 (swap current ↔ snapshot semantics, INTERPRETED) followed
  by `DirtyTileNeighborhood(x,y,level)` @0x190370 per changed tile for redraw.
- **cRoofUndoable** @0x18bf60: +0x24 = cTSString of the PREVIOUS roof pattern name
  (copied from `cRoofLayer::GetRoofPattern` @0x2dc950), +0x28 float = previous pitch
  (`cRoofLayer::GetPitch` @0x2dcac0). `UndoSelf` @0x18beb0 SWAPS stored ↔ current
  (style then pitch), so `RedoSelf` @0x18be50 is just a 44 B tail-call to the
  **UndoSelf virtual (vt+0x24)** — roof undo/redo is a pure toggle. Submitted from
  `cWinRoofPanel::TSOnCommand` (2 call sites @0x29ce48/0x29cfd0).
- **ObjectUndoable** @0x182e80: ~136-byte record (`__ct(short, Action, int,
  cTool*)`, zeroes +0x2C..+0x84, int arg at +0x38); UndoSelf/RedoSelf @0x1823c0/
  0x182190 drive the ObjectModule (create/delete), CommitSelf @0x1825c0 finalizes.
  Later tranche for the port.

## 5. Lifecycle: save + mode switches (DECODED via full-binary bl scan)

Every `bl` in the 6 MB code section was resolved (scan_undo_calls.py). External
callers of the lifecycle methods:

- `FlushCommandQueues` (= ReleaseUndoList + ReleaseRedoList + `ObjectState::
  FlushHandles` @0x1817a0): called ONLY from (a) SubmitUndoable's commit-fail path,
  (b) `UndoManager::__dt` @0x194908, (c) **`CPState::SetMode(Mode, bool)`**
  @0x210890 — guarded by `CPState+0x8 (old mode) == 2 (BUILD)` and null checks on
  the tools/manager (corrected polarity) → **LEAVING BUILD MODE CLEARS BOTH
  STACKS**.
- `ReleaseRedoList`: extra external caller `cMoveTool::Cancel` @0x176318 —
  canceling a drag also calls `UndoLastCommand` @0x176310 (undoes the in-progress
  move) then clears the redo list.
- **No save/load/lot-teardown path calls SubmitUndoable, ReleaseUndoList,
  ReleaseRedoList or FlushCommandQueues.** The stacks are runtime-only BSS vector
  state; **undo history SURVIVES save** (a save serializes whatever the current,
  possibly-undone, world is), dies on build-mode exit and on shutdown.
- `HouseViewer::DoCommand(short, long)` @0x1d6570 (the menu/hotkey path) also
  invokes UndoLastCommand @0x1d6b20 (guarded by `mUndoList.size != 0`) and
  RedoLastCommand @0x1d6b40 (guarded by `mRedoList.size != 0`); redo's command id
  compares == 237 (0xED) @0x1d6b2c; the undo id is the adjacent dispatch case
  (presumed 236 — residual §8).

## 6. Submit caller census (DECODED — what one undo step covers)

`SubmitUndoable` is called from exactly 12 sites: cMoveTool Deselect/DropObject/
Release, cNewFloorTool.Release, cNewWallpaperTool.Release, cNewWallTool.Release,
cPoolTool.Release, cSimpleAltTool.Release (stairs/fireplace), cDirtTool.Release
(terrain), cWaterTool.Release, and cWinRoofPanel.TSOnCommand ×2 (pitch, pattern).
**One pointer-drag = one Undoable = one undo step**; a roof pitch/style click is
also one step. Undo therefore steps over whole drag gestures, not individual tiles.

## 7. Vtable slot law (semantically pinned)

`Undoable` virtual slots used by the wrappers: **vt+0x20 = CommitSelf,
vt+0x24 = UndoSelf, vt+0x28 = RedoSelf** — pinned by the call sites (Commit→+0x20,
UndoIt→+0x24, RedoIt→+0x28) and confirmed by cRoofUndoable::RedoSelf tail-calling
vt+0x24 (= its UndoSelf; a RedoSelf slot there would recurse infinitely). The
static vtable image could NOT be cross-checked (residual §8). Default
`CommitSelf` @0x192b80 = 8 B `return 0`.

## 8. Residuals (honest) — what a runtime capture would add

- **Vtable slot order**: semantically pinned (§7) but the static data image could
  not confirm entries — pef_load's relocation replay is approximate for late-stream
  C-run patches (vtable entries patch into that stretch and read back as neighboring
  8-byte stubs). A runtime capture (read object+0, dump 12 words) confirms §7 and
  the base-class slot fill.
- **Global G's identity**: TOC −0x778C (data 0x874) is an unnamed init-game global
  (value 0x8510c, a BSS address; ~600 refs binary-wide). Only `G+0x18 = cGameTools*`
  is pinned. A runtime capture names G's dynamic type.
- **FloorChange/WallpaperChangePacket internal layout**: record size (60 B) and
  fields' existence (tile, pattern, walls, cost, flags) are decoded from
  AddChange/UndoOne signatures and the ±60 B iteration; the byte-exact struct is
  not needed by the port (it builds its own records) and was not further decoded.
- **HouseViewer undo command id**: redo = 237 decoded; undo assumed 236 from the
  adjacent case block (the switch table itself not decoded).
- **TerrainUndoable snapshot array dimensions** (bytes per tile: 4 corners + a
  second array) decoded at the field level, not to exact array extents.
- **Sound/event names**: `GlobalDispatch(short, long)` @0x60ca0 — ids 145 (undo zap,
  latch-gated) and 238 (command blip); human-readable names unknown statically.
