# R253 serialization/integration follow-up — 2026-09-08

## Scope and ownership

Follow-up to `tools/iff-dump/r252-integration-audit/round.md` (authored in the
independent `simitone-cas-support` checkout, branch `cas-r252-integration`).
This round lives in the main development checkout `simitone-fork` (branch
`mac-port`, FreeSO submodule `mac-port-rel`). It:

1. **integrates audit-fix `a96458e`** (recordfmt verdict hardening + CAS
   signature preservation) into the main source, adapted to the current tree;
2. **fixes the two P1 NBRS serialization defects** the audit reproduced
   (header count mismatch; legacy person-data tail erasure) using the included
   synthetic reproducers;
3. **assesses and selectively integrates** the earlier CAS/packaging
   improvements on `cas-r252-integration`; and
4. **validates in isolation** (private settings/saves, single instance).

The ongoing R253 generic-import/export research
(`tools/iff-dump/r253-import-export/port-audit.md`) is a separate
design/audit artifact and was **not** changed or committed by this round. No
concurrent game instance was run; validations were serialized.

## Audit-fix integration

`a96458e` was authored on top of the merged `cas-r252-integration` branch;
each hunks applied cleanly to `mac-port` (the R252 base already had
`RecordFmtFailed`/`_recordFmtPassed`/`_recordFmtFailed`, the `simitone_debug.log`
append, and the R252 recordfmt block). Ported files:

- `Client/Simitone/Simitone.Client/AutotestCASFlow.cs` — replaced
  `RecordFmtFailed` with `RecordFmtSucceeded`:
  `_recordFmtCompleted && _recordFmtPassed > 0 && _recordFmtFailed == 0 && Failed == 0`;
  added the `_recordFmtCompleted` flag (set at the end of `CheckRecordFmt`);
  diagnostics now print `completed=` and `flowFailed=`.
- `Client/Simitone/Simitone.Client/AutotestRunner.cs` — `recordfmt` verdict now
  uses `RecordFmtSucceeded` instead of `RecordFmtFailed == 0`.
- `Client/Simitone/Simitone.Client/UI/Screens/TS1CASScreen.cs` — `SetFamilies`
  writes family diagnostics via `Console.WriteLine` instead of
  `File.AppendAllText("simitone_debug.log", …)`, removing the write that
  mutated the signed bundle and broke read-only installs.

This closes the audit's P2s: a focused `recordfmt` run can no longer turn green
through zero counters when the CAS flow never reaches the format checks, and a
CAS run no longer invalidates the app signature.

## NBRS serialization fixes (audit P1s)

`FreeSO/TSOClient/tso.files/Formats/IFF/Chunks/NBRS.cs` (FreeSO `mac-port-rel`):

### P1a — header count must equal emitted records

`Write` previously wrote `Entries.Count` (which includes placeholders/malformed
decodes that live only in `Entries`) but iterated `NeighbourByID.Values` (the
validly-decoded, id-keyed records). A native reader desynced on the tail. The
count now writes `NeighbourByID.Count` — the number of records actually
serialized. **Placeholder policy (documented, not silently chosen):** records
with `Unknown1 <= 0` are runtime-only and are not persisted; the header count
matches the emitted live records.

### P1b — legacy person-data tail preserved on round-trip

Version `0xA` records carry 256 shorts on disk but the runtime `PersonData`
view is 88 shorts. The reader previously read 88 then discarded the remainder;
`Save` re-wrote the remainder as zeros — erasing opaque data on any
read+rewrite. Added an additive `short[] LegacyPersonDataTail` field that is
populated from the remaining `totalShorts - 88` (168 for 0xA; null for 0x4
which carries 80 shorts, all within the view) and re-emitted verbatim on save.
Runtime interpretation of `PersonData` is unchanged (still length 88).

## Earlier CAS/packaging assessment

Assessed the four commits on `cas-r252-integration` that predate the audit-fix:

- **`836734b` (private settings dirs)** — **integrated.** Adds `-autotest-userdir`
  to `Simitone.Desktop/Program.cs`, letting an autotest isolate its settings/
  saves dir. Low-risk, directly enables the isolated validation below.
- **`743253e` (packmac resources out of the executable dir)** — **integrated.**
  Moves `Content/` to `Contents/Resources/Content` with a `MacOS/Content`
  symlink, removes the duplicated nested app, and makes `codesign --deep
  --strict` depend on real verification. Packaging robustness; no game behavior.
- **`0529f98` (CAS preview in isolated fixed-size surface) + `6102a8b` (CAS
  dialog focus + staging-Sim hide)** — **assessed, NOT integrated.** These are
  a larger, interdependent CAS-preview/focus campaign (new `UIOriginalVitaPreview`
  render-target class, `VitaSurface` wiring, focus management, 17 extra ucasflow
  assertions). They are not required for the audit-fix or the serialization
  fixes; the main checkout's R252 `ucasflow` already passed 94/0 without them.
  Integrating them without their own focused visual validation would conflict
  with the standing "do not claim parity from passing traces alone" rule, so
  they are left on `cas-r252-integration` for a dedicated CAS-preview round.

## Validation (isolated, serialized)

No game process was running at the start of any run; `validate.py` refuses to
start if another `TheSims` is active and never stops another process. Every run
used a private `-autotest-userdir`; the owner's
`~/Documents/Simitone/config.ini` hash is unchanged in every isolation record,
and game-data `Neighborhood.iff` was byte-identical/untouched throughout.

### Serialization reproducers (reflection-free, published FSO.Files.dll)

- **Before** (original published `FSO.Files.dll` `5b68002d…`):
  `{"declaredRecords":2,"actualRecords":1,"countMatches":false,"legacyWord100Before":123,"legacyWord100After":0,"retainedPersonDataWords":88,"afterEqualsBefore":false}`
- **After** (rebuilt `FSO.Files.dll` `e894bff3…`):
  `{"declaredRecords":1,"actualRecords":1,"countMatches":true,"legacyWord100Before":123,"legacyWord100After":123,"retainedPersonDataWords":88,"legacyTailWords":168,"afterEqualsBefore":true}`

Both defects are reproduced before and fixed after; `retainedPersonDataWords`
stays 88 (runtime view unchanged).

### recordfmt verdict probe

`verdict-probe` against the rebuilt `Simitone.Client.dll`:
**7/7 scenarios** (never-reached-checks, zero-counters, partial-checks,
completed-but-empty, format-failure, later-flow-failure, success). Only the
genuine success case is a pass; all false-green paths correctly fail.

### Packaged runs (isolated `dist/The Sims-arm64.app`, strict codesign verified)

- **Combined `ucasflow,recordfmt` @ 1024×768:** `recordfmt passed=28 failed=0
  completed=True flowFailed=0`; `ucasflow passed=94 failed=0`; overall
  `SUMMARY passed=2 failed=0`; clean `after Run` / `after Dispose`; no
  `simitone_debug.log` created in the bundle.
- **Default suite @ 1024×768:** `SUMMARY passed=142 failed=0 skipped=0`,
  `RESULT PASS`, clean Run/Dispose. `simitone_debug.log` absent from the bundle.

## Hashes (publish == packaged app)

- `Simitone.Client.dll` `860dae1be7abe7b01a1a7bf5af470cd155f830e6eefbd942503d189be87045e5`
- `FSO.Files.dll` `e894bff3f1af6e3a881d006b50563a0bcac84e4af8854f95997136ae04bfe50e`
- `dist/The Sims-arm64.app` ≈ 774 MB

## Honest boundaries

- The isolated runs prove the audit-fix verdict, the CAS signature fix, the
  two NBRS serialization fixes, and no regression on the default suite. They do
  **not** establish full game parity or native-reader compatibility for the
  placeholders-policy choice (the port's end-of-stream-tolerant reader is
  tested; a strict third-party reader is not).
- The CAS-preview/focus campaign (`0529f98`/`6102a8b`) is **not** integrated
  here; `ucasflow` remains 94/0 (not the 111/0 seen with those changes).
- The R253 generic import/export work remains a separate, design-only artifact.
