# R252: created-record format alignment — the port now writes byte-faithful original NBRS/FAMI

R246 established the created-record canon and documented a list of *port-chosen,
unproven-against-the-engine* divergences. R252 pins and closes them: the port now
**writes** the ORIGINAL created-record format and **reads** it correctly.

## The format (skeptic-verified from the owner's staged saves)

* **NBRS** — chunk-data Version `0x3E` (UserData) / `0x3F` (UserData2-8) [the port wrote `0x49`]; per-record Version `0x4` (80-short PersonData, `0xa0` bytes) [port `0xA`/256]; `PersonMode 5` [port 9]; `Name` = **lowercase character-file stem** `userNNNNN` [port literal `"iffname"`]; skin `pd[60]` = **lgt=1, drk=2, med=3** (non-monotonic) [port 0/1/2]. PersonData shorts little-endian.
* **FAMI** — Version `7`, data `40 + 4·guidCount`, **zero trailing bytes** [port Version 9 + 16 trailing zeros].
* **Field map**: `pd[2..7]` personality (pd[4]=Generous always 0), `pd[58]` age (9 child/27 adult), `pd[60]` skin, `pd[61]` family id = FAMI chunk id, `pd[65]` gender (1 female/0 male), `pd[70]` zodiac (0).

The skeptic independently re-derived every claim from the raw staged bytes (its own
IFF walker + record parser + a STR#200 slot decoder) and **confirmed all nine**; it
also **corrected R246**: the NBRS record `Name` is the **lowercase** stem
(`user00023`), not `User00024` (a filename-casing conflation). `verify.py` re-pins the
binary SHA256 and asserts the record bytes — PASS.

## What the port now does

* **Write** (NBRS.cs, FAMI.cs, SimitoneNeighbourGenerator.cs): chunk `0x3E`, record
  `Version 0x4`, 80-short PersonData, `PersonMode 5`, `Name = CharStem`, `pd[60] =
  SkinToDisk(SkinTone)` (0→1, 1→3, 2→2), FAMI `Version 7` no trailing zeros, honouring
  the `Version` field in `FAMI.Write`.
* **Read** (VMTS1ActivatorNew): version-aware `DiskSkinToAppearance` — for Version `0x4`
  disk `1→Light, 3→Medium, 2→Dark`; for legacy `0xA` disk `0→Light, 1→Medium, 2→Dark`.
  This **fixes a pre-existing bug**: the shipped original-format saves carry disk skin
  `1/2/3`, but the port's old cast `(AppearanceType)pd[60]` mis-decoded them
  (light→Medium, medium→garbage). `RecordVersionFor` now falls back to the `0x4`
  dialect (the correct one for original + new records, since `pd[31]` is 0 on both).
* **Reader compatibility**: the NBRS reader and FAMI reader already branch on the
  version and tolerate `0x4`/`0xA` and v7/v9, so old port saves still load.

## The recordfmt opt-in check

Opt-in (NOT in the default `Config.Checks`, byte-identical). Uses the R246 isolation
idiom and the real `SaveFamily` drive, reads the written `Neighborhood.iff` with a
fresh `IffFile`, and asserts: chunk `0x3E`, record `Version 0x4` + measured 160-byte
PersonData span, `PersonMode 5`, `Name == charStem`, `pd[2..7]` 0..1000 with `pd[4]==0`,
`pd[58]==27`, `pd[65]==0`, `pd[60]==1` (Light), `pd[61]==fami id`, `pd[70]==0`, FAMI
`Version 7` length `40+4·guidCount`, a write→read→re-write round-trip that loses no
field, and the six `skinmap` map assertions (original + legacy dialects). The `ucasflow`
format pins were repointed from the divergent values to the original (PersonMode 5,
record 0x4, name-stem, skin 1/2/3) — strengthened, not weakened.

## Validation

* Focused `recordfmt` (`-autotest-opts recordfmt`, run without `mood` to avoid the
  R251 harness interaction): **`recordfmt passed=28 failed=0`** (`focused-recordfmt.log`).
* Focused `ucasflow`: **`ucasflow passed=94 failed=0`** (`focused-ucasflow.log`).
* Full default suite: **`AUTOTEST SUMMARY passed=142 failed=0 skipped=0`** /
  `AUTOTEST RESULT PASS` (`default-final.log`).
* The adversarial review found and the repair fixed a P0 (`RecordVersionFor` fell back
  to the legacy `0xA` dialect, so disk `1/2/3` was still decoded as `0/1/2`); the skin
  map is now asserted directly. Dist == publish; DLL hashes in `hashes.txt`.

## Disclosed residuals (honest)

* The `recordfmt`/`ucasflow` flows do not spawn the created sim into a lot, so
  `ConvertAvatar`→`DiskSkinToAppearance` on a runtime person object is not exercised
  end-to-end — the direct `skinmap` map assertions cover the remap function only (a
  lot-load skin probe is a named follow-up).
* `TS1NeighbourProvider.ImportAddNeighbor` (the FAM-import path) still writes the legacy
  `0xA`/PersonMode-9/256-short format — deliberately unchanged, disclosed inline.
* Value-level defaults: `MakePersonData` writes `pd[36]=50, pd[56]=-1-if-0, pd[57]=4,
  pd[33]=25, pd[29]=1, pd[32]=2` and randomizes skills/interests, whereas the original
  leaves several at 0 / uses a different dialect — structural-vs-value, not aligned here.
* The NBRS declared-count vs saved-record mismatch (r248 P2-4), the `rsmp` drop,
  `FAMI.Unknown=24` vs original 1, `pd[67]/pd[68]`, and the runtime-skin clobber
  (`VMNetAvatarPersistState:217`) are pre-existing disclosed residuals.
