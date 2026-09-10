# R153 — WHO WRITES THE ALL-10 INTEREST SENTINEL? (binary decode)

Round target: close the R151 residual #2 — "the ctor and Reset write ZEROS, so
who ever produces the all-10 state that arms RandomizeAllInterests?" Binary:
`game-data/The Sims/The Sims Complete` (PPC PEF, Complete Collection). All
addresses are FILE offsets into sec0 (raw code, file 0x8E90..0x5C22E8).

## VERDICT UP FRONT

**Byte-verified NEGATIVE: no code writer of an all-10 interest sentinel
exists — and none is needed, because R151 misread the gate's branch polarity.
The gate that arms `RandomizeAllInterests` is "all eight words 46..53 are
<= 10", and it is armed by the ZERO sweep itself.** The 10s R151 saw as a
sentinel are only ever DATA (NBR records / save streams), never code-written.

The polarity proof (raw words, Reset gate 1 / Initialize gate):

```
Initialize @0x111fa0-0x112014 (clean copy):
0x111fa4: 38600000  li    r3, 0            <- flag = 0 (rA field = 0!)
0x111fa8: 2c00000a  cmpwi r0, r0, 10       <- r0 = lha r0, 0x5e8(r30) (word 46)
0x111fac: 4181005c  bgt   0x112008         <- BO=12 BI=1 = CR0.GT: word > 10 -> FAIL
  ... eight words, +0x5e8..+0x5f6, all bgt-on->10 ...
0x112004: 38600001  li    r3, 1            <- all eight <= 10 -> flag = 1
0x112008: 5460063f  rlwinm. r0, r3, 0,24,31
0x11200c: 4182000c  beq   0x112018         <- flag == 0 -> skip
0x112014: 4bfff63d  bl    0x111650         <- RandomizeAllInterests
```

`0x4181xxxx` is **bgt** (the classic PPC encodings: 0x4182=beq, 0x4082=bne,
0x4181=bgt). The cascade continues while each word is <= 10 — NOT "== 10".
R151 printed these as "bne(1)" and read them as != 10. Two independent
cross-checks pin the polarity: (1) the bucket re-roll loop uses
`0x4081` (branch-if-GT-false) for "bucket empty" — the mirror encoding;
(2) the same `li rX,0 / bgt` shape appears in both gates and both compile
units. Also, R151's reading of the flag init as `r3 = word46's value` was an
artifact: `3bc00000`/`38600000` have **rA field = 0** → they are `li r30,0`
/ `li r3,0`, not register moves.

Consequences:
* A freshly-constructed or Reset person (words swept to 0) PASSES the gate →
  `RandomizeAllInterests` runs. The engine's canonical init is: zeros →
  gate passes → ×100 randomization. No sentinel state is ever required.
* A person whose gate words contain any value > 10 (i.e. any ×100-scale
  interest, which are the only values the engine itself writes) FAILS the
  gate → interests are preserved.
* Tree-written raw values (0..10, all <= 10) PASS the gate → would be
  re-randomized IF Reset/Initialize ran after the tree (see §1 order map:
  on the creation paths it never does; on the matter-transfer load path it
  can — §4).

## 0. Tools built this round (all in tools/iff-dump/r153/)

* `r153-pef-symbols.py` → `r153-symbols.txt/.json` — 14,578 function symbols
  recovered from the CodeWarrior traceback records embedded in sec0
  (`<L> '.'name <NUL>` trailing each function; ctor records use a '..'
  prefix and may lack the NUL). Anchors verified: RandomizeAllInterests
  0x111650, Reset 0x111910, Initialize **0x111cd0** (R151's "@0x112014" is
  the bl site), cXPerson ctor **0x112200**, LoadInterestData 0x0b4a80,
  ChangeSimBaseType **0x0a6340** (R151's 0x0a6410 is the sthx site),
  GetNextRandomNumber 0x1411d0, SetInterest 0x405b10.
* `r153-caller-graph.py` → `r153-caller-graph.txt` — direct b/bl xrefs for
  the six targets + TOC/data slot resolution.
* `r153-virtual-callers.py` → `r153-virtual-callers.txt` — `lwz r12,K(r12)` +
  glue-call census (108 sites load vtable slot +72 / 119 slot +68 of *some*
  class; receiver typing is manual — offsets are class-agnostic).
* `r153-xref.py` — generic direct-call xref CLI.
* `r153-store-inventory.py` → `r153-store-inventory.txt`,
  `r153-sentinel-hunt.txt` — task 2/3 scans.
* Disassemblies: `r153-dis-initialize.txt`, `r153-dis-postload.txt`,
  `r153-dis-reconstream.txt`, `r153-dis-save-persistent.txt`,
  `r153-dis-getpersistentfields.txt`.
* `r153-symbol-census.txt` — 682 Person/Interest/CAS/Template/... symbols.

PEF layout re-verified from the container header: sec0 = Code (raw,
total=unpacked=container=0x5b9458 @0x8e90), sec1 = Data PID (packed,
unpacked 0x7bf80 = r104/sec1-unpacked.bin), sec2 = Debug @0x80. The
"cXPerson vtable" lives in sec1 image: vptr value **0x48ee0** (ctor does
`lwz r0,-28892(r2); stw r0,40(r30)`; TOC[0x0f24]=0x48ee0). Verified slots:
Initialize **+68**, Reset **+72**, PostLoad +76, PreSave +80,
ReconStream **+132** (byte-checked against DoStream's calls). Virtual-call
glue @0x5a29e0: `lwz r0,0(r12); mtctr r0; lwz r2,4(r12); bctr`.

## 1. TASK 1 — CALLER GRAPH AND CREATION/LOAD ORDER

### 1.1 Direct callers (whole-sec0 b/bl scan, r153-caller-graph.txt)

| target | direct callers |
|---|---|
| RandomizeAllInterests 0x111650 | `bl` from Reset@0x111bc8, Initialize@0x112014. No tail-jumps, no TOC slot. (R151's "exactly two" confirmed.) |
| Reset 0x111910 | none direct — virtual only (vtable+72) |
| Initialize 0x111cd0 | none direct — virtual only (vtable+68) |
| LoadInterestData 0x0b4a80 | `bl` from ChangeSimBaseType@0x0a6f28, TryCreateObject@0x0f61cc, Reset@0x111bdc (STATICALLY DEAD — see §1.4) |
| ChangeSimBaseType 0x0a6340 | `bl` from AnyoneToDog/Cat/Kid/Adult (0x0a7108/0x0a7288/0x0a7408/0x0a7588) |
| GetNextRandomNumber 0x1411d0 | 45 sites (EditPerson::ChooseProxy, terrain, TryRandom, TryMakeNewCharacter, Censor, timer msgs) |

### 1.2 The creation/load machinery (all byte-pinned)

* `ConstructObject__12ObjectModule` @0x0e7410: dispatches on ObjSelector
  +0x12/+0x14: **type==2 → cXObject (396 B, ctor 0xcab50); type!=2 with
  +0x14!=0 → cXPerson (3012 B, ctor 0x112200 @0x0e74b4)**; +0x14==0 →
  cXPortal/cXMTObject. Just news + ctors, nothing else.
* `MakeNewOutOfWorldObject__12ObjectModule` @0x0e7040: ConstructObject →
  AddObject → **Initialize (vtable+68, sites 0x0e7164/0x0e7300)** — and on
  the second sub-path also **Reset(obj, 1) (vtable+72, site 0x0e7320)**.
* `TryCreateObject__8cXObject` @0x0f5920 (the VM Create-Object-Instance
  primitive): `bl MakeNewOutOfWorldObject` @0x0f6144 → for persons
  (type!=2): helper 0x591230 @0x0f61c0 → **LoadInterestData @0x0f61cc**.
* `DoStream__12ObjectModule` @0x0e7c70 (house/lot stream load): per-object
  loop 0x0e7da8-0x0e7eac = ConstructObject → AddObject → **Initialize
  (+68 @0x0e7e80)**; THEN a second phase (0x0e7ec8-0x0e7fc8) walks the
  created list calling **ReconStream (+132 @0x0e7f04)**.
* `DoStream__14cXObjectStream` @0x0fbe90 (single-object stream — the matter
  transferance format): StreamHeader (→ ctor) → **ReconStream (+132
  @0x0fbfc0)** → if the object-definition-unchanged flag is clear,
  **Reset (+72 @0x0fbfe4, arg 0)**.
* `LoadAllObjects__12ObjectModule` also calls Initialize (0x0e57a0-class
  sites) — the boot-time character-file master load.
* `cXPerson::PostLoad` @0x1113b0 (r153-dis-postload.txt): touches
  relationships/suit caches; NO writes to +0x58c words, no interest calls.

### 1.3 Load orders (answers to task 1's three cases)

(a) **CAS-created person** — created through PersonFinder /
BeginDesignAPerson / EditPerson::Begin (xrefs in r153-xref output), ending
in MakeNewOutOfWorldObject → cXPerson ctor (**zero sweep, §2**) →
Initialize: gate sees ZEROS → **RandomizeAllInterests writes ×100 into all
15 topics**. This is the only interest writer on the CAS path. No NBR data
involved.

(b) **Character-file person (UserData/Characters/*.iff)** — two stages.
Boot: LoadAllObjects → MakeNewOutOfWorldObject → ctor zeros → Initialize →
×100 randomization of the master. Instantiation into a lot (TryCreateObject
primitive): MakeNewOutOfWorldObject (ctor zeros → Initialize → ×100
randomize) → **LoadInterestData (NBR record values OVERWRITE the record's
field subset, raw scale)** → the person's own init BHAV tree runs LAST
(raw 0..10 rolls over words 46..55 + word 20 = 5). Net final state for a
shipped townie: tree/NBR raw values.

(c) **NBR / neighborhood record load** — `ChangeSimBaseType` (pet/kid/adult
transformations) → 0x591230 → LoadInterestData; `Reset` itself loads via
its middle call `bl 0xb4bc0` = **LoadPersistentData__12Neighborhood** (the
record walker with `sthx r4,r30,r0` @0x0b4c88 — a NEW sibling of
LoadInterestData R151 did not list). In every case the record values land
AFTER any zero sweep and (in Reset) BEFORE gate 2 — see §1.4.

### 1.4 Reset's true structure (corrects R151 §2.1)

```
0x111944-0x11194c  save words +0x5cc/+0x5d0/+0x5d2
0x111950-0x1119d4  ZERO sweep (li r0,8 -> mtctr; li r0,0; 8x32 sth; bdnz)
0x1119d8-0x1119e0  restore the 3 saved words
0x1119e4-0x1119f0  virtual call (vtable+28 of [this+0xa8c])  [no region writes]
0x1119f8-0x111a6c  GATE 1  (post-sweep -> vacuously passes, r30 = 1)
0x111a70-0x111a84  LoadPersistentData(Neighborhood, this, personsVec)  <- bl 0xb4bc0
0x111a88-0x111b30  personality / 1966-aging / misc (no +0x5e8-region writes)
0x111b34-0x111bac  GATE 2  (sees the RECORD-LOADED values!)
0x111bac  r30 != 1 -> LoadInterestData(this)   [STATICALLY DEAD: r30 is always 1]
0x111bb8  r4  != 1 -> skip
0x111bc4  both == 1 -> RandomizeAllInterests
```

So Reset = "wipe words, reload the persistent record, and re-randomize
interests unless the record proves they were already ×100-randomized (any
gate word > 10)". The LoadInterestData arm can never execute (gate 1 reads
post-sweep zeros); it is a compiler-preserved arm of the source's if/else.

## 2. TASK 2 — EXHAUSTIVE STORE INVENTORY (+0x5e8..+0x5f6)

Every store family that can write the eight gate words (r153-store-inventory.txt):

| # | writer | site | value written |
|---|---|---|---|
| 1 | cXPerson ctor sweep | 0x112330-0x1123b0 (`sth r0,0x58c(r3)` x32, r3+=64, bdnz; r0=0 from `li` 0x11232c) | **0** (whole +0x58c..+0x78b) |
| 2 | Reset sweep | 0x111950-0x1119d4 (same shape, r0=0 via `li` 0x11193c) | **0** (whole array; +0x5cc/d0/d2 preserved around it) |
| 3 | RandomizeAllInterests loop 1 | 0x111760 `b01e05e8 sth r0,0x5e8(r30)`, r30 += 2, 10 iters (cmpwi 10 @0x11175c) | roll x100 (mulli 100 @0x111754) = 0..1000 |
| 4 | ReconStream person restore | call sites: ver-71 full restore `bl 0x11e9c0 (Recon16)` with r4=this+0x58c, r5=256 @0x10bd5c; older: 80/64 words @0x10bd78/0x10bd8c | whatever the save stream holds (any scale) |
| 5 | ReconStream old-version zeroing | 0x10bf34-0x10bf50 (`li r0,0`; 8 sth +0x58e/+0x59c/+0x5b6..+0x5c0) | 0 into words {1,5,13..18} when save version < 45 |
| 6 | LoadInterestData | 0x0b4b48 `sthx r4,r30,r0` | record[idx] (raw NBR data) |
| 7 | LoadPersistentData (NEW) | 0x0b4c88 `sthx r4,r30,r0` | record[idx] (same walk) |
| 8 | ChangeSimBaseType | 0x0a641c `sthx r4,r13,r0` | record[idx] |
| 9 | VM person-data writes | BHAV `MyPersonData` stores (R151 corpus scan: 1212 instructions; raw 0..10) | tree-computed raw |
| 10 | Cleanup__8cXPerson | 0x10862c `sth r0,0x58c(r30)` (single word 0 only, teardown) | 0 |

D-form scan of the whole section finds `0x5e8(r30)` @0x111760 as the ONLY
direct-displacement writer inside the gate region; the indexed-store (sthx)
census over all 285 functions that have one yields exactly items 6-8 as
record walkers (plus the two save-side walkers below). No other store —
stw/stwu/stb/stbu/sthu/stmw/stwx/stbx variant — can land in +0x5e8..+0x5f6.

## 3. TASK 3 — SENTINEL HUNT

(a) **`li rX,10` followed by a store** (r153-sentinel-hunt.txt, 113 sites):
every hit is a stack local, a field-index push, a version constant, or a
byte-munger. The ONLY hit storing into person memory is
`0x111750: li r0,10 -> 0x111754: mulli r0,r0,100 -> 0x111760: sth
r0,0x5e8(r30)` — i.e. the bucket-2 TOP ROLL: it stores **1000**, not 10
(reached from the bucket cascade branch at 0x1116dc). **No store of raw 10
into +0x5e8..+0x5f6 exists anywhere in the code section.**

(b) **Block copies**: the only block writer into the word array is Recon16
(0x11e9c0) via ReconStream (item 4 above); its source is the save stream,
not a static template. The other memcpy-family calls near +0x58c
(0x142360 = string/vector init) never target the word array. There is no
data blob of eight 0x000A halfwords to dump — the record values come from
file data at runtime (maid NBR1 values 6,8,1,6,10,10,0,10,... are FILE
bytes, R151 r151-writers-evidence.txt).

(c) **Constant-10 loops**: none. The only loops storing into the region
are items 2/3 (zeros / ×100 rolls).

(d) **Symbol census** (r153-symbol-census.txt): 16 Interest symbols, 682
Person/etc. New this round: `GetInterestPersistentDataFields__8NeighborFv`
@0x0a4321 (expansion migration list: pushes {46..53,13,14,54,55,...} with
version tag 7 — the Hot Date interest fields),
`LoadPersistentData__12NeighborhoodFP8cXPersonRCvector` @0x0b4bc0,
`SavePersistentData__12NeighborhoodFP8cXPerson` @0x0b4800,
`GetPersistentDataFields__8NeighborFi` @0x0a4600,
`GetLatestPersistentDataVersion__8NeighborFv` (= `return 9` @0x0a42e0).
`Begin__10EditPersonFP11ObjSelectorP8cXPerson` @0x0720f0 (CAS editor) —
its 26 callers are suit/outfit/UI paths; none write the word array.

## 4. TASK 4 — SAVE SIDE

`SavePersistentData__12NeighborhoodFP8cXPerson` @0x0b4800 (and its
all-persons wrapper `SaveAllPersistentDataForMatterTransferance` @0xb46c0,
which loops the neighborhood's person vector at +0x14c/+0x150):

```
0x0b481c: lha r4, 1482(r30)       ; person word 31 = Neighbor index
0x0b483c-48: lwzx r31             ; r31 = the Neighbor record
0x0b4854: bl 0xa42e0              ; GetLatestPersistentDataVersion (=9)
0x0b4858: stw r3, 24(r31)         ; fresh container at Neighbor+24
0x0b485c: bl 0xa4600              ; GetPersistentDataFields(Neighbor, version)
loop 0x0b4868-0x0b4898:
  r0 = [field]                    ; field index
  0x0b487c: 7c9e22ae lhax r4, r30, r4   ; r4 = person[index]   (+0x58c+2*idx)
  0x0b4880: 7c9f032e sthx r4, r0, r31   ; record[index] = person[index]  (+0x74+2*idx)
```

* The save record is a **verbatim halfword mirror** at **Neighbor+0x74** of
  the person words on the field list — no scaling, no clamping, no default
  values, and **no 10s are ever injected**.
* The field list (39 indices, r153-dis-getpersistentfields.txt):
  2,3,4,5,6,7 (personality), 9,10,11,12,16,20,15,26,17,18 (skills/Hobby/
  **Style word 20, Exercise 13?** — 13/14 appear later), **46..53 (the
  eight gate topics)**, 13, 14 (Exercise/Food), 58,60,61,56,57,63,65,67,
  68,69,74, **54, 55 (Technology/Romance)**. ALL 15 panel topics persist.
* The load mirror (LoadInterestData 0x0b4b24-0x0b4b48 and
  LoadPersistentData 0x0b4c60-0x0b4c88) is the identical walk in reverse:
  `lhax r4, r31, r4` (record) → `sthx r4, r0, r30` (person).
* Default-record construction: `0xa42e0` = `return 9` (version constant
  only). Fresh records carry NO values — values exist only after a save
  writes them or DoStream__8Neighbor restores them from the NBRS chunk.
* Consequence for loads: since saves mirror the words verbatim, a ×100-
  randomized sim saves >10 values → on the cXObjectStream path Reset's
  gate 2 fails → interests preserved. A raw-scale set (all <=10) saves raw
  → gate 2 passes → **Reset re-randomizes it ×100 on that path**. On the
  house-load path (DoStream__12ObjectModule) ReconStream runs AFTER
  Initialize and simply overwrites, so saved interests persist at any
  scale regardless of the gate.

## 5. CORRECTIONS TO PRIOR ROUNDS

1. **R151 §2.1 gate polarity** — "calls the randomizer ONLY if ALL EIGHT
   == 10" is wrong; the branches are `bgt` (0x4181xxxx), the test is
   **<= 10**, and the zeros written by the ctor/Reset sweeps ARM the
   randomizer. The all-10-sentinel model (and residual #2) is dissolved.
2. **R151's Initialize/ChangeSimBaseType addresses** were call-site/sthx
   addresses, not function starts (0x111cd0 / 0x0a6340 are the starts).
3. Reset's `LoadInterestData` call @0x111bdc is statically dead; the live
   record loader inside Reset is **LoadPersistentData** @0xb4bc0 (called at
   0x111a84, between the two gates) — a routine R151 did not list.
4. The `addi rX, r0, 0` idiom in these functions is `li rX, 0` (rA field
   0), not a register move — matters for anyone re-reading the dumps.

## 6. RESIDUALS (honest)

1. **Which loader runs for the normal house save** (DoStream__12ObjectModule
   vs DoStream__14cXObjectStream) is INTERP: both are byte-mapped, but the
   static evidence does not prove which one the "Save/Load House" UI drives.
   The distinction matters only for raw-scale interest sets (§4 bullet 5).
2. **TryCreateObject's helper 0x591230** (runs before LoadInterestData) was
   not fully named — it creates/returns the person's record-linked object
   (same pattern in ChangeSimBaseType). INTERP from two call sites.
3. The NBR1 record's field order (which raw halfword maps to which word
   index) remains undecoded (R151 residual #4, unchanged).
4. Runtime frequencies (how often gate-2 re-randomization actually fires
   for tree-initialized sims on the matter-transfer path) are not
   statically determinable.
5. The vtable slice below +68 (indices 0..13) was not named beyond what the
   census shows; irrelevant to interests.
