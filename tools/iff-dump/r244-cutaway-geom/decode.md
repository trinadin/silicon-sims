# R244 — cutaway geometry decode (remaining R243 unknowns)

Read-only decode round against `game-data/The Sims/The Sims Complete`
(SHA256 `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`).
All addresses are file offsets into that executable. Disassembly excerpts in this
directory were produced with `r145/capdis2.py` at the addresses in their text.
No original assets copied; no production file touched.

This round resolves the R243 open items:

* A — direction table, rectangle-coordinate naming, ResolveDiagonal, sweep
  termination law of `Room::ComputeCutawayMatrix` 0x125130..0x1259ac.
* B — `HouseViewer::TileToPoint` 0x1d3850..0x1d3ab8 full projection.
* C — `GetTheoreticalWallExtent` 0x1cef30..0x1cf064 complete algebra
  (including `GetPackedAlt` 0x161e00..0x161ed4 and `HasWalls` 0x1259e0..0x125aa0).
* D — the room+34 eligibility word (named with writer evidence).

## 0. Container facts used this round

* Data section: PEF-packed at file 0x5C22F0, packed length 0x6D834, unpacks to
  0x7BF80 initialized bytes (`tools/iff-dump/pef_unpack.py` law, r104); BSS
  extends to 0x989A4. TOC: `r2 = data + 0x8000` (r143 law); a `lwz rX, off(r2)`
  reads the pointer stored at data offset `0x8000+off`.
* TOC slots resolved from the unpacked image (all BSS unless noted):

| slot | data addr of slot | value | role |
|---|---|---|---|
| -0x73e8 | 0xC18 | 0x7F040 (BSS) | 8-entry direction table (§A.1) |
| -0x73e4 | 0xC1C | 0x7F0B8 (BSS) | rotation LUT, 4 pages x 8192 bytes |
| -0x7048 | 0xFB8 | 0x907D0 (BSS) | current `HouseViewer` instance (+0/+4 origin, +8 zoom) |
| -0x7350 | 0xCB0 | 0x85C48 (BSS) | current `cRotatableWorld` pointer variable |
| -0x778c | 0x874 | 0x8510C (BSS) | game/sim object; its +4 is the viewer passed as `this` |
| -0x7214 | 0xDEC | 0x93C38 (BSS) | wall segment masks, 4 rotations x 256 bytes |
| -0x7354 | 0xCAC | 0x85C44 (BSS) | floor-table global (room-id grids) |
| -0x5790 | 0x2870 | 0x4AD40 (data img) | per-rotation altitude byte permutation (§C.2) |
| -0x5844 | 0x27BC | 0x59A698 (code) | literal pool in code (file 0x5A3528): `7fffffff 7fffffff 80000000 80000000` — INT_MIN/MAX pair for the dead min-box (§A.4) |

---

## A. Room::ComputeCutawayMatrix 0x125130..0x1259ac

### A.1 The direction table (R243 item: "decode the actual direction offsets")

`lwz r31, -0x73e8(r2)` at 0x12514C loads BSS 0x7F040. It is an array of eight
3-byte `CTilePt` records `{dx, dy, dz}` built once by a static initializer at
0x610B0..0x61250 (three `stb rX, off(r31)` stores per record followed by
`bl 0x590A30` — the `CTilePt` constructor; excerpt `dir-table-init.txt`):

| entry | offset | dx | dy | dz | store evidence |
|---|---|---|---|---|---|
| 0 | +0x00 | 0 | -1 | 0 | 0x61128 / 0x6112C / 0x61130 |
| 1 | +0x03 | 0 | +1 | 0 | 0x61144 / 0x6114C / 0x61154 |
| 2 | +0x06 | -1 | 0 | 0 | 0x61170 / 0x61168 / 0x61178 |
| 3 | +0x09 | +1 | 0 | 0 | 0x61194 / 0x6118C / 0x6119C |
| 4 | +0x0C | -1 | -1 | 0 | 0x611B0 / 0x611B8 / 0x611C0 |
| 5 | +0x0F | +1 | +1 | 0 | 0x611D4 / 0x611DC / 0x611E4 |
| 6 | +0x12 | +1 | -1 | 0 | 0x611F8 / 0x61204 / 0x6120C |
| 7 | +0x15 | -1 | +1 | 0 | 0x61220 / 0x6122C / 0x61234 |

(the eight compass+diagonal unit steps, dz always 0).

### A.2 Sweep law ("three candidate positions per sweep")

The walk state is one tile position stored as two bytes at `r1+0x40/0x41`
(sign-extended with `extsb` for every compare). Per sweep iteration the code is
unrolled three times, each advancing the position by one table entry:

* candidate 1 delta = entry 1 `(0,+1)` — `lbz r6,3(r31)` / `lbz r5,4(r31)` at
  0x125498/0x1254A0;
* candidate 2 delta = entry 6 `(+1,-1)` — `lbz r9,0x12(r31)` / `lbz r8,0x13(r31)`
  at 0x125638/0x125640;
* candidate 3 delta = entry 1 again — 0x1257BC/0x1257C4;
* on continuation, entry 1 again — 0x125948/0x125950, looping to 0x1254D4.

So per sweep with origin `s` the three absolute candidate cells are
`s+(0,+1)`, `s+(+1,0)`, `s+(+1,+1)`, and the next sweep's origin is `s+(+1,+1)`.
The sweep marches diagonally "toward the viewer" in rotated tile space, testing
the south, east and south-east frontier cells each step.

Per candidate (0x1254D4..0x125630, repeated at 0x1256C8.. / 0x125844..):

1. Bounds (strictly inset world bounds of `cRotatableWorld`): in bounds iff
   `world+0x70 + 1 <= x <= world+0x74 - 1` and `world+0x78 + 1 <= y <=
   world+0x7C - 1` (signed compares, evidence 0x1254DC/0x1254F4/0x12550C/0x125520).
   Out-of-bounds increments `r23` (0x125634 / 0x1257B8 / 0x12593C).
2. In bounds: the bit mask comes from helper 0x591B10 called with the universal
   argument triple `(r3, r4, r5) = (0, 1, x)`. The helper is a plain 64-bit
   left shift `r3:r4 <<= r5` (see helpers.txt): with `r3` = high word, `r4` =
   low word and PPC `slw/srw` zeroing for counts >= 32, the result for every
   integer `x` is exactly the single bit `1 << (x & 63)` placed in the
   corresponding word — x in 0..31 → `(0, 1<<x)`; x in 33..63 →
   `(1<<(x-32), 0)`; x = 32 → `(1, 0)` (high-word bit 0 only; no aliasing into
   the low word); negative x folds through `x & 63` the same way. There is NO
   mirroring into both row words (x=5 → `(0,32)`, x=40 → `(256,0)` — verified
   against the pinned instruction stream; see skeptic-response.md).
   Row = `room+0x7C + 8*(sbyte)y`, two 32-bit words OR'ed separately
   (`r3` → row+0, `r4` → row+4). NOTE: native indexes the row by the
   sign-extended y with NO bounds check beyond the world-bounds test — if the
   world's min/max admitted y outside the 64-row domain, the row write would
   walk below room+0x7C (unchecked by design; the port must keep the same
   domain assumptions or add an explicit guard).
   If the bit is already set in the row, the candidate is skipped WITHOUT
   incrementing `r23` (bne 0x125588 / 0x12570C / 0x125890).
3. Otherwise `GetTheoreticalWallExtent(viewer, {x, y, z})` (z = the room tile's
   z from `r1+0x42`, never modified during the sweep) and the strict overlap
   test below; overlap sets the bit.

### A.3 Sweep termination law

`cmpwi r23, 3; bge 0x125980` at 0x125940/0x125944 after candidate 3; otherwise
`li r23, 0` at 0x125954 and continue. Since `r23` counts only out-of-bounds
candidates (already-marked candidates skip without incrementing), the sweep
ends exactly when **all three candidate positions of a sweep are outside the
world bounds**; any in-bounds candidate resets the counter. There is no
radius/step cap. This proves the R243 statement with the exact register law.

### A.4 Rectangle-coordinate naming (room tile screen rect)

For each room tile (after rotation, §A.6):

* `TileToPoint` result word at `r1+0x5C`: `x = low 16 bits (lha 0x46(r1))`,
  `y = high 16 bits (lha 0x44(r1))` — matches the packing written inside
  TileToPoint (`sth y,0x48` / `sth x,0x4A` at 0x1D3A98/0x1D3A9C).
* `h = (viewer+0x58) / 2`, signed division truncating toward zero
  (`lwz 0x58(r30)`, `srwi 31`, `add`, `srawi 1` at 0x125168..0x125174);
  `r25 = h - 2` (0x1251BC).
* Tile rect words at `r1+0x88..0x94` (stores 0x1252EC..0x1252F8):

| field | addr | value |
|---|---|---|
| R0 | r1+0x88 | `x - h + 1` |
| R1 | r1+0x8C | `y + 1` |
| R2 | r1+0x90 | `x + h - 2` |
| R3 | r1+0x94 | `y + h - 2` |

* Diagonal sides 1/3 adjust the x-range (after the ResolveDiagonal id match):
  side == 1 → `R2 -= h` (0x1253A0..0x1253A8); side == 3 → `R0 += h`
  (0x1253B8..0x1253C0). Sides 2/4 change nothing.
* Dead min-box: `r1+0x78..0x84` initialized from the code literal
  (INT_MAX, INT_MAX, INT_MIN, INT_MIN, loaded 0x12518C..0x1251B4) and min'ed
  with each tile rect (0x125414..0x1254C0) — but the four words are never read
  afterwards in this function. Recorded as dead computation; do not port it.

### A.5 Strict overlap test (exact operand order)

Wall extent `W[0..3]` (sret at `r1+0x98` etc.) against tile rect `R0..R3`
(candidate 1 at 0x12559C..0x1255E8; candidates 2/3 identical at 0x125720.. /
0x1258A4..). PPC `subf rD,rA,rB` computes `rB - rA`:

| insn | computation | meaning |
|---|---|---|
| 0x1255AC `subf r4,r5,r4` (r5=W0, r4=R2) | `R2 - W0 - 1` (addi 0x1255B4) | wall left vs tile x-right |
| 0x1255BC `subf r3,r3,r0` (r3=R0, r0=W2) | `W2 - R0 - 1` | wall right vs tile x-left |
| 0x1255C8 `subf r5,r6,r5` (r6=W1, r5=R3) | `R3 - W1 - 1` | wall top vs tile y-bottom |
| 0x1255D4 `subf r5,r8,r7` (r8=R1, r7=W3) | `W3 - R1 - 1` | wall bottom vs tile y-top |

`or. r0, r4, r0` + `blt` (0x1255E4/0x1255E8): overlap iff **all four** values
`>= 0`, i.e. `R2 >= W0+1 && W2 >= R0+1 && R3 >= W1+1 && W3 >= R1+1`
(a touch-only edge does not count). W0/W2 are the wall's x-range
(`px-V5C-1 .. px+V5C-1`), W1/W3 its y-range (`py-F .. py+V60`) — see §C.

### A.6 Rotation of the room tile

If `world+0x84 (rot) != 0`, the tile bytes are replaced by the rotation LUT
pair addressed as `LUT + rot*8192 + x*128 + y*2 - 8192` (0x1251F8..0x125220).
Because of the `-0x2000` displacement the EFFECTIVE PHYSICAL PAGE is one less
than the rotation term: for the 64x64 tile domain (`x*128 + y*2` in
[0, 8190]) the in-page offset is `x*128 + y*2` and the physical page index is
`rot - 1` (so rot 1..3 address pages 0..2; rot 0 skips rotation entirely). The
LUT global is TOC -0x73E4 = BSS 0x7F0B8, four 8192-byte pages of space, built
at runtime (world load). HasWalls and GetPackedAlt instead address term
`((4-rot)&3)` on the ALREADY rotated coordinates — effective physical page
`((4-rot)&3) - 1` — i.e. pages `rot` and `(4-rot)&3` behave as inverse maps
(180 degrees self-inverse, consistent with the two observed uses).

### A.7 ResolveDiagonal (full behavior)

`RoomManager::ResolveDiagonal(manager, tile&, Room** outA, Room** outB,
Sides* sideA, Sides* sideB)` 0x128360..0x12851C (`resolve-diagonal.txt`).
Called from ComputeCutawayMatrix at 0x125318 with `manager = room+0x78` only
when `HasWalls(world, tile, segment 0x20) != 0` (0x1252C8/0x125300..0x125318).

* Map-hit path (`0x12A330` find over the tile map at manager+0x14, end sentinel
  manager+0x18; node fields `id1` u16@+0x10, `side1` u32@+0x14, `id2` u16@+0x18,
  `side2` u32@+0x1C, copied at 0x128438..0x12847C):
  * `outA = FindRoom(id1)` (find over manager+4..+8 via 0x12A120; end -> NULL,
    else `*(it+0x10)`), `sideA = side1` (0x128484..0x1284B0).
  * If `id2 >= 0xFFFB` (0x1284AC `cmplwi`): no second half — `outB = outA`,
    `sideA = 0`, `sideB = 0` (0x1284F4..0x128504). Note sideA is reset here.
  * Else `outB = FindRoom(id2)`, `sideB = side2` (0x1284C8..0x1284EC).
  * Returns 1 (0x128508).
* Map-miss path (0x1283AC..0x128434): read the room id straight from the floor
  grid — `*(floorGlobal) -> floor z entry at +tz*4+0x28 -> grid base at +0xC ->
  column grid[tx] (u32) -> u16 id at column + ty*2`; `outA = outB =
  FindRoom(id) or NULL`; `sideA = sideB = 0`; returns 0.

Caller-side post-processing (0x12531C..0x125410): each nonzero side is rotated
`side' = ((side - 1 + ((4 - rot) & 3)) & 3) + 1` (0x125330..0x125344 and
0x12535C..0x125370); then for each output room that is non-NULL and whose
`lhz 0(room)` equals this room's id: side 1 → `R2 -= h`, side 3 → `R0 += h`.

### A.8 Per-room inputs (context recap, now with names)

* room+0x00 id (u16); floor = `((id >> 10) & 3) + 1` (GetLevel 0x1250F0).
* room+0x08 tile count, room+0x0C tile list (3 bytes per tile).
* room+0x34 eligibility word — see §D.
* room+0x78 owning RoomManager (set by GetNewRoom, 0x128878).
* room+0x7C cutaway matrix: 64 rows x 64 bits (8 bytes per row), 512 bytes.
* `IsOutside` 0x124FE0 caches at room+0x71 (known) / room+0x72 (value); outside
  rooms keep an empty matrix (early return 0x125180..0x125188).

---

## B. HouseViewer::TileToPoint 0x1d3850..0x1d3ab8

True ABI (settled by the callee, `tile-to-point.txt`): `r3` = sret pointer to a
4-byte packed point; `r4` = `this` (viewer); tile bytes in `r5/r6/r7`
(stb to `r1+0x40/0x41/0x42` at 0x1D3884..0x1D388C); `r8` = zoom to project at;
`r9` = optional pointer to a precomputed altitude byte (only `*r9` is read,
0x1D393C/0x1D3940, copied into the altitude slot `r1+0x45`; when NULL the
function calls `GetPackedAlt(world, tileCopy)` at 0x1D3914 itself and uses that
record's byte 1).

Zoom handling (0x1D3944..0x1D399C, restore 0x1D39F0..0x1D39F8). The global
viewer instance (TOC -0x7048 = BSS 0x907D0) owns `+0` view-origin-x, `+4`
view-origin-y, `+8` current zoom. When the zoom argument differs from the
global zoom, the function projects with the origin RESCALED to the argument
zoom — but the rescale is **local to the call**: the pre-call global values are
written back at 0x1D39F0/0x1D39F4/0x1D39F8 (`r3`/`r4`/`r0` hold the values
loaded at 0x1D3944/0x1D3958/0x1D395C and are never reassigned in between), so
the stored current-view state is fully restored before return and the port
must NOT mutate it. The transient law (`subf. r6, r30, r3` at 0x1D3950 computes
`r6 = globalZoom - argZoom`; branch polarity `ble` at 0x1D3960):

```
r6 = globalZoom - argZoom
if r6 > 0:   effOrigin = origin / (1 << r6)   // 0x1D3968..0x1D3970, PPC divw: signed, truncates toward zero
else:        effOrigin = origin * (1 << -r6)  // 0x1D3980..0x1D3990
effZoom = argZoom                             // projection uses rescaled origin + argZoom
... project ...
globalZoom, originX, originY <- pre-call values  // 0x1D39F0..0x1D39F8
```

ComputeCutawayMatrix passes the current zoom, taking the fast path at
0x1D3A24 (`beq` 0x1D394C) with no rescale at all.

Projection (identical algebra on both paths; slow path 0x1D399C..0x1D3A94,
fast path 0x1D3A24..0x1D3A9C), with `z = globalZoom` after the update,
`tx/ty/tz` sign-extended, `F = this->floorHeights[3 - z]` where the array is at
`this+0x124` (`lwz 0x124(r4)` at 0x1D39C0 / 0x1D3A44, index `(3-z)*4`):

```
px = originX + (8 << z) * (tx - ty)                     // mullw/add, 0x1D3A84/0x1D3A8C
py = originY + (4 << z) * (tx + ty)
     - (tz - 1) * F                                     // 0x1D3A80/0x1D3A88
     - (altByte1 << (z + 1))                            // 0x1D3A90/0x1D3A94
return (short)py << 16 | (ushort)px                     // sth 0x48/0x4A, lwz, sret
```

`altByte1` is byte 1 of the 4-byte packed altitude record (§C.2). Coordinates
wrap as int16. `tz - 1` means floor 1 is the vertical baseline.

---

## C. GetTheoreticalWallExtent 0x1cef30..0x1cf064

ABI: `r3` = sret (16-byte rect), `r4` = `this` viewer, `r5` = `CTilePt&`
(confirmed by the caller: `addi r3, r1, 0x98; mr r4, r30; addi r5, r1, 0x40`
at 0x12558C..0x125598). It fetches the packed altitude record itself from the
current world (`*(TOC -0x7350)`) on a tile COPY (`GetPackedAlt` at 0x1CEF84).

Complete algebra (registers traced instruction-by-instruction in
`wall-extent.txt`), with `Z = globalViewer+8`, `F = this->floorHeights[3-Z]`
(`lwz 0x124(r30+(3-Z)*4)` at 0x1CEFC8), `altB1` = record byte 1
(0x1CEFE4, loaded from the staged copy at `r1+0x41`), `V5C = this+0x5C`,
`V60 = this+0x60`:

```
px = originX + (8 << Z) * (tx - ty)                       // 0x1CEFFC
q  = originY + (4 << Z) * (tx + ty)
     - (tz - 1) * F                                       // 0x1CF000/0x1CF018
     - (altB1 << (Z + 1))                                 // 0x1CF020/0x1CF024
W0 = px - V5C - 1        // 0x1CF010..0x1CF01C  (sret +0x00)
W1 = q - F               // 0x1CF028/0x1CF030    (sret +0x04)
W2 = px + V5C - 1        // 0x1CF004..0x1CF00C    (sret +0x08)
W3 = q + V60             // 0x1CF02C/0x1CF034     (sret +0x0C)
```

i.e. the projected rectangle of a "theoretical wall" at the tile: horizontally
centered on `TileToPoint.x` with half-width `V5C`, vertically spanning
`[py - F, py + V60]` where `(px, py)` is exactly the TileToPoint projection of
the same tile (same inputs, same zoom) — the `lfd/stfd` pair at 0x1CF038..0x1CF044
is only a 16-byte register copy, not floating-point math.

### C.1 HasWalls 0x1259e0 (the diagonal-segment gate)

`HasWalls(world, tile&, segment)` rotates the tile copy via LUT term
`(4-rot)&3` (effective physical page `((4-rot)&3) - 1`, §A.6; 0x1259E8..0x125A38), then reads the per-tile wall byte:
`*(*(world + 0x40 + 4*tz) + 0xC)[tx]` at byte offset `ty*14`
(0x125A3C..0x125A84), and the segment mask
`*(BSS 0x93C38 + ((4-rot)&3)*256 + segment*4)` (0x125A80..0x125A8C), returning
`(wallByte & mask) == mask` (0x1... 0x125A90..0x125A9C `and/subf/cntlzw/srwi`).
Only segment value 0x20 is used by ComputeCutawayMatrix (diagonal segment).

### C.2 GetPackedAlt 0x161e00 (packed altitude decode)

* Rotates the tile in place via LUT term `(4-rot)&3` (effective physical page
  `((4-rot)&3) - 1`, §A.6) when nonzero (0x161E08..0x161E5C) — same as HasWalls.
* The 4-byte record is fetched through a per-rotation byte permutation table at
  **unpacked data image** offset 0x4AD40 (TOC -0x5790; a data-virtual offset
  into the unpacked data section per §0, not a raw file offset), 16 bytes:
  rot0 `00 01 02 03`, rot1 `03 00 01 02`, rot2 `02 03 00 01`, rot3 `01 02 03 00`
  (immediately followed by the `cRotatableWorld` RTTI name — contextual check).
* Column base: `*(*(world+0x14) + 0xC)` indexed by `*(colTable + tx*4)`,
  advanced by `ty*4` (0x161E60..0x161E9C); then
  `record[k] = *(colBase + perm[rot][k])` for k = 0..3 (0x161E98..0x161EB8).
* Consumers only ever use byte 1 (the "altitude" byte that TileToPoint shifts
  by `zoom+1`); which physical byte that is rotates with the view:
  `colBase[1], colBase[0], colBase[3], colBase[2]` for rot 0..3.

---

## D. The room+34 eligibility word

Room+0x34 is written in exactly five places (byte-scan of 0x124000..0x12A000
found six `stw .., 0x34` stores; the sixth at 0x1296E4 is
`RoomManager::__ct` storing to the MANAGER's +0x34, a different object):

| site | function | value | evidence |
|---|---|---|---|
| 0x127384 | `Room::Clear` 0x127360 | **0** | `li r0,0; stw r0,0x34(r30)` |
| 0x125B38 | `Room::AbsorbNewRoomList` (unnamed; string fragment `bsorbNewRoomList` at 0x125B6C) | **1** | after Clear + tile-list copy `bl 0x129D80` |
| 0x128800 | `RoomManager::GetNewRoom` 0x1287B0, reuse path | **1** | after `Room::Clear` |
| 0x1288AC | `RoomManager::GetNewRoom`, fresh 0x284-byte allocation | **1** | after insert `bl 0x12BAA0` |
| 0x128D28 | `RoomManager::ComputeRooms` 0x128BA0 | **1** | after `Room::Clear` at 0x128D18 |

The word is only ever 0 (room cleared/reset) or 1 (room handed out / rebuilt),
and every reader in the room system treats it as a nonzero gate
(`ComputeCutaway` 0x128B0C; also ResetRooms 0x127ECC/0x127F94/0x12805C/0x128098,
PrintStats 0x1289E0/0x128A08, ComputeRooms 0x128C94/0x128E54/0x128EB8/0x129010,
AllRoomsScoreChanged 0x1290D4, AllRoomsLightingChanged 0x1291C4).

**Name: "room is live/in-use" boolean.** GetNewRoom allocates or recycles a
room, Clear marks it dead (0), and the room becomes live (1) when fully handed
out or rebuilt. It is NOT a dirty bit (it is set to 1 unconditionally on every
GetNewRoom/ComputeRooms rebuild, not on change) and NOT a wall/tile count
(ComputeCutaway only tests `!= 0`). This resolves the R243 warning with writer
evidence.

---

## PORTABLE PSEUDOCODE CONTRACT (C#-ready)

Native field names below are semantic; every formula is instruction-proven.
`V8(V)` denotes the global current-viewer instance (origin/zoom owner);
`this` is the viewer object passed by the caller (native passes the sim's
current viewer from `*(simGlobal)+4`; origin/zoom always read from the global
instance, per the register evidence).

```csharp
// ---- native data contracts (offsets in comments) ----
class HouseViewer {                 // viewer object ('this')
    int   TileHalfRaw;             // +0x58  (h = RawSDiv2(TileHalfRaw))
    int   WallHalfWidth;           // +0x5C
    int   WallHalfHeight;          // +0x60
    int[] FloorHeights;            // +0x124, 4 ints, indexed [3 - zoom]
}
static class CurrentViewer {        // BSS 0x907D0 (TOC -0x7048)
    public static int OriginX;     // +0x00
    public static int OriginY;     // +0x04
    public static int Zoom;        // +0x08  (0..3)
}
class RotatableWorld {              // *(TOC -0x7350)
    public int MinX, MaxX, MinY, MaxY;  // +0x70/+0x74/+0x78/+0x7C (rotated space)
    public int Rotation;                // +0x84 (0..3)
    public byte[] PackedAlt(CTilePt t); // GetPackedAlt: 4 bytes; byte[1] used
    public bool HasWalls(CTilePt t, int segment); // (byte & mask) == mask
}
class Room {
    public ushort Id;               // +0x00 ; Floor = ((Id >> 10) & 3) + 1
    public int    TileCount;        // +0x08
    public CTilePt[] Tiles;         // +0x0C  ({x,y,z} bytes)
    public bool   Live;             // +0x34  (native stores only 0 or 1)
    public bool   IsOutside;        // +0x71/+0x72 cached pair
    public RoomManager Manager;     // +0x78
    public uint[][] Matrix;         // +0x7C  64 rows x 2 words (row+0 = word0, row+4 = word1)
}
static readonly (int dx, int dy)[] Dir = {          // BSS 0x7F040, init 0x610B0
    (0,-1),(0,1),(-1,0),(1,0),(-1,-1),(1,1),(1,-1),(-1,1)
};                                                  // entries 0..7, dz = 0

static int SDiv2(int v) => (v + (v >> 31)) >> 1;    // srawi pair, trunc toward 0
static int SDiv(int a, int b) {                     // PPC divw semantics
    int q = a / b; if ((a % b != 0) && ((a < 0) != (b < 0))) q++; return q; }

// ---- HouseViewer::TileToPoint (0x1D3850) ----
// zoom argument: caller's zoom; ComputeCutawayMatrix always passes CurrentViewer.Zoom
// altOut: optional caller-provided altitude BYTE (native reads *r9 only);
// when null, GetPackedAlt supplies record byte 1. CurrentViewer is NEVER mutated:
// the rescaled origin exists only inside this call (restored 0x1D39F0..0x1D39F8).
(short px, short py) TileToPoint(HouseViewer v, CTilePt t, int zoom, byte[] altOut = null) {
    byte altB1 = altOut != null ? altOut[0] : RotatableWorld.Current.PackedAlt(t)[1];
    int gz = CurrentViewer.Zoom;
    int effX = CurrentViewer.OriginX, effY = CurrentViewer.OriginY, effZ = gz;
    if (zoom != gz) {
        int d = gz - zoom;                       // subf r6, arg, global (0x1D3950)
        if (d > 0) {                             // globalZoom > argZoom: divide
            effX = SDiv(effX, 1 << d); effY = SDiv(effY, 1 << d);
        } else {                                 // globalZoom <= argZoom: multiply
            int s = 1 << -d; effX *= s; effY *= s;
        }
        effZ = zoom;
    }
    int F  = v.FloorHeights[3 - effZ];
    int tx = (sbyte)t.X, ty = (sbyte)t.Y;
    int x = effX + ((8 << effZ) * (tx - ty));
    int y = effY + ((4 << effZ) * (tx + ty))
          - ((t.Z - 1) * F) - (altB1 << (effZ + 1));   // (sbyte)t.Z - 1
    return ((short)x, (short)y);                     // int16 wrap; packed y<<16|x in native
}

// ---- GetTheoreticalWallExtent (0x1CEF30) ----
// returns W[0..3]: x-range = W0..W2, y-range = W1..W3
int[] GetTheoreticalWallExtent(HouseViewer v, CTilePt t) {
    int Z  = CurrentViewer.Zoom;
    int F  = v.FloorHeights[3 - Z];
    byte altB1 = RotatableWorld.Current.PackedAlt(t)[1];
    int tx = (sbyte)t.X, ty = (sbyte)t.Y, tz = (sbyte)t.Z - 1;
    int px = CurrentViewer.OriginX + ((8 << Z) * (tx - ty));
    int q  = CurrentViewer.OriginY + ((4 << Z) * (tx + ty))
           - (tz * F) - (altB1 << (Z + 1));
    return new[] { px - v.WallHalfWidth - 1, q - F,
                   px + v.WallHalfWidth - 1, q + v.WallHalfHeight };
}

// ---- Room::ComputeCutawayMatrix (0x125130) ----
void ComputeCutawayMatrix(Room room) {
    Array.Clear(room.Matrix);                        // 512 bytes
    if (room.IsOutside) return;
    var world = RotatableWorld.Current;
    var viewer = <sim current viewer>;               // *(simGlobal)+4
    foreach (var t0 in room.Tiles) {                 // tile list, 3 bytes each
        int x = t0.X, y = t0.Y;
        if (world.Rotation != 0)
            (x, y) = RotLut[world.Rotation].Map(x, y);   // eff. phys. page rot-1; UNRESOLVED(1)
        // seed own bit; NOTE: row indexed by (sbyte)y unchecked beyond world bounds
        var (s0, s1) = Mask64(x);
        room.Matrix[(sbyte)y][0] |= s0; room.Matrix[(sbyte)y][1] |= s1;
        var (px, py) = viewer.TileToPoint((byte)x, (byte)y, t0.Z, CurrentViewer.Zoom);
        int h  = SDiv2(viewer.TileHalfRaw);
        int R0 = px - h + 1, R1 = py + 1, R2 = px + h - 2, R3 = py + h - 2;
        if (world.HasWalls(new CTilePt((byte)x, (byte)y, t0.Z), 0x20)) {
            var (ra, sa) = ResolveDiagonal(room.Manager, x, y, t0.Z, slotA);
            var (rb, sb) = slotB;
            sa = RotateSide(sa, world.Rotation);     // ((s-1+((4-rot)&3))&3)+1
            sb = RotateSide(sb, world.Rotation);
            if (ra != null && ra.Id == room.Id) { if (sa == 1) R2 -= h; else if (sa == 3) R0 += h; }
            if (rb != null && rb.Id == room.Id) { if (sb == 1) R2 -= h; else if (sb == 3) R0 += h; }
        }
        // sweep: entry1, entry6, entry1 deltas per sweep; origin advances to 3rd candidate
        int sx = x, sy = y, oob;
        int[] step = { 1, 6, 1 };                    // Dir indices, per unrolled candidate
        while (true) {
            oob = 0;
            foreach (int e in step) {
                sx = (byte)(sx + Dir[e].dx); sy = (byte)(sy + Dir[e].dy);  // byte wrap
                int cx = (sbyte)sx, cy = (sbyte)sy;
                bool inb = world.MinX + 1 <= cx && cx <= world.MaxX - 1
                        && world.MinY + 1 <= cy && cy <= world.MaxY - 1;
                if (!inb) { oob++; continue; }
                var (m0, m1) = Mask64(cx);
                if (((room.Matrix[cy][0] & m0) | (room.Matrix[cy][1] & m1)) != 0)
                    continue;                                     // no oob++ here
                var W = GetTheoreticalWallExtent(viewer, new CTilePt((byte)cx, (byte)cy, t0.Z));
                if (R2 >= W[0] + 1 && W[2] >= R0 + 1 && R3 >= W[1] + 1 && W[3] >= R1 + 1) {
                    room.Matrix[cy][0] |= m0; room.Matrix[cy][1] |= m1;
                }
            }
            if (oob == 3) break;                     // all three candidates outside world
        }
    }
}

// ---- RoomManager::ComputeCutaway gate (0x128AE0) ----
void ComputeCutaway(RoomManager mgr, int floorArg) {
    foreach (var room in mgr.Rooms)
        if (room.Live && (floorArg == 0 || floorArg == ((room.Id >> 10) & 3) + 1))
            ComputeCutawayMatrix(room);
}

static int RotateSide(int s, int rot) => s == 0 ? 0 : ((s - 1 + ((4 - rot) & 3)) & 3) + 1;

// exact law of native matrix-mask helper 0x591B10 (args (0,1,x)): a plain
// 64-bit shift; single bit, correct word. x=32 -> (1,0); NO two-word mirroring.
static (uint w0, uint w1) Mask64(int x) {
    int c = x & 63;                          // PPC slw/srw count (>=32 -> zero)
    if (c < 32) return (0, 1u << c);
    return (1u << (c - 32), 0);
}
```

Note on the sweep loop above: the three `Dir` steps are applied statefully in
native order (entry1, entry6, entry1); the absolute cells equal sweep-origin +
{(0,+1), (+1,0), (+1,+1)} and the origin advances by (+1,+1) per sweep.

---

## UNRESOLVED (with reason)

1. **Rotation LUT contents/generator (BSS 0x7F0B8).** The four 8192-byte pages
   are built at runtime (world load); this pass proved the addressing law
   (effective physical pages: `rot - 1` in ComputeCutawayMatrix 0x1251F8,
   `((4-rot)&3) - 1` in HasWalls 0x125A70.. / GetPackedAlt 0x161E08..; only
   pages 0..2 addressed in the 64x64 domain) and that the two selections act
   as inverse maps. The generator itself was not located
   (candidate users of TOC -0x73E4 are listed in the round scan; world-load
   path not decoded). A portable port needs this generator.
2. **Friendly semantics of viewer +0x58/+0x5C/+0x60/+0x124.** The arithmetic
   roles are exact (tile rect half-size, wall half-width, wall half-height,
   per-zoom floor-height array indexed [3-zoom]); their UI-facing names and
   the array's per-zoom values are runtime viewer state, not statically
   decodable constants.
3. **Physical meaning of the 4 packed-altitude bytes.** Only byte 1 is consumed
   anywhere decoded; the column-table data behind `*(world+0x14)+0xC` is world
   data whose per-byte meaning (altitude vs slope flags) is inferred from
   usage only.
4. **ResolveDiagonal node-map builder.** The tile->node map (manager+0x14, node
   fields +0x10/+0x14/+0x18/+0x1C) is filled by room partitioning
   (ComputeRooms path), not decoded here; the Sides enum values 1..4 are
   decoded only at their use sites (1 = left half, 3 = right half of the
   screen rect; 2/4 no rect change).
5. **Wall segment enum.** Only segment 0x20 (diagonal) appears in this path;
   the full segment id list and per-segment mask layout in BSS 0x93C38 are not
   decoded.
6. **Dead min-box purpose.** The per-tile min rectangle at r1+0x78 (initialized
   INT_MAX/INT_MIN from the 0x59A698 literal) is never read in
   ComputeCutawayMatrix; recorded as dead code, purpose unknown.

## Reproduction

`python3 tools/iff-dump/r244-cutaway-geom/verify.py` pins the executable SHA,
132 instruction words relied on this round (two floor-gate addresses — 0x128B0C,
0x128B24 — intentionally also appear in r243's set, per skeptic request, so
this round's file is self-contained), and runs pure-python fixtures:
direction-table reconstruction from the pinned init instruction stream, the
native matrix-mask helper law (two-word form, single-bit placement, x=32 case,
counter-evidence against two-word mirroring), TileToPoint projection on
synthetic inputs, the zoom rescale law exactly as at 0x1D3950..0x1D3998
(divide when globalZoom > argZoom, else multiply) including the global-state
restore of 0x1D39F0..0x1D39F8, GetTheoreticalWallExtent box algebra, the
strict overlap truth table, the sweep candidate/termination enumeration, the
side-rotation law, and the floor-filter law. It writes only
`verified-inputs.json` inside this directory. Disassembly excerpts:
`compute-cutaway-matrix.txt`, `tile-to-point.txt`, `wall-extent.txt`,
`get-packed-alt.txt`, `has-walls.txt`, `resolve-diagonal.txt`,
`dir-table-init.txt`, `room-flag-writers.txt`, `helpers.txt`.
