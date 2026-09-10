# R252 — Review-fix round (addresses the adversarial review)

This round fixes the R252 issues raised by `review.md`. The headline P0 (skin
remap ineffective at the lot-load boundary) is fixed; the remap is now **directly
asserted** by `recordfmt`; the `recordfmt` byte-span tautology is fixed (the value
is now measured, not re-derived); and the remaining `ImportAddNeighbor` legacy
writer is disclosed inline and here. All gates re-run clean.

Paths are relative to the workspace root
`/Users/nathannoom/Developer/Games/The Sims/simitone-fork`.

---

## 1. The P0 fix (skin remap dialect selection)

**File:** `FreeSO/TSOClient/tso.simantics/Utils/VMTS1ActivatorNew.cs`,
`RecordVersionFor` (`:525`).

**Bug (from review.md area 4 / bug list #1):** `RecordVersionFor` resolved the
NBRS record by `person.PersonData[(int)VMPersonDataVariable.NeighborId]`
(**index 31**) and, on a miss, fell back to the **legacy 0xA** dialect. But
`pd[31]` is **0 on every shipped original record AND on the port's own new-format
CAS records** — the real `NeighbourID` is a separate NBRS record field
(`Neighbour.NeighbourID`), never written into the person-data shorts by the CAS
writer (`MakePersonData` leaves index 31 at its 0 default). So the
`NeighbourByID` lookup (key `0`, never a live neighbour id, which start at 1)
always missed and the method **always** took the fallback — which was `0xA`.
`DiskSkinToAppearance` therefore decoded an original/new record's disk `1/2/3`
value through the legacy `0/1/2` map: light (`disk 1`) → Medium (wrong), medium
(`disk 3`) → Light (wrong). The remap did **not** correct the shipped-save skin.

**Fix (one line + rationale comment):** the unresolved-case fallback changed from
`return 0xA;` to `return 0x4;` — the correct dialect for both the shipped
original saves and everything R252 writes. The successful-lookup path is
unchanged (returns the record's actual `Version`, which is `0x4` for new/original
records). A comment documents the `pd[31]`-is-zero rationale.

```csharp
// R252 P0: pd[31] is always 0 on original + new records (see the note
// above), so this is effectively the only path taken and it MUST be the
// ORIGINAL dialect 0x4, not the legacy 0xA.
return 0x4;
```

---

## 2. Skin remap now directly asserted by `recordfmt` (P0-2)

`DiskSkinToAppearance` was made **`public static`** (a small seam, mirroring the
public `SkinToDisk` write helper in `SimitoneNeighbourGenerator`) so the
`recordfmt` autotest — which lives in `AutotestCASFlow` (assembly
`Simitone.Client`) and cannot call a `private` FreeSO method — can assert the
production read-side map. The `recordfmt` pass gained six sub-assertions
(`AutotestCASFlow.cs`, `CheckRecordFmt`), covering the `AppearanceType`
`{ Light=0, Medium=1, Dark=2 }` constants on both dialect branches:

- `skinmap-original-1-light`  — `DiskSkinToAppearance(1, 0x4) == Light`
- `skinmap-original-3-medium` — `DiskSkinToAppearance(3, 0x4) == Medium`
- `skinmap-original-2-dark`   — `DiskSkinToAppearance(2, 0x4) == Dark`
- `skinmap-legacy-0-light`    — `DiskSkinToAppearance(0, 0xA) == Light`
- `skinmap-legacy-1-medium`   — `DiskSkinToAppearance(1, 0xA) == Medium`
- `skinmap-legacy-2-dark`     — `DiskSkinToAppearance(2, 0xA) == Dark`

This closes the review's P0-2 coverage gap ("neither `recordfmt` nor `ucasflow`
ever calls `DiskSkinToAppearance`"): the read-side dialect is now pinned at the
production function. Residual disclosed: the full lot-load end-to-end skin
(`ConvertAvatar` on a spawned sim) is **not** exercised by these gates, so the
direct-map assertions prove the function, not the spawn pathway — noted in §6.

---

## 3. `recordfmt` byte-span tautology fixed (minor #4)

The `nbrs-raw-pd-bytes-a0` assertion previously recomputed
`personDataBytes = Version==0x4 ? 0xa0 : 0x200` and then asserted `0xa0 == 0xa0`
— circular. It now **measures** the written byte span:

- `FreeSO/TSOClient/tso.files/Formats/IFF/Chunks/NBRS.cs` — added a
  non-behavioural `Neighbour.PersonDataBytes` field, set inside the
  `Neighbour(IoBuffer)` read ctor as the stream-position delta around the
  PersonData read loop (`io.Position` before vs after).
- `Client/Simitone/Simitone.Client/AutotestCASFlow.cs`,
  `RawScanNbrs` — returns `nb.PersonMode > 0 ? nb.PersonDataBytes : 0` instead of
  re-deriving the size from `Version`.

So the recordfmt raw scan asserts the genuinely-written byte span
(`nbrs-raw-pd-bytes-a0 (160): PASS` — 0xa0 = 80 shorts) rather than echoing the
version branch.

---

## 4. `ImportAddNeighbor` legacy writer disclosed (honesty gap #3)

`FreeSO/TSOClient/tso.content/TS1/TS1NeighbourProvider.cs`,
`ImportAddNeighbor` (`:1124-1166`) still writes the **legacy** port record shape:
the constructed `Neighbour` keeps the class defaults (`Version=0xA`, `Unknown3=9`)
and sets `PersonMode=9`, so `Save` emits a `0x200`-byte (256-short) PersonData
block. It is the family-import / FAM path (r247), left deliberately unchanged as
separate and risky. A code comment now documents the residual at the construction
site, and it is recorded in `implementation.md` §8 and this file. The
CAS/create path (`SimitoneNeighbourGenerator.AddNeighbor`) is the byte-faithful
one everything R252 asserts.

---

## 5. Clean rebuild

The previously-published `Simitone.Client.dll` was stale (the source had
`recordfmt`, the built DLL did not). Forced a full recompile:

```
rm -rf Client/Simitone/Simitone.Client/bin Client/Simitone/Simitone.Client/obj \
       FreeSO/TSOClient/tso.simantics/bin  FreeSO/TSOClient/tso.simantics/obj \
       FreeSO/TSOClient/tso.files/bin       FreeSO/TSOClient/tso.files/obj

dotnet publish Client/Simitone/Simitone.Desktop/Simitone.Desktop.csproj \
  -c Release -r osx-arm64 --self-contained true -o publish/osx-arm64 \
  /p:TreatWarningsAsErrors=false /p:WarningsAsErrors="" -p:NoWarn=NU1605

./packmac.sh arm64
```

Result: `Built dist/The Sims-arm64.app` (2.1G), publish exit 0, zero build errors.

**Symbol verification in the rebuilt bundle** (`dist/The Sims-arm64.app/Contents/MacOS/`):
- `Simitone.Client.dll` — `recordfmt` identifiers present (`grep -ci recordfmt` = 9;
  the literal `recordfmt` is stored in the .NET UTF-16 user-string heap — 4
  UTF-16LE hits — so the raw ASCII `grep -c recordfmt` returns 0; the check name
  literal IS present).
- `FSO.Files.dll` — `NbrsFormat` present (1).
- `FSO.SimAntics.dll` — `SkinToDisk`, `DiskSkinToAppearance`, `CharStem`
  present (1 each).

---

## 6. Real gate output (re-run)

`recordfmt` and `ucasflow` ran with `-autotest-opts` against the absolute
game-data path; `recordfmt` excluded `mood` (R251-harness interaction caveat).
Binary: `dist/The Sims-arm64.app/Contents/MacOS/TheSims`,
`-path"/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims"`.

### recordfmt (focused) — **28/0 PASS**
```
AUTOTEST recordfmt nbrs-chunkver-3e (62): PASS
AUTOTEST recordfmt nbrs-raw-member-found: PASS
AUTOTEST recordfmt nbrs-raw-rec-version-4 (4): PASS
AUTOTEST recordfmt nbrs-raw-pm-5 (5): PASS
AUTOTEST recordfmt nbrs-raw-name-stem (user00024 vs user00024): PASS
AUTOTEST recordfmt nbrs-raw-pd-bytes-a0 (160): PASS      <-- MEASURED span
AUTOTEST recordfmt member-version-4: PASS
AUTOTEST recordfmt member-pm-5: PASS
AUTOTEST recordfmt member-name-stem: PASS
AUTOTEST recordfmt pd2-7-personality (generous=0): PASS
AUTOTEST recordfmt pd58-adult-27 (27): PASS
AUTOTEST recordfmt pd65-male-0 (0): PASS
AUTOTEST recordfmt pd60-skin-light-1 (1): PASS
AUTOTEST recordfmt pd61-family-chunkid (6): PASS
AUTOTEST recordfmt pd70-zodiac-0 (0): PASS
AUTOTEST recordfmt skinmap-original-1-light: PASS        <-- new
AUTOTEST recordfmt skinmap-original-3-medium: PASS       <-- new
AUTOTEST recordfmt skinmap-original-2-dark: PASS         <-- new
AUTOTEST recordfmt skinmap-legacy-0-light: PASS          <-- new
AUTOTEST recordfmt skinmap-legacy-1-medium: PASS         <-- new
AUTOTEST recordfmt skinmap-legacy-2-dark: PASS           <-- new
AUTOTEST recordfmt fami-version-7 (7): PASS
AUTOTEST recordfmt fami-no-trailing (len=44 expect 44): PASS
AUTOTEST recordfmt roundtrip-member: PASS
AUTOTEST recordfmt roundtrip-ver-pm-name: PASS
AUTOTEST recordfmt roundtrip-pd-present: PASS
AUTOTEST recordfmt roundtrip-fields-preserved: PASS
AUTOTEST recordfmt roundtrip-pd60-still-disk-1 (1): PASS
AUTOTEST recordfmt: PASS
AUTOTEST recordfmt passed=28 failed=0
AUTOTEST SUMMARY passed=1 failed=0 skipped=0
AUTOTEST RESULT PASS passed=1 failed=0
```

### ucasflow (focused) — **94/0 PASS**
```
AUTOTEST ucasflow: PASS
AUTOTEST ucasflow passed=94 failed=0 phase=15
AUTOTEST SUMMARY passed=1 failed=0 skipped=0
AUTOTEST RESULT PASS passed=1 failed=0
```

### Full default suite (no `-autotest-opts`) — **142/0 PASS**
```
AUTOTEST SUMMARY passed=142 failed=0 skipped=0
AUTOTEST RESULT PASS passed=142 failed=0
```
Zero `FAIL` lines across the whole run. The default `Config.Checks` string was
left byte-identical (`recordfmt`/`ucasflow` are opt-in only, not added to it).

---

## 7. Still open / residual (honest)

- **Lot-load end-to-end skin not gate-tested.** The `recordfmt`/`ucasflow`
  flows never spawn the created sim into a lot, so `ConvertAvatar` →
  `DiskSkinToAppearance` + `RecordVersionFor` on a *runtime* person object is not
  exercised end-to-end. The direct `skinmap` assertions pin the map function;
  the review's "untested at spawn" concern is thereby reduced but not fully
  closed (that would need a lot-load skin probe or a `ConvertAvatar` unit test).
- **`ImportAddNeighbor` legacy writer** — disclosed, unchanged (FAM import path).
- **Runtime-skin clobber** (`VMNetAvatarPersistState:217` writes the logical
  0/1/2 into `pd[60]` on a runtime skin change) — pre-existing disclosed residual,
  unchanged.

Everything else the review flagged is fixed and gate-verified.
