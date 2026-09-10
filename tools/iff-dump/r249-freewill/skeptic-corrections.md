# R249 skeptic re-verification — corrections to decode.md

Independent re-derivation from the original executable (same SHA256), all cited
ranges re-disassembled with r145/capdis2.py plus hand-decoding of the words
capstone cannot decode. Root cause of the top findings: **capstone silently
emits `.long` for every `fcmpo` (XO=32) word** — `0xFC010040` = `fcmpo cr0, f1, f0`
— and the two most load-bearing compares in this decode are `fcmpo` words whose
operand order was guessed, not decoded. Pins in verify.py store the correct
bytes; several pin COMMENTS and three fixtures encode the misreadings.

Severity-ordered corrections:

## S1 CRITICAL — the comparator sorts DESCENDING, not ascending (0x10a040)

decode.md C claims `delta <= eps1 → return 1`, `delta < eps2 → return -1`
(→ ascending, "best (most negative) first"). Actual bytes:

```
10a040 lfs f1, 8(r4)      ; b.score (qsort 2nd arg)
10a044 lfs f0, 8(r3)      ; a.score (qsort 1st arg)
10a04c fsubs f1, f1, f0   ; delta = b - a
10a050 lfs f0, 0x24(r3)   ; eps1
10a054 fcmpo cr0, f1, f0  ; A = delta, B = eps1      (0xFC010040 — capstone prints .long)
10a058 ble  0x10a064      ; delta <= eps1 -> tail
10a05c li   r3, 1 ; blr   ; delta >  eps1 -> return +1  => b BEFORE a
10a064 lfs f0, 0x28(r3)   ; eps2
10a068 fcmpo cr0, f1, f0  ; A = delta, B = eps2
10a06c mfcr r0 ; 10a070 srwi r0,r0,0x1f ; 10a074 neg r3,r0 ; blr
                          ; returns -1 iff delta < eps2, else 0
```

Comparator is `+1` iff `b.score > a.score (+eps1)` — positive means "b first",
so qsort produces a **DESCENDING** order: the vector head is the HIGHEST score.
The decoder swapped the branch and fall-through returns. The `mfcr/srwi 31`
tail extracts CR bit 31 (CR7's SO/UN, compiler idiom), so the `delta < eps2`
case returns -1 or 0 depending on stale CR7 — both non-positive, hence the net
order is strictly descending on `delta > eps1` regardless. verify.py fixtures
`comparator_ascending_eps0` and `sort_head` encode the inverted law (tautologies
relative to the misread bytes).

Consequence: the K-draw samples the **top-K best**, not top-K worst.

## S2 CRITICAL — winner cutoff polarity inverted; the gate is Posture (0x109edc)

decode.md C/D: "accept iff `0.1 >= score`" (reading the compare as A=0.1,
B=score). Actual: `0x109edc: 0xFC010040` = `fcmpo cr0, f1, f0` with
`f1 = winner.score` (lfs f1, 8(r5)), `f0 = 0.1` (lfs f0, 0(r3)) — A=score,
B=0.1. `bge 0x109ee8` (LT==0) therefore **accepts iff `score >= 0.1`** and
rejects everything scoring below 0.1; the fall-through (`li r6, 0`) is the
reject path. Instruction-exact law:

* `person+0x58c == 0` → accept unconditionally (no cutoff);
* `person+0x58c != 0` → accept iff `winner.score >= 0.1` ("min autonomy score
  for sitting", TOC-0x70cc; runtime-overridable, see S9).

Field identity (resolves two of decode.md's UNRESOLVED items): person+0x58c is
**person-data word 0 = Posture**. Evidence: `InterpValue__8cXObjectFssb`
(0xede20) computes `*(s16*)(obj + 2*idx + 0x58c)` — the SimAntics attribute
array base — and the r151 decode + FreeSO `VMPersonDataVariable` confirm
`word N = cXPerson + 0x58c + 2N` with `Posture = 0`. There are zero
immediate-offset stores to 0x58c in the code section (array, not a scalar
boolean). So: a STANDING sim (posture 0) takes the drawn winner with no cutoff;
a sitting/kneeling sim requires score >= +0.1. This also settles the score sign
question decode.md left open: scores are happiness-gain (positive good), which
is self-consistent with S1's descending sort. verify.py fixture
`winner_cutoff` encodes the inverted polarity.

## S3 HIGH — missing deterministic winner path (idx = 0), 0x109e30-0x109ea8

Not present anywhere in decode.md (prose, pseudocode, or the FreeSO table):

```
109e30 lha r0, 0x5cc(r30); cmpwi r0,0; bne 0x109e48   ; PersonType != 0 (visitor)
109e3c lbz r0, 0xc(r17); cmplwi r0,0; beq 0x109e70    ; OR cand[0].flag != 0
       (fall-through when both false -> uniform draw)
109e48..: GetZoningType(lot); zoning == 1 -> 0x109e70 (uniform draw)
                                   zoning != 1 -> 109ea8: li r3, 0  ; idx = 0
```

Law: if the person is a **visitor (word 32 PersonType != 0)** OR the **first
sorted candidate's flag byte (ScoredInteraction+0xc, = Interaction flags bit 0)
is set**, and the lot is **NOT downtown** (GetZoningType != 1), the winner is
**always index 0** — no random draw at all. The uniform `rand() % min(K,count)`
draw only runs otherwise. This removes one of decode.md's four FreeSO
divergences in modified form: the native DOES have a deterministic-first path,
just conditioned differently than FreeSO's `AutoFirst`.

## S4 HIGH — free-will prelude scope wrong

decode.md E: "If the byte is set, the ENTIRE gating prelude (… house-first-person
check … and the *(TOC-0x7110) autonomy-suspended byte) is SKIPPED — with free
will ON, selection runs unconditionally." Wrong: the free-will branch
(`bne 0x109928` at 0x1098a0) lands at 0x109928, and BOTH the house-person-0 gate
(0x109934-0x109958: `*( *( *(TOC-0x778c) )+0x10 )+0x10c == 0` AND
`PersonFinder.GetPersonInHouseIndex(0) == person` → return 0) and the
`*(TOC-0x7110)` byte check (0x10995c-0x109970 → return 0) live at/after
0x109928 — they execute **regardless of the free-will byte**. Only the
person+0x5cc / person+0x60e / `*(TOC-0x7114)` checks (0x1098a4-0x1098d0) and
the visitor family-number/gender-parity check (0x1098e4-0x10991c) are
free-will-gated. The pseudocode's `if not FREEWILL:` block must be split
accordingly.

Precise free-will-OFF prelude (for the record): PersonType(word32)==0 →
continue only if `(word65 & 6) != 0` AND `*(*(TOC-0x7114)) == 0`, else return 0;
PersonType!=0 (visitor) → continue unless the chain word equals
person word 61 (TS1FamilyNumber) AND the word-65 parity idiom fails
(the idiom `((g^1)>>1) - ((g^1)&g)` bit-30 test passes iff `g >= 2` for
non-negative g — same dance as the self-target gate at 0x109bb0).

## S5 HIGH — missing candidate skip: H' == CFG.f4 drops the candidate

0x109d4c-0x109d54 (immediately after GetInteractionScore):

```
109d4c lfs f0, 4(r27)     ; CFG+0x4
109d50 fcmpu cr0, f0, f1  ; CFG.f4 vs H' (this one capstone DID decode)
109d54 beq 0x109db0       ; equal -> skip candidate (no push)
```

A candidate whose post-interaction mean score exactly equals AutonomyConstants
+0x4 is silently discarded. decode.md C mentions CFG+0x4 only as an
UNRESOLVED "score sentinel / IsSleeping threshold"; the skip itself is absent
from section C, the pseudocode, and the "safe to implement" list. (With the
pool unrecoverable — see S9 — the runtime value of CFG.f4 is unknown; if it is
0.0, candidates whose H' evaluates to exactly 0.0 are dropped.)

## S6 MODERATE — stratum parity filters inverted in the pseudocode

TryFindBestAction 0x109c34-0x109c88: stratum 2 → `beq 0x109c8c` — **even object
ids are processed**, odd skipped (decode pseudocode skips even). Stratum >= 3
(and negative) → `beq 0x109dcc` — **odd ids are processed**, even skipped
(pseudocode skips odd). The stratum-1 distance test (process iff
`dist <= CFG.f1c`) and the "stratum forced 0 on non-residential lots" reading
are correct as written.

## S7 MODERATE — GetAttenuationValue enum mapping wrong (0x155340)

decode.md C: "entry+0xc enum: 0 → entry's own float +0x10; 1/3/4 → low/
moderate/high". Actual (both the family and visitor halves):

* 0 → entry+0x10 (own float)
* 1 → `*(TOC-0x57c0)+0` (default pair, first)
* 2 → LOW  (TOC-0x70e0 family / TOC-0x70ec visitor)
* 3 → MODERATE (TOC-0x70e4 / TOC-0x70f0)
* 4 → HIGH (TOC-0x70e8 / TOC-0x70f4)
* other → `*(TOC-0x57c0)+4` (default pair, second)

Enum **2 is the low slot**, and enum 1 returns the default pair — decode.md
omits 2 entirely and misassigns 1. verify.py fixture `attenuation_enum` encodes
the wrong mapping (its own pins at 0x15538c/0x1553f8 point at the enum-2 arms,
so the pins contradict the fixture).

## S8 MODERATE — person field identities resolved (decode.md: UNRESOLVED/misnamed)

Established via `InterpValue__8cXObjectFssb` (0xede20, base 0x58c + 2*idx),
zero immediate-offset stores to these offsets, and FreeSO's
`VMPersonDataVariable` (r151 decode in-tree):

* person+0x58c = word 0 **Posture** (0 = standing; gates the S2 cutoff)
* person+0x5b0 = word 18 **LogicSkill** — the K divisor operand is a skill,
  not a personality trait and not a motive: `K = max(1, RSC − trunc(LogicSkill/2000))`
* person+0x5cc = word 32 **PersonType** (visitor flag — decode.md's "visitor"
  reading confirmed)
* person+0x5d0 = word 34 **GreetStatus**
* person+0x5d4 = word 36 **AutonomyLevel** — the advertise-weight cap is the
  sim's autonomy level (`weight <= AutonomyLevel`), a person-data variable, not
  an engine-derived maximum
* person+0x600 = word 58 **PersonsAge** ("child" tests = age in [1,18))
* person+0x606 = word 61 **TS1FamilyNumber** (prelude compares the active-house
  chain word against it)
* person+0x60e = word 65 **Gender** (the "bit31/30/29 flagged person" checks are
  gender bit masks 0x1/0x2/0x4)
* GetInteractionScore's motive-value read is a double indirection decode.md
  garbled: `mv = *(s16*)( *(MotiveEffects+0xbc) + idx*2 + 0x58c )` — the +0xbc
  field is a pointer (to the person), and +0x58c is offset from THAT, i.e. the
  same person-data array.

## S9 MODERATE — constants are runtime values; the "BSS 0x59a524" address is
inconsistent with decode.md's own container map

The AutonomyConstants pointer stored at TOC-0x58e8 is 0x59a524, and the whole
pool (MOTIVETAB 0x59a1e0, MotiveConstants 0x59a2f4, magic double 0x59a308, CFG
0x59a524, attenuation defaults 0x59a840) lies OUTSIDE the image decode.md
itself documents (data 0..0x7bf80, BSS 0x7bf80..0x989a4). It is a relocated
constants pool; its static bytes are not recoverable with the project's
unpacker (searched: the magic double 0x4330000080000000 occurs nowhere in the
file's mapped data under any linear base). Confirmed by scan: all 14 TOC-0x58e8
load sites inspected (0x108180, 0x109894, 0x10a048, 0x10aa08, 0x10b9c0,
0x10bc68, 0x10c00c, 0x10c834, 0x10cb08, 0x10d5b0, 0x10e224, 0x1107dc, 0x111e0c,
0x112760) — zero stores through the CFG register anywhere, so decode.md's "no
writer" is right and the epsilons CFG+0x24/+0x28 are plausibly BSS-zero — but
that is an assumption, not a pin, and it equally applies to CFG+0x1c/+0x20/+0x4,
CFG2.f0/f4/f8, and MOTIVETAB's content (MOTIVETAB is load-bearing for every ad
row with row[0] != 0). Meanwhile `AutonomyConstantsClient::UpdateConstants`
(0x112750) and `MotiveConstantsClient::UpdateConstants` (0x9ef10) OVERWRITE the
named-constant slots at runtime from the FloatConstants resource (FCNS 2 in
global.iff per FreeSO's own comment): min-scores 0.2/0.05/0.1, random selection
count 10, and the six attenuation values are pre-load defaults only. A port
should read the same names from global.iff at runtime, not hardcode 0.1/10.

## S10 MINOR — TestInteraction / entry-flag masks

* "entry+0x14 bit0 (autonomy-allowed) must be set" is wrong as stated
  (0x10639c): bit0 SET skips the visitor gate entirely; bit0 CLEAR fails only
  for visitors (PersonType != 0) when the lot is downtown OR entry PPC-bit26 is
  clear. Non-visitors ignore bit0.
* The entry-flag bit numbers in decode.md B do not match the in-memory masks
  (bit N here = PPC numbering, mask 1<<(31-N)): unconditional candidate skip =
  bit24 (0x01000000, Append only, 0x105b6c); child (age 1..17) blocks bit27
  (0x08000000, TestInteraction 0x106478); Gender-bit0x2 requires entry bit21
  (0x04000000); Gender-bit0x4 requires entry bit22 (0x02000000); plain adult
  blocked by bit25 (0x00200000, TestInteraction 0x1064c8); downtown visitors
  require bit0 or bit26 (0x00000010). decode.md's {23,20,18,19,22} match
  nothing in either numbering.
* Entry+0 == 0 (action number 0) auto-passes TestInteraction without running
  the tree (0x1064f8-0x106500).
* The tree call (0x106570) is `(actor, target->ObjSelector+0xc, target+0xf0
  objectID, params)` — the TTAB action number rides in params[2] (stack
  0x4c = entry+0x18), not as the third register argument as decode.md item 4
  implies.
* Interaction flags: bit2 set + bit28 cleared on entry (0x1062f0); bit3 =
  passed (0x1065f0); bit4 = tree ran with nonzero person+0xae (0x10661c).

## S11 MINOR — assorted

* ScoredInteraction field offsets are {0: u16 objId, 4: u16 actionNumber,
  8: f32 score, 0xc: u8 flag} (16-byte element); decode.md's field list reads
  as packed 0/2/4/8. Comparator and winner reads confirm.
* AddAction's re-test gate is entry flags PPC bit29 (mask 0x4, 0x10af7c), not
  "bit28".
* SetFreeWill (0xc9d40) mirrors to `SetGlobal(0x1e, v)` only when the simulator
  chain (`*( *(TOC-0x778c) )+0x1c`) is non-null.
* Dead code decoded: 0x109d64 `fcmpo cr0, f0, f28` result unused; f28
  (0.2/0.05) confirmed dead in this routine as decode.md claims.
* Confirmed correct (spot-checked against the binary): prim dispatch
  (TryElement case 0x10c430 → bl 0x109860, range [3,0x2f], table *(TOC-0x5904));
  score formula `atten*(H'-H)` with `atten = CFG.f20/(CFG.f20+dist*entryAtt)`;
  MEAN-over-entries scoring and reciprocal-span curve interpolation in
  GetCurrentScore/GetInteractionScore; 1000−mv inversion and
  `row1 + mv*row2/CFG2.f4` (row1@+2, row2@+4); int→double 0x4330 idiom via
  TOC-0x5b34; K arithmetic (0x10624dd3 mulhw + srawi 7 + sign-fix = trunc(t/2000),
  clamp >= 1, `rand() % min(K,count)`); free-will byte 0x45f1c (init 1, TOC
  slot -0x7248 verified), GetFreeWill/SetFreeWill/OptionsMgr tail-call;
  candidate vector 0x814/0x818/0x81c cleared per run (0x116040); Interaction
  0x3c layout with CTGString clear at +0x28 (0x106f10); TTAB backward iteration
  and `weight in (0, word36]` gates; vehicle class {4,7} token override
  (GetInventoryByNeighbourID + FindToken); qsort(base=0x81c, n, 0x10, TVector
  at TOC-0x58f8 → data 0xd9d0 → {0x1011b0, 0x8000} → 0x10a040) — the comparator
  TVector claim is correct; TryGosubFoundAction hand-off (stack+4, person+0x72,
  entry+0x18 match, ctor 0x9ca90 with entry+2, SetStackVars, SetCurrentAction
  0x108e60, returns 3); 8-slot ring at +0x820 (GetIndAction: head +0xa00,
  count = tail-head, `(head+i)&7 * 0x3c`); Simulate never calls selection;
  TryFindBestAction never calls CanChooseAutonomously; person-person Append
  tail (selector+0x12==2, +0xa08 GetEntry flags PPC-bit30, ctor 0x9cc50);
  downtown-visitor room==0 restriction (0x105c04-0x105c18); AppendInteractions
  dispatch (tester+8 → 0x105900).

## verify.py audit

Ran: PASS (80 code pins, 12 fixture groups). All 80 pins independently
reproduced against the binary — the pinned BYTES are genuine. Problems:

* Pin comments misstate the five `fcmpo` words capstone drops (0x109c64,
  0x109d64, 0x10a054, 0x10a068, 0x109edc): e.g. `0x109EDC: 0xFC010040 #
  fcmpu cr0, f0, f1` is actually `fcmpo cr0, f1, f0` — the exact inversion
  behind S1/S2.
* Fixtures that encode misreadings (tautological vs the claims, not vs the
  binary): `comparator_ascending_eps0`, `sort_head` (S1), `winner_cutoff` (S2),
  `attenuation_enum` (S7).
* Sound fixtures: `selection_pool_K`, `uniform_draw`, `int_to_double_idiom`,
  `curve_interp`, `score_mean`, `distance_attenuation`, `action_ring`.
  `freewill_mirror` is a restatement of the claim (no binary content).
* Data pins verified: data[0x45f1c]=1; 0.2/0.05/0.1/10 at 0x48ea4-0x48eb0 with
  TOC slots -0x70bc/-0x70c0/-0x70c8/-0x70cc → 0x48ea8/0x48ea4/0x48eb0/0x48eac
  (decoder checked values but never the slot→address links; skeptic-verified
  here); comparator chain TOC-0x58f8 → 0xd9d0 → {0x1011b0,0x8000} → 0x10a040;
  constant name strings at the blob.

## Verdict

FIX FIRST. The structural skeleton (gather → test → score → sort → draw →
cutoff → hand-off) and most of the per-candidate law survive, but the sort
direction, cutoff polarity, missing deterministic-draw path, missing sentinel
skip, free-will prelude scope, attenuation enum, and stratum parities are each
individually sufficient to make a rebuilt VMFindBestAction observably wrong.

## Maintainer adjudications (post-implementation)

P2-13 (r249 free-will repair round, 2026-09) resolved three decode-vs-code
conflicts IN FAVOR OF THE LANDED ENGINE. These are settled; do not "fix" the
engine back to the contradicting note:

1. **The min-score gate at 0x109d64-0x109d74 is LIVE.** The S11 bullet above
   ("f28 confirmed dead in this routine") is superseded: the `fcmpo` + `blt`
   window admits only candidates with `score >= FCNS min-autonomy-score`
   (family/visitor). The engine implements it as `if (score < minScore)
   continue;` (VMFindBestAction.cs, LAW STEP 3b tail).
2. **Deterministic idx-0 iff zoning == 1 (downtown).** The branch table above
   ("zoning == 1 → uniform draw; zoning != 1 → idx = 0") has the polarity
   INVERTED. The mechanically re-verified reading (subfic/cntlzw chain yields
   1 iff zoning == 1; `bne` targets the `li r3,0` at 0x109ea8) and the CFG
   decode §4(c) agree: idx 0 is forced ON downtown, uniform draw otherwise.
   The engine codes exactly that (ZoningIsDowntown is constant false on
   Simitone's residential-only mount, so the uniform draw runs in practice).
3. **The landed rlwinm masks are the record.** Append/AppendForAuto flag
   gates ride rlwinm 0x15/0x15 (entry 0x400), 0x16/0x16 (0x200), 0x1b/0x1b
   (0x10), 0x19/0x19 (0x40), and the word-65 test masks 0x1e/0x1d (0x2|0x4).
   The engine's TTABFlags literals match these verbatim; earlier test-bit
   readings that disagree are stale.
