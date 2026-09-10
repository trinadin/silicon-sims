# R151 — EXPANSION-ERA INTEREST TOPICS: STORAGE, WRITERS, LABELS, SCALE

Round target: close the R150 residual — "Exercise/Food/Parties/Style/Hollywood/
Tech/Romance sit in the expansion-era runtime block (+0x5a6..+0x5c0) with no
port person word". Binary: `game-data/The Sims/The Sims Complete` (PPC PEF,
Complete Collection = FINAL engine). All code addresses are FILE offsets.
Data-stored code pointer P → file P+0x8E90; sec1 image (r104) addresses are
direct image offsets, r2 = image+0x8000.

VERDICT UP FRONT: the claim is **confirmed with corrections**. The seven
expansion topics read s16 halfwords of the SAME cXPerson person-word array the
base topics use — NOT a separate runtime block:

* **Technology = +0x5f8, Romance = +0x5fa = person words 54/55** — the
  init-traits tree's own rolls #9/#10 (R150 already stores these words).
* **Exercise = +0x5a6, Food = +0x5a8, Parties = +0x5ac, Style = +0x5b4,
  Hollywood = +0x5c0 = person words 13/14/16/20/26** — the SAME word array,
  lower indices (the "+0x5a6..+0x5c0 block" is just words 13..27 of it; the
  panel picks 5 of its 15 halfwords).
* Person word N ⇔ cXPerson + 0x58c + 2·N, bounds-checked 0..255. The array
  starts at +0x58c and runs 512 bytes to +0x78b.

## 1. TOPIC → STORAGE MAP (panel order, all byte-verified)

`cWinInterest::SetInterest` @0x405b10 (r145 §2.5) — jump table verified this
round from TOC slot −0x491c → sec1 0x69e10 (r151-setinterest-jumptable.txt;
each entry +0x8E90 lands on the case whose `lha r0, disp(r4)` displacement is
listed). r4 = the cXPerson pointer.

| widget | topic (STR# 140 label) | person word | struct +off | label note in STR# 140 |
|---|---|---|---|---|
| 0 | Travel | 46 | +0x5e8 | Interest01 |
| 1 | Money | 47 | +0x5ea | Interest02 (art enum "Violence") |
| 2 | Politics | 48 | +0x5ec | Interest03 |
| 3 | The 60's | 49 | +0x5ee | Interest04 |
| 4 | Weather | 50 | +0x5f0 | Interest05 |
| 5 | Sports | 51 | +0x5f2 | Interest06 |
| 6 | Music | 52 | +0x5f4 | Interest07 |
| 7 | Outdoors | 53 | +0x5f6 | Interest08 |
| 8 | Toys | 46 | +0x5e8 | InterestKid01 (shares w0's case) |
| 9 | Aliens | 47 | +0x5ea | InterestKid02 (shares w1) |
| 10 | Pets | 48 | +0x5ec | InterestKid03 (shares w2) |
| 11 | School | 49 | +0x5ee | InterestKid04 (shares w3) |
| 12 | Exercise | **13** | **+0x5a6** | Interest09 "*New - Hot Date*" |
| 13 | Food | **14** | **+0x5a8** | Interest10 |
| 14 | Parties | **16** | **+0x5ac** | Interest11 |
| 15 | Style | **20** | **+0x5b4** | Interest12 |
| 16 | Hollywood | **26** | **+0x5c0** | Interest13 |
| 17 | Technology | **54** | **+0x5f8** | Interest14 (word-run continuation) |
| 18 | Romance | **55** | **+0x5fa** | Interest15 |

The hot-spot scan (`r151-disp-scan.txt`, whole code region 0x1000..0x5c22f0)
finds the 15 panel offsets loaded ONLY here (lha r0, off(r4), 15 sites) —
the panel is the sole reader family, pets included via the shared cases.

Labels: `GameData/Live.iff` STR# 140 — [0] "Interests" title, then per-topic
name + author note ("InterestNN Tooltip and Popup Title", expansion topics
tagged "*New - Hot Date*"; kid topics "InterestKid01..04"). Descriptions =
STR# 142. Popup/static wiring unchanged from r145 §2.6.

Struct identity: cXPerson (symbol strings `__ct__8cXPersonFP11ObjSelectorP12ObjectModule`
@0x112443, `RandomizeAllInterests__8cXPersonFv` @0x111655). The port's
"CAS-era fields sit outside the word run" wording is wrong — everything above
is inside the one 256-word array.

## 2. WRITER LAW (the big correction)

Three writer families exist in the original engine + data. NOTHING writes
words ≥ 56, and no script writes Exercise/Food/Parties/Hollywood.

### 2.1 ENGINE: `cXPerson::RandomizeAllInterests()` @0x111650 — writes ALL 15
panel topics at ×100 scale (`r151-dis-randomizeallinterests.txt`)

* Loop 1 (10 iterations, `addi r30,r30,2; blt` back-edge, counter cmpwi 10
  @0x11175c): bucketed roll → `mulli r0, r0, 100` @0x111754 →
  `sth r0, 0x5e8(r30)` @0x111760, r30 walking +0x5e8..+0x5fa — i.e. words
  46..55 = the 8 base topics **plus Technology and Romance**.
* Loop 2 (5 iterations r30=0..4, cmpwi 5 @0x1118b4): switch @0x11184c-74 →
  i=0→+0x5a6, 1→+0x5a8, 2→+0x5ac, 3→+0x5b4, 4→+0x5c0, each store preceded by
  `mulli r0, r0, 100` (@0x111878/884/890/89c/8a8).
* Roll law (both loops): `rand = GetNextRandomNumber()` (Park-Miller LCG
  @0x1411d0: state·16838+12345) → `rand % 3` (÷3 magic 0x55555556) picks a
  bucket; exhausted bucket → re-roll; value = bucket range:
  bucket0 → rand%4-family → **0..3** (rotate idiom @0x1116f0-0x111700, INTERP),
  bucket1 → 4 + rand%3 → **4..6** (@0x111708-0x111728), bucket2 → 7 + rand%4
  → **7..10** (@0x111730-0x111748). Bucket counters: loop 1 = TOC blob
  **{4,3,3}** (slot −22828 → file 0x5a33a8, `r151-writers-evidence.txt`);
  loop 2 = li 1 / li 2 + the leftover 3 → **{1,2,3}** (@0x11176c-0x11178c).
* Callers (full-code-section bl scan, exactly two): `Reset__8cXPersonFUc`
  @0x111bc8 and `Initialize__8cXPersonFv` @0x112014. **Both gate it**: they
  test words 46..53 (`lha`/`cmpwi 10`, 8× at 0x1119f8-0x111a6c and
  0x111fa0-0x112004) and call the randomizer **only when ALL EIGHT are
  exactly 10** — the engine's "interests unset" sentinel (raw halfword 10).
* cXPerson ctor @0x112200 and Reset @0x111910 both ZERO-sweep the whole word
  array (+0x58c..+0x78b, 8×32 `sth r0, K(r3)`, r3 += 64; ctor 0x112330-0x1123ac,
  Reset 0x111950-0x1119d0; Reset preserves +0x5cc/+0x5d0/+0x5d2).

So the engine's own canonical initialization (new/CAS person) is: zeros →
(if untouched = all-10 sentinel state) RandomizeAllInterests writes 0..1000
into all 15 panel topics.

### 2.2 SCRIPT: the character files' own 'init traits' trees (RAW 0..10 scale)

Re-verified from bytes (`r151-iffscan.py`): BHAV 4100 of every
UserData/Characters/*.iff = ins0 `MyPersonData[20] = 5` (Assign, literal 5 —
**word 20 = STYLE: every tree-initialized sim has Style = 5**), then
`Temps[0]=46; loop { Parameters[0] = NextRandom(11); bucket-gate vs
Local{3,3,3,1}; MyPersonDataByTemp[0] = roll; Temps[0]++ } until == 56` —
words 46..55 inclusive, RAW 0..10 (R150's decode confirmed instruction-exact).
Whole-corpus scan (`r151-bhav-persondscan.txt` + FAR/loose sweep, 65 FARs +
all loose IFFs, 1212 person-data-write instructions total):
* the ONLY direct person-data write in all 20 character files is
  `MyPersonData[20] = 5` (rolls go through the by-temp indirection);
* NO script anywhere writes words ≥ 56, and none writes words 13/14/16/26;
* the only ×100-scale script writers found are the Hot Date **TemplateNPCs**
  (HDT_Bombshell/DonJuan/Fatale/LoungeLizard/Party/Generic) writing
  **personality** words 2..7 at 100..1000 (e.g. Bombshell: PD[6]=900,
  PD[2]=800, PD[7]=400, PD[5]=300, PD[3]=100, PD[29]=0 —
  `r151-hdt-bombshell-main.txt`) — proof that person words carry ×100-scale
  fields in shipped script data, but NOT for the interest words.

### 2.3 NEIGHBORHOOD: NBR records loaded RAW

`LoadInterestData__12NeighborhoodFP8cXPerson` @0x0b4a80 and
`ChangeSimBaseType__12Neighborhood…` @0x0a6410 walk (index,value) records and
`sthx` them straight into person +0x58c+2·index (`r151-dis-loadinterestdata.txt`).
The NBRS chunk of UserData/Neighborhood.iff (NBR1 records, e.g. the maid:
…, 18 raw halfwords 6,8,1,6,10,10,0,10,8,6,7,5,7,5,6,7,10,0) is raw 0..10
scale. `SaveAllPersistentDataForMatterTransferance` mirrors it for save.

### 2.4 The scale tension (stated honestly)

The VM path is scale-free: `InterpValue__8cXObjectFssb` (@0x0ed680) resolves
person data as `lha r3, 1420(rX)` with `rlwinm r5, idx<<1` and a
`cmpwi idx, 256` bounds check (0xeddd8) — raw s16, no scaling — and the
Random-Number primitive `TryRandom` @0x0f1070 computes `LCG % range`
(divwu/mullw/subf @0x1110-0x1118; float form @0x1138-0x1168 for double
destinations) — no ×100 anywhere (`r151-dis-tryrandom.txt`,
`r151-interpvalue-win1/2.txt`). Yet RandomizeAllInterests (engine, ×100) and
the panel (÷100) both speak the ×100 dialect. Both facts are byte-pinned; how
the shipped game reconciles them for a given sim (tree runs → raw values →
panel ≈ 0 vs CAS-created → ×100 values → full bars; or a load-order ×100
bridge not yet found) needs runtime emulation and is THE open residual. The
all-10 sentinel gate means the two writers never fight: tree-written sims
(almost never all-10) are never re-randomized.

## 3. DISPLAY LAW (unchanged from r145, now confirmed per-widget)

Every SetInterest case, expansion topics included: `value = raw / 100`
(signed magic 0x51EC851F mulhw + srawi 5 + sign fix, e.g. 0x405c68-0x405c80
for Exercise) → `widget+0x190 = value`, clamp `value ≤ 10` (@0x405d5c-0x405d6c,
no lower clamp); TSPaint fills the 40×16 bar with `value × 4 px` of res 1619.
The ÷100 is why the tree's raw 0..10 renders ≈0 in the original and why the
port's bridge (×100 into the display scale, R150) reproduces visible bars.

## 4. WHAT THE PORT MUST USE (exact words)

Person-word storage (port VM words, raw 0..10 tree scale, panel bridges ×100):

| widget | port word today | R151 finding |
|---|---|---|
| 0-7, 8-11 | 46-53 / share 0-3 | unchanged |
| 12 Exercise | 101 (dead) | **word 13** — writer: engine randomize only |
| 13 Food | 102 (dead) | **word 14** — same |
| 14 Parties | 103 (dead) | **word 16** — same |
| 15 Style | 104 (dead) | **word 20** — tree writes **5** for every sim with an init-traits tree; engine randomize otherwise |
| 16 Hollywood | 105 (dead) | **word 26** — engine randomize only |
| 17 Technology | 54 | already correct (tree roll #9) |
| 18 Romance | 55 | already correct (tree roll #10) |

Port-side mirror of the engine randomizer (if the five cells are to carry
live values like a CAS-created original sim): roll words 46..55 with buckets
{4,3,3} over ranges {0..3, 4..6, 7..10} and words 13/14/16/20/26 with
{1,2,3}, each roll ×100 under the engine's own scale — or keep raw tree scale
and mirror the ranges directly (the port's disclosed ×100 display bridge makes
the two equivalent visually).

## 5. Evidence files (tools/iff-dump/r151/)

* `r151-scan-disp.py` / `r151-disp-scan.txt` — whole-code hot-spot scan for
  +0x5a4..+0x5c2 and +0x5e0..+0x5fe loads/stores.
* `r151-setinterest-jumptable.txt` — 19 table entries + case verification.
* `r151-dis-randomizeallinterests.txt`, `r151-dis-initialize.txt`,
  `r151-dis-reset.txt`, `r151-dis-ctor.txt`, `r151-dis-loadinterestdata.txt`,
  `r151-dis-tryrandom.txt`, `r151-interpvalue-win1/2.txt` — full disassemblies
  with raw words.
* `r151-writers-evidence.txt` — bucket blob {4,3,3}, the two call sites, the
  maid NBR1 record hex.
* `r151-iffscan.py` / `r151-scan-bhav-writes.py` / `r151-bhav-persondscan.txt`
  — old-format IFF walker + corpus person-data-write scan (65 FARs + loose).
* `r151-hdt-bombshell-main.txt` — TemplateNPC ×100 personality words sample.

## 6. Residuals (honest)

1. **The raw-vs-×100 reconciliation** (§2.4): where the shipped game scales
   tree-written raw interests to the panel's ÷100 dialect (or whether
   tree-initialized downtown/CAS sims really show near-zero bars in the
   original) is not settled statically. Everything on BOTH sides is
   byte-pinned; an emulating run of Initialize/Reset vs the init tree would
   close it.
2. **Who writes the all-10 sentinel**: ctor and Reset both write zeros; the
   all-10 state that arms RandomizeAllInterests must come from another
   creation path (CAS/EditPerson or the neighborhood record writer) — not
   traced to a store site (no `sth` of 10 exists in the scan; the sweepers
   write 0).
3. **The low-bucket rotate idiom** @0x1116f0-0x111700 (bucket0 0..3) is
   INTERPRETED (ROTL/mask sequence not hand-simulated to a named modulus;
   consistent with counters {4,3,3} and the 4..6/7..10 siblings).
4. The NBR1 record's field layout (which of its raw halfwords map to which
   person word indices) was not decoded beyond the value run.
5. RandomizeAllInterests' random seed/LCG wiring (GetNextRandomNumber static
   state at TOC −10400) was not correlated with the VM's NextRandom.

---
**R152 CORRECTION (loop-2 counters):** section 2.1's "{1,2,3}" for loop 2
misreads the second `stw r0` — the raw words at 0x11176c-0x11178c show
`li r4,1 -> stack[64]`, `li r0,2 -> stack[68] AND stack[72]` (r0 is never
clobbered between the stores), so the counters are **{1,2,2}** (sum 5 =
the five loop-2 stores; {1,2,3} summed 6). Loop 1 stays {4,3,3}. The
ported law and the uiexprand gate use {1,2,2}; see ../r152/r152-randomizer-port.md.

---
**R153 CORRECTION (gate polarity — dissolves residual #2 and rewrites §2.1's
gate sentence):** the branches at 0x1119f8-0x111a6c and 0x111fa0-0x112004
that this round printed as "bne(1)" and read as "word != 10 fails" are raw
`0x4181xxxx` = **bgt** (BO=12, BI=1, CR0.GT): the cascade FAILS when a word
**exceeds 10**. The flag init/set words `38600000`/`38600001` have rA=0 —
they are `li r3,0` … `li r3,1`, not register moves. So the gate is **"all
eight words 46..53 <= 10"**, and the ctor/Reset ZERO SWEEP ITSELF ARMS
RandomizeAllInterests — no all-10 sentinel exists, no code writes one
(r153 exhaustive `li 10`-store hunt: the only one feeding a person store
passes mulli-100 and stores 1000), and no shipped record carries one
(r153 NBRS census: 203 records, 0 with words 46..53 all 10). Residual #2 is
therefore closed as a MISREAD, not a writer. Corroboration:
PersonGlobals.iff BHAV 9761 'Convert Interests to 0-1000' fronts the SAME
<=10 gate (8x operator-15 comparisons) before `*= 100` on all 15 topics.
Also corrected this round: Initialize's start is **0x111cd0** (0x112014 is
the bl site), ChangeSimBaseType's start is **0x0a6340** (0x0a6410 the
sthx site), Reset's LoadInterestData call @0x111bdc is statically dead
(gate 1 reads post-sweep zeros; the live record loader inside Reset is
**LoadPersistentData__12Neighborhood @0xb4bc0**, called between the two
gates), and residual #4 (NBR1 layout) is CLOSED: each record embeds a pd
shorts block that IS the word-indexed mirror (record word i -> person
+0x58c+2i). Full evidence: ../r153/r153-binary-decode.md +
../r153/r153-data-survey.md.
