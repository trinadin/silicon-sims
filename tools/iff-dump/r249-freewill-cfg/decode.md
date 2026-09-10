# R249 follow-up — the autonomy constants, the person attribute array, the curve
# resource, the real "sort", and the corrected winner law

Resolves the items the r249-freewill decode left UNRESOLVED. All code addresses
are executable FILE OFFSETS (code section starts at file 0x8e90); data addresses
in `sec1+k` refer to the UNPACKED data section image (r104 law: 0x6d834 packed
bytes at file 0x5c22f0 unpack to exactly 0x7bf80; TOC register r2 = sec1+0x8000,
so `TOC-k` = sec1+0x8000-k). Stored pointers < 0x7bf80 are sec1-relative;
larger stored pointers are CODE-section-relative (file = 0x8e90 + value).
Nothing outside this directory was written.

Headline corrections to r249 (its instruction evidence still holds; the
constants and one interpretation change):

1. **CFG is not BSS and is not zero-initialized.** `*(TOC-0x58e8)` = sec1+0x2718
   stores `0x59a524`, which is a CODE-SECTION literal-pool object (file
   0x5a33b4). Its fields are compiled-in constants, all present in the file.
2. The comparator epsilons are **+1e-7 and -1e-7** (not 0.0).
3. The routine called as "qsort" (0x59a370) is a **heapsort**, and with the
   game's one-sided comparator its sift can never swap strictly-different
   scores; the shipped behavior is a **rotate-left-by-one** of the candidate
   array (plus identity-only permutations inside near-equal groups). The draw
   therefore does NOT pick from a score-sorted head.
4. The twelve autonomy constants are **overridden at runtime by the FCNS
   resource in the GLOB**: the live sitting cutoff is **1e-6** and the live
   **random selection count is 4** (the compiled statics 0.1/10 are boot-time
   placeholders that UpdateConstants overwrites).
5. The motive curves come from the person GLOB file's string sets **STR# 501
   "InteractionScoreCurves"** (adult) / **STR# 503 "InteractionScoreCurvesChild"**
   — both present in `Global.iff` inside `GameData/Global/Global.far`, and the
   curve data settles the score sign: **higher score = motive improvement.**

## 1. The CFG object (AutonomyConstants) — location, layout, runtime values

`TOC-0x58e8` (sec1+0x2718) stores `0x59a524` = code-relative → file 0x5a33b4.
`UpdateConstants__23AutonomyConstantsClientFv` (0x112750, no direct callers;
virtual — vtable slots referencing it at file 0x5abc2c and inside the packed
data at 0x5c7290) opens with `lwz r31, -0x58e4(r2)` (string pool, sec1+0x491c0)
and `lwz r30, -0x58e8(r2)` — **r30 is this same 0x59a524 block**, read as the
DEFAULTS table. Full layout (floats unless noted; values read from the file):

| off | value  | consumed by |
|-----|--------|-------------|
| +0x00 | 0.5  | (not traced to a reader) |
| +0x04 | 0.0  | `IsSleeping__8cXPersonFv` 0x108180: returns person+0x7b8 > CFG+4 |
| +0x08 | 3.0  | UpdateConstants → "functional distance attenuation" → *(TOC-0x7150) (sec1+0x48eb4, float, initial 3.0) |
| +0x0c/+0x10 | +0.1875 / -0.1875 | (not traced) |
| +0x14/+0x18 | 45.0 / 180.0 | (not traced) |
| **+0x1c** | **7.0** | TryFindBestAction 0x109c60: stratum-1 distance gate (`CalcShortDistance <= 7.0`) |
| **+0x20** | **1.0** | TryFindBestAction 0x109d2c: attenuation normalizer N (`atten = N/(N + dist*entryAtt)`) |
| **+0x24** | **+1.0000000116860974e-07** | comparator epsilon eps24 (0x10a050) |
| **+0x28** | **-1.0000000116860974e-07** | comparator epsilon eps28 (0x10a064) |
| +0x2c..+0x44 | -99, 13, 100, -1, 2, 60, 30 | (not traced) |
| +0x48 | 0.2  | default for "min autonomy score for family" → *(TOC-0x70c0) AND for "min autonomy score for sitting" → *(TOC-0x70cc) |
| +0x4c | 0.05 | default "min autonomy score for visitors" → *(TOC-0x70bc) |
| +0x50 | 0.002 | default "low attenuation" → *(TOC-0x70e0) and "visitor low attenuation" → *(TOC-0x70ec) |
| +0x54 | 0.02 | default "moderate attenuation" → *(TOC-0x70e4) and "visitor moderate attenuation" → *(TOC-0x70f0) |
| +0x58 | 0.1  | default "high attenuation" → *(TOC-0x70e8) and "visitor high attenuation" → *(TOC-0x70f4) |
| +0x5c | 10.0 | default "random selection count" → int *(TOC-0x70c8) |
| +0x60 | 25.0 | default "friendship threshold" → int *(TOC-0x726c) (sec1+0x48eb8, initial 25) |

UpdateConstants builds a local `FloatConstants`, `Load`s it from an iResFile
(resource type **'FCNS'**), then for each name calls
`Get__14FloatConstantsFPCcfb(name, default, 1)` and stores the result into the
corresponding **writable sec1 static** (all pointer targets live in sec1:
0x4aad0..0x4aae4, 0x48ea4..0x48eb8). The name pool (sec1+0x491c0) contains, in
consumption order: "min autonomy score for family", "min autonomy score for
visitors", "low attenuation", "moderate attenuation", "high attenuation",
"visitor low attenuation", "visitor moderate attenuation", "visitor high
attenuation", "min autonomy score for sitting", "random selection count",
"friendship threshold", "functional distance attenuation".

**The shipped GLOB's FCNS chunk (Global.iff inside Global.far, id 0x194)
defines ALL TWELVE autonomy names** — the FCNS record format is
`name \0 [pad] f32le \0\0` (pad byte when the name length is even; the FCNS
carries house-score and motive-decay constants in earlier records, then the
twelve autonomy records, values extracted in verify.py / curve-set.txt):

| name | FCNS value (runtime) | compiled default |
|------|----------------------|------------------|
| min autonomy score for family | **1e-7** | 0.2 |
| min autonomy score for visitors | **1e-7** | 0.05 |
| low attenuation | **0.1** | 0.002 |
| moderate attenuation | **0.3** | 0.02 |
| high attenuation | **0.6** | 0.1 |
| visitor low attenuation | **0.01** | 0.002 |
| visitor moderate attenuation | **0.02** | 0.02 |
| visitor high attenuation | **0.03** | 0.1 |
| min autonomy score for sitting | **1e-6** | 0.2 |
| random selection count | **4** | 10 |
| friendship threshold | **50** | 25 |
| functional distance attenuation | **3.0** | 3.0 |

FloatConstants::Save writes the table back to the file slot after the pass
(0x85e70 call at 0x112938), and latches a byte at r2-0x2ccb.

**Runtime values (after the boot-time UpdateConstants pass):**

* attenuation normalizer N = **1.0** (CFG+0x20, read live by TryFindBestAction)
* stratum-1 distance gate = **7.0** (CFG+0x1c)
* comparator epsilons = **+1e-7 / -1e-7** (CFG+0x24/+0x28, read live)
* family min autonomy score = **1e-7** @sec1+0x48ea4 (dead in this build — f28
  loaded at 0x109994/0x1099a0, never read); visitor **1e-7** @0x48ea8 (dead, f28)
* **min autonomy score for sitting = 1e-6** — compiled static 0.1 at
  sec1+0x48eac, overwritten by UpdateConstants with the FCNS value; the live
  winner cutoff (0x109ed0) reads *(TOC-0x70cc)
* **random selection count = 4** (int at sec1+0x48eb0 via pointer
  TOC-0x70c8; FCNS overrides the compiled 10) — the K base in the draw
* family attenuation set = **{low 0.1, moderate 0.3, high 0.6}**
  (sec1+0x4aad0/4/8); visitor set = **{0.01, 0.02, 0.03}** (0x4aadc/0/e4)
* friendship threshold = 50 (int, sec1+0x48eb8); functional distance
  attenuation = 3.0 (sec1+0x48eb4)
* MotiveConstants "CFG2" = *(TOC-0x5b2c) = 0x59a2f4 (file 0x5a3184), also a
  literal-pool const table read LIVE by GetInteractionScore/GetCurrentScore:
  **+0x0 = 0.0** (accumulator seed / zero-curve contribution), **+0x4 = 1000.0**
  (mv divisor), **+0x8 = 0.001** (effect scale), +0xc = -100.0 / +0x10 = +100.0
  (the placeholder curve point the MotiveEffects ctor installs before
  LoadFromFile), +0x14 = 176.0 etc.

## 2. person+0x5b0 — the K-law trait = person attribute slot 18

The person object carries a **SimAntics attribute array: 256 s16 slots at
person+0x58c** (slot i at +0x58c+2i):

* `__ct__8cXPerson` (unnamed function 0x1121d8..0x112750): zeroes all 256 slots
  (8 iterations × 32 `sth`, 0x112330-0x1123ac).
* `Reset__8cXPersonFUc` (0x111910): re-zeroes the array (0x111950-0x1119cc)
  but saves/restores +0x5cc, +0x5d0, +0x5d2 (slots 32/34/35).
* `ReconStream__8cXPersonFP11ReconBufferlb` streams the array with
  `Recon16` in 0x40/0x50/0x100-slot windows (0x10bd60-0x10bd94).
* `LoadInterestData__12NeighborhoodFP8cXPerson` (0xb4a80): writes the
  neighbor id to slot 31 (+0x5ca) and restores persisted slots
  {46,47,48,49,50,51,52,53, 13,14,16,20,26, 54,55} from the neighbor record
  (`GetInterestPersistentDataFields__8NeighborFv` 0xa4320 enumerates exactly
  those indices; each restored slot = neighbor field with 0..10-style bounds —
  `Reset` range-checks slots 46..52 against 10 at 0x1119f8-0x111a48).
* `GetInteractionScore` reads them as the ad "personality" values:
  mv = s16 at *(MotiveEffects+0xbc) + 2*MOTIVETAB[row0].idx + 0x58c, and the
  MotiveEffects ctor (0x9fd80, `stw r29, 0xbc(r28)` at 0x9fdcc) stores **the
  person pointer** at MotiveEffects+0xbc — so mv = **person attribute
  MOTIVETAB[row0].idx** (inverted 1000-mv when the row's invert flag is set).
  MOTIVETAB = *(TOC-0x5b30) = sec1+0x24d0 → 0x59a1e0 (file 0x5a3070), 12-byte
  rows {pad s16?, idx s32 @+4, invert s32 @+8}: row[1]=(idx 2, no inv),
  row[2]=(2, inv 1000), row[3]=(3,0), row[4]=(3,1), row[5]=(4,0), row[6]=(4,1),
  row[7]=(5,0) ... (adjacent direct/inverted pairs).

Against this array:

* **person+0x5b0 = attribute slot 18.** TryFindBestAction reads it with
  `lha r0, 0x5b0(r30)` (0x109e00) and computes
  `K = count - trunc(attr18 / 2000)` via the magic constant 0x10624dd3
  (2^39/2000; `mulhw`+`srawi 7`+sign-fix at 0x109e0c-0x109e1c), then
  `K = max(1, K)`. **2000 is a literal** (`lis r3, 0x1062; addi r3, r3, 0x4dd3`
  at 0x109dfc-0x109e04), not a CFG field. The engine never writes slot 18
  (ctor/Reset zero it; no other writer); only SimAntics-side writes
  (attribute expressions / `InterpValue__8cXObjectFssb` 0xed680, whose four
  +0x58c accesses are the generic attribute-index path) can set it. With the
  engine default 0, K = 4 always (FCNS base); K reaches 1 only if a tree
  drives the slot ≥ 8000 (and ≥ 20000 under the compiled base 10). **Slot name: UNRESOLVED engine-side**
  (not in the persisted-interest list; behavioral role: autonomy pool reducer).
* **person+0x58c = attribute slot 0** — see item 3.
* **person+0x5d4 = attribute slot 36** — see item 6.
* attr[31] (+0x5ca) = neighbor id; attr[32] (+0x5cc) = visitor flag;
  attr[34] (+0x5d0), attr[35] (+0x5d2) preserved across Reset;
  attr[58] (+0x600) = age-ish selector (1..17 → child curve set);
  attr[65] (+0x60e) = person flags word (bit30/29 gating etc.).

## 3. person+0x58c — attribute slot 0, the engagement/posture discriminator

Writers/ readers found (full displacement scan in disp-scan.txt):

* ctor + `Reset` + `Cleanup__8cXPersonFP8cXObject` (0x10862c) all WRITE 0 —
  Cleanup zeroes it right after running the current interaction's exit tree
  and calling `SetCurrentAction` with a fresh empty Interaction (0x108620-0x108e60),
  i.e. "sim disengaged".
* No engine code sets it to nonzero — it is set from SimAntics (attribute
  writes in the person trees), consistent with it being the posture/engagement
  flag the FreeSO comment maps to "sitting".
* Readers: TryFindBestAction 0x109eb4 (cutoff gate, below);
  `AskOthersToMove__8cXPersonFP6XRoute` 0x10e2a4 (attr0 != 0 → do NOT ask
  blocking sims to move); `TryGotoRoutingSlot` 0x10eb5c (attr0 == 0 → set
  route flag +0x88 = 1 and failure code 7); `TryLookTowards` 0x10fa68
  (attr0 != 0 → refuse the look-towards, return 0).

So slot 0 is the "sim is seated/engaged in a directed posture" flag: while it
is 0 the winner is accepted unconditionally; while nonzero the winner must
satisfy `score <= min-autonomy-score-for-sitting` (runtime **1e-6**, FCNS
override; see item 1).

## 4. Score sign and the WINNER DIRECTION (the big one) — resolved

**(a) The curves.** The MotiveEffects curve data is loaded in
`cXPerson::PostLoad` (0x1113b0): if s16 person+0x118→ObjSelector+0x66 != 0,
`LoadFromFile(motiveEffects@person+0xa9c, GetPrivFile(), that id)` (custom
curves); else if `0 < s16 person+0x600 < 0x12` (child) →
`LoadFromFile(motiveEffects, GetGlobFile(), 0x1f7)`; else → id **0x1f5**
(0x111584-0x1115e0). `MotiveCurveSet::LoadFromFile` (0x9f500) loads the string
set and fills entry j from string j+1 via `AddPointsFromText`
(sscanf format **"(%d;%d)"**, TOC-0x58d8 → sec1+0x49570).

The person GLOB is `Global.iff` inside `GameData/Global/Global.far` — its
resource map names **STR# 501 "InteractionScoreCurves"**,
**502 "HappyWeightCurves"**, **503 "InteractionScoreCurvesChild"**,
**504 "HappyWeightCurvesChild"**, **505 "HouseScoreCurves"**. Extracted data
(curve-set.txt; FAR offsets 0x1f61a4-area member starting 0x1d707f):

```
STR# 501 InteractionScoreCurves (10 strings; entries 0..8 use 1..9):
 1  (-100;-100) (-72;90) (100;90)
 2  (-100;-100) (-80;-30) (10;10) (-45;0)
 3  (-100;-100) (50;50) (-25;40)
 4  (-100;-100) (25;25) (-35;15)
 5  (-100;-100) (50;50) (-20;40)
 6  (-100;-100) (-50;-80)
 7  (-100;-100) (0;0) (-50;-10)
 8  (-100;-100) (50;50) (-25;40)
 9  (-100;-100) (75;75) (0;65)
STR# 503 InteractionScoreCurvesChild: identical except string 2 has a stray
     ';' between pairs ("(10;10);(-45;0)") — parses the same.
STR# 502/504 HappyWeightCurves(Child): 7 strings, y in [1,15], x in [-100,100].
STR# 505 HouseScoreCurves: house scoring, unrelated here.
```

The y values RISE with the motive value (motive -100 → y=-100; motive ≥ -72 →
y=+90 on curve 1): **curve = motive satisfaction, higher = better.** An ad
raises the motive (effect = 0.001*(row1 + mv*row2/1000), row1/row2 = the TTAB
ad halfwords, mv = attribute value), so `H' - H >= 0` for useful ads and
**score = atten*(H'-H) is POSITIVE for improvement.** (Motive floats live on
[-100, 100] — the FCNS doc text says "distance of the hunger from -100" —
and GetCurrentScore/GetInteractionScore average over the **9 fixed entries**
(motive ids {5,6,7,8,9,3,13,14,15} — table *(TOC-0x5b38) = sec1+0x450f8),
dividing by the full count 9 (denominator at MotiveEffects+4, entries with an
empty curve contributing CFG2.f0 = 0.0).)

**(b) The "sort".** `CompareScoredInteractions` (0x10a040, exact):
`delta = b.score - a.score; if (CFG.f24 <= delta) return 1; if (CFG.f28 <
delta) return -1; return 0` with eps24 = +1e-7, eps28 = -1e-7:

* b higher by ≥ 1e-7 → 1
* |delta| < 1e-7 (including exact ties) → **-1**
* b lower by ≥ 1e-7 → 0

The routine at 0x59a370 that TryFindBestAction calls (0x109df8, args base =
person+0x81c, n = count, width 0x10, compar from TOC-0x58f8 → TVector
sec1+0xd9d0 = {0x1011b0, 0x8000}) is a **heapsort** (build phase r24 =
n/2+1 → 1 with sift-down; extraction phase swapping heap[1] ↔ heap[end] with
shrinking r25; sift children at 2i/2i+1; byte-copy swap loop of w+1 = 17
bytes per swap). Faithful transpile (verify.py, group H) shows:

* with a normal ascending comparator it sorts ascending (validates the
  transpile), and
* **with the game comparator it can never swap strictly-different scores**
  (sift stops unless |parent-child| < 1e-7), so the net effect of the "sort"
  is exactly **rotate-left-by-one** for strictly-distinct scores; elements
  whose scores tie within 1e-7 may additionally permute among themselves
  (unobservable in score terms). Verified outputs include
  [1..10] → [2..10,1], [5,1,9] → [1,9,5], n=2 swap.

**(c) The draw.** After the pseudo-sort (0x109dd8-0x109ea4):

```
K = max(1, *(int*)TOC-0x70c8 - trunc(attr18/2000))     # 10 - trunc(attr18/2000)
idx = 0                          if (visitor OR elem[0].flag) AND zoning == downtown
idx = GetNextRandomNumber() % min(K, count)   otherwise
```

The r249 decode missed one branch: at 0x109e30-0x109e44, if the person is NOT
a visitor (+0x5cc == 0) **and** byte at ScoredInteraction[0]+0xc == 0, the
K-draw path is taken directly; a visitor or a first element flagged bit0
routes through the downtown check (0x109e48-0x109e6c: GetZoningType == 1 →
`li r3, 0` — winner index forced to 0). The flag = Interaction+0x38 bit0 =
**TTAB entry-flags bit7** of the candidate's entry (set by the
`__ct__11InteractionFP8cXPersonP8cXObjectii` tail at 0x9cbc0-0x9cbcc; the
person-person ctor 0x9cc50 sets it unconditionally).

**(d) Net winner direction (shipped behavior).** Since the "sort" is a
rotation, the draw pool is **not score-ranked**: `idx` selects among the
first min(K, count) entries of the rotated array = **gather-order candidates
2..K+1** (K base 4 → at most 4; with tie-group permutations). Scores matter
only through:

* the **cutoff**: after drawing, if attr[0] != 0, the winner is accepted iff
  `1e-6 >= winner.score` (0x109ed0-0x109ee4; FCNS "min autonomy score for
  sitting" = 1e-6 — matching FreeSO's sitting threshold exactly) — i.e. a
  seated/engaged sim only accepts a ~zero-score (no-op) winner, while an idle
  sim (attr0 == 0) accepts anything;
* the **downtown-visitor fast path** (idx = 0 = post-rotation element 0).

Score magnitudes with the shipped curves: ad effect = 0.001*(row1+mv*row2/1000)
is ≤ ~0.2 motive units for typical rows; the steepest curve segment is
190/28 ≈ 6.8 y-units per motive unit, so a typical improving candidate scores
≈ 0.001..0.15 after the /9 mean (attenuation shrinks it further) — i.e.
**positive and small**: improving, but rejected whenever the sim is engaged.

**There is no paradox left because there is no ranking**: the "best action"
selection in this build = *a pseudo-random pick among the first ~K advertised
candidates in object/TTAB iteration order, vetoed while engaged only when the
drawn ad would move a steep curve segment by more than the sitting threshold.*
The intended design (descending sort + "random selection count" top-K draw) is
visible in the constant names but defeated by the comparator's sign layout.

## 5. The curve evaluation law (pinned, unchanged from r249 + resource)

Per entry j (20 bytes: points* +0, spans* +4, pointCount +8, motiveId +0x10):

* GetCurrentScore: `sum += curve_j(floats[motiveId])`, floats = person+0x78c
  (SetMotive__8cXPersonFif 0x10bb50: `person+0x78c + 4*i`; GetMotive 0x10bbd0).
* GetInteractionScore: `sum += curve_j(floats[motiveId] + effect_j)` where
  `effect_j = (row0 == 0) ? row1 + row2 : row1 + mv*row2 / 1000.0`, scaled by
  **0.001** (CFG2+8), with mv = attribute[MOTIVETAB[row0].idx] (1000-mv if the
  invert flag is set), row_i = s16 at ad + 2i.
* Entries with pointCount == 0 contribute 0.0 but still count in the mean
  (divide by MotiveEffects+4 = 9).
* curve(v) = piecewise-linear, binary search FROM THE TOP while v <= x[i];
  v below x[0] → y[0]; v above x[last] → y[last]; else
  y = y[i] + (v-x[i]) * (1/(x[i+1]-x[i])) * (y[i+1]-y[i]) — all in float32.
  Built by `SetMaxPoints(fn,4)` + `AddPoint` from the "(x;y)" text pairs
  (`AddPointsFromText` 0x117920; PiecewiseFn::AddPoint 0x117b40).

## 6. person+0x5d4 — attribute slot 36, the advertisement ceiling

AppendInteractionsForAuto reads `lha r25, 0x5d4(r4)` (0x1059b4) and rejects an
entry when `attr36 < entry_weight` (0x105b60-0x105b68: signed compare, `blt`
reject) — i.e. a candidate is admitted iff `0 < entry+0x1c <= attr36`.
Writers: ctor/Reset (0); `AppCheatCallback` (0x24e470) has two blocks writing
it for EVERY house person (module+0x14 → +0x88 person list):

* 0x24eee4: `sth r27, 0x5d4(r3)` — value from the cheat argument;
* 0x24f0f4: `li r16, 0x32; sth r16, 0x5d4(r3)` — fixed 50, in the same block
  that writes person+0x5c6 = 0 (the motive-decay disable flag,
  cf. `Sim__7MotivesFv` 0x9eac8 reading +0x60e/+0x5c6-class flags) and then
  calls `CleanupPeople` (0x24f130).

So slot 36 is the per-person **autonomy advertisement ceiling** (cheat-tunable;
engine default 0 — which would admit nothing — so live sims must receive it
from the person-side trees at spawn; the tree-side writer is IFF territory).
Slot name engine-side: UNRESOLVED.

## 7. Attenuation enum — values and sources

`GetAttenuationValue__14TreeTableEntryFb` (0x155340), called from the two
Interaction ctors (0x9cba4/0x9ceb0) with `bool = (actor->visitor(+0x5cc) != 0)`
(0x9cb94-0x9cba0), result stored at Interaction+0x24 (0x9cba8):

| entry+0xc | value |
|-----------|-------|
| 0 | entry's own float at entry+0x10 |
| 1 | *(*TOC-0x57c0)+0 = **0.0** (const pool 0x59a840 → file 0x5a36d0) |
| 2/3/4 | low/moderate/high from the family set *(TOC-0x70e0/4/8) = **0.1/0.3/0.6** (bool==0) or the visitor set *(TOC-0x70ec/0/f4) = **0.01/0.02/0.03** (bool!=0); compiled defaults 0.002/0.02/0.1 are overwritten by the FCNS values at boot |
| other | *(*TOC-0x57c0))+4 = **0.02** (const pool; not FCNS-driven) |

The family/visitor slot values are FloatConstants-driven; the shipped FCNS
overrides them (table in item 1).

## 8. Idle-entry dispatch (prim id + prelude pin)

`TryElement__8cXPersonFP9StackElemP12BehaviorNode` (0x10c3b0): opcode =
`s16(StackElem+0) >> 4` (rlwinm 0x11,0xf at 0x10c3c4); range check
`op-3 <= 0x2c` (0x10c3cc-0x10c3d0); jump table *(TOC-0x5904) = sec1+0x48f78,
indexed op-3:

* **op 3 → 0x10c430 → `bl 0x109860` TryFindBestAction** (only call site)
* op 0x1e → 0x10c404 → `bl 0x10fdc0` TryGosubFoundAction (winner consumer;
  only call site)
* other entries: TryLookTowards/TryGotoRoutingSlot/TryReach/TryIdleForInput
  (0x10c438) etc.; unmapped ops → 0x10c478 epilogue.

Prelude pin (free-will gating, from my own disassembly of 0x109860):
`lwz r5, -0x7248(r2)` (0x109874; sec1+0xdb8 → **0x45f1c**, initial 1),
`lbz r0, 0(r5)` (0x109884), `cmplwi r0, 0; bne 0x109928` (0x10988c-0x1098a0)
— **free will ON (byte != 0) branches straight past the entire prelude**
(visitor/flagged-person checks, house-first-person check via *(TOC-0x778c)
→+0x10→+0x10c, autonomy-suspend byte *(TOC-0x7110)). With free will OFF the
prelude runs: visitor (+0x5cc != 0) or flags bit30/bit29 continue;
*(TOC-0x7114) deref non-zero → continue; otherwise return 0; additionally
house-first-person and *(TOC-0x7110) gates at 0x109934-0x109970.

## Corrected portable winner law (shipped Mac build)

```
FREEWILL   = u8 at sec1 0x45f1c (init 1; SetFreeWill mirrors to SimGlobal[0x1e])
SITTING_MAX= float at sec1 0x48eac (FCNS override = 1e-6; compiled static 0.1)
K_BASE     = int at sec1 0x48eb0 (FCNS override = 4; compiled static 10)
ATT        = family {low 0.1, mod 0.3, high 0.6} at sec1 0x4aad0/4/8
             visitor {low 0.01, mod 0.02, high 0.03} at sec1 0x4aadc/0/e4

score(cand) = atten * (mean_j curve_j(m_j + effect_j(cand)) - mean_j curve_j(m_j))
  curve_j   = STR#501/503 string j+1 pairs, piecewise-linear f32, top-down search
  effect_j  = 0.001 * (row1 + mv*row2/1000)   [row0==0: row1+row2]
  mv        = attr[MOTIVETAB[row0].idx] (or 1000-mv if invert)
  atten     = N / (N + dist * GetAttenuationValue(entry, isVisitor)), N = 1.0

candidates: per (usable object, advertised TTAB entry), gather order =
  lot-object strata loop, TTAB entries visited BACKWARDS; admitted iff
  0 < entry.weight <= attr[36]

PERMUTE: heapsort-with-eps comparator  ==  rotate left by one
  (exact transpile in verify.py; |Δscore| < 1e-7 groups may permute internally)

K   = max(1, K_BASE - trunc(attr[18] / 2000))
idx = 0                                       if visitor or cand2.entryFlagBit7
                                              and zoning == downtown (GetZoningType==1)
    = GetNextRandomNumber() % min(K, count)   otherwise

winner = array[idx]
accept = attr[0] != 0 ? (winner.score <= SITTING_MAX) : true
if accept: stack[4] = winner.objectID; person+0x72 = winner.actionNumber
return accept
```

## What is safe to base a C# reimplementation on

* Attribute array = 256 s16 at person+0x58c; slot 0 = engagement/posture
  ("sitting") gate of the cutoff; slot 18 = K reducer (÷2000 literal); slot 36
  = ad ceiling; slot 31 = neighbor id; slot 32 = visitor; slot 58 = age-class
  (child if 1..17); slot 65 = flags word.
* Curve source = person GLOB STR# 501 (adult) / 503 (child); the nine curve
  strings extracted above; ids {5,6,7,8,9,3,13,14,15}; mean over all 9 with
  zero contributions for empty curves; effects scaled by 0.001 with mv/1000.
* Score sign: positive = improvement; magnitudes ~0..0.15 typical.
* The shipped pseudo-sort = rotate-left-one (transpile-faithful); a port can
  reproduce it exactly with verify.py's heapsort_59a370.
* Draw: K = max(1, 4 - trunc(attr18/2000)) (FCNS count = 4); the pseudo-sort =
  rotate-left-one (heapsort transpile), so the pool is gather-order 2..K+1;
  idx = rand()%min(K,count), except idx = 0 for (visitor or elem0 entryflag
  bit7) AND downtown.
* Cutoff: engagement gate attr0, threshold 1e-6 runtime (FCNS; compiled
  static 0.1) — a seated sim only accepts a ~zero-score winner.
* All autonomy constants and their compiled defaults + FCNS persistence.

## UNRESOLVED (with reason)

* **Engine-side names for attribute slots 0 / 18 / 36** (and the full
  person-attribute name table). The behavioral roles are pinned above, the
  person tree (IFF BHAV) writers of slots 0/18/36 are not decoded.
* **Which cheat string maps to the two attr36-writing cheat blocks**
  (0x24eee4 value form; 0x24f0f4 fixed-50 + decay-flag-clear + CleanupPeople).
  The cheat string table near file 0x60c349 contains "motives" and "autonomy"
  but the fall-through dispatch chain was not resolved to a name.
* **Whether AutonomyConstantsClient::UpdateConstants runs before the first
  selection** (virtual-only dispatch). If it never ran, the compiled statics
  would apply instead of the FCNS overrides; the FCNS data + the Save pass +
  MotiveConstants' identical pattern (motive-decay constants demonstrably
  load from it) make run-at-boot near-certain.
* **Engine-side names for slots 46..52** (the 0..10 personality-like block
  Reset checks) and the remaining persistence slots 13/14/16/20/26.
* person+0x7b8 (IsSleeping input float) identity: threshold CFG+4 = 0.0, read
  semantics pinned, field name not.

## Files

* `verify.py` — SHA-256 + 64 code pins + 45 data pins + 49 fixture checks
  (heapsort transpile = rotate, comparator table, curve eval + end-to-end
  walkthrough, K law, jump table, FCNS override values, MOTIVETAB, curve-set
  extraction); writes only `verified-state.json`; ruff clean; PASS from any
  working directory.
* `sec1-unpacked.bin` — unpacked data-section image (regenerated by verify.py).
* `*.txt` — disassembly excerpts: updateconstants-autonomy, person-reset,
  person-ctor-tail, motivecurve-load (LoadFromFile + Set/GetMotive + Motives::Init),
  curveload-callers, addpointsfromtext, loadinterestdata, interestfields,
  qsort-cw (the heapsort), tryelement-pins, interaction-ctor, disp-scan,
  motives-sim, curve-set.txt (the extracted curve strings).
* `curves/` — extracted Global.far members (glob.iff = the person GLOB;
  PersonGlobals.iff, TemplatePerson.iff for provenance).
