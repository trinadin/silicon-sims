# R252 — Implementation: write/read the ORIGINAL TS1 created-record format

Round R252 makes the port WRITE a byte-faithful ORIGINAL TS1 created-record
(NBRS + FAMI) and READ it back correctly. The original byte format was decoded
and skeptic-verified in `decode.md` / `skeptic-review.md` / `port-audit.md`
(authoritative). The port previously wrote a divergent, port-chosen format. This
document records the edits, the name-stem sourcing decision, the version-aware
skin remap, the new opt-in `recordfmt` autotest, and the `ucasflow` pin
repoints, plus the real gate output.

---

## 1. Summary of what changed

| area | before (divergent port) | now (original TS1) |
|------|-------------------------|--------------------|
| NBRS chunk-data Version | `0x49` | `0x3E` |
| NBRS record `Version` | `0xA` (class default; +Unknown3) | `0x4` |
| record PersonData size | `0x200` (256 shorts) | `0xa0` (80 shorts) |
| `PersonMode` | `9` | `5` |
| record `Name` | literal `"iffname"` | lowercase char-file stem `userNNNNN` |
| skin `pd[60]` | logical `0/1/2` | disk `1/3/2` (lgt=1, drk=2, med=3) |
| FAMI `Version` | `9` (hardcoded) | `7` (honours the field) |
| FAMI trailing bytes | 4 x zero int32 (16 bytes) | none (`40 + 4·guidCount`) |
| FAMI reader | tolerant | remains tolerant (unchanged) |
| spawn skin read | `(AppearanceType)pd[60]` | version-aware `DiskSkinToAppearance` |

The reader was already flexible (`NBRS.Read`/`Neighbour` branch on `Version`,
`FAMI.Read` try/catches the trailer), so **no reader change was needed** — only
the writers and one consumer (skin).

---

## 2. Exact edits (file : line)

All paths relative to the workspace root `/Users/nathannoom/Developer/Games/The Sims/simitone-fork`.

### FreeSO (nested git repo) — the format codecs

**`FreeSO/TSOClient/tso.files/Formats/IFF/Chunks/NBRS.cs`**
- `:18-24` — added `public static class NbrsFormat` with `CHUNK_VERSION=0x3E`,
  `RECORD_VERSION=0x4`, `PERSON_MODE=5`.
- `:82` — `io.WriteUInt32(0x49)` → `io.WriteUInt32(NbrsFormat.CHUNK_VERSION)`
  (the NBRS chunk-data Version, now `0x3E`). Read path untouched.
- No change to `Neighbour.Save` — it already writes `Version`, `PersonMode`,
  `Name` and selects the person-data size from `Version`, so setting
  `Version=0x4`/`PersonMode=5` in the generator is sufficient.
- **`Neighbour.PersonDataBytes`** (added this round): a measured on-disk
  PersonData byte span recorded as a stream-position delta in the
  `Neighbour(IoBuffer)` read ctor (`:181-195`). Non-behavioural — it exists so the
  `recordfmt` raw-scan tautology fix can assert the truly-written byte span
  instead of re-deriving it from `Version`.

**`FreeSO/TSOClient/tso.files/Formats/IFF/Chunks/FAMI.cs`**
- `:13` — `public uint Version = 0x9` → `= 0x7`.
- `:84` — `io.WriteUInt32(9)` → `io.WriteUInt32(Version)` (honours the loaded
  version; a loaded v7 saves as v7, a legacy v9 round-trips as v9).
- `:96-98` — removed the `for (int i=0; i<4; i++) io.WriteInt32(0);` trailing-zero
  block. The chunk now writes the data size exactly `40 + 4·guidCount`.
- Reader unchanged: `FAMI.Read` (`:68-75`) already try/catches the 4 trailing
  int32 reads, so a v7 no-trailer FAMI parses fine (verified in `port-audit.md`
  §2c and by the passing `recordfmt` run).

**`FreeSO/TSOClient/tso.simantics/Utils/SimitoneNeighbourGenerator.cs`**
- `:35` — in `PrepareTemplatePerson`, after `var userid = neigh.NextSim;`, set
  `info.CharStem = "user" + userid.ToString().PadLeft(5, '0');`.
- `:82` and `:91` — `AddNeighbor(guid, 9, info.MakePersonData())` →
  `AddNeighbor(guid, NbrsFormat.PERSON_MODE, info.MakePersonData(), info.CharStem)`.
- `:160` — `AddNeighbor` signature gains `string name`.
- `:172-181` — the new `Neighbour` is built with `Version = NbrsFormat.RECORD_VERSION`
  (`0x4`) and `Name = name` (was literal `"iffname"`).
- `:202` — added `public string CharStem;` to `SimTemplateCreateInfo`.
- `:325` — `pd[60] = SkinTone;` → `pd[60] = SkinToDisk(SkinTone);`.
- `:338-346` — added `SkinToDisk(short logical)`.

**`FreeSO/TSOClient/tso.simantics/Utils/VMTS1ActivatorNew.cs`**
- `:433` — `ConvertAvatar(OBJMInstance inst)` → `ConvertAvatar(OBJMInstance inst, NBRS neighbors)`.
- `:467` — `SkinTone = (AppearanceType)person.PersonData[...]` →
  `SkinTone = DiskSkinToAppearance(person.PersonData[(int)VMPersonDataVariable.SkinColor], RecordVersionFor(neighbors, person))`.
- `:497-523` — added `DiskSkinToAppearance(short disk, int recordVersion)`.
- **`DiskSkinToAppearance` made `public static`** (this round) — a small seam so
  the `recordfmt` autotest (in `Simitone.Client`) can assert the production
  read-side map directly (mirrors the public `SkinToDisk` write helper).
- `:525-551` — added `RecordVersionFor(NBRS neighbors, OBJMPerson person)`.
- **P0 fix (this round):** in `RecordVersionFor`, the unresolved-case fallback
  `return 0xA` → `return 0x4`, because `pd[31]` is 0 on both shipped original
  and the port's new records, so the lookup always misses and the fallback IS the
  effective dialect selector (see §4).
- `:660` — `ConvertAvatar(inst)` → `ConvertAvatar(inst, neighbors)`.

### Parent repo — the autotest harness

**`Client/Simitone/Simitone.Client/AutotestCASFlow.cs`**
- `:67` — added `internal static bool RecordFmtEnabled;`.
- `:90-91` — in `BeginIsolation`, accept `recordfmt` and set `RecordFmtEnabled`.
- `:161-169` — added `_recordFmtPassed/_recordFmtFailed/_recordFmtFailures/_recordFmtPasses`,
  `RecordFmtFailed`, `RecordFmtDiagnostics`.
- `:271-275` — added `CheckFmt(bool ok, string name)`.
- `:801` — `new IffFile(neighPath)` → `new IffFile(neighPath, true)` (retain
  `OriginalData` so the raw record bytes are available to recordfmt).
- `:874-876, 889` — **repointed ucasflow pins** (see §4).
- `:942` — `if (RecordFmtEnabled) CheckRecordFmt(...)`.
- `:949-1047` — added `CheckRecordFmt` (the recordfmt assertions, §5).
- `:1052-1100` — added `RawScanNbrs` (reuses the production `Neighbour` codec).

**`Client/Simitone/Simitone.Client/AutotestRunner.cs`**
- `:351` — `if (CheckEnabled("ucasflow") || CheckEnabled("uicasflow"))` →
  `... || CheckEnabled("recordfmt")`.
- `:594-601` — in `StateCASFlow`, surface `Pass("recordfmt")`/`Fail("recordfmt")`
  from `_casflow.RecordFmtFailed` (independent of the ucasflow verdict).
- The default `Config.Checks` string (`:76`) is **byte-identical** — `recordfmt`
  is NOT added to it (verified: 0 occurrences of `recordfmt` on line 76).

---

## 3. Name-stem sourcing decision (verified, not assumed)

The task hypothesised `Name = "user" + newID.ToString("D5")` where `newID` is the
NBRS `NeighbourID`. **This is wrong** — verified against the staged original
data: `user00023` has `NeighbourID=19`, `user00022` has `NeighbourID=20`, so the
NBRS `Name` is **not** the neighbour id.

The NBRS `Name` is the **character-file stem**, numbered by the `TS1NeighbourProvider.NextSim`
counter that names `User#####.iff`:
- `PrepareTemplatePerson` reads `userid = neigh.NextSim` **before**
  `SaveNewNeighbour` does `NextSim++` to write `User{NextSim}.iff`
  (`TS1NeighbourProvider.cs:216`).
- Therefore the correct stem is `"user" + userid.PadLeft(5,'0')`, captured into
  `info.CharStem` at `PrepareTemplatePerson:35` and threaded to `AddNeighbor`.

Why: because a sim's NBRS record name and its character file must correspond, and
they are both keyed off the same `NextSim`, not the neighbour-id free-slot.

---

## 4. Skin remap (both directions) and the `VMPersonDataVariable`/`AppearanceType` values

- `AppearanceType { Light=0, Medium=1, Dark=2 }` (`FreeSO/TSOClient/tso.vitaboy.model/AppearanceType.cs:6`).
- Original TS1 disk `pd[60]` (SkinColor): `lgt=1, drk=2, med=3` (non-monotonic).
- `VMPersonDataVariable.SkinColor = 60`, `.NeighborId = 31`, `.PersonsAge=58`,
  `.TS1FamilyNumber=61`, `.Gender=65`, `.TS1Zodiac=70`.

**Write side — `SkinToDisk(short logical)`** (`SimitoneNeighbourGenerator.cs:338`):
```
0 (Light)  -> 1
1 (Medium) -> 3
2 (Dark)   -> 2
```
`pd[60] = SkinToDisk(SkinTone)`.

**Read side — `DiskSkinToAppearance(short disk, int recordVersion)`**
(`VMTS1ActivatorNew.cs:497`):
- If `recordVersion == 0x4` (original): `1→Light, 3→Medium, 2→Dark`.
- Else (legacy 0xA port): `0→Light, 1→Medium, 2→Dark`.

`RecordVersionFor(neighbors, person)` (`:525`) looks up the sim's NBRS record by
`person.PersonData[NeighborId]` (index 31) via `Neighbours.NeighbourByID` and
returns its `Version`; **falls back to `0x4`** if absent.

> **R252 P0 fix (this round).** The review proved the fallback was `0xA` (the
> legacy map), and that `pd[31]` is **0 on every shipped original record AND on
> the port's own new-format CAS records** — the real `NeighbourID` is a separate
> NBRS record field (`Neighbour.NeighbourID`), never written into the person-data
> shorts by the CAS writer (`MakePersonData` leaves index 31 at its 0 default).
> So the `NeighbourByID` lookup (key 0) always misses and the method ALWAYS took
> the fallback. With the fallback at `0xA`, an original/new record's disk
> `1/2/3` value was decoded through the legacy `0/1/2` map → light (`disk 1`)
> rendered Medium, medium (`disk 3`) rendered Light. The fallback is now `0x4`,
> the correct dialect for both the shipped original saves and everything R252
> writes. The successful-lookup path is unchanged (returns the record's actual
> `Version`, which is `0x4` for new/original records).

The stored `PersonData` array is **not mutated**, so a save round-trips the
original `1/2/3` disk value verbatim (the `recordfmt` round-trip check confirms
`pd[60]` still `== 1` after a write→read→re-write).

---

## 5. The `recordfmt` opt-in assertions

`recordfmt` reuses the same `AutotestCASFlow` CAS drive (no new CAS driver),
piggybacking on `BeginIsolation` (temp UserDir redirect + game-data
`Neighborhood.iff` SHA-256 pin, reused verbatim from R246). When enabled it runs
`CheckRecordFmt(...)` at the end of `PhaseDisk`, using a **fresh**
`new IffFile(neighPath, true)` and the new character file. It asserts, on the
created sim:

**NBRS chunk-data Version = 0x3E**
- `nbrs-chunkver-3e`: `nbrs.Version == 0x3E`.

**Raw record (production-codec scan of chunk `OriginalData`)**
- `nbrs-raw-member-found`: the member (by family GUID) is present in the raw bytes.
- `nbrs-raw-rec-version-4`: record `Version == 0x4`.
- `nbrs-raw-pm-5`: `PersonMode == 5`.
- `nbrs-raw-name-stem`: `Name == charStem` (`userNNNNN`).
- `nbrs-raw-pd-bytes-a0`: on-disk PersonData byte span `== 0xa0`. **Measured, not
  re-derived (R252 tautology fix):** `RawScanNbrs` now returns the codec's actual
  `Neighbour.PersonDataBytes` — the stream-position delta recorded inside the
  `Neighbour(IoBuffer)` read loop — instead of recomputing `Version==0x4 ? 0xa0 :
  0x200`. So the assertion genuinely measures the written byte span rather than
  echoing the version branch.

**Skin map (direct production-map assertions, R252 P0-2)**
These call `VMTS1ActivatorNew.DiskSkinToAppearance` (exposed as a public static
seam) so the **read-side dialect is asserted directly**, closing the review's
P0-2 coverage gap where neither `recordfmt` nor `ucasflow` exercised the remap
(`ConvertAvatar` never runs on a created sim in those flows). `AppearanceType`
is `{ Light=0, Medium=1, Dark=2 }`.
- `skinmap-original-1-light` / `skinmap-original-3-medium` / `skinmap-original-2-dark`
  (the `0x4` map: `1→Light, 3→Medium, 2→Dark`).
- `skinmap-legacy-0-light` / `skinmap-legacy-1-medium` / `skinmap-legacy-2-dark`
  (the `0xA` map: `0→Light, 1→Medium, 2→Dark`).

**In-memory parsed member**
- `member-version-4`, `member-pm-5`, `member-name-stem`.
- `pd2-7-personality`: `pd[2..7]` on the 0..1000 scale with `pd[4]==0` (Generous).
- `pd58-adult-27`, `pd65-male-0`, `pd60-skin-light-1`, `pd61-family-chunkid`,
  `pd70-zodiac-0`.

**FAMI Version 7, no trailing bytes**
- `fami-version-7`: the new FAMI chunk `Version == 7`.
- `fami-no-trailing`: raw `OriginalData.Length == 40 + 4·guidCount`
  (the fixture's 1-guid family → `44`), proving **zero trailing bytes**.

**write → read → re-write round-trip (no data loss)**
- `roundtrip-member` / `roundtrip-ver-pm-name` / `roundtrip-pd-present` /
  `roundtrip-fields-preserved` (`pd[60]`, `pd[58]`, `pd[65]`, `pd[61]`, `pd[70]`
  all equal after reparse) / `roundtrip-pd60-still-disk-1` (proves the reader did
  **not** mutate the stored skin to `0/1/2`).

Verdict surfaced as `Pass("recordfmt")`/`Fail("recordfmt")` from
`AutotestRunner.StateCASFlow`. It is opt-in only and **not** in the default
`Config.Checks`.

---

## 6. `ucasflow` pin repoints (strengthened, not weakened)

The previously divergent (port-format) pins were repointed to assert the CORRECT
ORIGINAL format (AutotestCASFlow.cs):

| old pin (asserted the divergent port format) | new pin (asserts the original) |
|---------------------------------------------|--------------------------------|
| `personmode-9` (`:855`) | `personmode-5-original` (`:875`) |
| `nbrs-name-iffname` (`:856`) | `nbrs-name-stem` (`:876`) |
| *(no Version pin)* | `nbrs-record-version-4` (`:874`) |
| `pd60-skin-port-encoding` `pd[60]==0` (`:870`) | `pd60-skin-original-encoding` `pd[60]==1` (`:889`) |

`persondata-88` (in-memory `short[88]`) is kept; the recordfmt raw scan
independently proves the on-disk block is `0xa0` bytes.

> NOTE on `AutotestCASFlow.cs` line numbers: lines shifted as edits were applied;
> the pin repoints are at `:874-876` and `:889`, and the recordfmt block at
> `:942`/`:949-1100` in the final file.

---

## 7. Real gate output

### Focused (opt-in)

**`427-autotest-opts corpus,lot,motive,mood,load,ucasflow`** (CAS drive preempts the lot/motive path):
```
AUTOTEST uicasflow nbrs-record-version-4 (4): PASS
AUTOTEST uicasflow personmode-5-original (5): PASS
AUTOTEST uicasflow nbrs-name-stem (user00024 vs user00024): PASS
AUTOTEST uicasflow pd60-skin-original-encoding (lgt=1; 1): PASS
...
AUTOTEST ucasflow: PASS
AUTOTEST ucasflow passed=94 failed=0 phase=15
AUTOTEST SUMMARY passed=1 failed=0       (the runner-level ucasflow verdict)
AUTOTEST RESULT PASS passed=1 failed=0
```

REAL run this round: `08:35:59 AUTOTEST ucasflow: PASS` / `AUTOTEST ucasflow passed=94 failed=0 phase=15`.

**`-autotest-opts recordfmt`** (run separately from `mood` per the R251-harness caveat):
```
AUTOTEST recordfmt nbrs-chunkver-3e (62): PASS
AUTOTEST recordfmt nbrs-raw-member-found: PASS
AUTOTEST recordfmt nbrs-raw-rec-version-4 (4): PASS
AUTOTEST recordfmt nbrs-raw-pm-5 (5): PASS
AUTOTEST recordfmt nbrs-raw-name-stem (user00024 vs user00024): PASS
AUTOTEST recordfmt nbrs-raw-pd-bytes-a0 (160): PASS
AUTOTEST recordfmt member-version-4: PASS
AUTOTEST recordfmt member-pm-5: PASS
AUTOTEST recordfmt member-name-stem: PASS
AUTOTEST recordfmt pd2-7-personality (generous=0): PASS
AUTOTEST recordfmt pd58-adult-27 (27): PASS
AUTOTEST recordfmt pd65-male-0 (0): PASS
AUTOTEST recordfmt pd60-skin-light-1 (1): PASS
AUTOTEST recordfmt pd61-family-chunkid (6): PASS
AUTOTEST recordfmt pd70-zodiac-0 (0): PASS
AUTOTEST recordfmt skinmap-original-1-light: PASS
AUTOTEST recordfmt skinmap-original-3-medium: PASS
AUTOTEST recordfmt skinmap-original-2-dark: PASS
AUTOTEST recordfmt skinmap-legacy-0-light: PASS
AUTOTEST recordfmt skinmap-legacy-1-medium: PASS
AUTOTEST recordfmt skinmap-legacy-2-dark: PASS
AUTOTEST recordfmt fami-version-7 (7): PASS
AUTOTEST recordfmt fami-no-trailing (len=44 expect 44): PASS
AUTOTEST recordfmt roundtrip-member: PASS
AUTOTEST recordfmt roundtrip-ver-pm-name: PASS
AUTOTEST recordfmt roundtrip-pd-present: PASS
AUTOTEST recordfmt roundtrip-fields-preserved: PASS
AUTOTEST recordfmt roundtrip-pd60-still-disk-1 (1): PASS
AUTOTEST recordfmt: PASS   (passed=28 failed=0)
AUTOTEST RESULT PASS passed=1 failed=0
```

### Full default suite (`-autotest` with no `-autotest-opts`, i.e. the default `Config.Checks`)

```
AUTOTEST SUMMARY passed=142 failed=0 skipped=0
AUTOTEST RESULT PASS
AUTOTEST RESULT PASS passed=142 failed=0
```

**142/0 — the default suite stays green.** `recordfmt`/`ucasflow` are opt-in and
not in the default `Config.Checks` string, so the default suite is unaffected by
them; the production codec changes (NBRS/FAMI/generator/VMTS1Activator) are
read/write-compatible with the default checks. REAL run this round:
`08:40:50 AUTOTEST SUMMARY passed=142 failed=0 skipped=0`,
`AUTOTEST RESULT PASS passed=142 failed=0`.

---

## 8. Residual divergences (recorded honestly, NOT forced)

These are **not** fixed by R252 and are **not** asserted by `recordfmt`; they are
value-level defaults vs the original's 0s, or out-of-scope outright:

- **R252-remaining NON-original writer (DISCLOSED):** `TS1NeighbourProvider.ImportAddNeighbor`
  (`TS1NeighbourProvider.cs:1124-1166`) still writes the LEGACY port record
  shape — the constructed `Neighbour` keeps the class defaults (`Version=0xA`,
  `Unknown3=9`) and sets `PersonMode=9`, so `Save` emits a `0x200`-byte
  (256-short) PersonData block. This is the family-import / FAM path (r247), left
  deliberately untouched as separate and risky; it is documented inline (a code
  comment at the construction site) and in `review-fixes.md` §residuals. The
  CAS/create path (`SimitoneNeighbourGenerator.AddNeighbor`) is the byte-faithful
  one everything R252 asserts.
- **NBRS count/record mismatch** (`NBRS.Write` writes `Entries.Count` as declared
  but only `NeighbourByID.Values`, r248 P2-4; the shipped file has 49 declared /
  33 live, and the post-save file here reports 285 declared). Pre-existing,
  documented, not touched.
- **`rsmp` drop** on `SaveNeighbourhood` — documented residual.
- **`FAMI.Unknown = 24`** vs original `1` (port-chosen constant; `recordfmt`
  scopes to Version 7 + no trailing bytes only).
- **`pd[67]/pd[68]`** (`LingeringHouseNumber`/`IsGhost`) — original writes `5/1`
  contextually; port leaves `0/0`, acceptable for an un-moved-in family.
- **skin runtime clobber** — `VMNetAvatarPersistState:217` writes
  `AppearanceType` (0/1/2) into `pd[60]` on a runtime skin change; would
  round-trip to the wrong encoding. Not exercised by recordfmt; flagged for a
  later round (centralise disk↔logical skin).
- **Value-level defaults** — `MakePersonData` writes `pd[56]=-1, pd[57]=4,
  pd[36]=50, pd[33]=25, pd[29]=1, pd[32]=2` and randomises skills/interests,
  whereas the staged original leaves many at 0 / uses a different dialect. The
  *structure* is now byte-faithful; those *default values* (and the skills/
  interests 0..10 vs x100 dialect) are a separate, partially-open divergence
  flagged in `decode.md` §5 and `skeptic-review.md` §5. They are **not** pinned
  and **not** force-matched.

---

## 9. Build / packaging notes

- Publish uses the fork's documented workaround flags (the FSO.Server projects
  fail restore on NU1605 otherwise):
  `dotnet publish Client/Simitone/Simitone.Desktop/Simitone.Desktop.csproj -c Release -r osx-arm64 --self-contained true -o publish/osx-arm64 /p:TreatWarningsAsErrors=false /p:WarningsAsErrors="" -p:NoWarn=NU1605`
- `./packmac.sh arm64` — built `dist/The Sims-arm64.app` (2.1G).
- `game-data/` untouched (read-only spot-checked by the isolation SHA-256 pin).
- The `FreeSO/Other/libs/FSOMonoGame` pre-existing dirty mod was **not** touched.
- The generated `dist/` and `publish/` are git-ignored build artifacts; they were
  left rebuilt for the maintainer.
