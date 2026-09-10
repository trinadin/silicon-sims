# R252 pre-work audit: making the port write the ORIGINAL created-record format

Scope: map the port's NBRS / FAMI / person-record write path, prove the read path is
(or is not) compatible with the original created-record format, design the minimal
edit set, and specify an opt-in `recordfmt` autotest that asserts byte-faithful
Original-Format output.

This is a READ-ONLY audit. No production source, autotest pin, or game-data file
was modified. All "original" facts are established directly from
`game-data/The Sims/UserData/` with a standalone Python IFF walker (read-only).

---

## 0. The original created-record canon (re-established from game-data, not the round report)

I re-derived the canon from the shipped `game-data/The Sims/UserData/Neighborhood.iff`
(11657 bytes) and the `Characters/User#####.iff` files, rather than trusting R246's prose.
This matters because R246's prose is slightly imprecise about the name field **case**.

IFF layout (per `IffFile.AddChunk`, `IffFile.cs:226`): header 60-byte ident + 4-byte
resource-map offset, then `[type:4][size:u32 BE][id:2][flags:2][label:64][data]`,
`data` length = `size - 76`. Each chunk's internal fields are the first bytes of `data`.

### NBRS chunk (`type='NBRS' id=0x0001`)

```
data[0x00]  u32 pad            = 0
data[0x04]  u32 internal ver   = 0x3E   <-- "chunk subtype 0x3E" (port writes 0x49)
data[0x08]  4c magic           = "SRBN"
data[0x0C]  u32 declared count = 49
data[0x10]  records...         (each per Neighbour below)
```
(The port's `NGBH` chunk in the same file is also `0x3E`; the port writes `0x49` — see §6.)

### Per-record (`Neighbour`) for a real neighbour

`Unknown1=1` marks a live record; `Unknown1=0` are empty filler slots (the file has 49
declared, 33 live — this is the pre-existing count/record shape, not R252's concern).
For every **live** record the shape is:

```
i32 Unknown1   = 1
i32 Version    = 0x4          (NOT 0xA)
   (Version==0x4 => no Unknown3 field; new record PersonData size = 0xa0 = 80 shorts)
str Name       = "userNNNNN"  -- LOWERCASE 'user' + 5-digit zero-padded id, e.g. "user00023"
                  (odd length 9; the writer's even-length align byte is therefore absent)
i32 MysteryZero= 0
i32 PersonMode = 5            (0 for 'bones'/'templateperson'; 5 for real + user sims)
short[80] PersonData          (0xa0 bytes)
i16 NeighbourID
u32 GUID
i32 UnknownNegOne = -1
i32 relCount + rels
```

PersonData positions used by the original created/user record (confirmed from the walk):

| pd | meaning                | observed | port writes |
|----|------------------------|----------|-------------|
| 2..7 | Nice/Active/Generous/Playful/Outgoing/Neat, 0..1000 | e.g. user00022 = 0/900/0/900/700/0 | same, Generous=0 |
| 9..18 | skills (random)        | 0..1000  | same |
| 29,32,33,36 | Cheats/PersonType/Priority/Autonomy | 1/2/25/50 | same |
| 56,57 | JobType/JobPromotionLevel | -1 / 4 | same |
| 58 | age                    | 9 child / 27 adult | 27 for CAS adult |
| 60 | **skin**               | **{1,2,3}** | **{0,1,2}** |
| 61 | family id (TS1FamilyNumber) | family chunk id | family chunk id |
| 63 | JobPerformance         | 0 | 0 |
| 65 | gender                 | 0/1 | 0/1 |
| 70 | zodiac                 | 0 (never persisted) | 0 |
| 67,68 | LingeringHouseNumber / IsGhost | contextual 5/1 or 0/0 | 0/0 (port leaves 0) |

### Skin encoding — verified by cross-referencing the character files

I decoded the per-member `STR# 200` slot-14 skin token and compared it to the same
sim's NBRS `pd[60]`:

| character file | STR#200[14] skin | NBRS pd[60] |
|----------------|------------------|-------------|
| User00022.iff | `drk`            | 2           |
| User00023.iff | `med`            | 3           |
| User00015.iff | `lgt`            | 1           |
| User00017.iff | `med`            | 3           |

=> **original skin encoding: lgt=1, med=3, drk=2** (non-monotonic). R246's canon is
confirmed correct. (`AppearanceType` = Light=0/Medium=1/Dark=2, `tso.vitaboy.model/AppearanceType.cs:6`.)

### FAMI chunk

All six FAMI chunks in the shipped file have internal version **7** and **no trailing
bytes** after the member GUIDs:

```
data[0x00] u32 pad = 0
data[0x04] u32 internal version = 7
data[0x08] 4c magic = "IMAF"
           i32 HouseNumber, FamilyNumber, Budget, ValueInArch, FamilyFriends, Unknown
           i32 guidCount
           u32 guid[guidCount]
           <EXACTLY ENDS HERE — no trailing int32s>
```
E.g. the 1-... family (`id=0x0001`, 2 guids) payload = 40 + 4*2 = 48 bytes; the 3-guid
family (`id=0x0005`) = 40 + 4*3 = 52 bytes. Confirmed no trailing zeros.

### Name field: the one R246 prose imprecision

R246 says original `Name = character-file stem (e.g. "User00024")`. The actual NBRS
`Name` is **lowercase** `userNNNNN` — the same prefix the port already writes into the
OBJD label as `"user" + userid.PadLeft(5,'0') + " - " + Name`
(`SimitoneNeighbourGenerator.cs:35`). The **file** on disk is `User00024.iff` (capital
`U`, `TS1NeighbourProvider.cs:216`), but the NBRS `Name` is `user00024`. The port's own
casflow already derives the lowercase stem at `AutotestCASFlow.cs:881-882`
(`stem.ToLowerInvariant()`). So the canonical NBRS `Name` == `Path.GetFileNameWithoutExtension(charFile).ToLowerInvariant()`.

---

## 1. Write-path map

Every place the port WRITES an NBRS record and a FAMI chunk.

### 1a. NBRS chunk writer — `FreeSO/TSOClient/tso.files/Formats/IFF/Chunks/NBRS.cs`

- `NBRS.Write` (`NBRS.cs:60`): `io.WriteUInt32(0x49)` at `:65` writes the **chunk
  internal version** (the port's diverging `0x49`); magic `"SRBN"` `:66`; declared
  count = `Entries.Count` `:73`; then `NeighbourByID.Values` → `n.Save(io)` `:74-77`.
  This is the pre-existing count/record mismatch documented at `:67-73` (r248 P2-4),
  **not** R252's concern.
- `Neighbour.Save` (`NBRS.cs:207`): `WriteInt32(Unknown1)` `:209`; `WriteInt32(Version)`
  `:210`; `if (Version==0xA) WriteInt32(Unknown3)` `:211`; `WriteNullTerminatedString(Name)`
  `:212`; even-length align byte `:213`; `WriteInt32(MysteryZero)` `:214`;
  `WriteInt32(PersonMode)` `:215`; person data `:216-231` — **size selected by
  `Version`**: `(Version==0x4) ? 0xa0 : 0x200` `:218`; the 0x200 branch emits up to 256
  shorts, writing `PersonData[pdi++]` until `pdi>=88` then zeros (`:220-231`); then
  `NeighbourID` `:233`, `GUID` `:234`, `UnknownNegOne` `:235`, relationships `:237-247`.

### 1b. FAMI writer — `FAMI.cs`

- `FAMI.Write` (`FAMI.cs:79`): `io.WriteUInt32(9)` at `:84` (hardcoded — **ignores the
  `Version` field** at `:13`); `"IMAF"` `:85`; six field ints `:86-91`; guidCount +
  guids `:92-94`; **4 × `WriteInt32(0)` `:96-97`** (the "16 trailing zeros").

### 1c. The caller that builds the created family — `TS1CASScreen.cs` + `SimitoneNeighbourGenerator.cs`

CAS → generator boundary:
- `TS1CASScreen.BuildMember` (`TS1CASScreen.cs:1209`) builds a `CASFamilyMember`:
  `Personality` = `short[5]` in display order [Neat, Outgoing, Active, Playful, Nice] ×100
  (`:1220`/`:1226`); `SkinColor = CurrentSkin` (the skin string) `:1239`; `Gender` `:1237`.
- `TS1CASScreen.CASToNeighGen` (`:1134`) creates `SimTemplateCreateInfo(code, x.SkinColor)`
  `:1141` and maps the 5-slot UI vector into the 6-slot generator field
  `[Nice,Active,Generous,Playful,Outgoing,Neat]` with **Generous=0** `:1155-1162` (the
  R246 crash fix).
- `TS1CASScreen.SaveFamily` (`:1186`) →
  `SimitoneNeighbourGenerator.CreateFamily(lastName, count, WIPFamily.Select(CASToNeighGen).ToArray())`
  `:1190`.

Generator (`FreeSO/TSOClient/tso.simantics/Utils/SimitoneNeighbourGenerator.cs`):
- `CreateFamily(name,count,infos)` (`:68`): per member — `info.FamilyID = fami.ChunkID`
  `:75`; `PrepareTemplatePerson(guid, info)` `:76`; **`AddNeighbor(guid, 9, info.MakePersonData())`
  `:77`** (PersonMode literal `9`); then `SaveNeighbourhood(true)` `:79`.
- `CreateNeighbor` (`:83`): `PrepareTemplatePerson` `:85`; **`AddNeighbor(guid, 9, info.MakePersonData())`
  `:86`** (also PersonMode `9`).
- `CreateFamily(name,count)` (`:89`): constructs the `FAMI { ChunkID=newID, FamilyNumber=max+1,
  Unknown=24, Budget=20000 }` `:107-121` and a `FAMs` chunk `:123-133`.
- `AddNeighbor` (`:155`): builds `new Neighbour { Name="iffname", NeighbourID=newID,
  GUID=guid, Relationships=..., PersonMode=personMode, PersonData=personData }` `:168-176`.
  **Note `Version` is not assigned** — it stays the class default `0xA` (`NBRS.cs:129`),
  which is what selects the 256-short record size.
- `PrepareTemplatePerson` (`:26`): `userid = neigh.NextSim` `:30`; OBJ label
  `"user"+userid.PadLeft(5,'0')+" - "+Name` `:35`; writes CTSS/STR200 `:37-61`;
  `neigh.SaveNewNeighbour(tempObj)` `:63`.
- `SimTemplateCreateInfo` (`:183`): `PersonalityPoints = short[6]` `:193`; constructor
  `(code,skin)` maps `lgt→0, med→1, drk→2` `:226-234` (`SkinTone`); the pet constructor
  sets `SkinTone=0` `:249`.
- `MakePersonData` (`:252`): returns `short[88]` `:254`; sets `pd[2..7]` `:257-262`;
  `pd[36]=50` `:275`; `pd[56]` `:276`; `pd[57]=4` `:277`; `pd[63]=0` `:278`; skills
  `pd[9..18]` `:285-291`; `pd[33]=25` `:293`; `pd[29]=1` `:297`; `pd[32]=2` `:298`;
  interests `:309`; **`pd[58]=age` `:311`; `pd[60]=SkinTone` `:312`; `pd[61]=FamilyID`
  `:313`; `pd[65]=Gender` `:314`.**

The alternate caller `VMTS1MakeNewCharacter` (`Primitives/VMTS1MakeNewCharacter.cs`)
goes through the **same** `CreateNeighbor`→`AddNeighbor(guid, 9, ...)` path (`:125`) and
sets `PersonalityPoints[i]=1000` `:122-123`.

**Other write points for the character filename**: `TS1NeighbourProvider.SaveNewNeighbour`
(`:211`) writes `"User"+(NextSim++).PadLeft(5,'0')+".iff"` `:216`; `SaveNeighbourhood`
(`:333`) writes `Neighborhood.iff` at `:350-351`.

### 1d. The format constants the port currently writes

| thing                | current value | where                    | original |
|----------------------|---------------|--------------------------|----------|
| NBRS chunk internal version  | `0x49`        | NBRS.cs:65 (hardcoded)   | `0x3E`   |
| Neighbour record Version     | `0xA` (class default, not set by AddNeighbor) | NBRS.cs:129; Save :210 | `0x4` |
| record PersonData size       | `0x200` (256 shorts) | NBRS.cs:218 (selected by Version==0xA) | `0xa0` (80 shorts) |
| PersonMode                  | `9` (literal in AddNeighbor callers) | SimitoneNeighbourGenerator.cs:77/:86 | `5`   |
| Name string                 | literal `"iffname"` | SimitoneNeighbourGenerator.cs:170 | file stem `userNNNNN` |
| skin pd[60]                 | `0/1/2` (lgt/med/drk) | SimitoneNeighbourGenerator.cs:312 + :226-234 | `1/3/2` |
| FAMI internal version       | `9` (hardcoded) | FAMI.cs:84 + :13 | `7` |
| FAMI trailing zeros         | 4×i32 zero    | FAMI.cs:96-97             | none     |

---

## 2. Read-path map + compatibility verdict

The reader is `NBRS.Read` (`NBRS.cs:29`) plus the `Neighbour` ctor (`:148`) and
`FAMI.Read` (`FAMI.cs:49`).

### 2a. NBRS chunk read (`:31-49`)
Reads pad, `Version` (`:34`, no validation — accepts any value), magic `SRBN`, count.
**Does not branch on the chunk version**, so it accepts both `0x49` (port) and `0x3E`
(original).

### 2b. Neighbour record read (`:148-200`)
- `Unknown1 != 1` → early return `:151` (empty slot).
- `Version` `:152`; **if `Version==0xA`** reads `Unknown3` `:153-158`, else skips it.
- `Name = io.ReadNullTerminatedString()` `:159` (+ even-length align `:160`) — reads any
  name value, so both `"iffname"` and `"user00024"` parse.
- `PersonMode` `:163`; **if `PersonMode > 0`** reads person data, size
  `(Version==0x4)?0xa0:0x200` `:166`, into `PersonData = new short[88]` `:167`, filling
  the first 80 shorts (Version 4) or first 88 then skipping (Version 0xA) `:169-177`.
- `NeighbourID`/`GUID`/`UnknownNegOne`/relationships `:180-199`.

**Verdict for parsing (in-memory model):** the reader is already **flexible** — it
branches on `Version` and accepts both `0x4` (80-short, original) and `0xA` (256-short,
port). It does **not** hardcode the port's chunk version, PersonMode, or name. So a
port that switches its writer to the original format will still **parse** its own new
records.

### 2c. FAMI read (`:49-77`)
Reads pad, `Version` (`:54`, no validation → accepts 7 and 9), `IMAF`, the six fields,
guidCount + guids, then **tries** to read 4 trailing int32 in a `try/catch`
(`:68-75`) — so it tolerates both a FAMI with and without trailing bytes. **Verdict:
FAMI reader is flexible; a v7 no-trailing-zeros FAMI parses fine.**

### 2d. The one read-side CONSUMER that breaks — skin
`VMTS1ActivatorNew.cs:467`:
```csharp
SkinTone = (AppearanceType)person.PersonData[(int)VMPersonDataVariable.SkinColor]
```
`AppearanceType` = `{Light=0, Medium=1, Dark=2}` (`tso.vitaboy.model/AppearanceType.cs:6`).
If `pd[60]` holds the original disk encoding `{1,2,3}`, this cast maps lgt(1)→Medium
(wrong), drk(2)→Dark (coincidentally right), med(3)→out-of-range (garbage). This is the
**only** code path that reads `PersonData[SkinColor]` into an `AppearanceType`
(verified by grep; the other SkinTone sites are network persist/avatar fields, not
NBRS reads). So the **parse** is compatible but the **spawn** of an original-format
record is not, unless this consumer remaps.

### 2e. Also: NBRS.Name consumers
The only `Neighbour.Name` consumers surface it as a tooltip in the relationship panel
(`Client/Simitone/Simitone.Client/UI/Panels/LiveSubpanels/UIRelationshipSubpanel.cs:647`
`Tooltip = neighbor?.Name ?? "";`) and in autotest logs
(`AutotestRunner.cs:5928`/`:5982`). Changing `Name` from `"iffname"` to `userNNNNN`
changes that tooltip to the file stem (matching the original's stored data). No crash,
no data loss — a cosmetic move toward canon.

### 2f. Round-trip / no-data-loss verdict
- `Version` is **already** a field on `Neighbour` and `Save`/`Read` both select the
  record size from it. So a record loaded as `Version 0x4` re-saves as `0x4` (80
  shorts). Good.
- PersonData indices: the port's `MakePersonData` only writes indices ≤ 70
  (`SimitoneNeighbourGenerator.cs:257-314`), and no code reads `PersonData[i]` for `i>70`
  on created records (grep of `PersonData[N]` literals maxes at 70). So dropping the
  88→80 data is **lossless for created records** (pd[80..87] are always 0 for a fresh
  CAS family). Caveat: `VMPersonDataVariable` defines TSO-runtime slots ≥ 81
  (SkillLockBody=81..DeathTicker=87, etc.) that the **runtime avatar** array may carry;
  these are not persisted into NBRS created records and are unchanged by R252.
- FAMI round-trip: `FAMI.Write` currently hardcodes `9` regardless of the loaded
  `Version`, and always emits 16 trailing zero bytes. So a loaded v7 FAMI would be
  re-saved as v9 + zeros unless the writer is made to honour `Version`/drop the zeros
  (§3).

---

## 3. Backward- and forward-compatibility analysis

Question: if the writer switches to the original format, does the READ path still load
**both** old port records and new original-format records?

| record shape (disk) | parsed by port reader? | spawn/consumer | 
|---------------------|------------------------|----------------|
| old port: chunk 0x49, rec 0xA/256 shorts, PersonMode 9, Name "iffname", skin 0/1/2 | **Yes** — Version 0xA branch, Unknown3 read, 256→88 then skip, Name any, PersonMode 9>0. Old saves keep loading. | skin 0/1/2 → `(AppearanceType)` works (unchanged on-disk skin value). Backward-compatible. |
| new original: chunk 0x3E, rec 0x4/80 shorts, PersonMode 5, Name stem, skin 1/2/3 | **Yes** — Version 0x4 branch (no Unknown3), 80 shorts into [0..79], Name any, PersonMode 5>0. **Parses fine.** | **Breaks** at `VMTS1ActivatorNew.cs:467` (skin cast). Needs a remap helper. |

So the verdict: **read-compatible at the parse layer, forward-compatible to the original
bytes, with exactly one consumer-level edit (skin remap at `VMTS1ActivatorNew.cs:467`).**
Nothing else in the reader needs to change.

Name: the reader is value-agnostic; both formats parse. skin: the array is the disk
value; if we remap in the array we destroy round-trip, so remap **only** at the
consumer. size: reader uses `Version` to pick 80 vs 256; no change needed.

---

## 4. Risks in switching to the original format (honest assessment)

1. **P0 — skin encoding is non-monotonic and coupled at both boundaries.** lgt=1, med=3,
   drk=2. The writer must emit `1/3/2`, and `VMTS1ActivatorNew.cs:467` must map
   `1→Light, 3→Medium, 2→Dark`. If the implementer only changes the writer and forgets
   the consumer, spawned sims get the wrong skin (lgt renders as Medium, med as garbage).
2. **P1 — runtime changes can clobber the disk encoding.** `VMNetAvatarPersistState`
   (`tso.simantics/NetPlay/Model/VMNetAvatarPersistState.cs:217`) writes
   `avatar.SetPersonData(SkinColor, SkinTone)` where `SkinTone` is an `AppearanceType`
   `0/1/2`. Any runtime skin change on a spawned sim overwrites `pd[60]` with `0/1/2`,
   so a later save would emit `0/1/2`, not the original `1/2/3`. The recordfmt fixture
   uses a static CAS-created family (no runtime skin change), so it is unaffected; but a
   future "modify an imported sim and re-save" path could round-trip skin to the wrong
   encoding. Flag for a later round: centralise disk↔logical skin so runtime writes go
   through the same mapping.
3. **P2 — NBRS chunk `0x49`→`0x3E` and the `NGBH` sibling.** The shipped file's `NGBH`
   is also `0x3E`; the port's `NGBH.Write` (`NGBH.cs:95`) writes `0x49`. Full byte-parity
   of `Neighborhood.iff` also needs the NBRS **and** NGBH chunk versions set to `0x3E`.
   R252's stated scope is NBRS/FAMI/person, so NGBH is noted as an adjacent residual:
   `recordfmt` does not pin it, but a "whole-neighborhood byte-identical" goal would.
4. **P2 — pd[67]/pd[68].** Original user records carry `LingeringHouseNumber`/`IsGhost`
   as `5/1` for moved-in sims, `0/0` for others. The port leaves them 0. R246's canon
   pin set does **not** include them; the port's ucasflow family is created without
   moving into a house, so `0/0` is acceptable. Note as a residual, not a blocker.
5. **P3 — `FAMI.Unknown=24` vs original.** The port writes `Unknown=24` (`CreateFamily`
   `:118`); the shipped original created families carry `Unknown=1`
   (e.g. FAMI id=0x0005). R246 documents this as a port-chosen constant. The `recordfmt`
   FAMI assertions are scoped to **version 7 + no trailing zeros**, not the Unknown value;
   flag as out-of-scope.
6. **P3 — name display.** Relationship tooltips show `userNNNNN` instead of `"iffname"`.
   Cosmetic; the original stores the stem, so this is a fix toward canon.

---

## 5. Recommended minimal edit set

Do **not** change the reader's parsing (§2 shows it is already flexible). Change these
writers and one consumer.

**A. `NBRS.cs`**
- `:65` — write the chunk internal version from a named const / the `Version` field
  instead of the literal `0x49`. If using the `Version` field (loaded from the pristine
  file it is already `0x3E`), this makes the whole neighborhood round-trip byte-faithful.
  (For a brand-new NBRS chunk the field would be 0; the created-family path always loads
  an existing chunk, so it is safe, and a `default:0x3E` can be added if desired.)
- Add named constants, e.g. on `NBRS`/`Neighbour`:
  `ORIGINAL_CHUNK_VERSION = 0x3E`, `RECORD_VERSION = 0x4`, `RECORD_SHORTS = 0xa0/2 = 80`,
  `PERSON_MODE = 5`, and skin codes `SKIN_LIGHT=1, SKIN_MEDIUM=3, SKIN_DARK=2`.
- `Neighbour.Save` needs **no** structural change — it already writes `Version`,
  `PersonMode`, `Name` and selects the size from `Version`. Setting `Version=0x4` and
  `PersonMode=5` in the creator is enough.

**B. `FAMI.cs`**
- `:13` `Version` default `0x9` → `0x7`.
- `:84` write `Version` (or the `0x7` const) rather than the hardcoded `9` (makes the
  loaded-v7 round-trip work).
- `:96-97` remove the 4 `WriteInt32(0)` trailing zeros.

**C. `SimitoneNeighbourGenerator.cs`**
- `AddNeighbor` (`:155`) signature → `AddNeighbor(uint guid, int personMode, short[] personData, string name)`.
  Set `Name = name` instead of literal `"iffname"` (`:170`), and set `Version = NBRS.RECORD_VERSION`
  (0x4) on the new `Neighbour`.
- `CreateFamily` (`:77`) and `CreateNeighbor` (`:86`) → pass `PERSON_MODE=5` and the
  computed stem as the `name` argument.
- `PrepareTemplatePerson` (`:26`): capture `userid = neigh.NextSim` and set a new field
  `info.CharStem = "user" + userid.ToString().PadLeft(5, '0')` so the stem is available
  to the caller for `AddNeighbor` (it is computed before `NextSim++` in `SaveNewNeighbour`).
- `SimTemplateCreateInfo`: add `public string CharStem;`. Keep `SkinTone` as the logical
  `lgt=0/med=1/drk=2` (used by pets too), OR change it to encode the disk value — either
  works; the key is `MakePersonData` must emit the disk encoding `{1,3,2}`.
- `MakePersonData` (`:312`): `pd[60] = SkinToDisk(SkinTone)` where
  `SkinToDisk(0)=1, SkinToDisk(1)=3, SkinToDisk(2)=2`.

**D. consumer (`VMTS1ActivatorNew.cs:467`)**
- Replace `(AppearanceType)person.PersonData[60]` with a `DiskSkinToAppearance(pd60)`
  mapping `1→Light, 3→Medium, 2→Dark`. This is required for spawned sims to render
  correctly and must **not** mutate the stored array (so round-trip keeps the disk value).
  Put the helper next to `SkinToDisk` so write and read use the same pair.

**E. ucasflow pins that assert the current divergent values** (must be updated, not
weakened) — `AutotestCASFlow.cs:829` `personmode-9` → `5`;
`:830` `nbrs-name-iffname` → stem; `:832` `persondata-88` (keep, but note in-memory array is
88 with 80 real); `:844` `pd60-skin-port-encoding` → the disk encoding (1 for the lgt
fixture). And `FAMI` version/trailing-zero assertions added. These are opt-in pins
asserting the *old port behavior*; updating them to the canon is strengthening, and the
default suite (`Config.Checks` at `AutotestRunner.cs:76`) is untouched.

---

## 6. `recordfmt` fixture plan (opt-in autotest)

Name the check `recordfmt`. It must be OPT-IN: **do not add it to the default
`Config.Checks` string** (`AutotestRunner.cs:76`), which stays byte-identical.

### Registration / plumbing
- `AutotestRunner.StateWaitNeigh` (`AutotestRunner.cs:351`) currently enters the CAS flow
  when `CheckEnabled("ucasflow") || CheckEnabled("uicasflow")`. Add `|| CheckEnabled("recordfmt")`
  so a focused `-autotest-opts recordfmt` reaches the CAS drive. (Keep the canonical log
  line naming `ucasflow`.)
- `AutotestCASFlow.BeginIsolation` (`AutotestCASFlow.cs:77-84`): the enable test at `:84`
  checks `ucasflow`/`uicasflow`. Add `|| names.Contains("recordfmt")` so the temp-dir
  redirect happens for `recordfmt` too. This reuses R246's isolation idiom directly:
  `FSOEnvironment.UserDir` is redirected into a fresh temp dir **before** `Content.Init`
  (`:88-91`), the pristine UserData clone lands there, and the game-data
  `Neighborhood.iff` is pinned byte-identical by `FinishIsolationChecks`
  (`AutotestCASFlow.cs:936-961`) — all reusable as-is.
- **Reuse the same save drive:** `recordfmt` does **not** need its own CAS driver. It
  piggybacks on `AutotestCASFlow` (which already drives the real `SaveFamily` path —
  `AutotestCASFlow.cs:672` "REAL SaveFamily ... CreateFamily"). Add a
  `_recordFmt = CheckEnabled("recordfmt")` flag inside `AutotestCASFlow` and, when set,
  run the original-format assertions in the existing `PhaseDisk` (`:762`).
- Verdict hook: in `AutotestRunner.StateCASFlow` (`:585-593`), after the ucasflow
  verdict, when `recordfmt` was enabled also `if (recordFmtFailed==0) Pass("recordfmt") else Fail("recordfmt")`.
  (Expose the recordfmt failures separately, or fold the original-format `Check`s into the
  flow's own `Failed` count — either is acceptable; the task requires a `recordfmt`
  verdict line, so surface it as its own `Pass`/`Fail`.)

### Assertions (run on a fresh `new IffFile(neighPath)` and the fresh character file)

**NBRS — the written record must be the ORIGINAL format:**
- `iff.List<NBRS>()?.FirstOrDefault()` != null → `nbrs.Version == 0x3E` (chunk subtype).
- member record found by the new family GUID:
  - `member.Version == 0x4` (record Version → selects 80 shorts; the strongest codec-level
    byte-format assertion). Optionally also raw-scan the NBRS chunk bytes
    (re-open `Neighborhood.iff` raw like `TS1NeighbourProvider.PatchHouseSimiGlobal`
    `:376`, or set `RetainChunkData`) to confirm the member's PersonData region is exactly
    `0xa0` bytes.
  - `member.PersonMode == 5`.
  - `member.Name == stem` where `stem = Path.GetFileNameWithoutExtension(charFile).ToLowerInvariant()`
    (e.g. `user00024`) — i.e. equal to the OBJD label prefix, **not** `"iffname"`.
  - `member.PersonData.Length >= 88` (reader allocates 88; 80 real) — and for the
    bytes-faithful size, the raw record is 80 shorts.
  - personality on the 0..1000 scale with **Generous=0**: `pd[2..7]` match the chosen
    slider vector (same mapping ucasflow pins at `AutotestCASFlow.cs:837-842`, e.g.
    `pd[2]=Nice*100, pd[3]=Active*100, pd[4]=0, pd[5]=Playful*100, pd[6]=Outgoing*100,
    pd[7]=Neat*100`).
  - `pd[58]==27` (adult), `pd[65]==0` (male) if the fixture's chosen record is the R246
    adult-male-lgt sim.
  - **`pd[60] == skinDisk`** matching the chosen CAS skin — for the R246 fixture (skin
    `"lgt"`, `AutotestCASFlow.cs:524/529`) that is `1`. Cross-check the map: the fixture's
    `STR#200[14]` ("lgt") must agree (lgt→1).
  - `pd[61] == (short)newFam.ChunkID` (family id), `pd[70] == 0` (zodiac absent).
  - skills/interests within `[0,1000)` (presence/range only — skills are random by design).

**FAMI — the written FAMI must be Version 7, no trailing zeros:**
- `fami.Version == 7` (the new FAMI, `newFami`).
- No trailing bytes: raw-scan the new FAMI chunk's data length and assert it equals
  `40 + 4*guidCount` (for the 1-member fixture = 44 bytes), i.e. no 16-byte trailer.
  (The parsed `FAMI` doesn't expose the disk size; use a raw byte scan of
  `Neighborhood.iff`. Keep the existing ucasflow FAMI assertions — Budget 20000,
  HouseNumber 0, FamilyNumber max+1, unique GUID, unknown-24 documented — unchanged.)

**Round-trip / no-data-loss:**
- The fresh re-parse **is** the reload. Additionally, re-write the freshly parsed
  `IffFile` back out and re-parse again (a second round-trip) and assert
  `member.Version==0x4`, `PersonMode==5`, `pd[60]==skinDisk`, all fields equal — proving
  write→read→write is lossless. (This also catches an accidental in-array skin remap:
  if the read path mutated `pd[60]` to `0/1/2`, this round-trip would fail.)

**Isolation / integrity (reuse R246):** `BeginIsolation` redirect; the
game-data `Neighborhood.iff` SHA-256 + mtime untouched
(`AutotestCASFlow.cs:936-961`). Evidence: copy the written `Neighborhood.iff` +
`User#####.iff` into the round's evidence dir with SHA-256, like
`AutotestCASFlow.PhaseEvidence` (`:963-988`).

---

## 7. Cross-agent contract seam

The implementation build agent should expose/use these exact signatures and constants:

```csharp
// NBRS.cs
public static class NbrsFormat {
    public const uint CHUNK_VERSION       = 0x3E;   // was 0x49
    public const int  RECORD_VERSION      = 0x4;    // was 0xA
    public const int  RECORD_SHORTS       = 0xa0 / 2; // 80
    public const int  PERSON_MODE         = 5;      // was 9
    public const short SKIN_LIGHT = 1, SKIN_MEDIUM = 3, SKIN_DARK = 2; // disk encoding
}
public static short LogicalSkinToDisk(short logical)      // 0->1, 1->3, 2->2
public static AppearanceType DiskSkinToAppearance(short disk) // 1->Light, 3->Medium, 2->Dark
```

- `NBRS.Write` writes `NbrsFormat.CHUNK_VERSION` (or the chunk's `Version` field) instead
  of literal `0x49`.
- `FAMI.Write` writes `Version` (default `0x7`) instead of literal `9`; no trailing zeros.
- `SimitoneNeighbourGenerator.AddNeighbor(uint guid, int personMode, short[] personData, string name)`
  sets `Version = NbrsFormat.RECORD_VERSION; Name = name; PersonMode = personMode;`
- `CreateFamily`/`CreateNeighbor` pass `NbrsFormat.PERSON_MODE` and the stem:
  `AddNeighbor(guid, NbrsFormat.PERSON_MODE, info.MakePersonData(), info.CharStem)`
- `SimTemplateCreateInfo.CharStem` is set in `PrepareTemplatePerson` from `userid`
  (`"user" + userid.ToString().PadLeft(5, '0')`).
- `MakePersonData` writes `pd[60] = LogicalSkinToDisk(SkinTone)`.
- `VMTS1ActivatorNew.cs:467` uses `DiskSkinToAppearance(pd[60])`.

The `recordfmt` autotest calls the same `AutotestCASFlow` drive (no new drive code) and
asserts the §6 list.

---

## 8. Out-of-scope residuals (not fixed by R252, recorded honestly)

- **`rsmp` drop** on `SaveNeighbourhood` (documented at `AutotestCASFlow.cs:774-781`) —
  unchanged.
- **NBRS count/record mismatch** (`NBRS.cs:67-73`, r248 P2-4) — unchanged.
- **NGBH chunk version `0x49` vs `0x3E`** (`NGBH.cs:95`) — adjacent divergence; not in the
  NBRS/FAMI/person scope. Needed only for whole-file byte parity.
- **`FAMI.Unknown=24` vs original `1`** (`SimitoneNeighbourGenerator.cs:118`) — documented
  port constant; not asserted by `recordfmt`.
- **pd[67]/pd[68]** (`LingeringHouseNumber`/`IsGhost`) original writes `5/1` or `0/0`
  contextually; the port leaves `0/0`, acceptable for an un-moved-in family; not pinned.
- **Skin runtime clobber** (`VMNetAvatarPersistState.cs:217` writes `AppearanceType` into
  `pd[60]`) — see §4 risk 2; recordfmt doesn't exercise it.

**One honest caveat reiterated:** changing NBRS 256→80 shorts is lossless **only** because
the port never stores meaningful created-family data in `pd[80..87]` (MakePersonData
writes ≤70, and all other consumers read ≤70 on created records). The TSO-runtime
`VMPersonDataVariable` slots ≥81 are not persisted into NBRS created records. If a future
feature starts storing `pd[81..87]` (skill locks, etc.) into NBRS it would not survive an
original-format save — but that is not the current state, so the 80-short change is safe today.
