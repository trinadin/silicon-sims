# R154 — SCRIPT CALL-GRAPH: who calls PersonGlobals 9761 'Convert Interests to
# 0-1000' and 39200 'Add Hot Date Interests'?

Every claim below is byte-pinned by a script + dump in `tools/iff-dump/r154/`
(see §6). Paths: `game-data/The Sims/` for data, repo-relative for code.

## VERDICT UP FRONT

1. **NOTHING calls 9761 or 39200 in the shipped product. Byte-verified five-way
   negative over the ENTIRE corpus (2,238 IFFs = 974 loose IFFs + 61 `.fam`
   house bundles + every member of all 65 FAR archives; 28,158 BHAV chunks,
   60,267 call instructions, 1,808 TTABs, 2,479 by-name calls) AND the original
   engine binary:**
   * **Opcode calls: 0.** No instruction in any BHAV (loose chunk or embedded
     XXXX-wrapped blob) anywhere has opcode 9761 or 39200 (`r154-callers-dump.txt`
     §A; `r154-caller-graph.txt` §1). The ranges are not merely absent at these
     ids — corpus-wide, call opcodes in 9700-9799 and 39100-39299: **0 call
     sites** (`r154-caller-graph.txt` §2).
   * **TTAB entry points: 0.** All 1,808 TTABs checked — 142 plain (v8/v7)
     parsed, **1,355 field-encoded (v9, IffFieldEncode bitstream) decoded with a
     from-scratch Python reader (100% parse success)**, 311 old/degenerate
     (v2/v3/empty) raw-scanned. ActionFunction/TestFunction == 9761/39200: 0.
     The semiglobal ids TTABs DO reference top out at 8,663 — the 9700/39200
     ranges are never touched by the interaction system either
     (`r154-ttab-encoded-scan.txt`, `r154-callers-dump.txt` §B).
   * **By-name calls: 0.** Every `run_tree_by_name` (op 28) instruction
     corpus-wide (2,479) resolved through its STR# scope chain
     (VMRunTreeByName.cs law): none yields either routine name. The names
     'Convert Interests to 0-1000' / 'Add Hot Date Interests' occur ONLY in the
     rsmp directories and BHAV chunk labels of the 7 defining globals files —
     i.e., the definitions themselves (`r154-callers-dump.txt` §D/§E,
     `r154-byname-dump.txt`).
   * **Engine binary: 0.** Scanned the 6.4 MB PEF `The Sims Complete`: zero
     load-immediates (`li/addi`/`ori`) of 9761/39200 in sec0, zero raw 32-bit
     `0x2621`/`0x9920` words anywhere in the file, and every caller of
     `cXObject::RunTree` (17 sites: tutorial, TryReach, TryAnimate, magic book)
     and `Behavior::GetTree(short)` (4 sites: Backtrace, TreeSim::DoNodeAction,
     Gosub, ObjectDlgStubList::Search) carries no such constant
     (`r154-engine-constscan.txt`). The original ENGINE never hardcodes these
     ids either.
   * Caller-of-caller recursion (task 3's depth-5 walk): **vacuously empty —
     depth 0 has no nodes** (`r154-caller-graph.py`).
2. **Therefore the reconstructed runtime event is: NONE. In the shipped game
   both routines are dead script — never dispatched by data or engine.** This
   closes r153 residual #1 in the negative direction.
3. **What they were FOR (evidence inside the routines themselves):** a Hot
   Date-era migration pair. 39200 'Add Hot Date Interests' one-shot-fills the
   five HD topics (words 13/14/16/20/26) with raw 0..10 rolls (tier caps
   {2,2,1}, tier bases +7/+4/+0) gated on "all five == 0"; 9761 then converts
   the whole raw-scale set to the ×100 panel dialect (≤10 gate, then ×100 on
   words 46..55 and 13/14/16/20/26). In **PersonGlobals.iff and
   RentalClerkGlobals.iff the 39200 entry instruction was DISARMED** — both
   branch pointers of ins[0] point at 254 (return-true), making the body
   unreachable even if something called it — while the 5 vacation/downtown NPC
   globals keep the ARMED entry (`T=34`). Byte citations in §3.
4. **Port-side answer (task 4): NO — the port never executes 9761 or 39200
   today, and cannot** unless data that no longer ships called them. Details
   in §5. The port's only 9761 mentions are documentation comments
   (`VMInterestRandomizer.cs:34`, `AutotestRunner.cs:6877`,
   `UIOriginalInterestGiftSubpanels.cs:46`). Porting consequence: the R152/R153
   decision to mirror 9761's law directly in C# (`VMInterestRandomizer`) rather
   than call the tree remains correct; no wiring work is needed (and none is
   possible — there is no caller to port).

---

## 1. TASK 1 — the full-corpus caller scan

### 1.1 Corpus

`r154-callers-scan.py` walks all 8,871 files under `game-data/The Sims`:
* 974 loose `.iff` + **61 `.fam`** (IFF FILE 2.5 house bundles in
  `UserData*/Patch/`, `TemplateFamilyUnleashed/`, `UserData/Tutorial.FAM` —
  easy to miss; contain no BHAV chunks, now proven) 
* all 65 `FAR!byAZ` archives via their manifests, per-member reads, nested-FAR
  support (media members skipped)
* total: **2,238 IFFs, 1,378 containers, 28,158 BHAV chunks** indexed into
  `r154-bhav-index.json` (instructions + GLOB + STR# per container).

Two scan-tooling bugs were found and fixed en route (worth remembering for
future rounds): (a) the r153 `far_members` pattern loses the manifest position
after seeking to read member data — only member 0 of each FAR was being walked
in r153-style inline scans that reuse it across yields; (b) GLOB bodies store
the semi-global name as a **Pascal string without the `.iff` extension**
(`\x0dPersonGlobals`, `User00000.iff` GLOB 32768 'Semi-global file'), and
chunk tag `TPRP` (tree parameters, 16 TemplateNPC files) plus `Optn` were
missing from the r153 tag set — the resyncing walker silently stops at unknown
tags.

### 1.2 Hits

Direct calls (opcode >= 256 with opcode == target): **0** (§A of the dump).
TTAB ActionFunction/TestFunction == target: **0** (§B + encoded scan).
By-name matches: **0/2,479** (§E). Raw u16 scans of all non-BHAV chunks
(§C, 81,293 hits): adjudicated as media/string coincidences — every hit sits
in `Arry` (terrain heights), `ObjM`/`ObjT` (instance data), `SPR2` (sprites,
via chance BHAV-signature matches inside pixel data) or rsmp/STR text like
`"...s \x20\x99..."`; **zero** in OBJD/FCNS/TPRP/TREE/PRPT/NGBH/XXXX or any
script-adjacent chunk.

### 1.3 Where the routines live

| BHAV | defining files (all `GameData/Global/Global.far!…`) |
|---|---|
| 9761 'Convert Interests to 0-1000' | PersonGlobals, RentalClerkGlobals, SalesClerkGlobals, VacationCarnieGlobals, VacationDirectorGlobals, VacationMascotGlobals, WaiterGlobals (7) |
| 39200 'Add Hot Date Interests' | the same 7 (all `@…+0x…`, see §3) |
| 39200 'Interaction - Hug TEST' | CatGlobals + DogGlobals (6-instr UNRELATED pet routine — **id collision**, same id, different tree; also unreferenced) |

The potential invoker set (objects whose GLOB resolves to a defining globals
file): **255 person-family object files → PersonGlobals** (characters
`UserData/Characters/*.iff`, Genie/GrimReaper/Monster/… NPCs, HDT downloads),
plus the downtown/vacation NPC objects → their own globals
(`r154-caller-graph.txt` §3). None of them issues the call.

## 2. TASK 2 — decoded routines (full listings in `r154-routine-listings.txt`)

Pointer law (port `VMThread.MoveToInstruction`, which runs original TS1 data):
**254 = return TRUE, 255 = return FALSE, 253 = fall-through**; expressions
return true/false and take the T/F pointer.

### 2.1 BHAV 9761 'Convert Interests to 0-1000' — LIVE body, no caller

`Global.far!PersonGlobals.iff` BHAV 9761 @member+0x47553 (chunk size 364, sig
0x8002, args 0, locals 0, 23 instrs; r153's "@0x47507" was the chunk-header
offset). **All 7 copies byte-identical** (header + label + every instruction;
diff table in the listings file).

```
[0..7]   PD[46..53] <= 10      T=next  F=254 (return TRUE: already ×100 → no-op)
[8..15]  PD[46..53] *= 100     T=next  F=253 (fall-through; Mul always true)
[16..17] PD[54..55] *= 100     same
[18..22] PD[13,14,16,20,26] *= 100 ; [22] T=254 → return TRUE
```
ins[0] raw: `02 00 01 fe 2e 00 0a 00 00 0f 12 07` (op2 T=1 F=254, lhs 46,
rhs 10, oper 15 <=, scope 18 MyPersonData vs 7 Literal) — identical in all 7
files. Net law: **"if any of words 46..53 is > 10, do nothing (success);
else scale all 15 panel topics ×100."** Always returns true. Same ≤10 gate as
the engine's `cXPerson::RandomizeAllInterests` (r153).

### 2.2 BHAV 39200 'Add Hot Date Interests' — TWO shipped variants

38 instructions, sig 0x8002, args 0, locals 4.

* **ARMED variant** (SalesClerk, VacationCarnie, VacationDirector,
  VacationMascot, Waiter) — ins[0] raw `02 00 22 fe 0d 00 …`: 
  `PD[13] == 0` T=34 F=254.
* **DISARMED variant** (PersonGlobals, RentalClerkGlobals) — ins[0] raw
  `02 00 fe fe 0d 00 …`: `PD[13] == 0` **T=254 F=254 → unconditional
  return-true at entry; the 37-instruction body is unreachable.**

The armed control flow:
```
[0]  PD[13]==0 → [34]                        (else return TRUE)
[34] PD[14]==0 → [35]  [35] PD[16]==0 → [36]
[36] PD[20]==0 → [37]  [37] PD[26]==0 → [1]  (any nonzero → return TRUE)
     → runs ONLY when all five HD topics are zero (one-shot)
[1..5]  Local[0..3]=0;  [2] Temps[0]=NextRandom(3)          (tier draw)
[7..9]  tier 0/1/2 → [10]/[11]/[12]                          (caps: Local[0]<2,
[10..12] capped → next lower tier; [12]F=[10]                Local[1]<2, Local[2]<1)
[13] tier0: Temps[0]=NextRandom(4)=0..3 → [16] += 7 → 7..10
[14] tier1: Temps[0]=NextRandom(3)=0..2 → [17] += 4 → 4..6
[15] tier2: Temps[0]=NextRandom(4)=0..3 → (no add)  → 0..3
[16..20] bump the tier's Local; [21..25] topic dispatch on Local[3]
[26..30] PD[13]/[14]/[16]/[20]/[26] = Temps[0]               (RAW 0..10!)
[31] Local[3]+=1 → [6] until Local[3]==5 → return TRUE
[32/33] find-location(-1,-1,-1,-1) defensive end markers
```
So the armed routine = **"give a pre-Hot-Date sim a full RAW-scale HD interest
set (five topics, distribution 2×high, 2×mid, 1×low)."** The raw output is
exactly the dialect 9761 converts — the pair is a designed two-step
migration: 39200 fills raw, 9761 scales to ×100.

Correction to r153 §3.3: 39200's guard is not "per-topic == 0" — it requires
ALL FIVE topics zero, rolls with tier caps {2,2,1}, and (the big find) the
PersonGlobals/RentalClerk copies are entry-disarmed.

### 2.3 BHAV 39200 in Cat/Dog globals — different routine, same id

6 instructions, `Interaction - Hug TEST`: calls 8201, tests
`StackObjectID[0] != MyObject[11]`, `MyMotives[3] >= 0`, `Temps[0] >= 25`,
set-motive-change; F=255 (return-false) semantics. Unreferenced by id anywhere
as well (0 TTAB hits corpus-wide). Pure id reuse in Unleashed pet globals.

## 3. TASK 3 — the reconstructed event

With five independent byte-verified negatives (§ VERDICT 1), the shipped
product has **no runtime event that executes either routine** — no pie-menu
interaction, no autonomy pick, no queue flow, no by-name call, no engine-side
hardcoded invocation. The caller graph is empty at depth 0; there is nothing
to recurse into.

What CAN be reconstructed is the **designed** trigger, from the routines'
own bytes:

1. **Era:** id 39200 sits in the Hot-Date-era block of the globals files (the
   rsmp directory of each file records 'Add Hot Date Interests' immediately
   before the HD social/init entries, e.g. PersonGlobals …'init visitor' at
   0x4d33b follows it), and the five words it writes are exactly the five
   topics Hot Date added to the interests panel.
2. **Designed pipeline:** 39200 (raw HD top-up for pre-HD persons) → 9761
   (raw→×100 conversion of all 15 topics) — i.e., **a save/NPC migration
   executed on person objects when Hot Date-era ×100 interests were
   expected but the person carried raw 0..10 values** (exactly the state
   r153 proved ships in NBRS records and tree-written sims).
3. **Shipped state:** the migration was never wired to any dispatcher, and in
   the two most important files (PersonGlobals — the semiglobal of every
   playable sim — and RentalClerkGlobals) 39200's entry was rewritten to an
   unconditional return-true. Whatever Maxis used it for in 2001
   (most plausibly the HD install/migration pass, which would run OUTSIDE the
   shipped game against older saves), the retail artifact contains only the
   orphaned corpse of it. 9761 remains armed-but-orphaned in all 7 files.
4. The engine's OWN ≤10-gated ×100 randomizer (r153 binary round) made the
   script pair redundant for new sims; the migration pair is the script-side
   echo of the same law for already-existing sims.

## 4. (intentionally folded into §3 — no callers ⇒ no per-caller context)

## 5. TASK 4 — port wiring map (FreeSO / tso.simantics)

Every place a script tree could be invoked, and whether 9761/39200 can flow:

| vector | implementation | reachable with 9761/39200? |
|---|---|---|
| opcode dispatch | `Engine/VMThread.cs:528-538` `ExecuteInstruction`: opcode ≥ 256 → `ExecuteSubRoutine` (≥8192 → `ScopeResource.SemiGlobal.GetRoutine`, 4096..8191 local, <4096 global) | only if a loaded BHAV contains the call — proven none does (§1); port loads the same shipped data |
| TTAB interactions | `Engine/VMQueuedAction.cs:93,99` + `Entities/VMEntity.cs:667-676` `GetRoutineWithOwner` (same 4096/8192 split) | only from TTAB entries — none exist (§1) |
| run_tree_by_name | `Primitives/VMRunTreeByName.cs` (op 28): STR# string → `StackObject.TreeByName` | no STR anywhere contains the names (§1); also port `TreeByName` (`tso.content/WorldObjectProvider.cs:331-345`) is keyed by BHAV LABELS incl. semi-global labels, so the names ARE in the dictionary — but nothing ever requests them |
| named entry points | `Entities/VMEntity.cs:652` `ExecuteNamedEntryPoint` | engine-wide grep: the ONLY literal used is `"CT - FSO Player Joined"` |
| hardcoded ids | only `Engine/VMRoutingFrame.cs:46 ROUTE_FAIL_TREE = 398`; `VMGenericTS1Call` modes contain no tree ids; grow-up/move-in paths (VMGenericTS1Call 4/5/6/17/26, `VMTS1LotState.VerifyFamily`) never call trees by id | nothing in the 9700/39200 range |
| marshals | `VMTS1ActivatorNew.cs:83` resolves `frame.RoutineID >= 8192 → SemiGlobal` for save/restore only — not a dispatch |

Word-boundary grep for `9761|39200` across all port `.cs`: three hits, all
comments citing the r153/r154 findings (`VMInterestRandomizer.cs:34`,
`AutotestRunner.cs:6877`, `UIOriginalInterestGiftSubpanels.cs:46`).
(`AutotestCatalogCanon.cs` matches are hex-GUID coincidences, e.g.
`0xfc697613`.)

**Conclusion: the port never executes 9761 or 39200 — and, matching the
original shipped game, never can with the shipped data.** The canonical
×100 law the port needs is already embodied in `VMInterestRandomizer`
(R152/R153), which cites 9761 as its source; no porting action arises from
this round.

## 6. Evidence files (`tools/iff-dump/r154/`)

* `r154-callers-scan.py` → `r154-callers-dump.txt` (§A calls, §B TTAB, §C raw,
  §D names, §E by-name), `r154-byname-dump.txt` (all 2,479 op-28 calls
  resolved), `r154-bhav-index.json` (full instruction index: 1,378 containers,
  28,158 BHAVs).
* `r154-ttab-encoded-scan.py` → `r154-ttab-encoded-scan.txt` — Python
  IffFieldEncode reader; 1,355/1,355 encoded TTABs parsed, 0 target hits.
* `r154-decode-routines.py` → `r154-routine-listings.txt` — full listings +
  copy diffs + raw hex citations for 9761 (7 files) and 39200 (9 files).
* `r154-caller-graph.py` → `r154-caller-graph.txt` — reverse index of all
  60,267 call instructions; range census; GLOB invoker set.
* `r154-engine-constscan.py` → `r154-engine-constscan.txt` — PEF li/raw-word
  scan + RunTree/GetTree xref.

## 7. Residuals (honest)

1. What Maxis' 2001 HD installer actually ran (its own external code calling
   these trees against old saves) is outside every shipped artifact — by
   construction unknowable from this corpus; INTERP, and irrelevant to the
   port (the migration end-state is already the port's direct-write law).
2. The 81,293 raw-u16 hits were adjudicated by chunk-type + context sampling,
   not one-by-one; media-coincidence origin is unambiguous for the classes
   seen (terrain/sprite/instance/text bytes).
3. The 311 pre-v4/degenerate TTABs (v2/v3/empty) predate Hot Date entirely
   and were only raw-scanned; their old format cannot reference HD-era ids
   plausibly, and the raw scan found nothing.
4. Whether the disarmed ins[0] of PersonGlobals/RentalClerkGlobals 39200 is a
   deliberate Maxis kill-switch or a shipping mistake is INTERP; both readings
   preserve the verdict (armed copies are equally uncalled).

---

## R154 RESCAN ERRATUM (supersedes every id in this document)

The ids in this report are BYTE-SWAPPED. The rsmp/chunk ids are
LITTLE-ENDIAN u16s that this scan read big-endian: the converter is
**BHAV 8486** (0x2126, not 9761 = 0x2621), the top-up **BHAV 8345**
(0x2099, not 39200 = 0x9920), 'person main loop' is **8283** (0x205B, not
23328 = 0x5B20), 'load tree' is **8244** (0x2034, not 13344 = 0x3420).
The instruction-level DECODES remain valid (the bytes are real); the
five-way NEGATIVE does not: rescanning with the true ids
(r154-callers-rescan-trueids.txt) finds **15 direct call hits** — the
five ARMED expansion NPC globals' 'person main loop' (8283) calls 8345
then 8486, and 'load tree' (8244) calls 8345. The pipeline is WIRED for
expansion-NPC save migration. See r154-scale-saga-closed.md for the
corrected verdict.
