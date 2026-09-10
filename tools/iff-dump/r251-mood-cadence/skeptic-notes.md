# R251 — skeptic notes (how this decode could be wrong, and what I did to rule each out)

Adversarial self-review. Each risk below is a way the R251 conclusions could be
wrong, and the check that mitigates it. Confidence is graded per claim in
`decode.md`; this file is the load-bearing honesty dump.

---

## 1. Branch polarity — the #1 error class in this codebase

**Risk.** `ppc_decode.py` maps BO=4 → `eq`/`ne` and BO=12 → `ne`/`eq`, i.e. it
**inverts** compare branches. If I trusted it, the suit-gate block and the
cadence gate would read backwards. Concretely:

- `0x10baf0` word `41820010`: BO=12, BI=2. `ppc_decode` renders `bne(2)` but
  the truth is **`beq`** (branch if CR0[EQ]==1).
- `0x10bafc` word `4182000c`: BO=12, BI=2. True mnemonic **`beq`**, but
  `ppc_decode` prints `bne`.

**Mitigation.** I did **not** trust `ppc_decode` for any branch. I decoded every
branch word three ways: (a) capstone (`bne`/`beq`/`blt`/`bgt`), (b) manual
BO/BI field extraction, (c) the known-good cross-check `0x4082000c` (BO=4 →
capstone `bne`), which pins BO=4 = "branch if CR[BI]==0" and therefore BO=12 =
"branch if CR[BI]==1". capstone and manual agree on all seven branches in
CalcHappy/Simulate. `verify.py` asserts the raw words so a polarity error is
falsifiable. **Residual risk:** capstone is a real disassembler and its compare
branch polarity is correct for POWER (verified on the cross-check), so I am
confident, but the `rlwinm.`-set CR0 interaction (which flags CR0 after the
sign-bit extract) is worth re-checking: `0x10baec` is `rlwinm.` (Rc=1, word
`54000fff` has bit 0 set), so it DOES set CR0. If Rc were 0 (not setting CR0),
the `beq` at 0x10baf0 would test a stale CR0 — but `54000fff & 1 = 1`, so Rc=1.

## 2. The `li r0, 1` at 0x10bad8 (changing R223's reading)

**Risk.** I claim `38000001` is `li r0, 1` and **not** `addi r0, r0, 1`, which
is the crux of the corrected suit-gate decode. If this is wrong, the whole
suit-{0,1}-vs-{2,3,4,5} table collapses.

**Mitigation.** `rA = (0x38000001 >> 16) & 31 = 0`. In POWER, when the `rA`
field of `addi`/`addis` is 0, the effective addend is the **literal 0**, not
register r0's value. So `addi r0, 0, 1` ≡ `li r0, 1`, independent of the r0
carried into the block. capstone renders it `li r0, 1`. R223 disassembled this
as `addi r0, r0, 1` only because the capstone **bytes-to-mnemonic** path in
`capdis2.py` doesn't special-case `rA==0`. **Residual risk:** this is a
well-defined PPC constant; if a future reader questions it, decode the word
with a disassembler that honors the rA==0 literal (capstone does).

## 3. "CalcHappy is the only writer of person+1944"

**Risk.** I only searched for constant-disp `stfs`/`stfd` to disp 1944. There
could be an **indexed** store (`stfsx`) or a store through a computed address
(`person + 1932 + 4*3`) that writes the mood slot without a literal `1944`.

**Mitigation.** I searched the whole image for `stfs`/`stfd` with disp 1944 and
found three: `0x10bb18` (CalcHappy, base r3 = person) and two with bases r1/r2
(stack frame / TOC global), which are not person fields. I did **not** exhaustively
search for `stfsx` indexed stores targeting the mood slot. **Residual risk:**
an `stfsx`-based mood writer would invalidate the "single writer / pure
recompute, no smoothing" conclusion. Given CalcHappy's structure and that the
mood slot is a plain array element, I judge this unlikely, but it is the
strongest un-addressed hole and I disclose it.

## 4. The gate constant `[r8+0x30]` being 0.0

**Risk.** I read `[r8+0x30]` and `[r8+0x04]` as 0 via r2, assuming
`TOC = 0x5c1460` ("data + 0x8000"). But when I read the weight-table structs at
`TOC-0x58fc`/`TOC-0x5900` they pointed at string data (`"rites"`,
`"cWinTransformMeDlg"`), which suggests the pointers are **unrelocated
placeholders** or my TOC base is wrong. So the "0.0" constant, and even the
whole weight-table resolution, is **unreliable**.

**Mitigation / re-scope.** The **suit-gate table** (suit 0/1 vs 2-5 triggering
the value-gate) is derived purely from the sign-bit formula at
`0x10bad8-0x10baf0` — it does **not** depend on the constant value or the TOC.
So that part stands. Only the *label* "drops zero-valued motives" depends on the
constant being 0.0, and that label is what I hedge in `decode.md`. **Residual
risk:** if the constant is some value Bob/Betty actually have, my "suit gate is
ruled out for Bob/Betty" claim weakens. I mitigate by phrasing it conditionally:
*assuming the constant equals no Bob/Betty motive,* the gate doesn't drop their
motives. It is a caveat, not a proof.

## 5. The cadence "60" and the `blt` polarity

**Risk.** `0x10c0d8 addi r0, r3, 0x3c` is +60. If the `blt` at `0x10c0e0` were
actually `bge`, the cadence semantics invert (it would run *before* 60 units,
not after). Also `0x3c` is 60 — no other literal could be meant.

**Mitigation.** `0x3c = 60` is exact. capstone decodes `blt 0x10c108` (BO=0,
BI=0/LT) and I cross-checked branch polarity per risk #1. `verify.py` asserts
`3803003c` and `41800028`. **Residual risk:** I could not pin the **unit** of
`r26`, only that the cadence is "60 units of the `long time` passed to
`cXPerson::Simulate`". The absolute rate (Hz) is unknown and I say so.

## 6. "Motives::Sim is the real CalcHappy caller; R223's 'only vtable' was wrong"

**Risk.** I found a direct `bl 0x10b9c0` at `0x09edc8` (in Motives::Sim) and
`bl 0x9eab0` at `0x10c104` (in `cXPerson::Simulate`). R223 claimed CalcHappy is
only reached via a VTABLE. If I mis-attributed these (e.g., the `bl` targets are
wrong due to an address-space conversion), the whole chain is wrong.

**Mitigation.** The `bl` target computation in `capdis2.py`/`ppc_decode.py` is
the exact validated formula (24-bit LI×4, sign-extended; R129-corrected). The
MSP430... no, the PPC LI field decode is correct and `verify.py` asserts the raw
words `4806cbf9` (→ 0x10b9c0) and `4bf929ad` (→ 0x9eab0). These two calls
appear in the disassembly text files. **Residual risk:** low. The `+2`
symbol convention (file offset = symbol − 2) is consistent with the function
record markers (`00092040`/`00092041`) and the bl targets match.

## 7. Did I invent a "suit" semantic?

**Risk.** I called person+1550 the "suit index" because `SAnimator::ResetSuits`
writes 0-5 there. But `ResetSuits` is an animator (skin/outfit) function, and
the value 0-5 could encode something else (e.g., a "body type" or "life stage"
code) that happens to be set there.

**Mitigation.** The value is written as 0..5 by a function literally named
`ResetSuits`, and it selects among six branches keyed on which suit string
resolves (the `bl 0x141df0` string-lookup + `cmpwi` per branch). "Suit index" is
the most natural reading. **Residual risk:** the semantic label could be wrong,
and I flag that the *gate arithmetic* (suit 0/1 vs 2-5) is what's proven — the
name "suit" is a reasonable inference, not a proven fact.

## 8. The Bob/Betty "identical inputs" premise

**Risk.** I trust R223's dump (`house_probe --motives`) that the 8 non-mood
inputs are byte-identical old vs new. If the dump truncates or mis-parses the
floats, the "identical inputs" premise fails.

**Mitigation.** The dump gives both integer and float motives
(`motives`/`motivesF` and `motivesOld`/`motivesOldF`); for Bob and Betty the
non-mood slots are equal in BOTH the integer and float columns. The mood slat
(3) is the only differing column. **Residual risk:** low, but the whole
"mood ≠ f(motives)" argument rests on it. If the floats actually differed at
the 4th decimal, the conclusion could soften — but a Δ of 15 points needs a
*huge* input change, far larger than any float-rounding, so the argument holds.

---

## Bottom line

The two claims I am **most** confident of (both PROVEN, both bit-pinned and
SHA-pinned): (1) CalcHappy is a pure weighted-average recompute with **no**
smoothing term and is the primary writer of the mood slot; (2) the mood/motive
recompute is rate-limited to **once per 60 time-units** by `person[2064]`. The
**suit-gate arithmetic** (suit 0/1 vs 2-5) is proven as math but the semantic
label ("suit") and the gate constant (0.0) are inferred. The **Bob/Betty
mechanism** is NOT pinned: it must be state outside the 8 captured motives
(most plausibly a live room-score that differs at the recompute moment), and
the +1550 suit mask is *ruled out* as the explanation because neither Bob nor
Betty has a zero-valued motive for the gate to drop.
