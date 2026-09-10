# R251 — the mood update cadence and the smoothing law (evidence round)

This round closes the R223 gap. R223 recovered the mood *aggregation* law
(`mood = Σwᵢ·mᵢ/Σwᵢ`) and proved the bare aggregation is NOT the whole story:
in the staged original saves, five persons show all eight non-mood motive
inputs byte-identical between the two consecutive fixture states
(`MotiveDataOld`/`MotiveData`), yet the stored mood moved (Bob 74.073→57.385,
Betty 75.118→60.511). R223's named follow-ups were the **mood update cadence**
and the **person+1550 entry mask**.

Everything below is decoded from the original Mac PPC binary
`game-data/The Sims/The Sims Complete`, SHA256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`
(re-pinned by `verify.py`, which PASSES).

Addresses are **file offsets** in the PEF image (capdis2 address space). The
symbol-index `+2` convention (`CalcHappy` at 0x10b9c2) maps to file offset
0x10b9c0, so the function's first instruction is `0x10b9c0`.

---

## 1. CalcHappy is a PURE RECOMPUTE — there is NO smoothing/easing law

`cXPerson::CalcHappy` (0x10b9c0, symbol `CalcHappy__8cXPersonFv` 0x10b9c2,
352 bytes, function record `00092040` at 0x10bb24).

PROVEN (from the disassembly, `r251-disasm-calchappy.txt`):

```
0x10ba10: lwz  r0, 0x10(r4)   ; motive index for this table entry
0x10ba24: lfsx f4, r3, r0     ; f4 = person[1932 + 4*idx]  (the motive value)
...      evaluate the STR#502 weight curve at f4 -> f1 (the weight)
0x10bb00: fmadds f6, f4, f1, f6   ; f6 += m * w        (numerator)
0x10bb04: fadds  f5, f5, f1       ; f5 += w            (denominator)
...      loop over the table entries
0x10bb14: fdivs  f0, f6, f5       ; f0 = Σ(m*w) / Σw
0x10bb18: stfs   f0, 1944(r3)     ; person[1944] = mood   <-- slot 3 of the
                                   ;  16-float array at person+1932
0x10bb1c: blr                      ; return
```

There is **no** `mood += (target - mood) * k` anywhere in this function. It does
not read the previous mood value; it computes the weighted average from the
current motives and writes it directly. The mood slot (person+1944) is therefore
**the target**, not a smoothed version of it.

`verify.py` confirms this is the **only** store to `person+1944` in the whole
binary with a person base register. The other two constant-`1944` stores in the
image (0x2e086c `stfs f7,1944(r1)`, 0x2e12f0 `stfd f0,1944(r2)`) use r1
(stack frame) and r2 (TOC/global) as base — they are **not** person fields
(stack locals and a global), i.e. false positives.

**PROVEN:** the sim-core mood is NOT smoothed. It equals the CalcHappy target.

---

## 2. The mood update cadence: once per 60 time-units

CalcHappy is reached through a fixed call chain, all direct `bl` (R223's
"only through a VTABLE" remark is wrong for this path):

```
main loop -> cXPerson::Simulate (0x10bff0, symbol 0x10bff2) -- VIRTUAL entry
              |  0x10c104: bl 0x9eab0   -> Motives::Sim (0x09eab0, symbol 0x09eab2)
              |              |  0x09edc8: bl 0x10b9c0  -> CalcHappy
```

`cXPerson::Simulate` gates the `Motives::Sim` call (which advances the motives
and then recomputes mood) on a **60-time-unit cadence**:

```
0x10c0cc: clrlwi. r0, r27, 0x18    ; r0 = r27 & 0xff   (person-active/low flag)
0x10c0d0: beq 0x10c364             ; if inactive -> skip to epilogue
0x10c0d4: lwz   r3, 0x810(r25)     ; r3 = person[2064] = last motives-update time
0x10c0d8: addi  r0, r3, 0x3c       ; r0 = last + 60
0x10c0dc: cmpw  r26, r0            ; compare current time (r26) with last+60
0x10c0e0: blt   0x10c108           ; if current < last+60 -> SKIP motives update
0x10c0e4: stw   r26, 0x810(r25)    ; person[2064] = current   (reset the latch)
0x10c0e8: lha   r0, 0x5c6(r25)     ; person[1478]
0x10c0ec: cmpwi r0, 0
0x10c0f0: bne   0x10c108           ; if person[1478] != 0 -> SKIP
0x10c0f4: lha   r0, 0x8e(r25)      ; person[142]
0x10c0f8: cmpwi r0, 0
0x10c0fc: bne   0x10c108           ; if person[142] != 0 -> SKIP
0x10c100: addi  r3, r25, 1932      ; r3 = motive array base
0x10c104: bl    0x9eab0            ; Motives::Sim  -> ... -> CalcHappy
```

**PROVEN:** `Motives::Sim` (and therefore CalcHappy / the mood recompute) is
rate-limited to run **at most once per 60 time-units** (`person + 2064` is the
"last update" latch, compared against `current_time >= last + 60`, then
refreshed to `current`). It is additionally gated by `person[1478] == 0` and
`person[142] == 0`, and by the person-active flag `(r27 & 0xff) != 0`.

The 60-unit interval is measured in whatever `long time` unit the sim passes to
`cXPerson::Simulate` (the game clock). The absolute wall-clock rate therefore
depends on the tick rate, which I could **not** pin from this function alone
(it is the virtual caller's clock). This is the cadence, and it is a
rate-limit, not a per-tick recompute.

---

## 3. The person+1550 entry/suit mask (R223 follow-up #2)

### What person+1550 actually is

`person+1550` is written ONLY by `SAnimator::ResetSuits` (0x361432), at six
sites that set it to a **suit index 0..5** based on which suit is active:

```
0x361524: lwz r3, 0x130(r29)   ; r3 = cXPerson (person ptr from animator+0x130)
0x361528: li   r0, 0 ; sth r0, 0x60e(r3)   ; person+1550 = 0
0x361574: li   r0, 1 ; sth r0, 0x60e(r3)   ; person+1550 = 1
0x3615c0: li   r0, 2 ; sth r0, 0x60e(r3)   ; person+1550 = 2
0x36160c: li   r0, 3 ; sth r0, 0x60e(r3)   ; person+1550 = 3
0x361658: li   r0, 4 ; sth r0, 0x60e(r3)   ; person+1550 = 4
0x3616a4: li   r0, 5 ; sth r0, 0x60e(r3)   ; person+1550 = 5
```
So `person+1550` is a **suit index** (0-5), not an arbitrary per-motive
bitmask. It changes when the person is re-dressed (ResetSuits runs).

### The gate semantics in CalcHappy (0x10bad8-0x10baf0)

The gate block operates on `r6 = person[1550]` (the suit index). CRITICAL
CORRECTION to R223: the word at `0x10bad8` is `38000001` = `li r0, 1` — its
`rA` field is **0**, so it is a load-immediate that OVERWRITES r0 with 1,
*discarding* the byte offset r0 carried into the block. R223 and the naive
`addi r0, r0, 1` reading are wrong about what flows in.

```
0x10bad4: lha   r6, 1550(r3)          ; r6 = suit index
0x10bad8: li    r0, 1                 ; r0 = 1            (load-immediate)
0x10badc: xor   r0, r6, r0            ; r0 = suit ^ 1
0x10bae0: srawi r5, r0, 1             ; r5 = (suit^1) >> 1
0x10bae4: and   r0, r0, r6            ; r0 = (suit^1) & suit
0x10bae8: subf  r0, r0, r5            ; r0 = r5 - r0
0x10baec: rlwinm. r0, r0, 1, 31, 31   ; r0 = sign bit of ( (suit^1)>>1 - (suit^1)&suit )
0x10baf0: beq   0x10bb00              ; if sign == 0 -> ACCUMULATE (skip value gate)
0x10baf4: lfs   f0, 0x30(r8)          ; gate constant (read as 0.0)
0x10baf8: fcmpu cr0, f0, f4           ; compare const with motive value f4
0x10bafc: beq   0x10bb08              ; if motive == const -> SKIP this entry
0x10bb00: fmadds f6, f4, f1, f6       ; accumulate m*w
0x10bb04: fadds  f5, f5, f1           ; accumulate w
0x10bb08: addi   r4, r4, 0x14         ; next entry
```

Evaluating `sign( (suit^1)>>1 - (suit^1)&suit )` for each suit:
| suit | t=suit^1 | t>>1 | t&suit | diff | sign | behavior |
|------|----------|------|--------|------|------|----------|
| 0    | 1        | 0    | 0      | 0    | 0    | accumulate all |
| 1    | 0        | 0    | 0      | 0    | 0    | accumulate all |
| 2    | 3        | 1    | 2      | -1   | 1    | value-gate |
| 3    | 2        | 1    | 2      | -1   | 1    | value-gate |
| 4    | 5        | 2    | 4      | -2   | 1    | value-gate |
| 5    | 4        | 2    | 4      | -2   | 1    | value-gate |

**PROVEN (derived):** the person+1550 suit index is a two-way switch. For suite
index 0 or 1, every table entry is accumulated unconditionally. For suit index
2,3,4,5 the entry is additionally dropped when its motive value equals the gate
constant `[r8+0x30]` (read as **0.0** — `lfs f0, 0x30(r8)`; `r8 = *(TOC-0x58e8)`,
a small float table whose +4 word is the 0.0 accumulator init and +0x30 word is
the 0.0 gate constant). In effect, **suits 2-5 ignore zero-valued motives; suits
0-1 count every motive.**

Because `person+1550` is a *suit* field and the gate only ever drops *zero*
motives, it does **not** implement R223's "per-person mask that skips whole
motive numbers". It is a per-person *suit-dependent zero-suppression* switch.

---

## 4. The Bob/Betty mismatch — what CAN and CANNOT explain it

The staged House07 rows (from `r223-save-tuples-house07.txt`):

**Bob (objectID 105)** — inputs (slots 5,6,7,8,9,13,14,15) identical old/new:
Energy 99.625, Comfort 98.480, Hunger 98.582, Hygiene 98.830, Bladder 70.952,
Room 50.222, Social 98.870, Fun 19.750. Stored mood old = **74.073**, new =
**57.385** (Δ = −16.688).

**Betty (objectID 137)** — inputs identical old/new: Energy 99.625, Comfort
98.480, Hunger 98.582, Hygiene 98.830, Bladder 75.952, Room 50.222, Social
98.857, Fun 22.750. Stored mood old = **75.118**, new = **60.511** (Δ = −14.607).

Now the logic is airtight on one point:

- CalcHappy is a **pure deterministic function** of its 8 motive inputs (the
  weighted average), with no state carried between calls and no smoothing.
- Therefore `mood = f(motives)`, and `f(motives_old) == f(motives_new)` whenever
  the 8 inputs are identical.
- The 8 inputs ARE identical for Bob and Betty (bytes 5,6,7,8,9,13,14,15 match).
- But `74.073 != 57.385`, so **the stored mood is NOT f(motives)**.

So `mood ≠ f(motives)` — the stored mood is **state-dependent on something not
in the 8 captured motives**. This CONFIRMS R223's "state-dependent" conclusion.
But it also **REFUTES R223's option (a)** ("smoothed toward the CalcHappy
target"): CalcHappy writes the target directly and is the only writer of
person+1944, so there is no in-sim smoothing to produce an off-target value.

What COULD produce the delta (must be state outside the captured inputs):

1. **A different live room-score (slot 13).** CalcHappy consumes `Room` as a
   weighted input. The room score is recomputed by `UpdateCurrentRoom`
   (0x109452) on a separate cadence and stored to `person+1984` (slot 13) —
   `Simulate` calls `bl 0x109450` at 0x10c130 immediately after the motives
   block. If, between the two CalcHappy evaluations, the live `Room` differed
   from the value captured in the save snapshot (the save stores `Room` at a
   possibly different moment than CalcHappy reads it), the weighted average
   changes. This is my leading hypothesis but I could **not** confirm it from
   static analysis — the save shows `Room`=50.222 identical in both snapshots.
2. **A different person+1550 suit state** at the two recompute moments.
   However, the suit gate only drops *zero* motives, and neither Bob nor Betty
   has any zero non-mood motive, so a suit change would **not** change their
   weighted average. Hence the +1550 mask is **ruled out** as the explanation
   for Bob/Betty.
3. **The two snapshots are from different recompute cycles where the live
   inputs genuinely differed**, and the save's "identical" columns only reflect
   the current snapshot, not the inputs CalcHappy saw when it produced each
   stored mood.

I could **not** pin which of these; that needs a live trace / a running
simulation with the motive and room states sampled at each CalcHappy call.

### Residual

For a pure-recompute law the residual under "identical inputs" is structurally
zero — but the observed pair is Δ ≈ −16.7 (Bob) and Δ ≈ −14.6 (Betty). This is
the residual R223 could not close, and it is the direct demonstration that
`mood ≠ f(motives)`. R223's best single-subset fit (`{Comfort, Hunger, Bladder,
Fun, Room}`, room weight 8) being within 1.73 of all five stored moods is
consistent with a *recompute-driven* mood that each snapshot samples at a
different live-room/state point, but that fit is not pin-grade because the
exact curve→motive mapping and curve constants are not in the PEF's static data
(the curves live in the STR#502 IFF resource and the weight-table pointers are
relocation targets I could not resolve from the file).

---

## Summary

| Question | Answer | Confidence |
|----------|--------|------------|
| Does CalcHappy **smooth/ease** the mood? | **No.** It sets `mood = Σwᵢ·mᵢ/Σwᵢ` directly (fdivs + stfs person+1944). No blend term. | PROVEN |
| Is there another writer of person+1944? | No (only CalcHappy; the other 1944 stores are stack/global). | PROVEN |
| Mood **cadence** | Once per **60 time-units**, rate-limited by `person[2064]`, inside `cXPerson::Simulate -> Motives::Sim -> CalcHappy`. | PROVEN |
| `person+1550` meaning | **Suit index 0..5** (set by `SAnimator::ResetSuits`). | PROVEN |
| `person+1550` gate effect | Suits 0/1 accumulate all motives; suits 2-5 drop **zero-valued** motives. Not a per-motive-number skip mask. | PROVEN (derived) |
| Why Bob/Betty mood changed | State outside the captured motives (live room-score and/or snapshot timing). The +1550 suit mask is **ruled out** (no zero motives). Exact mechanism NOT pinned. | UNRESOLVED |

## Files

- `r251-disasm-calchappy.txt` — CalcHappy, capstone-corrected (branch polarity
  and the `li r0,1` fix).
- `r251-disasm-cadence.txt` — the `cXPerson::Simulate` cadence gate
  (0x10c0cc-0x10c134).
- `r251-disasm-motives-sim.txt` — `Motives::Sim` (0x09eab0-0x09eee0), including
  the direct `bl CalcHappy`.
- `verify.py` — re-pins the SHA256, asserts the exact instruction words, and
  checks the derived suit-gate table. **PASSES.**
- `skeptic-notes.md` — how this decode could be wrong.
