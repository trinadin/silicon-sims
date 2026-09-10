# R186 House score curves: exact subset and residuals

Source is the user's owned **The Sims Complete** executable and
`GameData/Global/Global.far!Global.iff`. This note records behavior and tuning
data only; it does not copy game art or redistribute an asset.

## Resource proof

`House::Initialize` (`0x08d3e0`) constructs the `PiecewiseFn` objects at
`House+96` and `House+100`, gives each eight slots, loads global `STR# 505`, and
passes strings 1 and 2 to `PiecewiseFn::AddPointsFromText` at `0x08d534` and
`0x08d550`. The owned chunk is:

- member: `Global.far!Global.iff`
- chunk: `STR# 505`, label `HouseScoreCurves`, 172 bytes
- full-chunk SHA-256:
  `17706c053cdea8ccc9e2ddbc08dbf361476b60157add074ca18156d35ae395f0`
- size curve: `(0;0) (150;25) (300;50) (600;75) (1200;100)`
- furnishings curve: `(5;0) (4;10) (3;20) (2;35) (1;60) (0;100)`

`PiecewiseFn` clamps outside the first/last key, linearly interpolates between
adjacent points, and each House getter converts with `fctiwz` (truncate toward
zero).

## Size: exact and implemented

`House::GetHouseStats` (`0x08c2e0`) calls `Family::CountMembers` (`0x076c10`)
and writes it to `HouseStats+4`. `House::GetSizeScore` (`0x08bf90`) returns zero
when that count is zero; otherwise its curve input is the single-precision
quotient `HouseStats.squareFeet / HouseStats.numSims`. The first STR# 505 curve
then supplies the score.

Port mapping is direct: the existing square-foot result plus
`CurrentFamily.FamilyGUIDs.Length`, the persistent member vector corresponding
to native `Family::CountMembers`. `OriginalHouseStats.ComputeSizeScore` exposes
the pure law and `UIHouseSubpanel` now feeds it to bar 0. The `uihouse` gate pins
the guard, every knot, interpolation/truncation, both clamps, and the live bar.

## Furnishings: exact and implemented

The native getter at `0x08be4c` reads `ObjectModule+0xa0` and
`ObjectModule+0xa4`. With a nonzero `+0xa0`, the second curve's input is the
single-precision quotient `(+0xa4)/(+0xa0)`; a zero denominator uses `100.0f`.
Earlier R135 prose mistook PowerPC's `0x43300000` integer-to-double conversion
word for a literal `1127219200.0` numerator; that interpretation is corrected
here.

The complete writer chain is `House::PrepareForBudgetWindow`,
`House::EnterLiveMode`, the matching command path, and the save path
(`0x08c580/0x08c6f8/0x08c934/0x08cca0`). Each calls
`ObjectModule::ComputeStats` (`0x0e2820`) and stores output 1 at `+0xa0`.
Output 2 plus `cFixedWorld::ComputeArchValue` is stored at `+0xa4`.

The exact `ComputeStats` value phase (`0x0e29a0..0x0e2b10`) visits one
representative per multitile, skips OBJD type 7, and uses
`cXObject::GetCurrentValue`. That getter halves the signed current value toward
zero whenever the object or any multitile part has a nonzero `RepairState`.
An object enters output 1 only when `IsInWorld` (tile X/Y both in
`[1, lotDimension-2]`) and `IsDeletedByEvict`; output 2 instead admits master
`BuildModeType != 0 && != 4`. Type 4 is trees/plants and is intentionally in
neither sum.

These are the same two channels used by the port's TS1 SIMI save law: movable
objects (`SimulationInfo.ObjectsValue`) and fixed build-mode objects; the
latter is combined with the already-decoded architecture value to form
`SimulationInfo.ArchitectureValue`. `VMArchitectureStats.GetObjectValue` now
implements the native representative, bounds, eviction, category, and damaged
value rules, so it and `GetArchValue` are direct port equivalents. In
particular, `HouseStats+28` (`FurnishingsValue`) remains a separate indoor-room
aggregate and is not substituted into this score.

`OriginalHouseStats.ComputeFurnishingsScore` preserves the native
integer-to-single conversion, single-precision division, piecewise
interpolation, and truncation sequence; House bar 1 now reads the live result.
The score inherits `GetArchValue`'s already-disclosed missing roof-tile
contribution because the port has no roof-tile layer.

## Yard: exact and implemented

`House::GetYardScore` (`0x08bda0`) computes and truncates
`clamp(RoomManager+0x4c * yardMultiplier, 0, 100)`. `RoomManager::ComputeRooms`
(`0x128e98..0x128ee0`) resets `+0x4c` and accumulates `Room+0x68` for rooms for
which `Room::IsOutside` is true.

`NeighborhoodConstants::UpdateConstants` (`0x0b7570`) loads the multiplier by
the key `yard score multiplier`. The owned `Global.iff` `FCNS 1024` value and
the executable default pool both encode the same single-precision value
`0.33000001311302185f` (source text/default `0.33`).

The missing `Room+0x68` operand is now decoded end to end:

- `cXObject`'s 72-short stack-object-variable array starts at `+0x4a`, so
  `RoomImpact` (variable 7) is the signed short at `+0x58`.
- `Room::CollectObjectStats` (`0x125e30..0x1261a0`) skips hidden and
  out-of-world entities. For an outside room it adds negative `RoomImpact`
  values to `Room+0x50`; every nonnegative value increments `Room+0x54`.
- The earlier `cXObject+0x9a` / `0x400` test is variable 40 (`FlagField2`) bit
  10, `GeneratesLight`. It gates only the light-registration block and jumps
  directly to the room-impact collection at `0x126158` when clear; it is not a
  room-score eligibility filter.
- `Room::ComputeRoom`'s outside branch (`0x126988..0x1269dc`) writes
  `Room+0x68 = Room+0x54 * 1.0f + Room+0x50 * 2.0f`. The named constants are
  `outdoor good object count factor` and `outdoor room impact factor`; their
  executable defaults are 1 and 2.
- `RoomManager::ComputeRooms` (`0x128e98..0x128ee0`) sums `Room+0x68` over
  outside rooms into `RoomManager+0x4c`, exactly the Yard getter operand.

The port now performs that per-entity/per-room walk over `VMRoomInfo`, preserves
the native float accumulation, and feeds bar 2 through the exact multiplier,
clamp, and truncation. It intentionally does not use `RoomLighting.RoomScore` or
the unrelated outdoor purchase-value aggregate.
