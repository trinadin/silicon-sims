# R252 — Adversarial skeptic review of the created-record decode

**Verdict: STRUCTURE CONFIRMED — implement, with FIX-FIRST on the skin read-boundary remap (P0).**

I re-derived every claim **from the raw bytes**, not from `verify.py` or `decode.md`. I wrote my
own independent Maxis-IFF walker + NBRS/FAMI record parser (Python) and re-correlated the skin
against a proper `STR#200` slot-level decode (format code `-3`). All nine claims are
**CONFIRMED**. There is **one inherited caveat** (personality trait names) and **one P0
implementation requirement** (skin read-boundary remap). No structural claim was refuted.

`verify.py` output is genuine and passes (see §10; the script's assertions are also independently
confirmed — I re-ran them by hand, it is not asserting its own assumptions into existence).

---

## 1. NBRS "0x3E/0x3F" is the chunk-DATA Version field — **CONFIRMED**

- `Neighborhood.iff` @0xb8: `NBRS` chunk, **chunk ID = 1** (BE u16 @0x0c0 = `00 01`), size 6810,
  data 6734. So the IFF chunk ID is `1`, not `0x3E`.
- Chunk data begins @0x104. `data+0x04` = `3e 00 00 00` (LE) = **0x3E**. `data+0x08` = `SRBN`,
  `data+0x0c` = `31 00 00 00` = 49 records.
- `UserData2`…`UserData8` and `TemplateUserData` all report NBRS data-version **0x3F** with
  count 49 (verified each file independently).
- The record-level version is a **separate** field inside each record = `4` (see §2).
- Mentally gate-checked: reading the chunk-data version big-endian would give `0x3E000000`
  (nonsense). LE is correct.

**DECODE IS RIGHT.** R246's "chunk 0x3E/0x3F" is the chunk-data Version field, not the chunk ID.

## 2. Record Version = 4, PersonData = 80 shorts (0xa0 bytes) — **CONFIRMED**

Independent parse of all 49 declared records consumes **exactly 6734/6734 bytes** (data size) and
all 33 live records read `Version == 4`, `PersonData` = **80 shorts (0xa0 bytes)**. The pad/name
rule, the relat-array, the nid/guid/negone all line up to land exactly on the data end — any
off-by-one would desync. (Same for `UserData2`: consumed 3162/3162.)

Concrete bytes for `user00023` (file offsets):
- `Unknown1 = 1`  @ **0x2de**
- `Version  = 4`  @ **0x2e2**  (`04 00 00 00`)
- `Name`          @ **0x2e6**  (`75 73 65 72 30 30 30 32 33 00` = `user00023\0`)
- `PersonMode = 5` @ **0x2f4** (`05 00 00 00`)
- `PersonData[80]` @ **0x2f8** (0xa0 bytes)

This matches the agent's `iff-excerpt.txt` byte-for-byte.

## 3. PersonMode = 5 (0 only for bones/templateperson) — **CONFIRMED**

Every record that carries person-data has `PersonMode == 5` (31 records); the two bodiless records
are `bones` and `templateperson` with `PersonMode == 0` and **no** person-data block. (In
`UserData2` the same pattern: PM 5 with pd, 0 for `tragicclown`/`robot`/`genie`/…/`bones`/
`templateperson`.) If PM 0 were treated as "has pd", the parse would desync — it doesn't.

## 4. Name = lowercase file stem `userNNNNN` — **CONFIRMED** (R246 was WRONG)

All 20 created records have `Name == "userNNNNN"` (lowercase `user` + 5 zero-padded digits), e.g.
`user00023`. Each maps to an existing **`UserNNNNN.iff`** (capital `U`) in
`UserData/Characters/`. No record equals `"iffname"` (the port's literal). So the NBRS `Name` is
the **lowercase stem**, and the on-disk file is the capital-U form.

**R246 said "UserNNNNN" (capital U) — that is WRONG.** The decode agent's correction is correct:
the field is lowercase. (R246 conflated the field value with the filename casing.)

## 5. Skin pd[60] = 1/2/3, lgt=1 / drk=2 / med=3 (non-monotonic) — **CONFIRMED**

I decoded the character files' `STR#200` (chunk id 200, **format code `-3`**) slot-level strings —
not a naive substring scan — and read **slot 14 (skin)**, slot 12 (gender), slot 13 (age number),
slot 0 (age category), for all 20 created sims, then paired them with the same sim's NBRS `pd[]`:

```
pd[60]  STR#200[14]  count
 1      lgt           8
 2      drk           3
 3      med           9
```
**Zero mismatches.** Therefore: **light=1, dark=2, medium=3** — i.e. `lgt=1, med=3, drk=2`
in (light, medium, dark) order. Non-monotonic (medium=3, not 2). R246's "lgt=1/med=3/drk=2" is
**correct**. The port writes `AppearanceType {Light=0,Medium=1,Dark=2}` (0/1/2) — off by one and
reordered — confirming the decode's divergence claim.

## 6. FAMI Version 7, size 40 + 4·guidCount, zero trailing — **CONFIRMED**

All six FAMI chunks, parsed independently:

| id | @offset | data size | 40+4n | trailing | ver | guidCount |
|----|---------|-----------|-------|----------|-----|-----------|
| 4  | 0x1b52  | 56 | 56 | 0 | 7 | 4 |
| 3  | 0x1c32  | 48 | 48 | 0 | 7 | 2 |
| 2  | 0x1d09  | 44 | 44 | 0 | 7 | 1 |
| 5  | 0x1ddd  | 52 | 52 | 0 | 7 | 3 |
| 1  | 0x1eb5  | 48 | 48 | 0 | 7 | 2 |
| 0  | 0x1f8b  | 40 | 40 | 0 | 7 | 0 |

All are `pad=0`, magic `IMAF`, **Version 7**, and **no trailing bytes**. The port's `FAMI.Write`
hardcodes version **9** and writes 4×`WriteInt32(0)` (16 trailing zero bytes). Confirmed divergence.

## 7. Field offsets — **CONFIRMED** (one inherited caveat)

| pd[] | claim | verified bytes (created records) |
|------|-------|----------------------------------|
| 4  | Generous = 0 | `{0: 20}` all 20 |
| 58 | age 9 child / 27 adult | `{9:5, 27:15}`; STR#200 slot 13 = `9`/`27`, slot 0 = `child`/`adult`; 0 mismatches |
| 60 | skin 1/2/3 | `{1:8, 2:3, 3:9}`; §5 |
| 61 | family id = FAMI chunk id | `{0:8,1:2,2:1,3:2,4:4,5:3}`; for moved-in sims (fid 1..5) the sim's GUID is in the matching FAMI's guid list — verified; `0` = in-bin (no FAMI membership) |
| 65 | gender 1 female / 0 male | `{0:10, 1:10}`; STR#200 slot 12 = `male`/`female`; 0 mismatches |
| 70 | zodiac = 0 | `{0: 20}` all 20 |
| 2..7 | personality 0..1000 | `pd[2..7]` raw values (see below); `pd[4]` always 0 |

**Endianness:** chunk header fields (chunk size/id) are **BE**; chunk-data fields, the record
version, and the **person-data shorts are LE**. Proof: as LE, `pd[60]` reads `3` (a valid skin);
as BE it reads `0x0300 = 768` which cannot be a skin and breaks the §5 correlation across all 20
files. The agent's BE/LE split is correct (the format is a shared Maxis/Windows layout, not PPC-BE).

**Inherited caveat (not independently re-derived):** the *trait-name → slot* order (`pd[2]=Nice …
pd[7]=Neat`) is taken from the R246/port canon, per `decode.md` §6. The **slot indices 2..7**, the
**0..1000 scale**, and the **always-zero `pd[4]`** are byte-proven; the *names* are inherited. The
decode is honest about this. It does not block the implementation, because the port writes and reads
personality through the same mapping (self-consistent round-trip), but a "which slider is Nice vs
Active" swap would not be caught by this decode.

## 8. Port read-path flexibility — **CONFIRMED** (not a blocker)

- `NBRS.Read` (`NBRS.cs:29`): reads `Version` (`:34`) with **no validation**.
- `Neighbour` ctor (`NBRS.cs:148`): `Unknown1!=1` early-return `:151`; `Version` `:152`;
  **`if (Version==0xA)` reads `Unknown3`** `:153-158` (else skips); `Name = ReadNullTerminatedString()`
  `:159` + even-align `:160` (accepts any name); `PersonMode` `:163`; **`if (PersonMode>0)`** `:164`
  reads person data with `size = (Version==0x4) ? 0xa0 : 0x200` `:166`; fills `short[88]` `:167-177`.
- `Neighbour.Save` (`:207`) writes `Version`, `PersonMode`, `Name`, and selects the person-data size
  from `Version` (`:218`) — so `Version==0x4` already writes 80 shorts.
- `FAMI.Read` (`FAMI.cs:49`): version `:54` with **no validation** (accepts 7 and 9); after the
  guids it **tries** to read 4 trailing int32 in `try/catch` `:68-75`. Because `IffFile` reads the
  chunk into a **bounded `MemoryStream` of exactly `chunkSize-76`** (`IffFile.cs:251`, then
  `new MemoryStream(chunk.ChunkData)` `:328`), reading past the end **throws**, which the `catch`
  handles. So a v7 no-trailing FAMI parses fine. **CONFIRMED.**

So the reader is flexible for both the original (0x3E / rec-4 / 80-shorts / PM5 / stem-name) and
the port's own legacy (0x49 / rec-0xA / 256-shorts / PM9 / `iffname`) format. **Not a blocker.**

> Separate residual (pre-existing, out of R252 scope, but a real native-load blocker): `NBRS.Write`
> (`NBRS.cs:73`) writes `count = Entries.Count` (49) but only **saves** the `NeighbourByID.Values`
> (33) — so a native engine loading a port-written NBRS would expect 49 records and desync on the
> tail. This is the documented r248 P2-4 mismatch, independently confirmed at lines 73-77. If
> "byte-faithful whole-file native load" is a goal, this must also be fixed; R252 scopes it out.

## 9. Skin cast at `VMTS1ActivatorNew.cs:467` — **CONFIRMED (P0 break)**

```csharp
SkinTone = (AppearanceType)person.PersonData[(int)VMPersonDataVariable.SkinColor]
```
`AppearanceType { Light=0, Medium=1, Dark=2 }` (`tso.vitaboy.model/AppearanceType.cs:8-10`);
`VMPersonDataVariable.SkinColor = 60` (`VMPersonDataVariable.cs:86`). With the original disk
encoding `1/2/3` (light=1, dark=2, medium=3):
- `(AppearanceType)1` = **Medium**  → light skin renders medium (WRONG)
- `(AppearanceType)2` = **Dark**    → dark skin renders dark (accidental coincidence)
- `(AppearanceType)3` = **out-of-range enum value** → medium skin renders garbage

So the skin mapping **breaks** at the spawn/read boundary. **Fix-first requirement:** remap
`1→Light, 3→Medium, 2→Dark` at the consumer, and **do not mutate the stored `pd[60]` array** (so a
round-trip write preserves the original `1/2/3` disk value).

---

## 10. `verify.py` real output

`python3 tools/iff-dump/r252-record-format/verify.py` **runs clean and asserts PASS**:

```
[ok] binary SHA256 pinned  33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f
[ok] NBRS chunk @0xb8 data=6734 bytes
[ok] NBRS chunk-data version 0x3e, magic b'SRBN', count=49, parse consumed exactly 6734/6734 bytes
[ok] 33 of 49 records are Unknown1==1, ALL record Version == 0x4
[ok] PersonMode == 5 on 31 records, 0 on 2; person data is 80 shorts (0xa0 bytes) on all
[ok] 20 created records: NBRS Name == character-file stem ... NOT the literal 'iffname'
[ok] all FAMI chunks Version 7 and byte-exact (no 16 trailing zeros)
[ok] FAMI chunk id == in-game family id == NBRS pd[61] for all moved-in created sims
...
ALL ASSERTIONS PASSED.
```

I did **not** take this on faith — I reproduced every non-trivial assertion by hand in §1-§9. The
script is faithful, not self-fulfilling: its FAMI id/guid check correctly scopes to moved-in sims
(`fid!=0`) because `fid==0` sims are in the bin (no FAMI membership), which I confirmed.

---

## What I could NOT verify (honest)

1. **Personality trait-name order** (`pd[2]=Nice … pd[7]=Neat`). Byte-proven: indices 2..7, 0..1000
   scale, `pd[4]=0`. Not independently re-derived; inherited from R246/port canon. A wrong name→slot
   order would swap traits cosmetically but not break loading.
2. **Full age scale** beyond child=9 / teen=10 (on `papercarrierf1`, 0x3F) / adult=27. No elder value
   present in this staged data. The port's child/adult 9/27 are the values actually in-file.
3. **Exact interest value distribution** — the staged values are the raw 0..10 dialect; the exact
   creation distribution isn't pinned (decode asserts presence/range only).
4. **`pd[67]`/`pd[68]` semantics** (observed contextual `5`/`1` on some records; alias is a
   TSO-era name). Port leaves `0/0`; acceptable for an un-moved-in family, and not in the claim set.
5. **Field-VALUE parity is NOT asserted by claims 1-9.** The staged original leaves several fields
   at `0` (per `decode.md` §2.3) while the port's `MakePersonData` (`SimitoneNeighbourGenerator.cs`)
   writes `pd[36]=50`, `pd[56]=-1-if-0`, `pd[57]=4`, `pd[33]=25`, `pd[29]=1`, `pd[32]=2`, and
   randomizes skills/interests. That is a **structural-vs-value** distinction: the *structure* is
   byte-faithful; those *default values* are a separate, partially-open divergence that the decode
   flags honestly. If "byte-identical to the original" means every value, this is still open — it is
   **not** one of the claims I was asked to adjudicate, and it does not invalidate the structure.

---

## Decisive verdict

**STRUCTURE CONFIRMED — IMPLEMENT, with FIX-FIRST on the skin read-boundary remap (P0).**

The decode's structural claims (1-9) are all verified against the raw staged bytes; the port reader
is already flexible (§8); `verify.py` genuinely passes; the name-case correction over R246 is correct
(§4); and the non-monotonic skin is light=1 / dark=2 / medium=3 (§5).

**Before implementing, the following must be in place (FIX-FIRST, P0):**
- `VMTS1ActivatorNew.cs:467` must **not** cast `pd[60]` straight to `AppearanceType`. Use a
  `DiskSkinToAppearance` map: `1→Light, 3→Medium, 2→Dark`, leaving the stored array untouched.
- Writer: NBRS chunk-data version `0x3E`, record `Version=0x4` (80 shorts), `PersonMode=5`,
  `Name = lowercase file stem`, `pd[60] = 1/3/2` (lgt/med/drk); FAMI `Version=7`, drop the 16
  trailing zero bytes. (These match the `port-audit.md` edit set A-E.)

**Not a blocker but should be tracked separately (not claims 1-9):** the `NBRS.Write` count/record
mismatch (49 declared vs 33 saved, `NBRS.cs:73`), the field-value default dialect, `FAMI.Unknown`
(24 vs original 1), and `pd[67]/pd[68]`.
