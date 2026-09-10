# R153 — THE ALL-10 SENTINEL: VERDICT = IT NEVER EXISTED (gate-polarity misread)

Round target: the R152-disclosed residual / r151 decode §6.2 — "the ctor and
Reset write ZEROS, so who ever produces the all-10 state that arms
RandomizeAllInterests?" Binary: `game-data/The Sims/The Sims Complete`
(PPC PEF). All addresses are FILE offsets. Baseline: main 0095094 / FreeSO
ed755a79, green via r152-gate-run1.log (93/93, clean exit).

## VERDICT UP FRONT

**Byte-verified NEGATIVE: no code writer and no shipped datum produces the
all-10 state — because R151 misread the gate's branch polarity. The gate is
"all eight words 46..53 <= 10", and the ctor/Reset ZERO SWEEP itself arms
the randomizer.** Two independent agents (binary + data) plus this round's
own re-dump of the raw words agree; the script corpus independently carries
the SAME <=10 gate (PersonGlobals BHAV 9761).

The polarity proof (re-verified by hand this round, not from the agent dump):

```
Initialize gate @0x111fa0-0x112014:
0x111fa4: 38600000  li r3, 0        (rA=0 -> li, NOT a register move)
0x111fa8: 2c00000a  cmpwi r0, r0,10 (r0 = lha word 46)
0x111fac: 4181005c  bgt +0x5c       <- FAIL when word > 10  (x8, +0x5e8..+0x5f6)
0x112004: 38600001  li r3, 1        <- all eight <= 10
0x112008: 5460063f  rlwinm. r0,r3,0,24,31
0x11200c: 4182000c  beq +0xc        <- flag == 0 -> skip
0x112014: 4bfff63d  bl 0x111650     (target re-computed = 0x111650 exact)
Reset gate 1 @0x1119f8: same cascade (3bc00000 li r30,0; bgt x8).
```

`0x4181` is **bgt** (BO=12, BI=1, CR0.GT). R151 printed these "bne(1)" and
read them as "!= 10 fails", constructing an "all-10 sentinel" that no writer
ever wrote. The corrected reading also dissolves the need for one: a
freshly-constructed person (words swept to 0) passes the gate, so the
engine's canonical creation is zeros -> randomize x100 — no sentinel state
required, ever.

## 1. THE FULL CORRECTED LAW (binary agent + verification)

* **Gate** (Reset gate 1 @0x1119f8, gate 2 @0x111b34; Initialize
  @0x111fa0-0x112014; function starts: Reset 0x111910, **Initialize
  0x111cd0** — 0x112014 is the bl site): pass iff words 46..53 ALL <= 10.
  Any x100-scale value (>10) fails => interests preserved.
* **Reset's true structure** (corrects r151 §2.1): save 3 words -> ZERO
  sweep -> gate 1 (post-sweep, vacuously passes) -> **LoadPersistentData
  @0xb4bc0** (NEW, called @0x111a84 between the gates — the live record
  loader r151 missed) -> personality/aging -> gate 2 (sees the
  RECORD-LOADED values) -> RandomizeAllInterests iff both gates pass. The
  LoadInterestData call @0x111bdc inside Reset is STATICALLY DEAD (r30
  always 1 post-gate-1) — a compiler-preserved arm.
* **Creation/load order** (all bl-verified):
  - CAS: MakeNewOutOfWorldObject 0xe7040 -> ConstructObject 0xe7410 ->
    cXPerson ctor 0x112200 (zero sweep) -> Initialize -> gate sees ZEROS ->
    x100 randomize. The ONLY interest writer on the CAS path.
  - Character file: boot LoadAllObjects -> same as CAS (master); lot
    instantiation via TryCreateObject 0xf5920 -> ctor/Initialize (x100) ->
    LoadInterestData 0xf61cc (NBR record values overwrite, raw) -> the
    person's init tree LAST (raw 46..55 + Style=5). Net for a tree sim: raw.
  - House save (DoStream__12ObjectModule 0xe7c70): Initialize THEN
    ReconStream (+132) -> saved words win at ANY scale (the gate never sees
    them). This is why shipped saves keep raw tree values indefinitely.
  - Matter transfer (DoStream__14cXObjectStream 0xfbe90): ReconStream THEN
    Reset -> sweep + record restore + gate-2 re-randomize when the record is
    all-<=10 (raw) — the ONE original path that converts raw -> x100
    re-roll. Quirky, byte-pinned; the port has no matter-transfer flow (N/A,
    disclosed).
* **Save side**: SavePersistentData 0xb4800 / wrapper 0xb46c0 = verbatim
  halfword mirror person->record (Neighbor+0x74) over 39 fields incl. ALL
  15 panel topics; version stub 0xa42e0 = `return 9`. No defaults, no 10s.
* **Store inventory closed**: 10 writer families total (2 zero sweeps,
  randomizer x100, Recon16 restore + old-version zeroing, 3 sthx record
  walkers [LoadInterestData / LoadPersistentData / ChangeSimBaseType], VM
  tree writes, 1 teardown zero). The ONLY `li 10` feeding a person store
  (0x111750) passes mulli-100 -> stores 1000.

## 2. THE DATA SIDE (data agent; all files under r153/)

* **NBRS layout DECODED** (closes r151 residual #4): record = {Unknown1,
  NVersion (4->80-word pd block, 10->88), name, PersonMode (0/5/9), **pd
  shorts = person words i**, id/GUID, relationships}. The record's pd block
  IS the word-indexed mirror (record word i -> +0x58c+2i); the (index,value)
  pairs exist only in the engine's in-memory field-list walk.
* **Census (9 NBRS chunks, 203 real records): ZERO have words 46..53 all
  == 10.** 22 records touch 46..55 at all — all userNNNNN sims with raw
  0..10 tree rolls; NPC records carry zeros (or fixed x100 for TemplateNPCs,
  e.g. PD[46]=700). The r151 "18 halfwords" sample = pd words 2..19 of the
  maid record (id 11): personality 6,8,1,6,10,10 + words 9..19; the maid
  ships Exercise=5/Food=7/Parties=6 (words 13/14/16) with base interests 0.
* **Character files**: no stored person block (OBJD structural only; BHAV
  4097/4098 wrap 4100; 4100's only literal person write = PD[20]=5).
* **PersonGlobals.iff BHAV 9761 'Convert Interests to 0-1000'**: gates all
  eight words 46..53 **<= 10** (operator 15), then `*= 100` all 15 topics —
  the missing SCRIPT-side raw->x100 bridge and independent corroboration of
  the binary gate polarity. BHAV 39200 'Add Hot Date Interests' top-ups
  words 13/14/16/20/26. Neither tree's CALLER is traced (residual).
* Corpus-wide literal-10 writes to 46..55: **0**. The 8x0x000A pattern
  exists only in a font file and PCM WAVs.

## 3. THE PORT (this round's changes)

* `FreeSO .../Utils/VMInterestRandomizer.cs`: `IsUnsetSentinel` (== 10,
  the dissolved misread) RENAMED + CORRECTED to `NeedsRandomize` — the
  decoded gate (words 46..53 all <= 10; pass = interests not yet in the
  x100 dialect). Class comment rewritten with the r153 law + cites.
* `FreeSO .../Utils/VMTS1Activator.cs`: the R152 `else if IsUnsetSentinel
  -> Apply` hook REMOVED — that state has no writer (byte-proven), and with
  the corrected predicate it would re-randomize tree-written sims every
  session, diverging from the original's load order (the gate runs at
  CREATION, before any record/tree writes; the house-load path restores
  saved words AFTER Initialize). The R150 IsZeroed safety net stays.
  Behavior-neutral by construction: the hook could never fire (no writer
  produces the state; runtime path uses VMTS1ActivatorNew which never had
  it). Targeted run confirms: avatars still dialect=raw, all value pins
  unchanged.
* `Client .../AutotestRunner.cs` (uiexprand): the gate probe is CORRECTED
  and STRENGTHENED from 2 cases to 5 — all-10 -> true, one word 7 (still
  raw-scale) -> TRUE (this case PINS the polarity: false would mean ==10),
  one word 11 (x100 present) -> false, all zeros -> true, one word 500 ->
  false. The old "one broken -> false" pin encoded the misread law and is
  superseded per the SOP's correct-when-disproven rule. Stale comments at
  the uiexpint header, dispatch site, and the panel doc fixed; the post-
  Apply assertion becomes "the gate no longer passes" (deterministic:
  counters {4,3,3} force >= 6 words >= 400).

## 4. Why the generator needs NO gate call

`SimitoneNeighbourGenerator.MakePersonData` builds pd from zeros — the
creation-mirror of the original's ctor-sweep -> gate-passes -> randomize.
The unconditional `ApplyTo` there IS the faithful net effect; a literal
gate call would be vacuous.

## 5. Evidence files (tools/iff-dump/r153/)

Binary: r153-binary-decode.md, r153-symbols.txt/.json (14,578 recovered
function symbols — reusable), r153-caller-graph.py/.txt,
r153-virtual-callers.py/.txt, r153-xref.py, r153-store-inventory.py/.txt,
r153-sentinel-hunt.txt, r153-symbol-census.txt, r153-dis-{initialize,
postload,reconstream,save-persistent,getpersistentfields}.txt.
Data: r153-data-survey.md, r153-nbrs-scan.py + r153-nbrs-dump.txt,
r153-charscan.py + r153-chars-dump.txt, r153-gamedata-scan.py +
r153-gamedata-dump.txt. Runs: r153-targeted-run1.log (10/10 PASS,
gate probe all-5-True), r153-gate-run1.log (full suite — see PARITY).

## 6. Residuals (honest)

1. **BHAV 9761 / 39200 callers untraced** — WHEN the original runs the
   script-side x100 conversion (move-in? first instantiation? never for
   tree sims?) is INTERP. The port's R152 dual-dialect bridge makes the
   question display-neutral; porting the conversion into the load path
   would be INTERP-driven and was deliberately NOT done.
2. Which DoStream the Save/Load-House UI drives (ObjectModule vs
   cXObjectStream) is INTERP — matters only for raw-scale sets on the
   matter-transfer path (unported, N/A).
3. TryCreateObject's helper 0x591230 not fully named (INTERP).
4. The matter-transfer gate-2 re-randomize (raw -> x100 re-roll) has no
   port counterpart (no such flow); disclosed.
5. VMTS1ActivatorNew (the real runtime load path) has NO interest hook —
   fine for shipped saves + generator-created sims (all carry values);
   flagged as a follow-up only if a zero-words avatar source ever appears.
6. Runtime frequencies of gate-2 re-randomization are not statically
   determinable.
