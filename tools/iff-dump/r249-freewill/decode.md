# R249 — the native autonomous-interaction selection law: candidate gather,
# scoring, ordering, winner draw, insertion, free-will gating

All addresses are executable FILE OFFSETS (code section starts at file 0x8e90;
stored data pointers are section-relative). Container facts per r244; data
section: packed 0x6d834 bytes at file 0x5c22f0 unpack to 0x7bf80 initialized
bytes (r104 law), memory image total 0x989a4 (BSS 0x7bf80..0x989a4, zero-filled
at load — r102 header reading). Nothing outside this directory was written.

Core discovery: `TryFindBestAction__8cXPersonFP9StackElem` (0x109860, 1756
bytes) is the handler of the person "Find Best Action" primitive, dispatched
from `TryElement__8cXPersonFP9StackElemP12BehaviorNode` (0x10c3b0) via the jump
table at *(TOC-0x5904) (prim range 3..0x2f; the FindBestAction case block at
0x10c430, `bl 0x109860`). The caller's brain BHAV is IFF territory (the port
already executes it live); EVERYTHING below is the engine side of the law.

## A. The routines

| role | routine | file off |
|---|---|---|
| prim dispatch | `TryElement__8cXPerson` | 0x10c3b0 |
| candidate gather + score + sort + draw | `TryFindBestAction__8cXPersonFP9StackElem` | 0x109860 |
| per-object candidate emission | `AppendInteractionsForAuto__10ObjTestSimFRQ23std6vectorI11Interaction...` (at 0x105900, dispatched from `ObjTestSim::AppendInteractions` 0x106220 when the tester was built with autonomous=1) | 0x105900 |
| per-candidate availability test (runs the object's test BHAV) | `TestInteraction__10ObjTestSimFP11InteractionPP14TreeTableEntry` | 0x1062d0 |
| current-happiness/discontent | `GetCurrentScore__13MotiveEffectsFv` | 0x9fc20 |
| post-interaction score | `GetInteractionScore__13MotiveEffectsFP11TreeTableAd` | 0x9f9d0 |
| ordering | `qsort` (CW runtime 0x59a370, median-of-3) + comparator `CompareScoredInteractions__FPCvPCv` (0x10a040, passed via TOC slot -0x58f8 → data 0xd9d0 TVector {0x1011b0, 0x8000} — verified) | 0x59a370 / 0x10a040 |
| winner → execution (gosub) | `TryGosubFoundAction__8cXPersonFP9StackElem` (0x10fdc0) → `SetCurrentAction__8cXPersonFRC11Interaction` (0x108e60) | 0x10fdc0 |
| winner → queue (ring) | `AddAction__8cXPersonFP11Interaction` (0x10af40); removal `RemoveAction__8cXPersonFP11Interaction` (0x10ac30, unnamed in index) | 0x10af40 |
| free-will | `SetFreeWill__8cXObjectFb` 0xc9d40, `GetFreeWill__11cOptionsMgrFv` 0x23a020, `SetFreeWill__11cOptionsMgrFb` 0x239ef0 (tail-calls the cXObject one) | — |
| constants | `UpdateConstants__23AutonomyConstantsClientFv` 0x112750 (named FloatConstants), `UpdateConstants__21MotiveConstantsClientFv` 0x9ef10 | — |

## B. The candidate law

Scratch storage per run (all on the person):

* `std::vector<Interaction>` — per-object scratch, a global singleton at
  *(TOC-0x58f0) (data 0x8587c), lazily constructed once (latch byte at
  TOC-0x2cd8 = data 0x5328; ctor args from *(TOC-0x70c4)/\*(TOC-0x58f4)).
  `TryFindBestAction` CLEARS it per object (bl 0x106f10 at 0x109cd0 — the
  vector `clear` destroys the CTGString at Interaction+0x28 of each 0x3c-byte
  element and sets count 0), then `SetStackObject(tester, obj)` (0x106690) and
  `AppendInteractions` (0x106220 → 0x105900).
* `std::vector<ScoredInteraction>` on the person at +0x814/{size +0x818, base
  +0x81c} (16-byte elements; the type name `ScoredInteraction` is in the
  function record of the vector `clear` at 0x116040). Cleared at the START of
  every `TryFindBestAction` run (bl 0x116040 at 0x109978). Elements:
  `{u16 objectID (obj+0xf0), u16 actionNumber (TTAB entry+0x18), float score,
  u8 flagsBit0}` — built on the stack at r1+0x48 and pushed by
  vector-insert 0x113f20.

`Interaction` = 60 (0x3c) bytes: `{s16 +0, cXPerson* actor +4, cXObject* target
+8, s16 temps +0xa.., +0x28 CTGString name, u32 flags +0x38, ...}`. The SAME
0x3c-byte structure is the action-queue element (see D).

`AppendInteractionsForAuto` (0x105900) iterates the object's TTAB **backwards**
(entry index count-1 .. 0) and appends ONE Interaction per advertised entry:
* gate: entry+0x1c (s16, the advertise weight) must be > 0 (0x1059f8/0x105a00);
* gate: person+0x5d4 (s16) >= entry+0x1c (0x105b60/0x105b64) — a per-person
  maximum advertise weight;
* gate: TTAB entry flags word entry+0x14 vs person flags (same law as
  TestInteraction, below);
* gate: zoning (GetZoningType(hood, lot) == 1 = downtown): visitors require
  entry bit0 (LSB) set; entry bit21 blocks visitors on residential lots;
  entry bit23 always blocks autonomy; child blocks bit20; person+0x60e bit31
  requires bit18, bit30 requires bit19, plain adult blocks bit22;
* vehicle override: target class +0xc0 in {4,7} re-admits an entry that failed
  the flag gates if the family inventory holds the object's GUID token
  (GetInventoryByNeighbourID + FindToken, 0x105a34-0x105a8c);
* downtown visitors with person+0x60e bit31/30 and !person+0x5d0: only objects
  whose room (+0x84) == 0 (outside) are considered (0x105c04-0x105c18);
* person-target special case: if ObjSelector+0x12 == 2, cast the target to
  cXPerson (id 0x28); if the target's current interaction entry (+0xa08 →
  GetEntry) has flags bit30, append a person-person Interaction (ctor
  `__ct__11InteractionFP8cXPersonP8cXPerson` 0x9cc50).
* entry selector+0x12 == 7 + plain adult → no candidates at all.

**Variants/duplicates:** the native appends exactly ONE candidate per
(object, TTAB entry). There is NO variant multiplication at selection time;
"Change Action String" (opcode 50) only runs inside trees (its port-side
aggregation in `VMFindBestAction` has no native counterpart here). Multiple
physical objects legitimately produce multiple candidates.

`TestInteraction` (0x1062d0) then runs per emitted Interaction:
1. entry flags gates (as above) against actor person+0x5cc/+0x600/+0x60e;
2. target obj+0x5a bit22 → fail; autonomous tester && target obj+0x7c (s16,
   in-use counter) > 0 → fail; entry+0x14 bit0 (autonomy-allowed) must be set;
3. vehicles (class 4/7) family-token override as above;
4. **the test tree**: `bl 0x153ec0 (person, target->ObjSelector+0xc, entry
   action number, params)` at 0x106570 — runs the TARGET's tree selected by the
   TTAB action number in test mode, with the actor's temp registers copied in
   (person+0x3a..0x48 → interaction+0xa..0x18); result s16 lands at
   person+0xae; a global byte *(TOC-0x7100) forces pass;
5. cross-room fixup: if target room (+0x84) != actor room, the copied
   TreeTableEntry's ad halfwords +0x52 := -+0x50 (0x1065ac-0x1065d4).

**Collection lifecycle:** the whole candidate set is built and consumed inside
ONE `TryFindBestAction` call: cleared at entry (0x109978), appended during the
object loop, qsorted, drawn, and left in place until the next run (the next
run clears it first). There is no cross-tick persistence of losers.

## C. The scoring + ordering law

Per passing Interaction (TryFindBestAction 0x109d08-0x109db0):

```
H   = GetCurrentScore(person->MotiveEffects @person+0xa9c)      # 0x10997c
dist = CalcShortDistance(person, obj)                           # 0x109c5c/0x109cf4
atten = CFG.f20 / (CFG.f20 + dist * interaction.f24)            # 0x109d2c-0x109d44
                                                  # interaction.f24 = GetAttenuationValue
H'  = GetInteractionScore(person->MotiveEffects, entryAd)       # 0x109d48
score = atten * (H' - H)                                        # 0x109d58-0x109d5c
```

* `GetCurrentScore` (0x9fc20): mean over the person's MotiveEffects entries
  (20 bytes: {curvePoints*, reciprocalSpans*, pointCount, motiveId, ...}) of
  curve(current motive float at MotiveEffects+0xc0[motiveId]).
* `GetInteractionScore` (0x9f9d0): mean over the same entries of
  curve(motiveFloat + effect*CFG2.f8), where effect for this entry/ad-row is:
  * ad row (TreeTableAd) = 6 s16 per motive at `ad + 6*motiveId`;
    row[0] != 0 → looks up MOTIVETAB[12*row[0]] {idx@+4, invert@+8}; mv = s16
    at MotiveEffects+0xbc + idx*2 + 0x58c; if invert: mv = 1000 - mv;
    **effect = row[1] + mv*row[2]/CFG2.f4** (0x9fa44-0x9faa0);
    row[0] == 0 → **effect = row[1] + row[2]** (0x9faac-0x9fad4);
  * CFG2 = *(TOC-0x5b2c) (BSS 0x59a2f4, MotiveConstantsClient): f8 multiplies
    the effect, f4 is the 1000-ish divisor, f0 is the accumulator seed (0.0).
* curve = piecewise-linear over {x,y} pairs with precomputed reciprocal spans
  (binary search from the top, exact semantics fixture-proven in verify.py;
  below-range → y[0], above-range → y[last]).
* int→double everywhere via the CW idiom
  `double({0x43300000, v ^ 0x80000000}) - (2^52 + 2^31)` — fixture-proven.
* attenuation value = `GetAttenuationValue__14TreeTableEntryFb` (0x155340):
  entry+0xc enum: 0 → entry's own float +0x10; 1/3/4 → "low/moderate/high
  attenuation" from *(TOC-0x70e0/-0x70e4/-0x70e8) (family) or
  *(TOC-0x70ec/-0x70f0/-0x70f4) (visitor, bool arg = visitor); other →
  defaults at *(*(TOC-0x57c0))+0/+4.

**Ordering predicate** (instruction-exact, comparator 0x10a040):

```
delta = b.score - a.score                 # f1 = b.f8, f0 = a.f8, fsubs
if delta <= CFG.f24: return  1            # b sorts before a
if delta <  CFG.f28: return -1            # a sorts before b
return 0
```

CFG = *(TOC-0x58e8) (BSS 0x59a524, AutonomyConstants object). CFG.f24/f28 are
its two comparator epsilons; no writer was found (see UNRESOLVED), so the
zero-initialized defaults 0.0/0.0 are the operative semantics: the comparator
returns 1 iff b.score <= a.score, else 0 — i.e. a (possibly unstable but
net-) ASCENDING sort by score, best (most negative) first. `qsort`
(0x59a370, median-of-3 quicksort) is called at 0x109df8 with
(base=person+0x81c, n=size, width=0x10, compar from TOC-0x58f8).

**Engine tie-break:** after sorting, the winner index is drawn UNIFORMLY:

```
K  = max(1, CFG_int - trunc(s16(person+0x5b0) / 2000))
    # CFG_int = "random selection count" (TOC-0x70c8, initialized-data 10);
    # 0x10624dd3 mulhw + srawi 7 + sign-fix = trunc(t/2000)  (0x109e00-0x109e24)
idx = GetNextRandomNumber() % min(K, count)     # 0x109e70-0x109ea4
```

so the engine's role is: aggregate all (object, entry) candidates, attach
`atten*(H'-H)`, sort ascending, and draw uniformly from the top-K head. The
brain BHAV (IFF) supplies only the advertisement rows via the TTAB; it does
not order candidates.

Winner acceptance (0x109eb4-0x109ee8): with g = s16 person+0x58c:
* g == 0 → accept unconditionally;
* g != 0 → accept iff `0.1 >= winner.score` where 0.1 =
  "min autonomy score for sitting" (TOC-0x70cc). Instruction-exact polarity:
  `lfs f0, 0(r3)` (0.1), `lfs f1, 8(r5)` (score), `fcmpu f0, f1`, `bge accept`
  — i.e. candidates whose |score| exceeds the constant are REJECTED. Under the
  happiness reading of H this would reject every improving action, so the
  coherent semantics is score-as-negative-improvement (discontent delta);
  see UNRESOLVED for the honest caveat.
* "min autonomy score for family" (0.2, TOC-0x70c0) and "...for visitors"
  (0.05, TOC-0x70bc) are loaded into f28 at 0x109994/0x1099a0 but never read
  afterwards — dead in this build.

On success: `person+0x72 (u16) = winner.actionNumber; stack+4 (s16) =
winner.objectID; return 1` (0x109ef0-0x109f10). On failure: return 0.

## D. The winner-insertion law

Two engine paths consume the winner:

1. **Gosub (autonomous path):** `TryGosubFoundAction__8cXPersonFP9StackElem`
   (0x10fdc0) — takes stack+4 (= objectID, 1-based index into the module
   object array person+0xdc→+0x20), finds the object's TTAB entry whose
   entry+0x18 == person+0x72, builds `Interaction(person, obj, entry.f2, 0)`
   (ctor 0x9ca90 at 0x10ff20), `SetStackVars`, then
   `SetCurrentAction__8cXPersonFRC11Interaction` (0x108e60) — the found action
   becomes the CURRENT action (gosub), not a queued one. Returns 3 on success.
2. **Queue:** `AddAction__8cXPersonFP11Interaction` (0x10af40) re-tests the
   interaction (fresh ObjTestSim + TestInteraction when entry+0x14 bit28 set),
   then inserts into the 8-slot ring at person+0x820 (0x3c-byte Interaction
   slots, head +0xa00, tail +0xa04; `GetIndAction__8cXPersonFi` 0x10abc0:
   `person+0x820 + ((head+i) & 7)*0x3c`). `RemoveAction` (0x10ac30) compacts
   the ring and runs the removed action's exit tree
   (GetTreeID(ObjFnTable, ObjEntryPoint=4) → TreeSim run).

Flags: Interaction+0x38 bit3 = "TestInteraction passed" (set 0x1065e8-0x1065f4,
required by TryFindBestAction 0x109d20), bit4 = "test tree ran"
(person+0xae != 0); the autonomous marker carried into ScoredInteraction+0xc
is flags bit0 of the Interaction. Losers: DISCARDED — nothing but the drawn
winner leaves the routine; the ScoredInteraction vector simply persists until
the next run's clear.

## E. Free-will gating

* The toggle is ONE global byte at data 0x45f1c, initialized to 1 (enabled).
  `GetFreeWill__11cOptionsMgrFv` (0x23a020) = `return *(u8*)0x45f1c`.
  `SetFreeWill__8cXObjectFb` (0xc9d40) writes the byte AND mirrors it into
  SimAntics: `cSimulator::SetGlobal(0x1e, (s16)v)` (0xc9d74) — **global 30
  (0x1e) is the BHAV-visible free-will flag** (SetGlobal law per r168:
  stores at simulator+0x10 + 2*index). `SetFreeWill__11cOptionsMgrFb` (0x239ef0)
  tail-calls the cXObject version; the options UI has no separate storage.
* Engine-side read in selection: `TryFindBestAction` opens with
  `lwz r5, -0x7248(r2); lbz r0, 0(r5)` (0x109874-0x109884). If the byte is
  set, the ENTIRE gating prelude (person+0x5cc visitor check, person+0x60e
  bits 29/30, house-first-person check at *(TOC-0x778c)→+0x10→+0x10c, and the
  *(TOC-0x7110) autonomy-suspended byte) is SKIPPED — with free will ON,
  selection runs unconditionally. With free will OFF the prelude applies:
  visitors (person+0x5cc != 0) and flagged persons (person+0x60e bit30/bit29)
  still select; a plain controlled family member returns 0 immediately
  (0x1098a4-0x1098d8), additionally suppressed when *(TOC-0x7114) derefs
  non-zero (0x1098c4-0x1098d0) or the *(TOC-0x7110) byte (BSS 0x92664, default
  0 — also read by the menu-count function at 0x105f60) is set (0x10995c).
* Per-candidate, free will also gates via entry flags (bit0 = autonomy allowed,
  bit23 = never autonomous) in AppendInteractionsForAuto/TestInteraction.
* Idle scheduling: `Simulate__8cXPersonFl` (0x10bff0) ticks motives
  (Sim__7MotivesFv gated on person+0x5c6/+0x8e and a 10-tick cadence), decays
  motive-floats from person+0xa84/+0xa88 entries, maintains +0xaac room
  lighting — it does NOT call selection. Selection is reached only as the
  "Find Best Action" PRIM inside the brain BHAV; the winner hand-off
  (stack+4, person+0x72) is consumed by the sibling prim
  TryGosubFoundAction. `TryIdleForInput` (0x10a0c0) is the separate
  idle-for-input prim (calls TestInteraction at 0x10a2b0 while idling).

## F. Cross-check vs FreeSO `VMFindBestAction.cs`

(FreeSO/TSOClient/tso.simantics/Primitives/VMFindBestAction.cs — read in full;
only this file was audited.)

| aspect | native (decoded) | FreeSO | observable divergence? |
|---|---|---|---|
| candidate source | every object's TTAB advertised entries (weight>0, ≤person+0x5d4), one candidate per (object, entry) | `ObjectQueries.WithAutonomy` per TreeTable.AutoInteractions entry; variants from CheckAction all added | near-equivalent; FreeSO adds string variants as extra candidates — native has none at selection time |
| score | `atten*(H'-H)`, H/H' = MEAN over the person's MotiveEffects curves (incl. motive-value 1000-inversion via MOTIVETAB row) | sum over advertised motives only of `curve(m + max*personalityMul/1000) - curve(m)`, weights uniform 1/9 | divergent in general: different curve-set averaging, no 1000-inversion path, personality multiplier (native: none found in the engine scoring — personality enters via TTAB row[2] mv-table only) |
| attenuation | `CFG.f20 / (CFG.f20 + dist*entryAtt)`, entryAtt enum 0/1/3/4 family-vs-visitor sets | `score / (1 + atten*distance)`, TTAB.AttenuationValues table | same algebraic shape (with N=CFG.f20); different constant source |
| cutoff | winner accepted iff `0.1 >= score` (only when person+0x58c != 0); 0.2/0.05 constants dead in this build | `score > 1e-6` (sitting) / `1e-7` else, applied per candidate | YES — different constants AND opposite polarity; FreeSO rejects negative scores, native accepts them (and rejects large positive ones) |
| ordering | qsort ascending by score (epsilons 0.0) | OrderByDescending | inverse sign convention of the same order — consistent only if score signs are flipped |
| winner draw | `rand() % min(K, count)`, `K = max(1, 10 - trunc(person+0x5b0/2000))` — UNIFORM over top-K | top-4 pool; weighted-by-score random, or deterministic first if `!Entry.AutoFirst` | YES — pool size law (4 vs 10-trait/2000), weighting (score-proportional vs uniform), and the AutoFirst short-circuit have no native counterpart in this routine |
| queueing | SetCurrentAction (gosub) with objectID@stack+4 + actionNumber@person+0x72 | EnqueueAction with Autonomous priority onto thread queue | different mechanism; the port's brainlive execution already mirrors the native prim pair |
| gating | byte 0x45f1c (+ SimAntics global 0x1e mirror); OFF still allows visitors/flagged | `VM.FreeWillEnabled` skipped for visitors/pets | close in spirit; native's off-path additionally consults *(TOC-0x7114) and *(TOC-0x7110) |

**Verdict:** the port's winner differs from the native's in observable cases:
(1) any candidate set where a negative-score (discontent-improving under the
native sign convention) candidate exists — FreeSO drops it at the 1e-6/1e-7
cutoff while the native accepts and can draw it; (2) sets with >4 viable
candidates — the native can draw from positions 5..K (K up to 10); (3) any set
where score-proportional weighting changes the draw; (4) sets exercising
AutoFirst. The K law also makes native selection fully deterministic
(K=1) when person+0x5b0 >= 20000, which FreeSO never is.

## Portable pseudocode (native selection law)

```
FREEWILL = u8 at data 0x45f1c                        # default 1; SetFreeWill
                                                     #   mirrors to SimGlobal[0x1e]
def find_best_action(person, stack):
    if not FREEWILL:
        if person.visitor_5cc == 0 and (person.f60e & 6) == 0: return False
        if person.f60e has bit30/bit29 and *(_toc_7114): return False
        if *(_toc_7110): return False
        if person is housePerson0 and chain_10c == 0: return False
    H = mean(curve(motives))                          # GetCurrentScore
    cands = []                                        # person+0x814 vector (cleared)
    for obj in lot_objects:                           # per-tick strata below
        if not usable(obj): continue                 # TTAB present, flags bit12,
                                                     #  in_use(obj+0x7c) <= 0, ...
        stratum = (lotWord + person.id) & 3          # r18, forced 0 off-residential
        if stratum == 2 and not obj.id & 1: continue
        if stratum == 1 and CalcShortDistance(person, obj) > CFG.f1c: continue
        if stratum >= 3 and obj.id & 1: continue
        tester = ObjTestSim(person, autonomous=True)
        for entry in reverse(obj.ttab.entries):
            if entry.weight <= 0 or entry.weight > person.f5d4: continue
            if entry_flags_block(entry, person): continue
            cands0 = [Interaction(person, obj, entry.actionNumber)]
            for it in cands0:
                ok, entryCopy = TestInteraction(tester, it)   # runs test tree
                if not ok: continue
                dist  = CalcShortDistance(person, obj)
                atten = CFG.f20 / (CFG.f20 + dist * it.attenuation)
                Hp   = GetInteractionScore(person.motiveEffects, entry.ad)
                cands.append(ScoredInteraction(obj.id, entry.actionNumber,
                                               atten * (Hp - H)))
    n = len(cands)
    if n == 0: return False
    qsort(cands, CompareScoredInteractions)           # ascending, eps CFG.f24/f28
    K   = max(1, RANDOM_SELECTION_COUNT - trunc(person.f5b0 / 2000))
    idx = randint() % min(K, n)
    w = cands[idx]
    if person.f58c != 0 and w.score > SITTING_MIN_SCORE:   # 0.1
        return False
    stack[4] = w.objectID                             # s16
    person.f72 = w.actionNumber                       # u16
    return True                                       # BHAV then calls
                                                      #   GosubFoundAction ->
                                                      #   SetCurrentAction
```

## What is safe to base a C# reimplementation on

Instruction-proven and fixture-backed (verify.py: 80 code pins, 12 fixture
groups, PASS):

* Candidate element: `ScoredInteraction {u16 objId, u16 actionNumber, float
  score, u8 flag}` in a person vector (+0x814/+0x818/+0x81c), cleared at the
  start of every run.
* One candidate per (object, advertised TTAB entry); weight gates
  entry+0x1c in (0, person+0x5d4]; entry flag law vs person+0x5cc/0x600/0x60e;
  vehicle token override; TestInteraction runs the target's tree at the entry's
  action number with the actor's temps; person+0xae = tree result.
* Score = atten*(H' - H); atten = N/(N + dist*entryAtt) with
  GetAttenuationValue enum 0/1/3/4 + family/visitor sets; H/H' = mean of
  piecewise-linear curve evaluations (reciprocal-span interpolation, below→
  y[0], above→y[last]); ad effect = row1 + (inverted mv)*row2/CFG2.f4, or
  row1+row2 when row0==0, scaled by CFG2.f8.
* Ordering: qsort + CompareScoredInteractions — ascending under the
  zero-initialized epsilons; comparator returns 1 iff b<=a else (fuzzy) -1/0.
* Draw: idx = rand() % min(max(1, 10 - trunc(f5b0/2000)), count); uniform.
* Winner cutoff: accept iff f58c == 0 or 0.1 >= score.
* Hand-off: stack+4 = objectID, person+0x72 = actionNumber; GosubFoundAction
  re-finds (object, entry) and SetCurrentAction's the Interaction; queue path
  = 8-slot Interaction ring at person+0x820 via AddAction.
* Free will: byte 0x45f1c (init 1) + SimAntics global 0x1e mirror; OptionsMgr
  delegates to cXObject::SetFreeWill.
* Named tuning constants (initialized-data defaults, FloatConstants-override
  at runtime): family 0.2 (dead here), visitors 0.05 (dead here),
  sitting 0.1 (live winner cutoff), random selection count 10 (live K base).

## UNRESOLVED (with reason)

* **CFG object runtime values** *(TOC-0x58e8) = BSS 0x59a524*: fields +0x1c
  (max-stratum-1 distance), +0x20 (attenuation normalizer N), +0x24/+0x28
  (comparator epsilons), +0x4 (score sentinel / IsSleeping threshold), and the
  defaults region +0x48..0x60 read by UpdateConstants. AutonomyConstantsClient
  has only UpdateConstants in the function records; no ctor/__sinit writer of
  these fields was found (all 14 TOC-slot readers inspected; none stores).
  The epsilons only shuffle near-equal candidates; the N normalizer and the
  distance gate change scores numerically but not the law's shape. A reimpl
  should treat them as named tuning constants (r225 pattern) and pin the
  epsilons to 0.0 (BSS default) until the writer is found.
* **Score sign semantics**: instruction-exact acceptance is
  `0.1 >= atten*(H'-H)`. Whether H is happiness (FreeSO's reading, which would
  make the native reject improving actions — absurd) or discontent (self-
  consistent with ascending sort + cutoff) is argued, not proven; the curve
  data direction (MotiveCurveSet in game data) settles it and was not decoded
  here (IFF-side).
* **person+0x5b0 scale** (the K divisor operand): s16, divided by 2000 — on a
  0..10 personality scale K is constant 10; on a 0..10000 motive-style scale K
  spans 5..10. Field identity not proven from the engine side.
* **person+0x58c** field identity (gates the 0.1 cutoff; FreeSO's comment
  maps its analogue to posture/sitting) — engine side shows only the gate test.
* **Idle-entry dispatch site**: the native call site that runs the brain tree
  when the stack empties (TreeSim/cSimulator side) was not located; the port's
  brainlive trace already proves the tree identity, so this only leaves the
  native entry-point address unpinned.
* **TryIdleForInput internals** (0x10a0c0) and **CanChooseAutonomously**
  (0xc3100) deeper structure: excerpted but not fully decoded; the main
  selection law does not depend on them (TryFindBestAction never calls
  CanChooseAutonomously).

## Files

* `verify.py` — 80 code pins + data pins + 12 fixture groups; PASS; ruff clean;
  writes only `verified-state.json`.
* `verified-state.json` — its output.
* `tryfindbestaction.txt`, `comparator.txt`, `append-autonomous.txt`,
  `testinteraction.txt`, `getinteractionscore.txt`, `getcurrentscore.txt`,
  `getattenuationvalue.txt`, `m106220.txt`, `m106f10.txt`, `m113f20.txt`,
  `m116040.txt`, `qsort-cand.txt` — candidate/scoring/ordering evidence.
* `unnamed-insert.txt` (GetIndAction + RemoveAction + AddAction call),
  `addaction.txt`, `trygosubfoundaction.txt`, `tryelement.txt` — winner
  insertion and prim dispatch.
* `setfreewill-xobj.txt`, `setfreewill-optmgr.txt`, `getfreewill-optmgr.txt`,
  `simulate-person.txt`, `tryidleforinput.txt`, `menu-count-autodis.txt`,
  `canchooseautonomously.txt`, `cfg-writer-cand.txt`,
  `objtestsim-helpers.txt`, `objtestsim-ctor2.txt` — gating and support.
