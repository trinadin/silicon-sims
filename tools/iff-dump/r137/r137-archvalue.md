# R137 — the live house value + the Layout flag correction (the broadened round)

Round protocol: Hermes x3 (port value-surface survey / value-string corpus
survey / the AddLayoutTick flag-predicate decode) + main-thread decode of
`cFixedWorld::ComputeArchValue`. Gate: **79/79 PASS** (`r137p2.log`; the
first run `r137p1.log` is an honest FAIL kept as evidence — the WallsChanged
subscription at InitializeLot time NRE'd because the architecture is not
attached yet, aborting every candidate lot load; fixed by lazy wiring inside
RefreshArchValue).

## 1. cFixedWorld::ComputeArchValue 0x15ea70-0x15eee0 (size 0x470)

Symbol trailer at 0x15eef0 (`ComputeArchValue__11cFixedWorldFPb` — the
trailing 'b' is the `bool*` out-param the budget/EnterLiveMode callers pass
as &local66). Machine-decoded structure:

```
for level in 1..2:                          # exactly two stories
  for y, x over the lot dimension (this+16):
    wallByte = wallLayer(this+48+level*4 → +12 grid)[x][y]
    if (wallByte & 0x04) == 0:              # validity bit
      rec = GetWallInfo(this, &rec, x, y, level)    # 0x1619d0
      if (rec.style == 0) *incomplete = 1
      for each set segment bit (ctz walk):
        r29 += wallStylePriceTable[rec.entry[i]]    # [TOC-28564] ×4/entry
      r28 += wallpaperPrice(rec.pattern)            # 0x1f57a0, FULL price
      r28 += floorPrice(rec.floor slots) / 2        # 0x1acfa0, HALF price
    roofByte = roofLayer(this+96+level*4 → +12)[x][y]
    if (roofByte == 16): r28 += 100                  # [TOC-28568] = 0x64
return r28 + trunc(r29 / 2)                # wall STRUCTURE at HALF value
```

**The law:** `archValue = Σ wallpaper(full) + Σ flooring(/2) +
Σ roofTiles×100 + trunc(Σ wallStyle / 2)`. Cross-validation: the port's own
build transactions already charge flooring at half price (RunCommands
FLOOR_FILL `/2`) — the value law and the charge law agree.

**Constants:** `[TOC-28568]` → sec1+0x4cc80 = **u32 100 — the roof-tile
price, recovered from file**. The style table `[TOC-28564]` and the
wallpaper/floor singletons (`[TOC-29512]/-29516]`) are BSS by the r135 rule
(code-rel reads land in identifier/instruction streams) — they are
RUNTIME-LOADED from Build.iff STR 0x81/0x83, the same tables the port's
`WorldWallProvider`/`WorldFloorProvider` read, so the port's prices are the
canon source already.

**Callers:** 0x8c58c / 0x8c704 / 0x8c940 / 0x8ccac — the
AddLayoutTick-adjacent House paths (PrepareForBudgetWindow 0x8c540,
EnterLiveMode-221/220) — the value is recomputed on those events.

## 2. The AddLayoutTick flag predicate — R136 CORRECTED

`cXPerson::Simulate(long)` 0x10bff0 (size 0x390, trailer 0x10c380) contains
the single call site (0x10c2d8). The decoded predicate:

```
if (delta % 10 == 0                    # sampled every 10th Sim-tick
 && s16[this+1484] == 0                # sleep/consciousness: AWAKE
 && s16[this+142] == 0                 # hidden flag: VISIBLE
 && f32[this+0x7b8] >= 0.0f)           # Motives[11] >= 0 (threshold = the
                                       #   [TOC-22760]+4 = 0.0f pool float)
    flag = (GetCurrentRoute() != NULL) # inlined 0x10d540: motions vector
                                       #   (+2724 count, +2728 data,
                                       #   sizeof(Motion)=156) non-empty
```

**So the B channel counts in-motion samples** — LayoutScore = 100×(1 −
2×route occupancy), decaying as sampled Sims spend more time trekking. The
R136 port's "non-portal transition" flag was a proxy guess (disclosed at the
time); R137 replaces it with the decoded semantics: each sample pass counts
every live avatar once (population channel) and flags those currently moving
(`VMAvatar.Velocity != 0`, the routing frame's per-tick movement flag =
the engine's GetCurrentRoute proxy). The awake/visible/motive[11] gates are
approximated by liveness — disclosed. Sampling only advances when the VM
clock ticks (pause = no Simulate).

## 3. Port

* **FreeSO (submodule, commit ed70a02b)** — `VMArchitectureStats.GetArchValue`
  on the engine law: wallpaper full, flooring `/2`, wall structure `/2`;
  FIXES the diagonal branch that passed style ids into `GetFloorPrice` and
  priced diagonal patterns at half. Roof contribution 0 (no port-side
  roof-tile storage — disclosed).
* **TS1GameScreen** — `RefreshArchValue()`: `ValueInArch =
  GetArchValue + build-mode objects` (the same formula UpdateSIMI snapshots
  into SIMI), mirrored into `SimulationInfo.ArchitectureValue`; wired
  lazily to `Architecture.WallsChanged`, refreshed every 600 frames and at
  Save(). The neighborhood net-worth surfaces (STR# 134 [0]) and the evict
  payout (STR# 131 [3], `Budget += ValueInArch`) now read a CURRENT number.
* **OriginalRouteHistory v2** — the corrected flag channel (above).

## 4. Corpus (Hermes survey, cross-validated by the canon generator)

The value displays read UIText.iff STR# 146 'budgetstrs' ([30]
'Household Account Total: %s', [34] 'Household Net Worth: %s', [35]
'Value of house and belongings'), STR# 134 'NghRollover' ([0] the
neighborhood rollover), STR# 131 'EvictModeStrs' ([3] the evict wording) —
no '§' anywhere in the tables (the engine prepends the glyph). Full-chunk
shas in `make_r137_value_canon.py` output (r137-value-canon.txt), pinned by
the gate.

## 5. Gate — 'uivalue' (78→79)

STR# 146/134/131 disk pins (labels + full-chunk shas + the verbatim entries,
generated pins) + LIVE: `RefreshArchValue()` then `family.ValueInArch ==
GetArchValue + GetObjectValue.Item2` (independent recompute), positive on
the built autotest lot, SIMI mirror equality. `uihouse` re-pinned for the
corrected flag channel: `flagged ≤ total` and a second same-frame Sample
(no clock advance ⇒ no Simulate) adds nothing.

## 6. Residuals banked

* The roof-tile layer (engine this+96 grids, byte 16 = roof tile) has no
  port-side storage — roof contributes 0 to the value (disclosed; §100/tile
  constant recovered and waiting for roof-tile data).
* The net-worth COMPOSITION shown by the neighborhood rollover (whether the
  engine adds the house's SIMI objects/lot base at display time) — undecoded;
  the port keeps Budget + ValueInArch.
* The budget window itself (STR# 146's [20]-[27] expense rows,
  SIMI.BudgetDays) — unported surface, natural future round.
