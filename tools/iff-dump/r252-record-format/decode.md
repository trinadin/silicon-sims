# R252 — ORIGINAL created-record save format (Pinned)

Round of the CAS-fork decode. We pin the ORIGINAL Simitone/The Sims 1
created-record byte format so the port can write a `FAMI`/`FAMs`/`NBRS` +
`Characters/UserNNNNN.iff` family run that is byte-faithful to what the
original PowerPC engine writes.

Everything below was read **read-only** from the staged original data under
`game-data/The Sims/` and the original binary under `game-data/The Sims/The Sims Complete`
(SHA-256 `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`).
The port codecs (`NBRS.cs`, `FAMI.cs`, `SimitoneNeighbourGenerator.cs`,
`VMInterestRandomizer.cs`) were read to expose **where the port diverges**; they
are the PORT, not the canon.

Supersedes/confirms R246's claims. Where R246 was right we say so and give the
byte evidence; where it was imprecise we correct it.

---

## 0. The container (Maxis "IFF FILE 2.5")

`Neighborhood.iff` is a **Maxis IFF**, not a FORM IFF. Layout:

```
0x00  60 bytes  'IFF FILE 2.5:TYPE FOLLOWED BY SIZE\x00 JAMIE DOORNBOS & MAXIS 1'
0x3C  4 bytes   rsmp offset (big-endian)   = 0x2296 in the staged file (points at the rsmp chunk)
0x40  ...       chunks
```

Chunk header (**big-endian** fields):

```
offset  size  field
0x00    4     chunk type 4CC   (e.g. 'NBRS', 'FAMI', 'FAMs', 'NGBH')
0x04    4     chunk size — TOTAL including the 76-byte header below
0x08    2     chunk ID
0x0a    2     chunk flags
0x0c    64    chunk label (NUL-padded)
0x4c    ...   chunk data = (chunk size - 76) bytes
```

There is **no** even-alignment pad byte after a chunk. The next chunk starts at
`chunk_start + chunk_size`. (Verified: every chunk boundary lines up and the walk
ends exactly at EOF.) The port's `IffFile.AddChunk` reads `chunkSize - 76` data
bytes and does not skip a pad — consistent.

---

## 1. Structure of the staged original save (`UserData/Neighborhood.iff`)

Chunk table (file offset, type, size, id):

```
0x40    NGBH  id=1
0xb8    NBRS  id=1   size 6810  (data 6734 @ 0x104)     <-- the neighbour/person records
0x1b52  FAMI  id=4   data 56
0x1bd6  XXXX  id=4
0x1c32  FAMI  id=3   data 48
0x1cae  XXXX  id=3
0x1d09  FAMI  id=2   data 44
0x1d81  XXXX  id=2
0x1ddd  FAMI  id=5   data 52
0x1e5d  XXXX  id=5
0x1eb5  FAMI  id=1   data 48
0x1f8b  FAMI  id=0   data 40
0x205a  TATT
0x20bd  FAMs  id=2
0x21ac  FAMs  id=4
0x2296  rsmp  id=0  (== the header's rsmp offset 0x2296)
...
```

Six families (FAMI chunk ids 0..5). The FAMI **chunk id** is the in-game family
number and equals NBRS `pd[61]`. The FAMI **FamilyNumber** field is a separate
auto-increment UID (9,8,7,6,5,-1), as the port's `FAMI.cs` comment describes.

The `FAMs` chunks carry the family-scope member list. (R246's "one FAMI + one FAMs"
is this shape; the staged neighborhood happens to hold 6 such families.)

---

## 2. NBRS record format — the core

### 2.1 Chunk-level header (chunk data, little-endian)

```
data+0x00  4  pad          = 0
data+0x04  4  Version      = 0x3E  (UserData) / 0x3F (UserData2..8)   <-- the "0x3E/0x3F"
data+0x08  4  magic        = 'SRBN'   (the 4CC 'NBRS' reversed, stored little-endian)
data+0x0c  4  record count = 49
data+0x10  ... records
```

**PROVEN** (staged bytes @0x108: `3e 00 00 00`; @0x10c: `53 52 42 4e`; @0x110:
`31 00 00 00` = 49).

R246's "chunk 0x3E/0x3F" is exactly this **chunk-data Version field**. There are
two save generations: the owner's staged `UserData` is **0x3E**, and the
`UserData2`–`UserData8` template neighborhoods are **0x3F**. The record format is
**identical** across both (only the version field differs).

> If the port writes `0x49` (as `NBRS.Write` does), a native engine load
> desyncs: the record header is read at a different size/layout.

### 2.2 Record layout (the per-neighbour record)

```
field            offset  type  original value
Unknown1          0x00    int32 1   (1 = valid slot; 0 = empty 4-byte slot)
Version           0x04    int32 4   (0x4)   <-- R246 "record Version 4"
[Unknown3         0x08    int32 9]  (ONLY when Version==0xA — absent in original 0x4)
Name              0x08    NUL-terminated string, padded to even length
MysteryZero       ...     int32 0
PersonMode        ...     int32 5   (0 for bodiless NPCs: bones, templateperson)
PersonData        ...     int32-length? No: PersonMode>0 => size bytes of int16
NeighbourID       ...     int16
GUID              ...     uint32
UnknownNegOne     ...     int32 -1
Relationships     ...     int32 count of { keycount(1), key, valuecount, int32[] }
```

Key facts, all **PROVEN** from bytes:

* **Version 4** on every valid record. The port writes **0xA** (+ `Unknown3=9`).
* **PersonMode = 5** for every record that carries person data; **0** for the two
  bodiless reference NPCs (`bones`, `templateperson`). The port writes PersonMode 9.
* **PersonData size = 0xa0 bytes = 80 shorts** when PersonMode>0 (Version 4).
  R246's "80 shorts" is exact. The port writes **0x200 (256 shorts)** for 0xA.
* **Name = the character-file stem**: lowercase `userNNNNN` (5 digits,
  zero-padded) or an NPC stem (`maid`, `officer`, `thief`, …). The matching file is
  `UserNNNNN.iff` (capital `U`, same digits). R246's "Name = character-file stem"
  is exact; the port's `"iffname"` is a divergence. Proven: every one of the 20
  `userNNNNN` names has a `Characters/UserNNNNN.iff` file, and no name is `iffname`.
* The name string is **NUL-terminated and padded to even** length: a 4-char name +
  NUL is followed by 1 pad byte (e.g. `'maid\0\xa3'` — the pad byte is whatever
  follows; the port conditionally reads 1 pad byte when `Name.Length % 2 == 0`).
  A 5-char name (`'maid'`-style even name gets a pad; an odd-length name does not).

The whole record stream parses to **exactly** the NBRS data size (6734 → 6734),
which is what makes this a trustworthy field map (no gaps, no overlap).

### 2.3 Person-data field map (the 80 shorts, `pd[0]` = first int16)

Person-data offsets use the `VMPersonDataVariable` enum. **PROVEN** via three
independent routes that agree:
(i) the raw staged values, (ii) the port's `VMPersonDataVariable` enum, and
(iii) the original binary routine `Neighbor::GetPersistentDataFields`
(`GetPersistentDataFields__8NeighborFi`) which emits exactly these field indices —
see `getpersistentdatafields-disasm.txt`.

| pd[] | meaning | original stored value | port writes | delta |
|-----|---------|----------------------|-------------|-------|
| 2  | NicePersonality     | 0..1000 (0..700 seen) | PersonalityPoints[0] | same slot |
| 3  | ActivePersonality   | 0..1000 (0..1000 seen)| PersonalityPoints[1] | same slot |
| 4  | GenerousPersonality | **always 0** | PersonalityPoints[2]=0 | **matches (0)** |
| 5  | PlayfulPersonality  | 0..1000 | PersonalityPoints[3] | same slot |
| 6  | OutgoingPersonality | 0..1000 | PersonalityPoints[4] | same slot |
| 7  | NeatPersonality     | 0..1000 | PersonalityPoints[5] | same slot |
| 9..12,15,17,18 | SkillEfficiency/Cooking/Charisma/Mechanical/Creativity/Body/Logic | **mostly 0** | rand 0..1000 | **divergence (port randomizes, original leaves 0)** |
| 13,14,16,20,26 | expansion interests | 0 (staged) | x100 rolls | note |
| 46..55 | base interests (8 topics + Technology/Romance) | **raw 0..10** (staged) | x100 rolls | **divergence (dialect)** |
| 29  | Cheats | 0 | 1 | divergence |
| 32  | PersonType | 0 | 2 | divergence |
| 33  | Priority | 0 | 25 | divergence |
| 36  | AutonomyLevel | 0 | 50 | divergence |
| 56  | JobType | 0 (or -1/6/9) | -1 if 0 | divergence (original mostly 0) |
| 57  | JobPromotionLevel | 0 (or 1/4) | 4 | divergence |
| 58  | PersonsAge | **9 = child, 27 = adult** | child?9:27 | **matches** |
| 60  | SkinColor | **1/2/3** (1=light, 2=dark, 3=medium) | 0/1/2 (AppearanceType) | **divergence (off-by-one order)** |
| 61  | TS1FamilyNumber | 1..5 (0 = in bin) | FamilyID | matches |
| 63  | JobPerformance | 0 | 0 | matches |
| 65  | Gender | 1=female, 0=male | Gender | **matches** |
| 67  | LingeringHouseNumber | 5 (on 8 records) | not written | divergence |
| 68  | IsGhost | 1 (on 8 records) | not written | divergence |
| 70  | TS1Zodiac | **always 0** | 0 | **matches** |

**PROVEN** (staged data):
* `pd[4]` (Generous) is 0 on all 20 created records — matches R246 "no original CAS
  record ever writes Generous" (the original UI has only five sliders).
* `pd[70]` (Zodiac) is 0 on all 20 — matches R246 "Zodiac is never persisted".
* `pd[58]` ∈ {9, 27} (created sims), `pd[60]` ∈ {1,2,3}, `pd[65]` ∈ {0,1},
  `pd[61]` ∈ {0,1,2,3,4,5}.

**INFERRED (not re-derived here):** the semantic name -> slot mapping for
personality (`pd[2]=Nice … pd[7]=Neat`) is the R246/port mapping. It is stated as
canon here because it is both what the port writes and what R246 triple-verified,
but this round did not independently re-derive the trait order. The *slots* (2..7)
and the *always-zero* `pd[4]` are PROVEN from bytes; the *names* are the R246 canon.

### 2.4 Skin encoding — PROVEN (light/dark/medium)

`pd[60]` is **not** in Light=1/Medium=2/Dark=3 order. Verified byte-exactly by
correlating `pd[60]` with the skin token embedded in each character file's
bodystring table (the `STR#` that contains `lgt`/`drk`/`med` and the BODY/HAND
outfit names suffixed with that token):

| pd[60] | bodystring token | colour |
|--------|------------------|--------|
| 1 | `lgt` | light |
| 2 | `drk` | dark |
| 3 | `med` | medium |

So **light=1, dark=2, medium=3**. R246's "lgt=1/med=3/drk=2" is **correct**.
The port writes `pd[60] = SkinTone = (short)AppearanceType` where
`AppearanceType { Light=0, Medium=1, Dark=2 }` — a **0/1/2** encoding that is
**off by one and in a different order** (port Medium=1 would be written as 1,
which the original reads as **light**; port Dark=2 reads as **dark** only by
accident). The port must write `1/2/3` with light=1, dark=2, medium=3.

### 2.5 Age and gender — PROVEN

`pd[58]` 9 = child, 27 = adult (each confirmed against the bodystring
`child`/`adult` age-category line and the `9`/`27` age number). `10` = teen
(appears on the `papercarrierf1` NPC, 0x3F template). `pd[65]` 1 = female,
0 = male (confirmed against `female`/`male` in the bodystring table).

---

## 3. FAMI record format — Version 7

The port's `FAMI.cs` reads/writes a fixed frame. The **original** FAMI chunk
data (little-endian) is:

```
data+0x00  4  pad           = 0
data+0x04  4  Version       = 7      <-- R246 "Version 7"
data+0x08  4  magic         = 'IMAF' ('FAMI' reversed)
data+0x0c  4  HouseNumber
data+0x10  4  FamilyNumber  (auto-increment UID; -1 for the townie family)
data+0x14  4  Budget
data+0x18  4  ValueInArch
data+0x1c  4  FamilyFriends
data+0x20  4  Unknown       (status flags; 0 for bin families, 1/17 for moved-in)
data+0x24  4  FamilyGUID count = n
data+0x28  ... n × uint32 FamilyGUIDs
(END — no trailing frame)
```

Chunk data size = `4+4+4+6*4+4+4n` = `40 + 4n`. Verified for all six FAMI chunks:
id4 (n=4) = 56, id3 (n=2) = 48, id2 (n=1) = 44, id5 (n=3) = 52, id1 (n=2) = 48,
id0 (n=0) = 40 — **all byte-exact, with zero trailing bytes**.

**PROVEN**: every FAMI chunk is Version **7** and has **no** trailing data.

**The port writes Version 9 + 16 trailing zero bytes.** For a byte-faithful
official FAMI the port must write **Version 7** and **no** trailing zeros (drop
the `for (i=0; i<4; i++) io.WriteInt32(0)` block in `FAMI.Write`, and write `7`
not `9`).

R246's FAMI-Version-7 claim is **correct**; R246's "16 trailing zeros" port
divergence is also **correct**.

---

## 4. The exact frame the port must write

### 4.1 NBRS record (one per created sim)

```
int32   Unknown1   = 1
int32   Version    = 0x4
        Name       = "<file stem>" NUL-terminated, padded to even (e.g. "user00023\0")
int32   MysteryZero= 0
int32   PersonMode = 5
int16   PersonData[80]   (0xa0 bytes)  — see field map in §2.3; must use 1/2/3 skin, 9/27 age,
                                       gender 0/1, family id, pd[4]=0, pd[70]=0
int16   NeighbourID
uint32  GUID
int32   UnknownNegOne = -1
int32   RelationshipCount, then per-relationship { int32 keycount=1, int32 key,
        int32 valuecount, int32[] values }
```

and the chunk header version field must be **0x3E** (or 0x3F, matching the
target save generation) with magic `'SRBN'`, **not** `0x49`.

### 4.2 FAMI record

```
int32  pad = 0
int32  Version = 7
char4  magic = 'IMAF'
int32  HouseNumber
int32  FamilyNumber (auto-increment UID)
int32  Budget
int32  ValueInArch
int32  FamilyFriends
int32  Unknown (status flags)
int32  FamilyGUID count = n
n × uint32 FamilyGUIDs
(no trailing int32s)
```

---

## 5. Corrected / confirmed vs R246

| R246 claim | verdict | evidence |
|-----------|---------|----------|
| chunk 0x3E/0x3F | **correct** (it is the NBRS chunk-data Version field) | @0x108 `3e 00 00 00` in UserData, `3f 00 00 00` in UserData2-8 |
| record Version 4 (80 shorts) | **correct** | every valid record Version=4, PersonData=0xa0=80 shorts; port parses to exact byte count |
| PersonMode 5 | **correct** | PersonMode=5 on all 31 person-data records |
| name = character-file stem | **correct** (it is the lowercase stem) | 20 `userNNNNN` names ↔ `UserNNNNN.iff` |
| skin pd[60] lgt=1/med=3/drk=2 | **correct** | bodystring token correlation (lgt/drk/med) |
| FAMI Version 7 | **correct** | all 6 FAMI chunks Version 7 |
| port writes 0x49 / 0xA / PM9 / "iffname" / skin 0/1/2 | **correct** | port source `NBRS.cs`/`Save`/`FAMI.cs` + `AppearanceType` |
| port writes FAMI Version 9 + 16 trailing zeros | **correct** | port source `FAMI.Write`; original has zero trailing bytes |
| field offsets 58/60/61/65, pd[4]=0, pd[70]=0 | **correct** | byte map + static enum + binary field-list |

**New / refined this round (beyond R246):**
* The NBRS record magic and the chunk-data header field (`Version=0x3E/0x3F`).
* The exact person-data **offset map** for the original (skills/interests dialect,
  the `pd[56]/[57]/[29]/[32]/[33]/[36]` default divergences, and the two extra
  fields `pd[67]`/`pd[68]` the port does not write).
* The original stores **skills/interests in the raw 0..10 dialect** (pd[46..55]
  = 1..10) whereas the port writes x100 rolls (0..1000) into pd[9..18] and
  pd[46..55]. This is a real divergence, flagged (R246 only asserted
  "presence/range").
* Binary-level cross-check via `Neighbor::GetPersistentDataFields`.

---

## 6. What we could NOT pin

* The exact **personality trait → slot name** order was taken from the R246/port
  canon, not re-derived (the slot indices 2..7 are proven; the names are canon).
* The full **age scale** beyond child=9/teen=10/adult=27 (elder value not present
  in this save; the port's child/adult 9/27 are the values actually written for a
  family with children+adults).
* The exact original **interest values** (they are creation-randomized; the staged
  values are the raw 0..10 dialect but the *distribution* is not pinned — R246
  deliberately asserted presence/range only).
* The semantics of `pd[67]`/`pd[68]` (observed 5/1 on some records; the port does
  not touch them, and the `VMPersonDataVariable` alias `LingeringHouseNumber`/
  `IsGhost` is a TSO-era name — exact TS1 meaning not resolved).
* The FAMs/XXXX/`NGBH` detail (out of scope: the target is FAMI + NBRS; FAMs
  members carry the per-family GUID list and were only cross-checked through the
  FAMI GUID ↔ pd[61] correspondence).
* Why `UserData` is 0x3E while the templates are 0x3F (two save generations; both
  use the same record structure — which version the port should target is a
  product decision, not a decode one).

---

## 7. Reproduce

```
python3 tools/iff-dump/r252-record-format/verify.py
```
re-pins the binary SHA256, re-parses the staged `Neighborhood.iff`, and asserts
every claim above against the real bytes. All assertions pass (see the script's
stdout in this round's record).
