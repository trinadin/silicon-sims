# R243 read-only cutaway investigation

No production change recommended from this bounded investigation. The proposed helper shortcut “generate cutaway for the PIP target's room” is not supported by the original. This directory contains derived disassembly/metadata only. No build, app launch, original-data write or settings change was performed.

## Verified findings

### ComputeCutaway's integer is a floor filter

`RoomManager::ComputeCutaway(int)` file128ae0..128b60 iterates its room list. It skips rooms whose +34 word is0. For the remaining rooms, argument0 means all floors; otherwise the argument must match **((roomID >> 10) & 3) + 1**. It calls `Room::ComputeCutawayMatrix`125130 for each matching room. `Room::GetLevel`1250f0 independently uses the identical expression.

This routine does not select a target room, test the PIP target, or derive line of sight from the view center. The meaning of the room+34 eligibility word remains unnamed here; its nonzero gate is proven. Do not silently reinterpret it as a dirty bit or wall count.

### Per-room masks are projected overlap geometry

`Room::ComputeCutawayMatrix`125130..1259ac:

* Clears512 bytes at room+7c: one 64×64 bit matrix.
* Returns immediately for `IsOutside()`. An outside room's matrix is therefore empty.
* Gets the current HouseViewer from the simulator, current rotated-world transform, tile projection scale, and the room's three-byte tile list (+8 count, +c pointer). It does not read tracked/selected person state.
* For each room tile, rotates it through the current world rotation lookup when rotation is nonzero, seeds its own bit, and projects the tile with `HouseViewer::TileToPoint`1d3850.
* Forms an inset screen rectangle for the room tile using half of viewer+58 (signed divide by2). It handles diagonal wall segment0x20 by `ResolveDiagonal`, matching the room ID and rotating side enums; sides1/3 adjust the rectangle bounds.
* Advances through three candidate positions per sweep, bounded by the rotated world's interior. For unmarked candidates it calls `GetTheoreticalWallExtent`1cef30 and checks strict rectangle overlap (four edge differences minus1 must all be nonnegative) against the current room tile's rectangle. Overlapping wall extents set the candidate bit.
* It continues sweeps until all three candidate positions are outside the world bounds, then advances to the next room tile. There is no literal radius4/5 bound in this routine.

The direction table entries and complete rectangle-coordinate naming have not been independently normalized into portable code here. The key distinction is nevertheless proven: native masks depend on projected wall extents, terrain altitude, viewer scale/floor-height terms and diagonal room sides, rather than merely a fixed number of room-map samples.

`GetTheoreticalWallExtent` independently reads packed altitude, current projection zoom, viewer+5c/+60 dimensions, and viewer+124 indexed floor-height data; it computes a projected rectangle. A change in PIP zoom or target floor can therefore require recomputing native per-room masks even when room membership itself is unchanged.

### Dynamic selection is a separate operation

`HouseViewer::DoDynamicCutaway(bool)`1cf3b0..1d026c:

* Returns immediately when viewer+49 (dynamic cutaway enabled) is false.
* Copies the prior viewer+204512-byte result to a temporary matrix.
* If its boolean argument is true, clears the result and ORs the matrices of rooms in viewer+1fc `cCutawaySet`. It notes whether any such room is outside. This boolean is not “target room” or a floor argument.
* Independently, when `CPState::GetMode()==2`, chooses a person: if `Animator` tracked-object global is null, uses `ObjectModule::GetSelectedPerson`; otherwise dynamically casts the tracked object from cXObject to cXPerson. A tracked nonperson does not fall back to selected person in this branch.
* Requires a nonnull chosen person, its floor at+110 equal to viewer+18, and its room ID at+aac below fffb. It then ORs that room's matrix. If the person is outside, a separate bounded rectangle around that person's tile is added.
* Then considers the current strategy and mouse position. It calls the strategy's virtual+5c suppression predicate and verifies mouse coordinates against the main animation buffer. The mouse Y is adjusted by **32 << viewer zoom** before the viewer screen-to-tile virtual call. Further direction offsets/rectangle iteration mark a cursor vicinity, and a strategy virtual callback can modify the matrix.
* XORs old/new matrices, uses differences to dirty the viewer, and stores the supplied bool back at+1f8. Outside-person, cursor and strategy subpaths are distinct from per-room matrix generation.

Numeric CPState mode2 is retained as evidence; this investigation does not infer its friendly mode name from a port enum. The exact mouse/strategy virtual-slot identity and full outside rectangle algebra remain to be recovered before reimplementing this routine.

The main HouseViewer constructor passes3 to `cCutawaySet` at1d9640. `insert`1aa030 checks for an existing value, leaves duplicates in place, and evicts one entry when capacity is reached before adding the new ID. This supports a three-room history but does not alone prove which mouse/selection events insert rooms. Those insertion callers are not decoded in this bounded pass.

### Reset and secondary render state

`ResetDynamicCutaway`1cf240..1cf374 scans prior set bits across current world bounds, invalidates those tiles at **viewer+18**, clears viewer+204512-byte matrix, writes0 to viewer+1f8 and dirties viewer+4e. It does not itself clear the history set; callers do so explicitly.

The original secondary `Render`1c0c60 proves:

1. It saves the tracked person and calls **SetTrackedObject(NULL)** at1c0ed4, before changing the secondary view. The setter47cb10 proves TOC-70b8 is precisely the tracked object global.
2. When target floor differs, it disables the old floor's wall cutaway, clears the history set, resets dynamic cutaway using the old viewer floor, writes the target floor, calls **ComputeCutaway(target floor)**, then calls DoDynamicCutaway if enabled.
3. On restoration it performs the matching floor reset/recompute sequence and later restores the tracked person at1c16fc.

There is no target-object argument to any of those room-selection operations. During the secondary operation the tracked global is null, so DoDynamicCutaway's mode2 person branch uses the selected person, subject to the target-floor match. The PIP target may coincide with that person but is not substituted by this code. The cursor/strategy branch also remains a relevant input.

## Maintained implementation comparison

`UILotControl.UpdateCutaway`:

* Maintains up to three hovered interior room IDs, even while not in dynamic mode; outside hover is not persisted.
* Desktop dynamic mode combines this history with a shifted5×5 mouse rectangle. It currently does not add the native selected/tracked-person room as a separately gated input.
* Recomputes room masks on floor/rotation/history changes via `VMArchitectureTools.GenerateRoomCut`. Mobile selects all indoor rooms on the floor instead of using desktop mouse behavior.

`GenerateRoomCut` uses the requested floor's room map and fixed5/4 step rays in three nearby directions. It does not take projection zoom, altitude, theoretical wall extents or diagonal-room halves. `ApplyCutRectangle` adds direct tile cuts. Their tile-membership output is an approximation of the original screen-overlap mask, not established native-equivalent geometry.

`WorldPictureInPictureRenderer.Render` temporarily shares `Blueprint.Cutaway`: same-floor PIPs use the main mask; cross-floor PIPs replace it with an all-false array. It restores the original array in `finally` and uses a separate wall component/view. Object `CutawayHidden` is respected only when the PIP and main floor coincide. No native room-mask recomputation is implemented there. Therefore cross-floor empty mask is incomplete compared with original recomputation, but generating a mask for the target's room would also be unjustified.

## Why no narrow production fix is proposed

There is no direct correction “target room → correct room” because the port lacks several separate native inputs and its mask-generation law differs. Adding the selected person's room alone would still need the CPState mode mapping, tracked-person ownership, room-history lifecycle, projection-sensitive mask and cursor/strategy rules. These affect both main-view wall visibility and PIP results, including which sprites inherit cutaway hiding. A cross-floor patch that mutates the shared Blueprint mask also risks changing main wall/object caches unless its draw state and restoration are explicitly owned.

A justified implementation should first derive the remaining direction-table/diagonal rectangle details and dynamic virtual callbacks, then introduce a view-specific cutaway calculation with explicit floor, rotation, zoom, selected/tracked person, room history, pointer/strategy inputs. PIP must preserve main mask identity/content, room history, object hidden flags and wall draw state through success and exceptions. Main rendering would need its own parity review rather than incidental change through a shared helper.

Required independent fixtures include: floor0/all versus floors1..4; indoor/outside room masks; flat versus sloped terrain; diagonal half-rooms; all rotations and zoom0..3; selected person on/off secondary floor; tracked person versus tracked nonperson versus null; outside selected person; pointer outside viewport and strategy-suppressed; same-floor versus cross-floor secondary render; and GPU pixel/state restoration against the main view. Fixed synthetic rectangles alone cannot establish full native room-selection parity.

## Reproduction

Run `python3 tools/iff-dump/r243-cutaway/verify.py`. It pins executable SHA256 `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`,29 key instruction words, and six independently calculated packed-room floor fixtures. This passed locally. Adjacent disassembly files were produced by `r145/capdis2.py` at the addresses in their text; all are original executable file offsets. No original assets are copied here.
