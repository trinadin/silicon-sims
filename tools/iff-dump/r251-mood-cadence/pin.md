# R251 FINAL PIN round — the three "un-pinnable" mood constants

This round attempts to pin the three items the R251 decode agent and skeptic both
flagged as "not pin-grade" (TOC-relocation problems):

1. the **curve→motive mapping** (which STR#502 curve index each of 8 motives uses),
2. the **room weight**,
3. the **`[r8+0x30]` suit-gate constant**.

Everything is decoded from `game-data/The Sims/The Sims Complete` (PPC PEF,
SHA256 `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`,
READ-ONLY — re-pinned by `pin-verify.py`, which PASSES). Addresses are **file
offsets** in the PEF image (capdis2 space). The code section loads with
runtime = file − 0x8e90; the data section loads at runtime 0x5b9460.

**Bottom line up front:**

| Item | Verdict |
|------|---------|
| STR#502 curves (7, exact knot floats) | **PINNED** (read from the IFF) |
| CalcHappy iteration order | **PINNED** `(7,5,6,15,8,14,9,13)` |
| `[r8+0x30]` suit-gate constant | **PINNED** = **13.0** (corrects the prior 0.0 claim) |
| `[r8+4]` accumulator init / no-curve weight | **PINNED** = 0.0 |
| curve→motive mapping | **NOT STATICALLY PINNABLE** (runtime-built) |
| room weight | **NOT STATICALLY PINNABLE** (runtime-built) |
| 60-unit cadence absolute clock unit | **NOT RESOLVABLE** statically (see §6) |

---

## 1. The STR#502 curves (authoritative, exact floats)

Read directly from `game-data/The Sims/GameData/Global/Global.far` →
`Global.iff` → chunk `STR#` id 502, label `HappyWeightCurves`, format code `-3`
(0xFFFD), 7 strings. Raw bytes parsed; exact knot floats:

```
curve  IN6                                  knot (value, weight) pairs
[0]  (-100;15) (-60;5) (-40;3) (0;1) (100;1)   [(-100,15) (-60,5) (-40,3) (0,1) (100,1)]
[1]  (-100;10) (-80;3) (-40;1) (100;1)         [(-100,10) (-80,3) (-40,1) (100,1)]
[2]  (-100;5)  (-80;3) (-40;1) (100;1)         [(-100,5)  (-80,3) (-40,1) (100,1)]
[3]  (-100;10) (-80;3) (-60;1) (100;1)         [(-100,10) (-80,3) (-60,1) (100,1)]
[4]  (-100;5)  (-40;2) (40;2)  (100;5)         [(-100,5)  (-40,2) (40,2)  (100,5)]
[5]  (-100;2)  (0;1)   (100;2)                 [(-100,2)  (0,1)   (100,2)]
[6]  (-100;5)  (-40;2) (40;2)  (100;3)         [(-100,5)  (-40,2) (40,2)  (100,3)]
```

This is the **authoritative** source for the curves. `pin-verify.py` re-reads it
from the IFF and compares against these exact floats — PASSES.

---

## 2. CalcHappy iteration order — PINNED

CalcHappy (`cXPerson::CalcHappy`, symbol `CalcHappy__8cXPersonFv` @ 0x10b9c2 →
file 0x10b9c0) iterates its weight table and reads each entry's **motive index
at entry+16** (0x10ba10 `lwz r0,0x10(r4)`). The static rodata copy of this order
— which `cXPerson::Initialize` uses to build the runtime table's +16 fields —
is at **file 0x5a45b8**, read as 8 big-endian words:

```
(7, 5, 6, 15, 8, 14, 9, 13)  =  Hunger, Energy, Comfort, Fun,
                               Hygiene, Social, Bladder, Room
```

(the sorted copy at file 0x5a32f8 = `(5,6,7,8,9,13,14,15)` is the Motives
iteration order, as R223 recorded). The table order and the motive set match
R223.

---

## 3. The `[r8+0x30]` suit-gate constant — PINNED = **13.0** (NOT 0.0)

This is the **key correction**. The prior decode and the skeptic both hedged
"[r8+0x30] is likely 0.0 but unverified". It is **13.0**.

### How r8 resolves

CalcHappy's first instruction (file 0x10b9c0) is `lwz r8, -0x58e8(r2)`. The slot
`[TOC − 0x58e8]` is at data runtime 0x5bbb78 (TOC = data_base + 0x8000 =
0x5b9460 + 0x8000 = 0x5c1460). Reading the **unpacked** data section (the
PEF data section is packed; see §5) at that slot gives the stored value
**0x59a524**, which is an **image-base-relative (absolute) runtime address**
(image base = 0). Map runtime → file: file = runtime + 0x8e90 = **0x5a33b4**.

The float table at file 0x5a33b4:

| file | runtime | float | role |
|------|---------|-------|------|
| 0x5a33b4 | r8+0x00 | 0.5 | (unused here) |
| 0x5a33b8 | r8+0x04 | **0.0** | `lfs f6,4(r8)` accumulator init — and the constant weight used when an entry's knot count is 0 (0x10ba2c) |
| 0x5a33bc | r8+0x08 | 3.0 | |
| 0x5a33c0 | r8+0x0c | 0.1875 | |
| 0x5a33c4 | r8+0x10 | −0.1875 | |
| 0x5a33c8 | r8+0x14 | 45.0 | |
| 0x5a33cc | r8+0x18 | 180.0 | |
| 0x5a33d0 | r8+0x1c | 7.0 | |
| 0x5a33d4 | r8+0x20 | 1.0 | |
| 0x5a33d8 | r8+0x24 | ~0.0 | |
| 0x5a33dc | r8+0x28 | ~−0.0 | |
| 0x5a33e0 | r8+0x2c | −99.0 | |
| 0x5a33e4 | r8+0x30 | **13.0** | `lfs f0,0x30(r8)` **THE GATE CONSTANT** |
| 0x5a33e8 | r8+0x34 | 100.0 | |
| 0x5a33ec | r8+0x38 | −1.0 | |
| 0x5a33f0 | r8+0x3c | 2.0 | |

`[r8+4] = 0.0` is *independently* confirmed: the accumulator `f6` is seeded from
`lfs f6,4(r8)` at 0x10b9cc, and a correct `Σ(wᵢ·mᵢ)` must start from 0.0. This is
strong corroboration that r8 = file 0x5a33b4 and that the float table read there
is the one CalcHappy actually uses.

### Gate semantics (unchanged structure, corrected constant)

```
0x10bad4: lha  r6, 1550(r3)        ; r6 = person+1550 = suit index (0..5)
0x10bad8: li   r0, 1               ; r0 = 1
0x10badc: xor  r0, r6, r0          ; r0 = suit ^ 1
0x10bae0: srawi r5, r0, 1
0x10bae4: and  r0, r0, r6
0x10bae8: subf r0, r0, r5
0x10baec: rlwinm. r0, r0, 1, 31, 31 ; CR0[EQ] set iff suit in {0,1}
0x10baf0: beq  0x10bb00            ; suit 0/1 -> accumulate every motive
0x10baf4: lfs  f0, 0x30(r8)        ; f0 = 13.0  (gate constant)
0x10baf8: fcmpu cr0, f0, f4        ; compare gate (13.0) with motive value
0x10bafc: beq  0x10bb08            ; if motive == 13.0 -> skip this entry
0x10bb00: fmadds f6, f4, f1, f6    ; accumulate m*w
0x10bb04: fadds  f5, f5, f1        ; accumulate w
```

The measured suit-gate behavior (unchanged from R251): suits 0/1 accumulate every
motive; suits 2–5 drop an entry whose **motive value equals 13.0**. The previous
"drops zero-valued motives" reading was based on the unverified 0.0 guess — it is
**wrong**; the constant is **13.0**. (For Bob/Betty the gate is moot: both are
suit 0/1 and in the accumulate-all branch, so this does not affect the R251
Bob/Betty conclusion.)

---

## 4. The type-split and suit-mask person fields — confirmed

```
0x10b9c8: lha   r4, 1536(r3)   ; person+1536 = person-type field
0x10b9dc: cmpwi r4, 0x12       ; 18
0x10b9e0: bge   0x10b9e8       ; >= 18 (or 0) -> table B
0x10b9e4: li    r5, 1          ; else (1..17) -> table A
0x10b9f0: lwz   r4, -0x58fc(r2); table A
0x10b9f8: lwz   r4, -0x5900(r2); table B
```

- `person + 1536` selects the weight table: `[1,17]` → table A, `0 or >=18` →
  table B. Both Bob and Betty have `person[1536]=27`, so both use table B.
- `person + 1550` is the **suit index** (0–5), written only by
  `SAnimator::ResetSuits` (0x361432). Its gate arithmetic is proven in §3.
- `person + 1932` is the 16-float motive array; motive `idx` is read as
  `person[1932 + 4*idx]`; the mood is written to `person[1944]` (= slot 3).

---

## 5. WHY the curve→motive mapping and room weight are NOT statically pin-able

The task's proposed strategy — "read the (value,weight) knot arrays directly from
the binary rodata" — **does not work for this binary**, and this is now **proven
from the code**, not just an unresolved TOC problem:

1. **The weight tables are BSS, not initialized data.** The two slot values
   that CalcHappy loads (`lwz r4, -0x58fc(r2)` = table A, `lwz r4, -0x5900(r2)` =
   table B) resolve as **TOC-relative** offsets 0x858b8 / 0x85958. Adding the TOC
   base (0x5c1460) gives data-section runtime addresses 0x646d18 / 0x646db8, i.e.
   data-section **indices 0x8d8b8 / 0x8d958**. The initialized portion of the data
   section unpacks to only ~**0x7bf80** bytes; 0x8d8b8 and 0x8d958 are far beyond
   it, in the BSS tail. So the tables are **zeroed at load** and filled at runtime.

2. **The knot arrays are heap-allocated at runtime.** The entry initialiser
   `0x117d70` (called from `cXPerson::Initialize`) is:

   ```
   0x117dcc: rlwinm r3, r31<<3      ; count*8  bytes
   0x117dd0: bl   0x591570          ; heap allocation -> entry+0 (knot values+weights)
   0x117ddc: addi  r0, r3, -1       ; count-1
   0x117de0: rlwinm r3, r0,2,0,28   ; (count-1)*4  bytes
   0x117de4: bl   0x591570          ; heap allocation -> entry+4 (per-segment slopes)
   ```

   So entry+0 (the knot `(value, weight)` array) and entry+4 (the slope array)
   are **runtime heap buffers**, filled from STR#502 by the curve loader
   (`MotiveCurveSet::LoadFromFile`, 0x9f500, called by `cXPerson::Initialize`
   at 0x111edc/0x111f40 with resource ids **502** and **504**).

3. **No static (value,weight) floats matching STR#502 exist anywhere in the
   image.** An exhaustive search of the raw file and of the unpacked data section
   for the interleaved `(value, weight)` byte sequences of all seven curves (and
   the value-only / weight-only splittings) returns **zero hits**. The only
   `-100/…/100` floats in the code rodata (file ~0x5a3000–0x5a3600) belong to
   unrelated constant tables (e.g. the R134 RoomScoreConstants family at
   0x5a3338–0x5a3354). The runtime table search for eight 20-byte entries whose
   +16 fields are `(7,5,6,15,8,14,9,13)` also returns **zero hits** in both.

**Therefore** the per-motive curve index and the room weight are determined at
runtime by the curve loader (from STR#502/STR#504) into BSS/heap, and are **not
present as static data**. To pin them you would need either a live trace of
`CalcHappy`/the curve loader, or a decode of the curve-loader's assignment loop
in `cXPerson::Initialize` (which reads a static template — resource ids 502 and
504 — whose per-entry knot **counts** are static).

Honest narrowings that remain **not pin-grade**:
- The 7 curves have knot counts `[5,4,4,4,4,3,4]`. IF the static template's
  per-entry counts matched curve knot counts, then count `5` → curve 0 and count
  `3` → curve 5 are unambiguous, but the four-knot curves {1,2,3,4,6} are not
  distinguishable by count alone. This was not resolvable from the static image.
- R223's best-fit used a room weight of **8**, but that was a least-squares guess
  over a subset with guessed curves — not pin-grade. The room weight follows
  whichever curve the Room entry's knot array uses, which is runtime state.

---

## 6. The 60-unit cadence absolute clock unit — NOT resolvable statically

The cadence gate in `cXPerson::Simulate` (file 0x10c0d4–0x10c104) is:

```
0x10c0d4: lwz  r3, 0x810(r25)   ; person[2064] = last motives-update time
0x10c0d8: addi r0, r3, 0x3c     ; + 60
0x10c0dc: cmpw r26, r0          ; current time (r26) vs last+60
0x10c0e0: blt  0x10c108         ; if current < last+60 -> SKIP
0x10c0e4: stw  r26, 0x810(r25)  ; person[2064] = current
```

The interval is **60 units of the `long time` value the sim passes into
`cXPerson::Simulate`** (r26). The absolute unit (game-minutes, sim-seconds, or
ticks) depends on the caller's clock tick rate, which is not determinable from
`Simulate` or `Motives::Sim` (0x09eab0) in isolation. R251's decode and the
skeptic both reached the same conclusion, and this round confirms it: **the
absolute clock unit is genuinely unresolvable from static analysis without
tracing the sim clock** (the value passed into `Simulate` and its tick source).

---

## 7. Final pinned facts to drop into C#

- **Aggregation (unchanged, proven):** `mood = Σ(wᵢ·mᵢ) / Σwᵢ` computed by
  `cXPerson::CalcHappy`, variable denominator (not `/8`), **no smoothing/blend**,
  written directly to `person[1944]` (motive slot 3) on the cadence boundary.
- **Iteration order (motives):** `(7,5,6,15,8,14,9,13)` =
  Hunger, Energy, Comfort, Fun, Hygiene, Social, Bladder, Room.
- **Curves (7, exact floats):** as listed in §1 (STR#502). Knot array layout per
  entry: `+0` pointer to `(value, weight)` float pairs (count×8 bytes),
  `+4` pointer to per-segment `1/(value[k+1]-value[k])` slopes ((count−1)×4 bytes),
  `+8` knot count, `+12` count copy, `+16` motive index.
- **Curve→motive mapping and room weight: NOT statically pin-able** — runtime
  state in BSS/heap, filled from STR#502. Do NOT hard-code a guessed mapping into
  an autotest; obtain it from the port's own curve loader or a live trace.
- **Gate constant:** `[r8+0x30] = 13.0`. For suits 2–5, an entry whose motive
  value equals 13.0 is dropped; suits 0/1 accumulate all. (`[r8+4] = 0.0` is the
  accumulator init / the weight used when an entry's knot count is 0.)
- **Type split:** `person[1536]` in `[1,17]` → table A; `0 or >=18` → table B.
- **Suit mask:** `person[1550]` = suit index 0–5 (written by ResetSuits).
- **Cadence:** at most once per 60 units of the sim clock, gated by
  `person[2064]`, person-active flag, `person[1478]==0`, `person[142]==0`.
- **Motive array:** `person+1932` (16 floats), mood slot = `person+1944`.

## Files

- `pin-verify.py` — SHA256 re-pin + instruction-word assertions + r8 float-table
  assertions + MotiveCurveSet static-map assertion + STR#502 curve read. **PASSES.**
- `pin.md` — this document.
