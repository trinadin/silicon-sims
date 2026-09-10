# R188 House Furnishings/Yard data-law audit

Source is the user's owned **The Sims Complete** PowerPC executable and game
data.  This note records behavior, constants, and hashes only; it does not copy
or redistribute proprietary art.

## Inputs and verdict

- `game-data/The Sims/The Sims Complete` SHA-256:
  `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`
- `GameData/Build.iff` `STR# 0x81` (`Wall`) full-chunk SHA-256:
  `c616dbb9dcd8f73aa2a0ab34be0bd26d4375d4f51be4c7a57698fb864c880cff`
- Native House label/bar order remains exactly **Size, Furnishings, Yard,
  Upkeep, Layout** (`cWinSubpanelHouse::TSPaint` `0x2a0458..0x2a04c4`).
  `UIHouseSubpanel` already wires bars 1 and 2 to Furnishings and Yard and
  requires no geometry, label, or bar-order change.
- The Yard operand and score law in `OriginalHouseStats` are exact.
- The Furnishings quotient, curve, object split, and damaged-object law are
  exact.  One production mismatch remained in the architectural numerator:
  the port halved each wall segment before summing, while native halves the
  aggregate wall-structure value once.  R188 corrects that rounding boundary.

## Furnishings end to end

`HouseStats::GetFurnishingsScore` (`0x08be30`) reads signed cached integers at
`ObjectModule+0xa0/+0xa4`.  If `+0xa0` is zero its curve input is `100.0f`;
otherwise it converts both integers to single precision and evaluates
`float(+0xa4) / float(+0xa0)`.  `House::Initialize` loads the second
`Global.iff STR# 505` curve:

`(5;0) (4;10) (3;20) (2;35) (1;60) (0;100)`.

`ObjectModule::ComputeStats` (`0x0e29a0..0x0e2b10`) supplies the pair.  It
visits one multitile representative, skips OBJD type 7, routes an in-world
`IsDeletedByEvict` object's signed `GetCurrentValue` to `+0xa0`, and routes
master `BuildModeType != 0 && != 4` to `+0xa4`.  `GetCurrentValue`
(`0x0c9dc0`) halves toward zero when any multitile part has nonzero
`RepairState`.  The current `VMArchitectureStats.GetObjectValue` mapping is
therefore exact.

### Architectural rounding boundary (corrected)

`cFixedWorld::ComputeArchValue` keeps two integer accumulators:

- `r28`: wallpaper at full price, each floor price already halved, and roof;
- `r29`: every wall-style/structure price at full price.

Every wall segment adds its unhalved style-table value to `r29` at
`0x15eba0..0x15ebb0`.  Only after the entire two-story/tile walk does native
execute signed divide-by-two toward zero at `0x15eebc..0x15eec4`, then add the
result to `r28` at `0x15eec8`.

This is not algebraically interchangeable with per-segment integer division.
The owned `Build.iff STR# 0x81` includes odd wall-style prices 45 and 35.  Two
45-value segments therefore contribute `trunc((45+45)/2) = 45` natively; the
old port computed `trunc(45/2)+trunc(45/2) = 44`.

R188 changes `VMArchitectureStats.GetArchValue` to accumulate wall styles in
`wallStyleValue` and call `CombineArchitectureValue(nonWall, wallStyles)` once.
The pure helper also pins signed truncation (`-45 / 2 == -22`) even though
shipped style prices are nonnegative.

### Floor rounding is deliberately still per tile

Flooring has a different native rounding boundary and the existing port was
already correct.  Each floor-price lookup is immediately divided by two and
then added to `r28`, before the next tile/slot is examined:

- `0x15ed44` lookup, `0x15ed48..0x15ed50` signed `/2`, add at `0x15ed54`;
- `0x15ed6c`, divide `0x15ed70..0x15ed78`, add `0x15ed7c`;
- `0x15eda0`, divide `0x15eda4..0x15edac`, add `0x15edb0`;
- `0x15edc8`, divide `0x15edcc..0x15edd4`, add `0x15edd8`.

Thus `GetFloorPrice(pattern) / 2` must remain inside the per-tile walk.  R188
does not move flooring into the aggregate wall-style accumulator.

## Yard end to end (no production correction)

`RoomManager::ComputeRooms` constructs an `ObjectIterator` for every tile in a
room (`0x128d90..0x128dc4`), so multitile parts and co-located/slotted entities
are collected per entity, not deduplicated by object group.  This corresponds
to each port entity appearing in its `VMRoomInfo.Entities` list.

`Room::CollectObjectStats`:

- skips `Hidden != 0` at `0x125e30..0x125e38`;
- skips object-data `Room < 0` at `0x125e3c..0x125e44`;
- reads signed `RoomImpact` at `cXObject+0x58`;
- for an outside room, adds negative impact to `Room+0x50`; every nonnegative
  entity increments `Room+0x54` (`0x126158..0x1261a0`).

`Room::ComputeRoom` then computes
`float(goodCount) * 1.0f + float(negativeImpact) * 2.0f` at
`0x126994..0x1269dc`.  `RoomManager::ComputeRooms` resets `+0x4c` and adds
`Room+0x68` for each outside room at `0x128e98..0x128ee0`.
`HouseStats::GetYardScore` multiplies that float sum by the owned FCNS/default
single `0.33000001311302185f`, clamps to `[0,100]`, and truncates.

`OriginalHouseStats.ComputeOutdoorRoomScore`, `ComputeYardScore`, and its
per-room entity walk preserve those filters, arithmetic, float accumulation,
and truncation.  The port-only dead/ghost exclusions do not remove a native
live-list object: dead objects are removed from the active object module and a
placement ghost has no native committed-room counterpart.

## Snapshot timing residual (not guessed here)

The native Furnishings operands are cached, not recomputed by the panel.  A
whole-code direct-call census finds exactly four `ObjectModule::ComputeStats`
call sites:

- `House::PrepareForBudgetWindow` at `0x08c580`;
- `House::EnterLiveMode` at `0x08c6f8`;
- `House::DoCommand(220, ...)` at `0x08c934`;
- `House::SaveFile` at `0x08cca0`.

The port currently recomputes the equivalent pair when House paints.  Stable
screens and a return from Build/Buy show the same values, but a mid-Live
current-value change (for example a part acquiring `RepairState`) may appear in
the port immediately while native retains its previous snapshot until one of
the four triggers.

This timing is not safely implementable solely inside `OriginalHouseStats` or
`UIHouseSubpanel`: the native budget-dialog lifecycle is not identical to a
generic UI mode switch, command 220 has no proven port House-command event,
and Save and initial EnterLive are owned by separate screen/VM paths.  A local
cache without all four proven invalidation hooks would replace a known fresh
value with unpredictably stale state.  R188 therefore fixes the proven value
law and leaves this trigger-mapping residual explicit rather than inventing
cache timing.

The other inherited Furnishings residual remains the native roof-tile
contribution (`100` per roof-layer tile): the port stores only roof style and
pitch, not the executable's per-tile roof layer, so there is no exact operand
to add.

## Focused regression

The `uihouse` law gate now pins aggregate wall rounding at 0, one odd segment,
two odd segments, and a signed negative value.  Existing checks continue to
pin every Furnishings curve knot/interpolation, the single-precision quotient,
the Yard room law/multiplier/clamps, live raw operands, and the displayed bar
values.
