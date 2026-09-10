# R251 — Adversarial skeptic review of the mood-cadence decode

Scope: independently verify (or falsify) the R251 decode agent's six claims at the
**instruction level**, from the raw binary myself, and resolve the R223 Bob/Betty
contradiction. I did **not** trust the prior agent. All words below were read back
from `game-data/The Sims/The Sims Complete` (SHA256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`,
re-pinned by `verify.py`) and decoded twice: with capstone 5.0.7 (POWER32 BE,
real disassembler) and by hand field-extraction. Where capstone and the prior
agent disagreed, I adjudicated from the raw word fields and `ppc_decode.py`.

Addresses are the capdis2/file-offset address space (byte offset in the PEF).

---

## 0. verify.py — does it actually run and pass?

Ran `python3 tools/iff-dump/r251-mood-cadence/verify.py`. **Real output: it
PASSES (exit 0).** It confirms the SHA256 and every asserted instruction word at
0x10b9c0/0x10bad4-0x10bb18 (CalcHappy), 0x10c0d4-0x10c104 (cadence),
0x09edc4/0x09edc8 (Motives::Sim→CalcHappy), and the six ResetSuits stores, plus
the derived suit-gate table. None of the assertions are wrong. The claim
"verify.py passes" is **CONFIRMED** — it is not over-claimed.

---

## 1. Claim-by-claim verdicts

### Claim 1 — NO smoothing law; CalcHappy is the only writer of person+1944
**CONFIRMED.**

CalcHappy `0x10b9c0` computes `f6 += m*w` (`0x10bb00 fmadds f6,f4,f1,f6`),
`f5 += w` (`0x10bb04 fadds f5,f5,f1`), `fdivs f0,f6,f5` (`0x10bb14
ec062824`), then `stfs f0,1944(r3)` (`0x10bb18 d0030798`). It **never** reads
`person+1944` (no `lfs … 0x798(r3)` anywhere in the function) and has no
`mood += (target-mood)*k` blend. It is a pure function of
(type, suit, 8 motive values) with no carried state. No smoothing.

Whole-image scan for **all** stores to disp `1944` (0x798):
- `0x10bb18 stfs f0,1944(r3)` — base r3 = person (the mood write).
- `0x2e086c stfs f7,1944(r1)` — base r1 = **stack frame** (a local).
- `0x2e12f0 stfd f0,1944(r2)` — base r2 = **TOC/global**.

So the decode agent's "two other 1944 stores use r1/r2, not person" is **right**.
I also sampled the indexed `stfsx`/`stfdx` stores in the person update path
(0x11ba64, 0x11bac4, 0x11c6dc, 0x1dcf24, …) — each writes to some
other object's array (`[r27+0x1b4]+4*(r28-1)` etc.), **not** person+1944.
Residual disclosed: I could not programmatically rule out an `stfsx` whose
registered base/index happen to equal person/0x798, but no candidate in the
sampled set reaches person+1944. Low risk. See §6 honesty.

### Claim 2 — the aggregation is Σ(wᵢ·mᵢ)/Σwᵢ
**CONFIRMED.** `0x10bb14 fdivs f0,f6,f5` divides the accumulated `m*w` (f6, seeded
from `[r8+4]` at 0x10b9cc, and accumulated by `fmadds`) by the accumulated weight
(f5, seeded `fmr f5,f6` at 0x10b9d4, accumulated by `fadds`). `stfs f0,1944(r3)`
stores the result. Note the denominator **Σwᵢ is not constant** — it varies with
each motive's weight, so the port's fixed `/8` is doubly wrong (wrong numerator
weights *and* wrong normalization).

### Claim 3 — cadence once per 60 time-units, rate-limited by person[2064], direct chain
**CONFIRMED** (with the known caveat that the absolute clock unit is unpinned).

Cadence gate in `cXPerson::Simulate`:
```
0x10c0cc clrlwi. r0,r27,0x18     ; person-active flag
0x10c0d0 beq  0x10c364            ; skip if inactive
0x10c0d4 lwz  r3,0x810(r25)      ; r3 = person[2064] = last motives-update time
0x10c0d8 addi r0,r3,0x3c         ; r0 = last + 60
0x10c0dc cmpw r26,r0             ; current time vs last+60
0x10c0e0 blt  0x10c108           ; if current < last+60 -> SKIP
0x10c0e4 stw  r26,0x810(r25)     ; person[2064] = current  (reset latch)
0x10c0e8 lha  r0,0x5c6(r25)      ; person[1478]
0x10c0ec cmpwi r0,0
0x10c0f0 bne  0x10c108           ; if !=0 -> SKIP
0x10c0f4 lha  r0,0x8e(r25)       ; person[142]
0x10c0f8 cmpwi r0,0
0x10c0fc bne  0x10c108           ; if !=0 -> SKIP
0x10c100 addi r3,r25,0x78c       ; r3 = motive-array base
0x10c104 bl   0x9eab0            ; Motives::Sim
```
The `blt` polarity, `0x3c`=60, and the `person+2064` latch are all exactly as the
decode agent states. `0x10c104 bl 0x9eab0` (word `4bf929ad`) and `0x09edc8 bl
0x10b9c0` (word `4806cbf9`) are confirmed **direct `bl`**, and each target has
**exactly one** direct caller in the image (CalcHappy ← 0x09edc8; Motives::Sim ←
0x10c104), so the chain `Simulate → Motives::Sim → CalcHappy` is unambiguous and
NOT vtable-mediated. **R223's remark that CalcHappy is reached "only through a
VTABLE" is WRONG for this path** — R251 is correct.

Caveat: `r26` is "the long time passed in", whose tick source I could not pin;
the "60" is 60 of those units, not an absolute wall-clock rate. The decode agent
discloses this correctly.

### Claim 4 — the `+1550` gate; `li r0,1` at 0x10bad8; person+1550 = suit index 0-5 written only by ResetSuits
**CONFIRMED (behavior table), with one minor wording correction.**

The crux correction to R223 is **right**. Word `0x10bad8 = 0x38000001`:
`rA = (0x38000001>>16)&31 = 0`, so `addi r0,0,1` ≡ `li r0,1` — it OVERWRITES r0
with 1 and discards the byte-offset r0 carried in. capstone 5.0.7 renders it
`li r0,1`. R223's `addi r0,r0,1` was wrong.

The gate block (`0x10bad4`–`0x10baf0`), decoded and hand-verified:
```
0x10bad4 lha   r6,0x60e(r3)      ; r6 = person+1550 (suit index)
0x10bad8 li    r0,1
0x10badc xor   r0,r6,r0          ; r0 = suit ^ 1
0x10bae0 srawi r5,r0,1           ; r5 = (suit^1)>>1  (arithmetic)
0x10bae4 and   r0,r0,r6          ; r0 = (suit^1)&suit
0x10bae8 subf  r0,r0,r5          ; r0 = r5 - r0  ->  (suit^1)>>1 - (suit^1)&suit
0x10baec rlwinm. r0,r0,1,31,31   ; Rc=1; result = rotl(diff,1) & 0x80000000
0x10baf0 beq   0x10bb00          ; if result==0 -> ACCUMULATE (skip gate)
0x10baf4 lfs   f0,0x30(r8)       ; gate constant
0x10baf8 fcmpu cr0,f0,f4
0x10bafc beq   0x10bb08          ; if motive == const -> SKIP this entry
0x10bb00 fmadds f6,f4,f1,f6      ; accumulate
0x10bb04 fadds f5,f5,f1
```

I re-derived the table by simulating every instruction bit-for-bit
(`suit^1`, arithmetic `srawi` by 1, `and`, `subf`, `rotl` by 1 then mask bit 31):

| suit | t=suit^1 | t>>1 | t&suit | diff | rotl1&0x80000000 | CR0[EQ] | behavior |
|------|----------|------|--------|------|------------------|---------|----------|
| 0 | 1 | 0 | 0 | 0 | 0 | 1 | accumulate all |
| 1 | 0 | 0 | 0 | 0 | 0 | 1 | accumulate all |
| 2 | 3 | 1 | 2 | 0xFFFFFFFF(-1) | 0x80000000 | 0 | value-gate |
| 3 | 2 | 1 | 2 | 0xFFFFFFFF(-1) | 0x80000000 | 0 | value-gate |
| 4 | 5 | 2 | 4 | 0xFFFFFFFE(-2) | 0x80000000 | 0 | value-gate |
| 5 | 4 | 2 | 4 | 0xFFFFFFFE(-2) | 0x80000000 | 0 | value-gate |

The decode agent's table is **correct**: suits 0/1 accumulate every motive; suits
2-5 enter the value-gate (drop the entry if its motive equals `[r8+0x30]`).

**Minor correction to the decode's language:** it calls `rlwinm. r0,r0,1,31,31`
"the sign bit of the diff." Actually `rotl(diff,1)&0x80000000` isolates **bit 30
of `diff`** (rotate-left-1 moves original bit 30 into bit 31). For the small diffs
here (0/-1/-2) that coincides with "is the diff negative," but it is *not*
literally the MSB. The behavior table is unaffected.

`person+1550` is a **suit index 0..5 written only by `SAnimator::ResetSuits`**
(`0x361432`). I scanned the whole image for writes to `0x60e` and the only
constant-disp stores are the six reported sites, each preceded by `lwz r3,0x130(r29)`
(person back-pointer) + `li r0,N` then `sth r0,0x60e(r3)`:
`0x361524/0x361528` (0), `0x361570/0x361574` (1), `0x3615bc/0x3615c0` (2),
`0x361608/0x36160c` (3), `0x361654/0x361658` (4), `0x3616a0/0x3616a4` (5).
CONFIRMED.

**Not verified (decode agent already hedges this):** the gate constant `[r8+0x30]`
being 0.0. The TOC and weight-table pointers are unrelocated placeholders in the
static image (resolving them gives string data), so "drops zero-valued motives"
rests on an unverified constant. This only affects suits 2-5; **neither Bob nor
Betty is suit 2-5**, so it does not bear on the Bob/Betty questions.

### Claim 5 — type/ghost weight-table split at person+1536 vs 18
**CONFIRMED (structurally).**
```
0x10b9c8 lha r4,0x600(r3)        ; r4 = person+1536
0x10b9d0 extsh. r0,r4            ; CR0 set on sign-extended value
0x10b9d8 beq  0x10b9e8           ; if person[1536]==0 -> r5 stays 0
0x10b9dc cmpwi r4,0x12           ; compare with 18
0x10b9e0 bge  0x10b9e8           ; if person[1536]>=18 -> r5 stays 0
0x10b9e4 li   r5,1               ; else r5=1   (1 <= person[1536] <= 17)
0x10b9e8 clrlwi. r0,r5,0x18
0x10b9ec beq  0x10b9f8
0x10b9f0 lwz  r4,-0x58fc(r2)     ; r5==1 -> table A (TOC-0x58fc = -22780)
0x10b9f4 b    0x10b9fc
0x10b9f8 lwz  r4,-0x5900(r2)     ; r5==0 -> table B (TOC-0x5900 = -22784)
```
So `person[1536]` in `[1,17]` selects table A (`-22780`); `0` or `>=18` selects
table B (`-22784`). Two distinct weight tables, gated by the person-type field.
CONFIRMED. I extracted the actual values for the two fixtures from the save's
`nonzero=` rows (word index = `(addr-0x58c)/2`): **Bob and Betty both have
`person[1536]=27`** (word 58 = 27), i.e. `>=18`, so **both use table B**
(`-0x5900`), the same table. This also means the type-split cannot explain any
*within-person* old/new difference for Bob/Betty (same table on both sides).

### Claim 6 — the Bob/Betty contradiction (the round-deciding question)
**Resolved as CONSISTENT with pure-recompute + cadence, given temporal misalignment.
The aggregation law is safe to implement. The exact mechanism is NOT pinned.**

Save data (house07):
- **Bob** (id105, gender0, ptype0): `motivesOldF[3]=74.073`, `motivesF[3]=57.385`.
  Non-mood inputs (slots 5,6,7,8,9,13,14,15) byte-identical old/new: Energy
  99.625, Comfort 98.480, Hunger 98.582, Hygiene 98.830, Bladder 70.952,
  Room 50.222, Social 98.870, Fun 19.750.
- **Betty** (id137, gender1, ptype0): `75.118→60.511`; non-mood identical.

From the save `nonzero=` rows I additionally extracted the two gate inputs:
- Bob: `person[1536]=27` (>=18 → table B), `person[1550]=0` → **suit 0**.
- Betty: `person[1536]=27` (table B), `person[1550]=1` → **suit 1**.

So **both Bob and Betty are suit 0/1, and both use the same (table-B) weights.**
This independently **eliminates the `+1550` suit mask** as an explanation for
their mood move (they are in the "accumulate-all" branch anyway, and neither has
a zero-valued non-mood motive to drop), exactly as the decode agent claimed, and
also eliminates the type-split (both use table B).

Now the logic:
- CalcHappy is a **pure deterministic function** of (type, suit, 8 motives),
  with no state and no previous-mood read. THEREFORE two recomputes with
  byte-identical 8 inputs, same table, same suit, MUST produce identical mood.
- The save shows Bob/Betty with identical 8 inputs, same table, same suit, but
  mood moved by ~16 points. So `saved_mood ≠ f(saved_motives)`.

The only way to reconcile a **pure-recompute** law with this is that the saved
mood and the saved motives are **not from the same recompute instant**. The
cadence (once per 60 units) exactly creates this: the mood stored in the save is
the value from the *last* CalcHappy, whose inputs (notably the independently-
computed **room score**, recomputed by `UpdateCurrentRoom` `0x109452` and called
*after* CalcHappy at `0x10c130` in the same Simulate pass) may have differed at
that recompute from the values captured in the snapshot. This is a save-timing
artifact, not a formula error.

**Decisive verdict: CONSISTENT, and therefore the aggregation law is safe to
implement.** The apparent "mood ≠ f(motives)" is a consequence of cadence-driven
staleness in the save, plus an unpinned live-room delta — it is **not** evidence
for a different formula or a smoothing term. Note framing: the decode agent
phrases "this refutes smoothing" partly as a data inference; actually the refutation
of smoothing is entirely on **code** grounds (no blend term, no previous-mood read
in CalcHappy, single writer). The data alone only shows save misalignment, which is
*orthogonal* to whether a smoothing term exists. The conclusion (no smoothing) is
still correct, just for the right reason.

---

## 2. The decisive question — FIX-FIRST or HOLD?

**FIX-FIRST (scoped).** Implement the aggregation law now; resolve the secondary
parameters before asserting exact numeric parity.

The *core* of the R251 decode is bit-verified and safe to implement:
- **mood = Σ(wᵢ·mᵢ)/Σwᵢ** with the STR#502 curve weights, **not** the port's
  equal-weight `/8`. The port must also use the **variable** denominator (not `/8`).
- **No smoothing/blend.** The native writes the weighted average directly on each
  recompute. This is a direct correction to `port-audit.md`, which proposed a
  `ComputeMoodTarget` + `AdvanceMood(smoothing)` seam based on R223's now-refuted
  "smoothed toward target" hypothesis. There is **no** `AdvanceMood` blend — the
  port should just store the recomputed weighted average on the cadence boundary.
- **Cadence/throttle**: recompute on the native cadence (once per 60 units of the
  sim clock), gated by the person-active flag and the `person[1478]`/`person[142]`
  zero-checks. Match the native's 60-unit interval to the port's clock.
- **person+1550 suit gate** (suits 0/1 all, suits 2-5 drop the entry if its motive
  equals the gate constant) and **person+1536 type split** (table A/B) are both
  real; include them once their exact semantics are pinned.

Before writing a PASS/FAIL autotest that asserts **exact** mood values:
- Pin the **STR#502 curve→motive mapping** and the **room weight** (R223's "best
  single-subset fit within 1.73" is not pin-grade; it used a subset and a guessed
  room weight of 8).
- Resolve the **`[r8+0x30]` gate constant** (claimed 0.0, unverified).

These are the FIX-FIRST items. They are refinements, not blockers: the port's
current `/8` average is definitively wrong and the weighted-average, no-smoothing
law is proven.

---

## 3. Corrections to the decode agent

1. **"Sign bit" is imprecise.** `0x10baec rlwinm. r0,r0,1,31,31` =
   `rotl(diff,1) & 0x80000000`, which isolates **bit 30** of the diff (not the MSB).
   For the small diffs this coincides with negativity; the table is unaffected.
2. **Framing of "refutes smoothing."** The refutation of a smoothing term rests on
   the **code** (no blend, no previous-mood read, single writer), not on the
   Bob/Betty data. The Bob/Betty data only demonstrates save-time misalignment,
   which is orthogonal to whether smoothing exists. The net conclusion is correct.
3. **port-audit.md design correction.** Do **not** implement an `AdvanceMood`
   smoothing/blend step — there is none. The mood is set directly to the
   recomputed weighted average on the cadence boundary. The two-phase fixture in
   port-audit.md chips on "mood should NOT jump to the new steady-state in a
   single boundary" is based on the refuted smoothing premise; the native *will*
   jump on a recompute (it is a pure recompute), it just does so at most once per
   60 units (so over a 10-game-min window the *shape* is stepped, not squashed by
   a time constant).
4. **R223's "only via vtable" is wrong** — R251's direct-chain finding stands.

Everything else in the decode (claims 1-5 behavior, the correction to R223's
`addi r0,r0,1`, the suit/index/type structure) is **confirmed** by my independent
decoding.

---

## 4. What I could NOT verify (honesty)

- **The gate constant `[r8+0x30]` = 0.0 is UNVERIFIED.** The TOC base and the
  weight-table pointers are unrelocated placeholders in the static image (reading
  them yields string data). I could not resolve them statically. It only affects
  suits 2-5. (The decode agent disclosed this; it does not change the verdict.)
- **The STR#502 curve→motive mapping and the room weight are NOT pin-grade.** I
  did not recompute the weighted average against the saves because the exact
  mapping is unresolved; R223's best-fit is approximate.
- **The absolute clock unit / rate of the 60-unit cadence is UNVERIFIED.** The
  "60" is 60 of the `long time` units the sim passes to `cXPerson::Simulate`. I
  could not identify the tick source.
- **I could not exhaustively rule out an indexed store (`stfsx`/`stfdx`) to
  person+1944** somewhere in the 6.4 MB image; I sampled the ones in the person
  update path and all wrote other objects' arrays. This is the decode agent's
  stated residual and remains open, though low-risk.
- **The exact Bob/Betty mechanism is NOT pinned.** I confirmed it is *consistent*
  with pure-recompute + cadence (and that suit-mask/type-split are excluded) but
  could not determine which live input (most plausibly the room score) differed at
  the recompute vs the save instant. This needs a live trace sampling the room and
  motives at each CalcHappy call.
- **CalcHappy's r3 = person relies on a back-pointer** (`this+0x80` → owner person,
  loaded at `0x09edc4 lwz r3,0x80(r3)`). I verified the chain reaches CalcHappy and
  that CalcHappy treats r3 as person (reads person+1536/1550/0x78c+idx, writes
  person+1944), but I did not exhaustively re-derive the object layout.
- **The "suit" semantic label** is inferred (ResetSuits writes 0-5 based on which
  suit string resolves). The gate *arithmetic* (suit 0/1 vs 2-5) is proven; calling
  the 0-5 value a "suit index" is a reasonable but not proven label.

---

## 5. Bottom line

- `verify.py` **runs and PASSES** (exit 0) — not over-claimed.
- **Claims 1, 2, 3, 4 (table), 5: CONFIRMED** at the instruction level.
- **Claim 6 (Bob/Betty): CONSISTENT with pure-recompute + cadence once temporal
  misalignment is allowed** — the aggregation law is safe to implement; the exact
  mechanism is UNRESOLVED (needs a live trace), but it is a save-timing artifact,
  not a formula error.
- The important correction to R223 (`0x10bad8 = li r0,1`, `person+1550` = suit
  index written only by ResetSuits, direct-bl chain) is **correct**.
- **Verdict: FIX-FIRST (scoped).** Implement `mood = Σ(wᵢ·mᵢ)/Σwᵢ` (STR#502 curves,
  variable denominator) with **no smoothing**, recomputed on the cadence. First
  pin the curve→motive mapping, the room weight, and the `+1550` gate constant
  before asserting exact autotest values.
