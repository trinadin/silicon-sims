# R134 — the object-side stats: FillInObjectStats decoded, the score constants PROVEN absent, TOC resolution cracked

Round protocol: Hermes x2 (port object-state survey + engine ladder hunt) →
decode → port → gate → evidence. Gate: **78/78 PASS** (`r134p2.log`; `r134p1`
keeps an honest soak flake — the death-reach probe collapsed av0's motives at
minute 37 and the mood check's final assertion caught the recompute mid-drift
(stored=0 vs computed=4); the same binary passed 78/78 on rerun, and the probe
fired identically in the passing R132/R133 runs — a tolerance-vs-timing flake
in the pre-existing mood check, no R134 code in that path).

## 1. Engine decode — ObjectModule::FillInObjectStats (entry 0xe2630, record 0xe27dc)

Signature (ObjectModule*, RoomManager*, HouseStats*); walks the object list at
mod+48 via next@+224. Per object:

* **stats+36 (the upkeep dividend):** `+= *globalB` when the object's +104
  flag is clear (working order); when set, `+= *globalA` if +152 > 0 — a
  per-object condition weight (globalA/B at [TOC-29328]/[TOC-29332]).
* **stats+40 (objectCount):** counted unless the resource fields 78/80/138
  are ALL zero (the architectural/portal skip test).
* **Value routing:** `value = 0xc9dc0(obj)` (GetCurrentValue); zero/negative
  values skip. Level-volume check first (level byte in [1, stories−1]), then
  `room = 0x128910(roomMgr, obj->132)`; if the room passes the outside test
  (`0x124fe0`, the same helper GetHouseStats uses): **OUTSIDE → stats+32
  (yard), INSIDE → stats+28 (furnishings)** — quality-over-quantity is
  literally Σ prices, split indoor/outdoor.

## 2. Room-level decodes (the engine agent's collection, r134/*.txt)

* **Room::CollectObjectStats** (entry 0x125df0): room counters — +64 all
  objects; type [obj+192]==8 → a flag bit into room+116; **type==4 → room+88++
  (the BEDROOM counter)**; **other types <8 → room+92++ (the BATHROOM
  counter)** — the flags GetHouseStats reads as bathroom/bedroom; +640/+636
  for resource+18 ∈ {2, 8}; +76 += obj+152 (the wear accumulator);
  +80 += obj+88 when outside/flagged, +84++ when nonzero; a light/geometry
  block using RoomScoreConstants 0.1875·(floorAlt−0.5) and floorAlt·3+5.
* **Room::CollectTileStats** (entry 0x126220): +84 pool tiles (pool layer
  byte < 16); outside rooms +56 += 2 and return; wall==0 → +56 += 2 + the
  pool table; else +56++ and for diagonal walls (walls&0x30) a two-pass
  segment classifier (codes {1,7,8,9} window-ish, {3,5,6,15,23} door-ish)
  feeding **room+96 (windows) / room+100 (doors)**.
* **ObjectModule::ComputeStats** (entry 0xe2820): six-output aggregate over
  the same object list with a 98-entry type jump table; GetCurrentValue sums
  into two of the outputs.

## 3. The ladder hunt — DEFINITIVE NEGATIVE

The gamestate→96/→100 score ladders have **no static copy in the binary**:
an exhaustive scan (raw file + unpacked DATA section, aligned + unaligned,
int32/float32 keys, strict and non-decreasing, both field orders, stride-16
variants) found zero {key,val} runs — in fact **no monotone small-int array
of length ≥ 7 exists anywhere in the 6.5 MB image**. The ladders are
runtime-built (resource-loaded or computed). The five House score VALUES are
therefore permanently closed to static RE — a PROVEN residual, upgraded from
R133's assumption. (The House tab's score bars mount at 0 with this proof
cited in-source.)

The float-init stub at 0x8bb10 (R133's false lead) is solved:
`.__sinit_:GUIDTranslation_cpp` — it loads the CW `<limits.h>` constants
(DBL_MIN/DBL_MAX/FLT_MIN/FLT_MAX at sec1+0x7b350-0x7b37c) and stores eight
min/max sentinel globals at [TOC-13272..-13232]. Not score constants.

## 4. RECIPE UPGRADES (future rounds)

* **TOC resolution**: r2 = TOC base + 0x8000; TOC = start of unpacked sec1
  (PEF `Joy!peffpwpc`; sec1 PACKED, unpacked size 0x7bf80 + bss to 0x989a4 —
  reuse r104's `sec1-unpacked.bin`). TOC entries are section-relative pointers
  (code-rel for jump/const-pool targets; >0x7bf80 = bss). Validated against
  r104's sway anchor and ComputeStats' 98-entry jump table. TOC-relative
  globals are now RESOLVABLE where they point into file data — the R129-era
  "CFM TOC statically unrecoverable" holds only for heap/bss targets.
* **Function-size trailers**: `[body…bclr][00000000][0009 204x][word3]
  [u32 size][u16 len + name]`; entry = record − 16 − size. Pins exact body
  starts without neighboring symbols.
* Decoder fixes: CW `mr rD,rA` prints as `or rD,rA,rD`; `7c0802a6/7c0803a6`
  = mflr/mtlr (printed mfxer/mtxer); A-form FP XO = (w>>1)&0x1F; lfsx added.
* Symbols containing `:` (e.g. `.__sinit_:…`) are invisible to the regex
  recipe — scan for them manually.

## 5. Port

`OriginalHouseStats.Result` grew the decoded aggregates: **FurnishingsValue**
(Σ MultitileGroup.InitialPrice of indoor objects — the engine's
GetCurrentValue equivalent; TS1 never advances wear so InitialPrice is the
value), **YardValue** (outdoor), **WorkingObjects/BrokenObjects** (the
stats+36 inputs; broken = the engine's own repair threshold
`RepairState >= 600` from VMFindBestObjectForFunction), **HasPool**. The
room walk now includes the outside room (dedupe per MultitileGroup; the
engine's level check is implicit in the port's room map). The upkeep
dividend formula (B·working + A·broken) awaits only the two TOC constants —
now potentially recoverable via the TOC-resolution recipe if their entries
point at file data (a small follow-up).

## 6. Gate (uihouse extended, still 78 checks)

New invariants: WorkingObjects + BrokenObjects == ObjectCount (the same
deduped filter), FurnishingsValue/YardValue ≥ 0, and the aggregate
recompute cross-check (values/broken/pool identical on an independent
Compute). uihouse PASS both runs.

## 7. Residuals banked

* The five score VALUES: proven statically unrecoverable (ladders runtime-
  built). To ever fill them: runtime extraction (debugger/memory dump of a
  live original process) — out of scope for static RE, disclosed.
* The upkeep weight constants A/B ([TOC-29328]/[TOC-29332]) — try the new
  TOC-resolution recipe against file data.
* Room::ComputeRoom / RoomManager::ComputeRooms (the room-score composition),
  cFixedWorld::ComputeArchValue (live house value) — mapped, undecoded.
