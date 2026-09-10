# R153 — DATA SURVEY: the all-10 interest state in shipped game data + the port's person-creation map

Round target: hunt person records whose interest words 46..53 are ALL 10 in
the shipped data files (NBRS neighborhoods, Character/*.iff, GameData
creation templates), and map the port's new-person code paths. Companion to
`r153-binary-decode.md` (same round, engine side). All paths relative to the
repo root; every claim below is byte-pinned by a script + dump in this
directory.

## VERDICT UP FRONT

1. **The shipped data is RULED OUT as a source of the all-10 state.** All
   203 real NBRS records across all 9 neighborhood files: ZERO have words
   46..53 all == 10 (`r153-nbrs-dump.txt`). The 22 records that touch words
   46..55 at all are all user-created sims carrying raw 0..10 tree-dialect
   rolls; every NPC record carries ZEROS there (personality words 2..7 at
   ×100 = 1000 instead).
2. **No static person-data block exists anywhere else.** The 20
   Character/*.iff store no person-word array (OBJD is structural; the only
   literal person-data write in any of them is `MyPersonData[20] = 5`);
   TemplatePerson.iff (the creation template) likewise; a whole-tree sweep
   of 8,871 files finds the 8-consecutive-10-short byte pattern only inside
   a font file and PCM WAVs. Verified negatives, all three.
3. **Two NEW interest trees found in GameData** (missed by every prior
   round): PersonGlobals.iff BHAV **9761 'Convert Interests to 0-1000'**
   and BHAV **39200 'Add Hot Date Interests'**. 9761 gates on words 46..53
   being **≤ 10** (operator 15) and then multiplies ALL 15 panel topics
   ×= 100 — it is the script-side raw→×100 bridge AND independent
   data-side corroboration of the binary round's gate-polarity correction
   (the engine gate is `<= 10`, not `== 10`). Neither tree writes 10s;
   corpus-wide literal-10 WRITES to words 46..55: **0**.
4. **Port conclusion (task 4):** the port's creation paths (CAS family AND
   the make_new_character townie primitive) funnel through
   `SimTemplateCreateInfo.MakePersonData`, which since R152 writes the
   final ×100 values directly via `VMInterestRandomizer.ApplyTo`. **No port
   writer should produce the 10-state** — it is not the original mechanism
   (creation stores zeros/rolls; the ≤10 gate arms on zeros). The port's
   `IsUnsetSentinel` (==10) branch tests a state that provably occurs in no
   shipped file and (per r153-binary-decode.md) in no engine code path.
   Also flagged: that branch lives only in the OLD `VMTS1Activator`; the
   runtime lot-load path now instantiates `VMTS1ActivatorNew`, which has no
   interest hook at all.

---

## 1. TASK 1 — NBRS chunk, full decode (all staged neighborhood files)

### 1.1 Files that exist (complete enumeration)

`game-data/The Sims/`:

| file | NBRS? | notes |
|---|---|---|
| UserData/Neighborhood.iff | yes @0xb8, datasz 6734 | v0x3e; the only roster with user families (33 real records) |
| UserData2..UserData8/Neighborhood.iff | yes, datasz 3162/2730 | v0x3f; Deluxe pristine (UserData2 has 2 user sims, 3..8 none). Verified: the NBRS chunks of UserData3-8 and TemplateUserData are BYTE-IDENTICAL (sha256 prefix e9c8555a3b53e8e2); UserData2's differs (has 2 user records). |
| TemplateUserData/Neighborhood.iff | yes, datasz 2730 | v0x3f; byte-identical roster family to UserData3-8 |
| TemplateCommunity/NeighborhoodDesc.iff, TemplateDowntown/DTDesc.iff, TemplateVacation/VIDesc.iff, TemplateStudiotown/STDesc.iff, TemplateMagictown/MTDesc.iff | **no NBRS** | string-table-only descriptors (STR# chunks) |

A whole-tree walk of all 974 IFF files under game-data (r153 script, inline
in shell log; see §3.2's sweep for the byte-level complement) finds NBRS in
exactly these 9 files — the expansion "neighborhoods" (downtown, vacation
island, studio town, magic town, old town) carry NO resident roster: their
NPCs are spawned from TemplateNPCs/*.iff + People/*.iff via the engine's
AddMissingNeighbors mechanism, not from NBRS data.

### 1.2 Chunk + record layout (byte-exact; replica of FreeSO's authoritative
`FreeSO/TSOClient/tso.files/Formats/IFF/Chunks/NBRS.cs` `Neighbour(io)`,
independently re-validated by full-chunk alignment on all 9 files:
consumed == datasz everywhere)

NBRS chunk = big-endian IFF header (tag/size/id/flags/label64), then
LITTLE-endian data:

```
chunk data:
  u32 pad (=0)          u32 version (0x3e UserData, 0x3f UserData2-8/Template,
                        FreeSO writes 0x49)     cstr4 magic 'NBRS'
  u32 count             <- the '1' in R151's "NBRS1" is this count's low byte:
                          UserData has 0x31 = 49 records (hex: 31 00 00 00)
  count x record:
    i32 Unknown1   == 1 for real records; != 1 -> 4-byte null slot, no body
    i32 NVersion   0x4: pd block = 0xa0 bytes = 80 shorts (words 0..79)
                     0xA: i32 Unknown3 follows, pd block = 0x200 bytes of
                          which the first 88 shorts are words 0..87
    sz  name + NUL (+pad byte if len(name) even)
    i32 MysteryZero(=0)   i32 PersonMode (0 / 5 / 9; <=0 => NO pd block)
    [pm>0] pd shorts — THE person-word array, word i at pd[i]
    i16 NeighbourID  u32 GUID  i32 (usually -1)
    i32 relCount x { i32 1, i32 key, i32 n, n x i32 }
```

Cross-validation against the loader disassembly
(`r151-dis-loadinterestdata.txt`, `LoadInterestData__12Neighborhood` @0x0b4a80):

* The engine walks an array of record pointers (Neighborhood+336, count
  +332) and matches `record+8` against the person's key — one record per
  sim, exactly the NBRS roster above.
* Values are copied by index: `lbzx/lhax r4, r31, (idx<<1)+K` →
  `sthx r4, (idx<<1)+0x58C, person` — i.e. **record word i → person word i**
  (`+0x58c+2*i`, r151's own map). The file's pd block IS that word-indexed
  halfword mirror; this closes r151 residual #4 / binary-round residual #3
  (record field order): there is no separate index/value interleaving in the
  FILE — the (index,value) pairs exist only in the engine's in-memory
  field-list walk (`GetPersistentDataFields`), which selects a subset of the
  80/88 stored words (39 indices at version 9: personality 2-7, skills,
  16/20/26, **46..53, 13, 14**, 54.., per r153-binary-decode.md §4).
* Record halfword +0 → person word 31 (Neighbor index); the in-memory
  record's `+24` version container (save side writes
  `GetLatestPersistentDataVersion()=9` there) is the gate the field-list
  walk compares against — the on-disk analog is NVersion 0x4 vs 0xA
  (80-word vs 88-word pd block).

### 1.3 The R151 "18-halfword sample" — decoded

R151's maid record hex (`r151-writers-evidence.txt`) ends with 18 halfwords
`6,8,1,6,10,10,0,10,8,6,7,5,7,5,6,7,10,0`. Under this layout they are
**pd words 2..19 of record 'maid' (NeighbourID 11, record idx 10) of
UserData/Neighborhood.iff**, pd block at chunk-data offset 0x4e:

```
pd[2..7]  = 6,8,1,6,10,10   personality (raw 0..10!)
pd[8]     = 0
pd[9..19] = 10,8,6,7,5,7,5,6,7,10,0
   of which the expansion topics: pd[13]=5 (Exercise), pd[14]=7 (Food),
                                pd[16]=6 (Parties), pd[20]=0, pd[26]=0
pd[46..55] = ALL ZERO (the eight gate words + Technology/Romance)
```

So the maid's shipped record carries RAW-scale values on the very words the
panel later exposes — Exercise=5/Food=7/Parties=6 are real shipped data —
while its base interests are zeros. ('thief' and 'repairman' share the same
value set; all other service NPCs carry words 2..7 = 1000 ×100 and every
interest word 0.)

### 1.4 The interest-gate census (the round's question)

From `r153-nbrs-dump.txt` (per record: pd[46..55], pd[13/14/16/20/26],
all nonzero words):

* **all eight words 46..53 == 10: 0 records** (out of 203 real records;
  31+13+11x6 pd-bearing). Not in any file, not even partial-family.
* "some 10s in 46..53 but not all eight": 10 records (random rolls that
  happened to hit 10 — e.g. user00022 pd[47]=10, user00021 pd[53]=10).
* "any nonzero word in 46..55": 22 records — every one a `userNNNNN` sim
  (20 in UserData, 2 in UserData2), values raw 0..10, EXACTLY the
  init-traits tree dialect; 0 records carry ×100-scale values there.
* NPC records (maid/officer/thief/repairman/papercarrierf1/repoman/
  mailcarrierf1/pizzadude/socialwkr/gardener/firefighter, and pm=0 entries
  bones/templateperson/grimreaper/genie/genie2/tragicclown/robot):
  words 46..55 all ZERO; `templateperson` (GUID 7fd96b54) is itself listed
  in the shipped roster as a pm=0 NPC entry (record idx 23, id 24).

Reading: the SHIPPED neighborhood save contains only (a) tree-rolled raw
interests for created sims and (b) zeros for NPCs. **No all-10 state, no
×100 interests, in any shipped NBRS.**

---

## 2. TASK 2 — Character/*.iff: no static person-data block (verified negative)

All 20 files in `UserData/Characters/` (User00000..User00023; 12/16/18/19
vacant) share one structure (`r153-chars-dump.txt`; needs a resyncing walker
— some odd-size chunks are stored without the pad byte, e.g. CTSS 2000 of
User00000.iff ends at off+size, and the r151 walker silently desyncs there):

```
rsmp | OBJD 128 "userNNNNN - Name" (216 B data) | CTSS 2000 (name/bio)
STR# 300 "Behavior editor string set" | STR# 304 "suit names" | STR# 200 "bodystring"
BHAV 4096 'Main' (4 instr) | BHAV 4097 'init tree' (2) | BHAV 4098 'load tree' (2)
BHAV 4100 'init traits' (21) | BMP_ 2002..2006 | GLOB 128 'Semi-global file' | SLOT 128
```

* **OBJD 128**: 108 LE-u16 structural fields (base GUID 0x7FD96B54 at
  fields[73..74], per-sim GUID at [14..15], tree ids 4097/4098 at [44]/[47])
  — same shape as TemplatePerson's OBJD; the tail is all zeros. **No
  person-word array, no interest values.**
* **BHAV 4097 'init tree'** = `[op 8192, call 4100]`; **4098 'load tree'**
  = `[op 8244, call 4100]` — both funnel into 4100. **BHAV 4100** re-decoded
  this round (sig 0x8002 header: count u16@+2 per FreeSO BHAV.cs): ins0
  `MyPersonData[20] = 5` (scope 18 ← Literal 7), Temps[0]=46, loop
  `random dest=[Parameters]0 range=Literal 11` with Local buckets {3,3,3,1}
  written through `MyPersonDataByTemp`, until Temps[0]==56 — R150's law,
  byte-confirmed. **The ONLY literal person-data write in any character
  file is PD[20]=5** (all 20 files); literal-10 writes to 46..55: 0.
* `TemplateUserData/Characters/_` is a 4,096-byte all-zero placeholder.
* FAMI chunks in Neighborhood.iff are 48-56 B metadata (house/lot, member
  GUID count/list) and XXXX chunks hold the family name string — no person
  data. Tutorial.FAM / TemplateFamilyUnleashed/*.FAM are exported
  FAMILY+LOT bundles (SIMI manifest + HOUS/Arry/objt/ObjM/BMP_/FAMI); their
  SIMI is a 16-byte-per-sim GUID manifest, and avatar state lives in the
  lot OBJM instance data, not an interest block.

**Negative is verified**: apart from the runtime BHAV scripts, character
files store no initial person-data block of any kind.

---

## 3. TASK 3 — GameData creation templates: no 10-writers; two new trees

### 3.1 Creation-template files (name-fragment hunt across 65 FAR manifests
+ loose IFFs; keyword set editperson/personed/newperson/createperson/
makeperson/cas/template)

| candidate | verdict |
|---|---|
| `GameData/Objects/Objects.far!People/TemplatePerson.iff` (8,568 B) | THE clone source for new sims (port mirrors it, TEMPLATE_GUID 0x7FD96B54). Contains OBJD 128 'TemplatePerson' + GLOB + SLOT + XXXX-wrapped trees: cid 0x210 trees 'load tree' ([op 0x2034, call 4100]) and 'Main'; cid 0x10 chunks hold 'EERT' TREE tables + one 'PRPT' (tree parameters). **No BHAV 4100 of its own, no person-word block, no 10s.** |
| `ExpansionPack5/ExpansionPack5.FAR!templatecat.iff / templatedog.iff` | pet templates; literal pd writes only {68 ghost, 61 family, 56 job} in Adopt/Abandon trees — no interest words |
| `TemplateNPCs/*.iff` (HDT_* + MMClownie + SSCeleb, 35 files) | 'init traits' 1040 writes ×100 literals to words 13/14/16/20/26 AND 46..55 (e.g. WallFlower: PD[46]=700, PD[47]=400 ... PD[55]=400, PD[13]=200..PD[26]=400) — expansion NPCs ship with FIXED ×100 interests; 'Clear Personality' 784 zeroes words 2..7. No 10s. |
| `GameData/*.iff` loose (Behavior/Live/UIText/fame/magic/...) | chunk labels only; no person-word blocks (keyword hits are Adopt/Template art names) |

### 3.2 Static-array negative, whole tree

Swept all 8,871 files under `game-data/The Sims/` for the byte patterns
`0a 00` x8 and `00 0a` x8 (eight consecutive 10-shorts, either endianness)
and x10: hits ONLY in `ExpansionPack6/Sound/Sound.far` (a .wav),
`ExpansionPack7/Sound/Sound.far` (.wavs), and `UIGraphics/UIGraphics.far`
(member `Fonts\font16b5.fon`). All three are coincidental byte runs in
PCM/font data. **No static 8x10 (let alone 10x10) halfword array exists
anywhere in the shipped data tree.**

### 3.3 The two NEW PersonGlobals trees (the round's find)

`GameData/Global/Global.far!PersonGlobals.iff` (383,543 B, 473 BHAVs):

**BHAV 9761 'Convert Interests to 0-1000'** @0x47507 (23 instructions;
identical copies also in RentalClerk/SalesClerk/VacationCarnie/
VacationDirector/VacationMascot/Waiter globals — these NPC globals are the
semi-globals of expansion NPC templates):

```
[0..7]   PD[46] <= 10; PD[47] <= 10; ... PD[53] <= 10
         (op 2, operator 15 = LessThanOrEqualTo, T=next F=254 return-false:
          any gate word ALREADY > 10 aborts the whole tree)
[8..15]  PD[46..53] *= 100      (operator 6 = MulEquals, literal 100)
[16..17] PD[54] *= 100; PD[55] *= 100
[18..22] PD[13] *= 100; PD[14] *= 100; PD[16] *= 100; PD[20] *= 100;
         PD[26] *= 100
raw hex of ins[0] and ins[8]: 2e000a00000f1207 / 2e00640000061207
```

This is (a) the missing script-side raw→×100 bridge that R151 §2.4
hypothesized ("a load-order ×100 bridge not yet found"), and (b) INDEPENDENT
data-side proof of the binary round's polarity correction: the script gate
uses **≤ 10**, the same law the corrected engine gate (`bgt` fail) uses —
"raw-scale set" arms the conversion, "any ×100 value" disarms it. It does
NOT write 10s anywhere.

**BHAV 39200 'Add Hot Date Interests'** @0x4ce1b (38 instructions): gates
`PD[13] == 0` per expansion topic, then rolls bucketed raw values
(NextRandom(8) draws, Local buckets) into words 13/14/16/20/26 for sims
that lack them — the Hot Date-era top-up for pre-Hot-Date saves. Also no
10-writes. Neither tree has any direct opcode caller in the person-file
corpus (9761 ≥ 8192 = semi-global range; invoked engine-side / by NPC
spawn flows — callers not in loose script data).

Corpus-wide, across PersonGlobals + all 24 Global.far members + People/*
+ EP5 templates + 35 TemplateNPCs + GameData loose (2,753 literal
person-data writes catalogued in `r153-gamedata-dump.txt`):
**literal-10 WRITES to words 46..55: 0. Interest gates testing 10: 56 —
all `<= 10`, all in copies of 9761.**

---

## 4. TASK 4 — the port's person-creation map (file:line)

### (a) CAS family creation — uses SimTemplateCreateInfo, NOT a SimTemplate object

* Screen: `Client/Simitone/Simitone.Client/UI/Screens/TS1CASScreen.cs`
  * `SaveFamily()` :1068-1074 → `SimitoneNeighbourGenerator.CreateFamily(
    lastName, WIPFamily.Count, WIPFamily.Select(CASToNeighGen).ToArray())`
  * `CASToNeighGen()` :1034-1057 builds a `SimTemplateCreateInfo`
    (body-type code + skin, `PersonalityPoints = x.Personality` — CAS UI
    values ×100 at :1101/:1107, body/head/hand string replaces)
* Engine side: `FreeSO/TSOClient/tso.simantics/Utils/SimitoneNeighbourGenerator.cs`
  * `CreateFamily` :68-81 — per member: `PrepareTemplatePerson(guid, info)`
    (:26-66: clones the TemplatePerson object — GUID `TEMPLATE_GUID =
    0x7FD96B54` :11 — retargets OBJD/GUID/CTSS/bodystrings, `SaveNewNeighbour`
    writes the new Character/*.iff) then `AddNeighbor(guid, 9,
    info.MakePersonData())` (:155-180 — stores the pd array into the NBRS
    roster, PersonMode 9).
  * **`SimTemplateCreateInfo.MakePersonData`** :252-317 — the single
    builder for CAS sims AND townies: personality, job canon, skills, then
    **`VMInterestRandomizer.ApplyTo(pd, rand)` :309** — the R152 engine-law
    mirror: words 46..55 buckets {4,3,3} and 13/14/16/20/26 buckets
    {1,2,2}, ranges 0..3/4..6/7..10, **each ×100 at the store**
    (`VMInterestRandomizer.cs` :67-88, header comment cites r151 §2.1 +
    the r152 counter correction).

So: the port answers the "SimTemplate?" question with **SimTemplate-
CreateInfo.MakePersonData** (data built in C#, cloned object from
TemplatePerson.iff), and interests come from **VMInterestRandomizer**, not
from any file template.

### (b) Townie / NPC generation — the same builder

* `FreeSO/TSOClient/tso.simantics/Primitives/VMTS1MakeNewCharacter.cs`
  (primitive `make_new_character`, registered `VMContext.cs` :471-475) —
  rolls head/body/gender/skin from BCF collections, `info.PersonalityPoints
  = 1000 x6` :118-123, then `SimitoneNeighbourGenerator.CreateNeighbor(guid,
  info)` :125 → `PrepareTemplatePerson` + `AddNeighbor(9, MakePersonData())`
  (:83-87). Pets (AvatarType 1/2) use the kat/dog SimTemplateCreateInfo
  ctor :237-250.
* NPC templates missing from NBRS: `FreeSO/TSOClient/tso.content/TS1/
  TS1NeighbourProvider.cs` `AddMissingNeighbors()` :142-159 — pd from
  `PreparePersonDataFromObject` (wired in `TS1GameScreen.cs` :231 to
  `Client/Simitone/Simitone.Client/Utils/PersonGeneratorHelper.cs` :37-48:
  instantiates the object in a temp VM and clones its live PersonData,
  Take(88)).

### (c) The load-time interest hook — and a placement caveat

* The hook: `FreeSO/TSOClient/tso.simantics/Utils/VMTS1Activator.cs`
  :303-322 — runs AFTER entry-point 11 execution (:295-299) and after
  `VerifyFamily` (:301); per avatar: `VMInterestTraits.IsZeroed(a)` →
  `ApplyInitTraits` (raw tree dialect, `VMInterestTraits.cs` :34-56,
  counters {3,3,3,1}) ELSE `VMInterestRandomizer.IsUnsetSentinel(a)` →
  `Apply` (×100 dialect, `VMInterestRandomizer.cs` :45-56 — the ==10 test).
* **Caveat discovered this round**: the runtime lot-load path is
  `TS1GameScreen.BlueprintReset` (`TS1GameScreen.cs` :858-905; no LocalHouse
  FSOV → `VMBlueprintRestoreCmd`), and `VMBlueprintRestoreCmd.cs` :54-56
  now instantiates **`VMTS1ActivatorNew`** — the `VMTS1Activator` line is
  commented out. `VMTS1ActivatorNew` (709 lines) contains NO interest hook
  (zero "Interest" occurrences; its tail :690-709 only spawns controllers
  and recovers queue names; `VerifyFamily` is called by the command at
  `VMBlueprintRestoreCmd.cs` :66). The OLD activator survives only in
  `Client/Simitone/Simitone.Client/Utils/SimitoneNeighOBJExporter.cs` :76.
  So the :314-321 hook is currently reachable only through the exporter,
  not through normal lot loads — worth a follow-up round (either the hook
  must move into VMTS1ActivatorNew / VMBlueprintRestoreCmd, or the New
  activator must call the old one's post-load fixups).
* Fresh family members are spawned by `VMTS1LotState.VerifyFamily`
  (`VMTS1LotState.cs` :30-56) via `CreateObjectInstance` with only
  TS1FamilyNumber set (:48) — their PersonData starts zeroed, which is
  exactly the state the (old) hook's `IsZeroed` arm covers.

### (d) Conclusion — which port writer "should" produce the 10-state?

**None.** The verified background's "creation stores 10s; load
re-randomizes" model is now corrected on both fronts: the engine's gate is
**≤ 10 armed by the ctor/Reset ZERO sweep** (r153-binary-decode.md §VERDICT,
§1.4), and no data file ever stores the 10-state (this survey, tasks 1-3).
The original creation law is "zeros → gate passes → ×100 randomization",
and the port already reproduces its OUTPUT directly
(`MakePersonData` → `VMInterestRandomizer.ApplyTo` writes final ×100
values; the NBRS `AddNeighbor` persists them like the original's
SavePersistentData mirror). Consequences for the port:

1. `VMInterestRandomizer.IsUnsetSentinel` (==10, VMTS1Activator.cs :320 /
   VMInterestRandomizer.cs :45-50) tests a state that no shipped file and
   no engine writer produces — it can only fire for hand-made saves; the
   honest gate is the ≤-10 law (zeros arm it; any value > 10 disarms it).
   Switching `IsUnsetSentinel` to the ≤10 law would also make it agree
   with shipped-data NPC records (all-zero interests) and with script tree
   9761 — but note a ≤10 gate would also fire for tree-written raw sims
   (as in the original engine, where that is precisely the re-randomize
   arm), so the port must keep the IsZeroed-first ordering it already has.
2. The hook's placement bug in (c) matters more than the sentinel: freshly
   spawned family avatars load zeroed on the New-activator path with no
   refill.
3. The ×100 bridge for legacy raw-scale sets exists in shipped data as
   PersonGlobals 9761 'Convert Interests to 0-1000' (§3.3) — if the port
   ever needs to mirror the original's scale migration, that tree is the
   canonical law (≤10 on all eight, then ×=100 on all 15 topics).

---

## 5. Evidence files (tools/iff-dump/r153/)

* `r153-nbrs-scan.py` / `r153-nbrs-dump.txt` — TASK 1: full NBRS decode of
  all 14 candidate files (9 with chunks), every record's pd[46..55] +
  expansion words + nonzero map, gate census (0 all-10 / 22 touching).
* `r153-charscan.py` / `r153-chars-dump.txt` — TASK 2: resyncing character-
  file walker, all chunks + full BHAV 4096/4097/4098/4100 instruction
  decodes + OBJD field dumps for all 20 files (and the all-zero `_`).
* `r153-gamedata-scan.py` / `r153-gamedata-dump.txt` — TASK 3: Global.far
  (all 24 members incl. PersonGlobals/Global), Objects.far People/*,
  EP5 templates, 35 TemplateNPCs, GameData loose: every literal person-data
  write (2,753), interest-word gates (56, all `<= 10`), creation-keyword
  label census. BHAV 9761 and 39200 dumps inline in §3.3 above (raw hex).
* Companion (same round, engine side): `r153-binary-decode.md` +
  `r153-dis-*.txt`, `r153-sentinel-hunt.txt`, `r153-store-inventory.txt`,
  `r153-caller-graph.txt`, `r153-symbols.txt`.

## 6. Residuals (honest)

1. Who invokes PersonGlobals 9761/39200 at runtime (no direct opcode
   callers corpus-wide; 9761 is semi-global range, so callers would live in
   NPC object files' private trees or the engine) — static data cannot
   settle it; an emulating run could.
2. The 'Add Hot Date Interests' bucket arithmetic (ins[2]/[13]-[15] random
   operand shapes) was catalogued but not hand-simulated to value ranges.
3. The XXXX wrapper chunk semantics (cid 0x10 = TREE/TPRP tables,
   cid 0x210 = sig-0x8002 trees) are inferred from content, not from a
   format spec.
4. Whether the maid/thief/repairman's raw words 9..19 (10,8,6,7,5,7,5,6,
   7,10,0) are "interests" of an older layout or decorative is undecided;
   they are reported as stored.

---
**R154 ERRATUM:** the routine this document calls "PersonGlobals.iff BHAV
9761 'Convert Interests to 0-1000'" is **BHAV 8486** (and "BHAV 39200" is
**8345**) — the r154-era ids were byte-swapped reads of the LE rsmp u16s.
The ≤10-gate observation that this document's TASK-3 summary rests on is
unchanged (chunk 8486's ins0 is exactly the decoded ≤10 gate). Also
correction by the same round: the pipeline is NOT orphaned — the five
expansion NPC globals wire 8345+8486 into 'person main loop' (see
../r154/r154-scale-saga-closed.md).
