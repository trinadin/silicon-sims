# R133 — the HouseStats computation: engine decode + the House tab on the original's own terms

Round protocol: Hermes x3 (port data sources / corpus captions / engine call-site
collection) → engine decode → port → pin in the gate → full gate green → this
evidence. Gate: **78/78 PASS** first try (`r133p1.log`).

## 1. Engine decode — the HouseStats class (The Sims Complete)

All 11 symbols + the WRITER found (symbol records; bodies precede):

| function | entry | finding |
|---|---|---|
| GetObjectCount | 0x8bb90 | `lwz r3, 40(r3)` — field this+40 |
| GetLotSize | 0x8bbd0 | field this+20 |
| GetNumBathrooms | 0x8bc10 | field this+12 |
| GetNumBedrooms | 0x8bc50 | field this+8 |
| GetLayoutScore | 0x8bc90 | field this+24 (PREcomputed by the writer) |
| GetUpkeepScore | 0x8bcd0 | computed: guard objectCount(this+40)≠0; `int((x−A)/(x−B))` over double(this+36), A/B TOC-global |
| GetYardScore | 0x8bda0 | computed: linear `f(field)·K` — reads gamestate→36→+76 floats, no HouseStats fields |
| GetFurnishingsScore | 0x8be30 | ladder over gamestate→100 table of live double(gamestate→32→+160)/K |
| GetSizeScore | 0x8bf90 | ladder over gamestate→96 table of double(squareFeet)/K·M, guard this+4≠0 |
| GetSquareFeet | 0x8c0f0 | field this+0 |
| __ct__10HouseStatsFv | 0x8c130 | zeroes this+0..40 (11 words) |

**Field map:** +0 squareFeet, +4 numSims (SizeScore guard), +8 numBedrooms,
+12 numBathrooms, +16 bool, +20 lotSize (0/1/2), +24 layoutScore,
+28/+32 object-module aggregates, +36 upkeep dividend, +40 objectCount.

**The ladder shape** (Furnishings/Size): gamestate→96/→100 hold
`{int key@0, float val@4}` entry arrays at table+0, a PARALLEL SLOPE array at
table+4, count at table+8; the walk finds the segment and computes
`val + slope·(x − key)` (fmadds at 0x8bf40/0x8c0a0) — piecewise-linear
interpolation (the R129 decoder-ladder pattern). The fcmpo/fmr pairs
(0xFC010040/0xFC200090) are compiler selects.

**The writer — `House::GetHouseStats(HouseStats*)` entry 0x8c190 (record
0x8c400):**
- zeroes the caller struct, refreshes rooms (0x124fe0/0x125d40 per room),
- room scan loop: `squareFeet += roomHelper(room) × 9` (engine-internal area
  units); **a room with flag+92 set counts as a bathroom, flag+88 as a
  bedroom**;
- **lot-size ladder (0x8c29c-0x8c2cc):** lot dimension v: `v < 40 → 0`,
  `40 ≤ v < 50 → 1`, `v ≥ 50 → 2` — the Small/Medium/Large of STR# 138;
- stats+16 from House→28→128 (a bool + 1);
- calls **`ObjectModule::FillInObjectStats(RoomManager*, HouseStats*)`**
  (entry 0xe2630, record 0xe27dc) which writes stats+28/+32/+36/+40 (the
  object-module aggregates incl. objectCount) via RoomManager walks;
- stats+4 = 0x76c10(House→16) (numSims — the corpus confirms size rating
  "also based on the number of Sims living in it");
- **stats+24 (layoutScore) computed at the tail** (0x8c2ec-0x8c3d8):
  `int(f(sum House+48..80, 2×sum House+52..92) / K)` — the room-connection
  sums ("smooth movement" per the corpus).

**Consumers** (bl call-site clusters): `cWinSubpanelHouse::TSOnCommand`
(0x2a040c-0x2a07d0 — the panel computes stats ON DEMAND on click),
`Neighborhood::AddFamilyHistoryStat` (0xad630+), an unnamed web-exporter
(0x225ea8+). Room infrastructure: `Room::CollectObjectStats` 0x1261e4,
`Room::CollectTileStats` 0x1267a8, `RoomManager::ComputeRooms` 0x129078,
`cFixedWorld::ComputeArchValue` 0x15eef0 (future rounds).

## 2. Corpus — the House subpanel's OWN tables (Live.iff)

- **STR# 133 'HouseSubpanelLabels'** (size 15891, sha256
  `0e80bb305fc4ee32fd4005753c531ee61ab3b2e99b8ab229df7a8087969c4297`, 10 English):
  [0] 'House', [1] 'Size', [2] 'Furnishings', [3] 'Yard', [4] 'Upkeep',
  [5] 'Layout', [6] 'Sq. Ft.: %d', [7] 'Bedrooms: %d', [8] 'Bathrooms: %d',
  [9] 'Lot: %s'.
- **STR# 135 'HouseSubpanelPopupText'** (size 68923, sha256
  `58edc85abd48705b218e1dddc805d1842f183dad4cfcb45ac5859851e9071593`, 18 English
  title+help pairs). The help bodies state the CLASSIFICATION RULES the engine
  implements: [11] square feet = "the area inside the house--the area enclosed
  by walls"; [13] "A bedroom is any room that has a bed" (with the original
  typo "As long you"); [15] "Any room that contains a toilet, tub, or shower is
  considered a bathroom"; [1] size rating "not just based on the size of the
  house, but also the number of Sims living in it".
- **STR# 138 'HouseSubpanelSize'** (size 4016, sha256
  `c643273986d4ccd324484405836b59c8d75ba79cd4b485418cfb44bb19c3d781`):
  [0] Small [1] Medium [2] Large.

**R132 CORRECTION:** the R132 House tab mounted STR# 154 [12] 'House Rating' /
[13] 'Friend Rating' as its captions — an interpretation. Those strings are
NEIGHBORHOOD-scope (the NghRollover family); the House panel reads 133/135/138.
R133 rebuilds the subpanel on the canon tables (the mandate: when data disproves
a past claim, correct it).

## 3. Port

- **`OriginalHouseStats`** (UI/Model/OriginalHouseStats.cs): the decoded rules
  as a testable static — `ComputeLotSize` (the decoded ladder verbatim) +
  `Compute(vm)`: SquareFeet = Σ enclosed `VMRoom.Area` (the corpus's
  "area enclosed by walls" over the port's own flood-fill room map); Bedrooms /
  Bathrooms = enclosed rooms whose `RoomInfo[r].Entities` hold an OBJD
  RoomFlags bit-1 / bit-2 object (the R122-established STR# 150 room order's
  internal flags — the corpus's bed / toilet-tub-shower rules); LotSizeIndex =
  the ladder over `Architecture.Width`; ObjectCount = deduped live
  VMGameObjects per MultitileGroup.
- **`UIHouseSubpanel` rebuilt**: header 133[0], the four value lines in the
  canon formats (`'Sq. Ft.: %d'` etc. via string substitution), the lot word
  from 138[ladder], and the five score bars (133[1]-[5] labels, kHouseSubpanelBars
  40×16 art) — score VALUES stay 0, disclosed: the engine's score constants
  live in CFM-TOC globals and gamestate ladder tables, statically unrecoverable.
- **`OriginalLiveStrings`**: Wanted += 133/135/138.

## 4. Gate — new 'uihouse' (77→78)

STR# 133/135/138 disk-pinned (full-chunk shas + all 10 entries + the 9 titles +
the 3 lot words); the ladder as pure math (0/39→0, 40/49→1, 50/63→2); LIVE: the
subpanel's captions equal the disk canon, the four value lines carry the real
computed numbers, the lot word matches 138[ladder(lot width)], and the figures
cross-check against an independent `Compute` + room-map presence. The uirate
house-caption assertions moved here (corrected canon; art/math/job/track
assertions unchanged).

## 5. Residuals banked

- The five score VALUES (Size/Furnishings/Yard/Upkeep/Layout): TOC-constant
  tables + gamestate ladders unrecoverable statically; FillInObjectStats
  internals (which object categories feed +28/+32/+36) undecoded — the natural
  R134 (ObjectModule decode +, if recoverable at runtime, the ladder tables'
  runtime values via a debugger-class probe are out of scope for static RE).
- House value recompute (`cFixedWorld::ComputeArchValue` 0x15eef0) — the port's
  ValueInArch stays file-read only.
- The web exporter + AddFamilyHistoryStat consumers (neighborhood family
  history) — unported surfaces.
