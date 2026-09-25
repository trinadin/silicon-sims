# R261 — UI-27 decode: relationship filter flags + report-card grade letters

Desk decode, read-only, binary `game-data/The Sims/The Sims Complete`
(sec0 code at file 0x8e90, sec1 packed at 0x5c22f0 regenerated to /tmp,
0x7bf80 exact via `pef_unpack.py`; TOC per r135 recipe).

## 0. TOOL ERRATUM — ppc_decode.py conditional-branch labels are inverted

`ppc_decode.py` line 140/154 keys B-form mnemonics on BO alone:
`{12:"ne", 4:"eq", 0:"lt", 8:"gt"}`. Real PPC: **BO=12 = branch-if-TRUE,
BO=4 = branch-if-FALSE**, BI picks the CR bit (0=LT, 1=GT, 2=EQ, 3=SO).
Printed → actual: `bne(2)`→beq, `beq(2)`→bne, `bne(0)`→blt, `beq(0)`→bge,
`bne(1)`→bgt, `beq(1)`→ble. The parenthetical IS the BI. Every r261 read
below is corrected. Any prior conclusion that hinged on ONE conditional
branch (vs runtime-confirmed laws) should be re-audited before reuse.
Ground truth that forced the fix: `BuildRelationships` 0x2898c8
`cmplwi r0,0 / [beq(2)]` — actual `bne` (skip the alloc when this+552
already exists); the tool's printed `beq` reads inverted.

## 1. Law A — the relationship friends/famous filters (R159 residual CLOSED)

There are **no stored filter flags and no BSS gate** in the filter path.
The R159 "BSS-gated expansion systems" attribution is retired — that
applied to the marker ART gates ([TOC-27020/24/28]), not the filters.

### 1.1 Button state (what selects the filter)
`cWinPeople::InitRelationshipSorts` 0x28e910 mounts the four cTSWinBtn
toggles stored at cWinPeople +404 (Family 4108) / +408 (Friend 4109) /
+412 (Famous 4111) / +416 (All 4110). `BuildRelationships` 0x289880 reads
each button's **state byte at +216** and the first one == 1 wins
(0x289b0c-0x289b90, corrected branches), calling
`PersonFinder::GetRelatedPeople(person, std::vector<int>&, mode)` **0x242360**
with mode 0/1/2/3 in Family/Friend/Famous/All order.

### 1.2 GetRelatedPeople switch (0x242a44-0x242a64, corrected)
Trailer symbols pin the name:
`GetRelatedPeople__12PersonFinderFiRQ212std3vector<i,Q212std31allocator<i>>`
and `PersonRelationTypes`.
* mode 0 **Family**: candidate Neighbor record halfword +238 (0xEE = person
  word 61, family id) == selected's → accept (0x242a68).
* mode 1 **Friends**: `PersonFinder::GetRelation(sel, cand, &rel)` 0x241ba0;
  accept iff **rel byte +5 == 1** (0x242a7c-0x242a94: `lbz 77(r1)`, rel at
  r1+72).
* mode 2 **Famous**: accept iff candidate Neighbor word 81
  (`lha 278(r3)`, neighbor+278 = +116+162 → word (278-116)/2 = 81 =
  TS1FameStarPower) **!= 0** (0x242be0). Persisted-data test — NO in-world
  requirement.
* mode >= 3 **All**: accept unconditionally.
Shared pre-gates for every mode: `Neighbor::IsCharacter()` 0xa4020 plus
neighbor+180 / family-0 checks (0x242628/0x242784; exact attribution of the
+180 (word 90) == 6 test not fully labeled — shared validity gate).

### 1.3 What SETS the flags — cJob… no, PersonFinder::GetRelation 0x241ba0
`GetRelation(i, j, PersonRelation*)` zeroes the out record (+0/+4/+5/+6),
then per direction walks PersonFinder's global table **[TOC-21088]** (BSS;
68-byte person entries: +0 id, +4 dirty halfword, +8 RelMatrix records,
RelMatrix at entry+12) and fills:
```
PersonRelation { +0  int   forward RelMatrix slot-0 (raw daily)
                 +4  byte  heart   = forward slot-1 != 0
                 +5  byte  FRIEND  = forward slot-0 >= G AND reverse slot-0 >= G
                 +6  byte  deep    = forward slot-3 != 0
                 +8  int   forward slot-2 (lifetime), clamped ±100
                 +12 int   forward slot-0, clamped ±100 }
```
Accessors named by symbols: `RelMatrix::GetValue` 0x1202e0 /
`RelMatrix::GetArraySize` 0x120a00. Slot law: 0 = daily score, 1 = heart
marker, 2 = lifetime score, 3 = deep-heart marker — exactly the RelVar
indices the VM `Relationship` primitive writes (fork `VMRelationship.cs`;
TS1 neighbor matrix `Neighbour.Relationships[target][RelVar]`).
G = `*(int*)[TOC-21088]` — the PersonFinder singleton's friend threshold,
runtime-constructed (BSS 0x92618; every access in the image is a read;
zero r2-relative or lis-pair stores). Two independent pins say **50**: the
fork's own VMMemory mutual-friend law (TS1NeighbourProvider family-import
step 16: `rel[0] >= 50 both ways`) and r184's famous-friend law.
`Neighborhood::GetFamousFriendCount` 0xb2030 uses a DIFFERENT global
[TOC-29292]+0 whose sec1-rel resolution (0x48eb8) reads **25** (code-rel
candidate is mid-instruction garbage) — tension with r184's "50" noted;
settle with a runtime probe if the famous-friend count is ever ported
exactly. The UI Friends filter consumes GetRelation's G (50 per fork pins).
Note: the mutual test compares the PRE-clamp raw slot-0 values (the ±100
clamps at 0x241f90+ apply only to the returned copies +8/+12).

### 1.4 Marker mapping confirmation
The port's `MarkerFor`/`MarkerFlagsFor` bits match the native roles:
smiley = rel+5 (the FRIEND class — hence mutual), heart = rel+4 (slot 1),
deep-heart = rel+6 (slot 3). These are r159's stack offsets +69/+68/+70.

### 1.5 Port verdict + plan (UIRelationshipSubpanel.cs, read-only today)
Current port predicates are WRONG against this law:
* `IsNativeFriend` = `forward[5] == 1` — RelVar 5 has NO engine meaning
  (fixture value lists are length 1-2; the slot is never written by the
  corpus) → the Friends filter yields ~nothing; the smiley marker starves.
* `PassesFilter` RelSort==1 consults only the forward row — the native law
  is MUTUAL.
* `IsNativeFamous` requires the sim to be resolved in-world — the native
  test reads the persisted neighbor's word 81 (works for out-of-lot sims).

Port plan (all insertion points in
`Client/Simitone/Simitone.Client/UI/Panels/LiveSubpanels/UIRelationshipSubpanel.cs`):
1. `IsNativeFriend(IList<short> forward)` → make it the mutual law. Change
   the signature to `IsNativeFriend(IList<short> forward, IList<short>
   reverse)` (or overload) returning `forward?.Count > 0 &&
   forward[0] >= 50 && reverse?.Count > 0 && reverse[0] >= 50` with a
   `FriendThreshold` const = 50. `IsMutualFriend` keeps forwarding.
2. `PassesFilter` RelSort==1 branch (line ~242): fetch
   `source.Relationships[target]` AND `other.Relationships[from]`, call the
   mutual predicate.
3. RelSort==2 branch: drop the live-avatar lookup; use
   `provider.GetNeighborByID((short)target)?.PersonData` word
   `TS1FameStarPower` != 0 (guard Count). Keep a try/catch as today.
4. Marker bits: `MarkerFlagsFor` bit0 becomes the mutual friend class
   (needs the reverse row it already receives).
5. Disclosed delta: native enumerates ALL person records and filters;
   the port enumerates `source.Relationships.Keys` — a family member with
   no stored relation row is invisible to Family mode. Optional follow-up:
   union the family list (word 61) into the candidate set for RelSort==0.
6. Gate: extend `uirel` — synthetic forward/reverse rows (0,49 / 50,50 /
   60,40 / 100,100) must classify exactly; famous must pass on persisted
   PD81 alone (no live avatar).

## 2. Law B — the report-card grade letter (Tier A item 7)

### 2.1 Chain (instruction-level, corrected branches)
`cWinSubpanelReportCard::TSPaint` 0x2a63d0:
`if (PersonFinder::GetCareer(p) != 0)` (0x240860, beq at 0x2a64bc skips the
grade entirely when no career) →
`value = PersonFinder::GetJobPerformance(p)` 0x23fd30 (child <18 → person
word 57; adult → word 63) →
`str = cJob::GetGrade(value)` 0x5a7c0 → drawn by local 0x2a6600 at
popup+84. `cmpwi r30, 10` @0x2a64d0 with actual `blt` to the normal path:
**value >= 10 FLASHES the grade button** (arm byte popup+413 ++, show
helper 0x51c500 with the engine-literal (333, 333, 0); the value<10 path
clears through 0x51c160). r159's "10 → A+ special case" reading is
corrected — it is a failing-grade flash, not a string rewrite.

### 2.2 GetGrade 0x5a7c0 (104 bytes)
`set = [TOC-23664]` (BSS 0x7efa0 StringSetBase). Return
`(v+1 <= Count(set,-1)) ? GetString(set, v+1, -1) : [TOC-23648]+134`.
* `StringSetBase::Load` 0x1452c0 (called from `LoadCareers` 0x59300 at
  0x595e8/0x595fc): fills this+0 resFile, +80 = STR id (halfword), +84 =
  'STR#', +88 = 0, **+89 = appLanguageByte - 1**, then loads the chunk.
  LoadCareers pairs: [TOC-23660] ← STR#4096, [TOC-23664] ← **STR#4097**
  from the 'Work' resource (work.iff; literal "Work" at [TOC-23648]+168,
  sec1 0x42bfc). ExpansionShared.far!work.iff sha
  4f9d3dcf… matches r184's pin.
* `Count` 0x146710: returns set+96 when langchar == set+89 or char == -1,
  else 0.
* `GetString` 0x144b00: index < 1 → NULL; index > Count(langchar) → NULL;
  else array[index-1] (1-based, bounds-checked; set+92 = string array).
* The out-of-range fallback literal is sec1 0x42c32 = [TOC-23648]+134 =
  **a single NUL byte — the EMPTY string** (the pool there: carpool names
  "0 School Bus"…"11 Clown Car", then ".5"/".0", "fame.iff", "Work").

### 2.3 Table content (fork's own game data)
ExpansionShared.far!work.iff STR#4097 (format −513, English pairs), 16
grades: `A+ A A- B+ B B- C+ C C- D+ D D- F F "F (danger of military
school)" "F (off to military school)"`. Grade value 0 = A+ … 15 = F
(military). Cassandra grade 4 = "B" (matches the R180 packaged survey
`text=B`).

### 2.4 Port verdict + plan (UIJobSubpanel.cs)
**Tier A item 7's "still string-set-indirected — numeric shown" is STALE.**
R180 already landed the exact law: `UIJobSubpanel.GradeForIndex` reads
`Content.Get().Jobs.JobResource` (= `TS1JobProvider.JobResource` =
work.iff) STR#4097 by the 0-based equivalent of the engine's value+1, and
the mobile "Grades: N" placeholder routes through the same table. This
round confirms at instruction level that the native source is the work.iff
STR table (not charm/career lookup, not computed), that the fork's game
data carries the exact table, and that the port's out-of-range "" IS the
native fallback (the NUL literal). Remaining native deltas, in priority
order:
1. **Flash** (not ported): when `gradeIndex >= 10`, blink the grade button.
   Plan: in `UpdateDesktop`'s `UIDesktopJobMode.ReportCard` case (and the
   shared mobile child branch), compute `bool flash = gradeIndex >= 10;`
   and drive `UIReportGradeControl` with a visible-phase toggle (native
   tick constant 333 unlabeled — pick ~500 ms and disclose, or reuse the
   pause-label cadence machinery). Draw path: `UIReportGradeControl.Draw`.
2. **Career gate** (edge case): native draws NOTHING when GetCareer == 0;
   the port always sets text for a child. Plan: guard the
   `ReportGradeControl.Text` writes with the existing `job != null` /
   word-57 >= 0 state and blank the control otherwise.
3. Out-of-range "" and the 16-value table already match — no change.

## 3. DeltaMeter [TOC-20288] re-verdict (R159 wall re-examined)

[TOC-20288] → TOC entry 0x30c0 → value 0x93508 ≥ 0x7bf80 = **BSS**.
Exhaustive scans over the whole code section (0x8e90-0x5c22f0):
* r2-relative D-field −20288: exactly two READS (`lwz`) —
  cWinRelationship::Init 0x29bbc0 and ctor 0x29c1e8 — zero stores;
* lis 0x0093 + ±16-bit-offset pairs landing in 0x93400-0x93600: zero.
The anchor point object is filled before use by runtime initialization
with no static image footprint. **WALL CONFIRMED** (upgrade from r159's
"code-rel read lands mid-instruction" reasoning to exhaustive negative
scans). Geometry stays as r159 decoded: cWinDeltaMeter (496 B) with
SetTopLefts 0x275af0, 20 meter columns at +6 px, ResetHistory 0x274730 /
PumpSample 0x2751d0. Port implication unchanged: anchor the trend meter to
the port's own card grid; the runtime anchor value is unrecoverable.

## 4. Evidence files

* `r261-loadcareers.txt` — LoadCareers 0x59300-0x59a20 (STR#4096/4097 loads)
* `r261-getrelation.txt` — GetRelation 0x241ba0-0x242004 (flag-byte law)
* `r261-getrelated.txt` — GetRelatedPeople 0x242360-0x242ce0 (mode switch)
* `r261-famefriend.txt` — cFameTrack::GetFamousFriendCount 0x57a80
* `r261-toc-lits.txt` — TOC resolutions + fallback-literal pool dump
* STR#4097 extracted from the fork's own
  `game-data/The Sims/ExpansionShared/ExpansionShared.far` (regenerable;
  bulk kept out of the repo)
