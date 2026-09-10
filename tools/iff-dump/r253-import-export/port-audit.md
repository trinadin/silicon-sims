# R253 — FAM import/export port audit (generic import + re-export)

Round R253 prepares making the port's FAM import **generic** (not tutorial-only) and
adding a genuine **Export/re-export** path. This file maps the *current* state,
states the export-absence finding, gives the minimal edit set for generic import +
export, and specifies an opt-in `impexport` fixture. It is a design/audit artifact
only — no production code is written in this round.

Line numbers are the checked-out state at audit time. Note two filename facts that
differ from the task brief:

- **No `VMTS1NeighbourProvider.cs` / `TS1NeighbourGenerator.cs` exist.** The engine
  halves live in `FreeSO/TSOClient/tso.content/TS1/TS1NeighbourProvider.cs`
  (import law) and `FreeSO/TSOClient/tso.simantics/Utils/SimitoneNeighbourGenerator.cs`
  (family creation).
- **Not-readable engine files:** the task listed
  `VMTS1NeighbourProvider.cs` + `TS1NeighbourGenerator.cs`; the real counterparts are
  the two files above.

---

## 1. Current-state map — the import is **tutorial-only**

### 1.1 The poll gate (the hard tutorial latch)

`TS1NeighborhoodProvider.CheckForNewImports()` — `TS1NeighbourProvider.cs:574`:

- Scans `Directory.GetFiles(UserPath+"Import", "*.FAM")` (`:580`); no `Import/` dir or
  no `*.FAM` → returns `0` (nothing staged).
- **The gate is a literal filename check:** `:588-590`

  ```csharp
  var tutorialPath = Path.Combine(importDir, "Tutorial.FAM");
  if (!File.Exists(tutorialPath)) return 0;
  ```

  So **only a file literally named `Tutorial.FAM` auto-imports.** Any other `.FAM`
  in `Import/` is silently ignored (return `0`). This is the "tutorial-only latch".

- `ValidateImportFile` (`:614`) then requires the file open as an IFF and carry an
  `EXPi` whose `FamilyID` resolves to a `FAMI`. Failure is a SILENT skip (return `0`),
  mirroring the native "if ok:" law (decode §1.2).
- `ImportFamFile` runs (`:596`); tri-state return: `0`→ok (client shows "Imported"),
  `-1`/`-50`→`2` (client shows "Couldn't import the family"). 

**The class-level doc comment (`:564-573`) owns the disclaimer** that this is the
tutorial auto-import path.

### 1.2 What triggers the poll

Client side (`Simitone.Client`):

- `TS1GameScreen.TickTutorialImportPoll()` — `TS1GameScreen.cs:1331`: runs in the
  screen `Update`, **only** while `!InLot && TS1NeighPanel != null`, at a 1000 ms
  cadence (`_lastTutorialImportPoll`), skipped entirely when
  `TutorialImportPollSuppressed`. Increments `TutorialImportPollAutoCount`.
- `TS1GameScreen.PollNeighborhoodImports()` — `TS1GameScreen.cs:1343`: calls the
  engine bridge `TutorialEngine247.PollImports()`; on `Imported` it rebuilds the
  neighborhood screen **only if** `GetLastImportHouse() != 0` (native §1.2
  reloadScreen gate); on `Error` it shows the debounced
  "Couldn't import the family"/"Error" dialog (once per session, ok→error transition).
- The engine bridge `TutorialEngine247.PollImports()` — `TutorialEngine247.cs:157`:
  reflection-dispatches `CheckForNewImports`; maps `bool`/`int`/`Enum` to
  `ImportPollResult { Nothing=0, Imported=1, Error=2 }` (`:36-41`).

So the poll runs on the **neighborhood-screen loop**, not the tutorial lifecycle. The
tutorial lifecycle only *stages* the file; the neighborhood screen *consumes* it.

### 1.3 What `ImportFamFile` produces and does NOT produce

`ImportFamFile(famPath, out newFamilyId)` — `TS1NeighbourProvider.cs:640`. The 25-step
law (stroke-by-stroke in `r247-fam-import/decode.md`, corrections in
`skeptic-corrections.md`). Produces:

- **Reads (steps 1-7, `:660-695`)**: IFF open; `EXPi`; `FAMI`; display name
  (`FAMs` string 0, CTSS-1000 override); `NBRS`; `FINV`; tutorial latch read
  `SIMI` global 26 → `tutFlag` (`:692-695`).
- **Pre-mutation guard block (`:700-738`)**: family-id law (keep `EXPi.FamilyID` if
  free in bin, else `0` — no replace-on-collision) (`:703-706`); house-file existence
  when `house != 0` (`:713`); id-0 occupancy when family-only (`:718-722`);
  multi-claimant (`:730-734`); template-person presence (`:737-738`).
- **Eviction (step 12, `:743-764`)**: the occupying family of a house import loses the
  house, keeps net worth (`Budget += ValueInArch`), `Unknown &= ~8`; its members'
  characters are deleted (`DeleteImportCharacter`, `:1190`).
- **Family creation (steps 8-11, `:770-798`)**: creates/reuses FAMI (id-0 Default
  sentinel in-place reset on collision), sets `Budget`/`ValueInArch`, `FamilyNumber`,
  `FAMs` name.
- **Member creation (step 14, `:805-842`)**: per member id, kind from `uChr` string 13
  (dog/cat skipped, pets-out-of-scope), fresh GUID
  (`GenerateImportGUID`), `PrepareImportTemplatePerson` writes
  `Characters/User#####.iff`, `ImportAddNeighbor` registers the NBRS record, `FINV`
  inventory carry.
- **GUID translation + RelMatrix carry (step 15, `:852-876`)**: Gtab pair per member,
  relationship rows copied OLD→NEW id (skeptic C1).
- **Gtab splice (step 16, `:884-895`)**: atomic `WriteGtabIntoFamFile` (`:1295`).
- **Friends count (`:898-916`)**.
- **Occupancy bookkeeping (steps 17-19, `:919-932`)**: house or family-only.
- **Tutorial latch (step 20, `:935-947`)**: if `tutFlag`, `TutorialHouse = house`,
  `NeighborhoodData[3] = 0`; else clears latch if re-importing over the same house.
- **Consume the file (steps 21-23, `:957-986`)**: house import moves `Houses/HouseNN.iff`
  → `.tmp`, FAM → `HouseNN.iff`, delete `.tmp` (rollback on failure, `-50`); family-only
  deletes the FAM.
- **Immediate neighborhood save (step 24, `:991`)** `SaveNeighbourhood(false)`.
- Sets `LastImportHouse` (`:999`) for the client refresh gate.

What it does NOT do:
- **No Export/ mirror.** Step 25 comment (`:993-998`): *"The `Export/` mirror refresh
  of the free wrapper (imported + evicted families, skip id 4000) is UNIMPLEMENTED:
  nothing in the port writes or reads `Export/`."*
- **No generic-family UI/confirmation.** The import is silent/automatic on the
  neighborhood screen; there is no confirmation for a generic family, no file-picker.
- **Pets** are skipped (out of scope, `:815-824`).
- **The 4000 reserved-id guard** on the out-id write / export refresh
  (ADAPT note `:950-953`).
- **Port record shape divergence** on re-created members — see §4.3 residual.

### 1.4 Codec round-trip readiness (for export)

All FAM chunks are registered in `IffFile.CHUNK_TYPES`
(`FreeSO/TSOClient/tso.files/Formats/IFF/IffFile.cs:30-83`): `FAMI` (`:66`), `FAMs`
(`:68`), `NBRS` (`:65`), `EXPi` (`:81`), `uChr` (`:82`), `FINV` (`:83`), `CTSS`
(`:33`), `SIMI`, `Gtab`. Each implements `IffChunk.Read`/`Write`:

- `FAMI.cs` — v7 write, data `40 + 4·guidCount`, **no trailing zeros** (R252).
- `NBRS.cs` — chunk 0x3E, record 0x4 / PersonMode 5 / `Name`=lowercase stem (R252).
- `EXPi.cs` — famId + 8 member ids + flag byte; Write pins version 0x3F.
- `uChr`/`FAMs` — `STR` subclasses.
- `FINV.cs` — positional inventories.
- `Gtab.cs` — reconstructed serialization (no shipped sample).

`IffFile.Write(stream)` (`IffFile.cs:279`) writes the header + each chunk via
`WriteChunk` (`:294`), so a freshly constructed `new IffFile()` + `AddChunk` +
`Write(stream)` is a valid IFF. **This is the export primitive.**

---

## 2. Export path — **there is no genuine FAM/family export**

Searched the whole client + engine. Three candidate surfaces, none of which writes a
family/`.fam`:

1. **Lot-query "Export" button — a disabled no-op.**
   `UIHouseSelectPanel.cs:337-343` builds four option buttons:
   `{ Evict/Bulldoze (family!=null), Rezone, Export, Back }`; `:334` sets
   `optionFunctions[2] = null` for "Export", and `:352`
   `btn.Disabled = optionFunctions[i] == null` makes it a **disabled, inert button**.
   Export is not "missing", it is present but does nothing.

2. **Neighborhood "Import Family" nav button — not ported.**
   `UINeighbourhoodSwitcher.cs:74` defines the `Import` slot; `WireClick`
   (`:332-368`) falls through its `default:` case (`:361-366`) which only does
   `GameLog.Write("uinav: Import click (system not ported)")`. The native
   `CycleThroughImports` file-picker flow is **decoded but unimplemented**
   (R248 disclosed residual).

3. **`VMWorldExporter` — house blueprint, not a family.**
   `SaveHouseButton_OnButtonClick` (`TS1GameScreen.cs:1162-1177`) calls
   `new VMWorldExporter().SaveHouse(vm, .../Blueprints/cas.xml)`.
   `VMWorldExporter.cs` writes only floors/walls/object-GUID XML — **no FAMI/FAMs/NBRS/
   character data.**

4. **`OriginalSnapshotAlbum.ExportName`** (`…/Model/OriginalSnapshotAlbum.cs:147-159`)
   computes a *photo-album* family export **name** (`Family::GetExportName` uses the FAMI
   resource id) — it names snapshots, it does not write a `.fam`.

**Finding: no code path in the port serializes a live family/household to a `.FAM`
file, and no code reads an `Export/` directory.** The only family persistence is (a)
in-memory FAMI/FAMs/NBRS in `Neighborhood.iff` via `SaveNeighbourhood`, and (b) the
per-character `Characters/User#####.iff`. This is the R253 gap.

Note: `Import/` and `Export/` directories DO exist in game-data
(`game-data/The Sims/UserData/{Import,Export}/`), each containing only a `_` placeholder,
and `game-data/The Sims/TemplateFamilyUnleashed/` holds six byte-canonical **non-tutorial**
reference FAMs (Burb_11, Charming_13, Goth Sr_12, Hick_10, Kat_6, Strays_4000) — read-only
fixture sources, see §4.1.

---

## 3. Minimal edit set

### 3.1 Generic import (make `CheckForNewImports` handle ANY `.FAM`)

The `ImportFamFile` body is **already generic** (id-collision → 0, eviction, empty/new
house, missing uChr/CTSS fallbacks, tutorial latch driven by the FAM's SIMI g26 not the
filename). The **only** gate is the literal `Tutorial.FAM` filename check. Minimal edit:

- **`TS1NeighbourProvider.CheckForNewImports` (`:574`)**: replace the
  `if (!File.Exists(tutorialPath)) return 0;` gate (`:589-590`) with a loop over
  `fams` (the `*.FAM` scan result). For each candidate, run `ValidateImportFile`; on the
  first valid one run `ImportFamFile`; return `1` if it succeeds, `2` if it refuses,
  `0` if none is valid. Import **one** FAM per poll (one-per-second cadence), leaving
  the rest for subsequent polls — keeps the per-poll work bounded and deterministic.
  Invalid/incomplete `.FAM` files are skipped silently (stay on disk), matching the
  native silence on a `GetImportInfoForFile` failure.

  ```csharp
  foreach (var famPath in fams)
  {
      if (!ValidateImportFile(famPath)) continue;   // silent skip, keep file
      var rc = ImportFamFile(famPath, out _);
      return (rc == IMPORT_OK) ? 1 : 2;             // import one per poll
  }
  return 0;
  ```

- **Do NOT weaken the `uitutorial-lifecycle` battery.** It stages
  `Import/Tutorial.FAM`; a generic loop imports that file identically (superset). The
  tutorial latch for it stays data-driven from the FAM's SIMI g26 (=1). The step-20
  latch write is unchanged.

### 3.2 Export (new path, reuses the R248 FAM layout)

New engine method on `TS1NeighborhoodProvider`:

```csharp
/// <summary>Serialize <paramref name="familyId"/> to a .FAM in &lt;UserPath&gt;Export/.
/// Returns the written path, or null on failure.</summary>
public string ExportFamily(ushort familyId);
```

Write path: `Path.Combine(UserPath, "Export")` (created on demand), file named
`<exportName>.FAM` where `exportName` derives from the `FAMs` display name (sanitized
against invalid filename chars) + family number to guarantee uniqueness. (The exported
file is NOT written into `Import/`; the user/battery moves it, matching the original's
copy-a-FAM → `Import/` model.)

Chunk set to write (via `new IffFile()` + `AddChunk` + `Write(stream)`):

- `EXPi` — `FamilyID = familyId`, `MemberIDs[0..n-1] = member ids`, `FlagByte=1`.
- `FAMI` — clone the live FAMI (HouseNumber, FamilyNumber, Budget, ValueInArch,
  FamilyFriends, Unknown, FamilyGUIDs).
- `FAMs` — display name.
- `NBRS` — a **subset** chunk containing only the family's member records (clone each
  member's `Neighbour`, its `PersonData`, `Relationships`, Name, GUID, NeighbourID).
- `uChr` per member — clone the character's STR# 200 bodystring set
  (from `Characters/User#####.iff` or the live `Resource.Get<STR>(200)`).
- `CTSS` per member — name/bio (character CTSS 2000 strings 0/1).
- `FINV` (optional) — member inventories, positional.
- `SIMI` (optional) — set global 26 = 1 if the house is a tutorial house
  (`IsTutorialHouse`), else omit/zero.
- `Gtab` — **omit** on fresh export (it is an old→new mapping produced by re-import).

### 3.3 Wire the UI surfaces

- **Lot-query "Export"** (`UIHouseSelectPanel.cs`): set `optionFunctions[2]` to a
  handler that calls `neigh.ExportFamily(family.ChunkID)` and shows a lightweight
  confirmation/alert with the written path; remove the `= null` so the button enables.
- **Neighborhood "Import Family"** (`UINeighbourhoodSwitcher.cs:361`): wire the
  `default`/Import case to an immediate `AvailableNeighborhoodPoll()` (or a file-picker
  that copies a chosen `.FAM` into `Import/`). Optional — the auto-poll already makes
  generic import work; this is the explicit user-facing trigger.

### 3.4 Edge cases for generic import + export

- **FAM in a full neighborhood** — a house import refuses if the target house file is
  missing (`:713`), more than one claimant (`:730`), or the id-0 slot is genuinely held
  for a family-only import (`:718`). These stay refusals (`return 2` → client "Couldn't
  import"). A **family-only** import into a full bin (every id taken incl. id-0) also
  refuses. Good.
- **FAM with a family-id collision** — handled: `newId = 0` (`:704`), in-place reset of
  the id-0 Default shell (`:770-791`).
- **Missing character files / uChr / CTSS** — `uChr` null → kind becomes human;
  `PrepareImportTemplatePerson` (`:1015`) blanks bodystrings and falls back to the
  `userNNNNN` name; `NBRS` record missing → template `PersonData`.
- **Export of the 4000 Strays** — the special-case guard skips the Export refresh for
  id 4000 (`:950-953`); the export function should mirror the "skip id 4000 refresh"
  nuance or explicitly handle a 4000 export.
- **Export a family currently in a lot** — snapshot the FAMI per its persisted
  `HouseNumber`/members; the live sim's transient state is not part of a FAM.

---

## 4. `impexport` opt-in fixture

### 4.1 Fixture sources (read-only game-data)

Two byte-canonical, **non-tutorial** sources in game-data (never modified):

- `game-data/The Sims/TemplateFamilyUnleashed/Strays_4000.FAM` — FAMI id 4000,
  **house 0** (family-only), budget 19728, **8 members**, EXPi ids
  `[179,178,177,175,174,173,172,171]`.
- `game-data/The Sims/TemplateFamilyUnleashed/Charming_13.FAM` — FAMI id 13, house 69,
  budget 1373, **2 members**, EXPi ids `[127,126]`.

For the "arbitrary non-tutorial FAM" stage, copy a template FAM into the isolated
`UserData/Import/` (a copy — game-data stays read-only). For the eviction scenario we
need a FAM whose target house is *occupied in the port's UserData*, which the pristine
clone may not have; use the export helper to build it (§4.3 Scenario 2).

### 4.2 Isolation + registration (R246/R247 idiom)

- Redirect `FSOEnvironment.UserDir` to `Path.GetTempPath()/simitone-r253-<guid>` in
  `Begin`, **before** `Content.Init` (mirrors
  `AutotestTutorialLifecycle247.BeginIsolation`, `AutotestTutorialLifecycle247.cs:92`,
  and `AutotestCASFlow.BeginIsolation`; both wired in `AutotestRunner.Begin`
  `AutotestRunner.cs:281,285`). The clone copies pristine `UserData` into the temp dir
  (`TS1NeighbourProvider.InitSpecific`, `TS1NeighbourProvider.cs:59`).
- Pin game-data byte-identical: SHA-256 `game-data/The Sims/UserData/Neighborhood.iff`,
  `.../Tutorial.FAM`, and the chosen `TemplateFamilyUnleashed/*.FAM` at Begin; re-hash
  at End. All reads into the temp dir are copies.
- **Register as opt-in, NOT in the default `Config.Checks`** (`AutotestRunner.cs:76`
  must remain byte-identical; `impexport` is absent). Add:
  - `AutotestRunner.Begin`: `AutotestImpexport.BeginIsolation(c);`
  - neighborhood-ready branch (mirror `:364-371`):
    `if (CheckEnabled("impexport")) { _impexport = new AutotestImpexport(Log); _state = 10; return; }`
  - a `StateImpexport()` that drives the battery and `Pass("impexport")`/`Fail("impexport")`
    (mirror `StateTutorialLifecycle247`, `:612-620`; `Pass`/`Fail` at `:22574-22584`).
- Do NOT use a hardcoded family id unless created in-dir; the port's family-id law
  (keep id if free else 0) means ids shift. Assert by `FAMs` name + member count +
  budget, and only pin an id after `CreateFamily` returns it.

### 4.3 Two concrete scenarios

#### Scenario 1 — generic import of a non-tutorial family (gate removal)

Stage `Charming_13.FAM` (2 members) into `UserData/Import/Charming_13.FAM`. **Patch its
FAMI house to a free/existing house (or to 0 for family-only)** in the temp copy so it
does not depend on game-data house 69 — patch the FAMI `house` int32 at payload offset
12 (layout `[pad:4][ver:4][IMAF:4][house:i32]`, LE) in the isolated copy. Drive
`PollNeighborhoodImports()` once.

Assertions:
- `result == Imported` (NOT Nothing) — proves the poll accepts a `*.FAM` that is **not**
  named `Tutorial.FAM`.
- `Import/` file consumed: `!File.Exists(Import/Charming_13.FAM)`.
- New `FAMI` present with `FamilyGUIDs.Length == 2`.
- `FAMs` at that id: `GetString(0) == "Charming"`.
- `FAMI.Budget == 1373` (from Charming_13) preserved.
- **No tutorial latch set** (the FAM's SIMI g26 is 0): `TutorialHouse == 0` and
  `TutorialState` unchanged from baseline.
- Two fresh `Characters/User*.iff` created beyond baseline, each with
  `OBJD` (id 128) and `STR` 200 present.
- If the import landed in an occupied house, the occupant is evicted (see Scenario 2).

#### Scenario 2 — export → re-import round-trip, then occupied-house eviction

(a) **Round-trip (empty slot):** In the isolated UserData,
`SimitoneNeighbourGenerator.CreateFamily("RoundTrip", 2, infos)` (returns `famId`),
record baseline: name "RoundTrip", 2 members, `Budget`, `FamilyNumber`, `FamilyGUIDs`.
Call `ExportFamily(famId)` → path P.

Assertions (export):
- `File.Exists(P)` and `File.Exists(Export/)`.
- Fresh-parse `IffFile(P)`: has `EXPi` with `FamilyID == famId`, `FAMI` with
  `FamilyGUIDs.Length == 2`, `FAMs.GetString(0) == "RoundTrip"`, and **two** `uChr`
  chunks whose member ids match the EXPi ids.

Assertions (re-import round-trip):
- Free the slot: `neigh.MainResource.FullRemoveChunk(famI)` +
  `SaveNeighbourhood(false)` so id `famId` is free (an "empty slot"); a house-0
  family-only re-import then claims `famId`.
- Stage P into `Import/`, drive poll. Assert a new `FAMI` with `ChunkID == famId`,
  `FamilyGUIDs.Length == 2`, `FAMs.GetString(0) == "RoundTrip"`, `Budget == baseline`,
  `FamilyNumber == baseline`, and the two members' `Neighbour` `PersonData` re-keyed
  (`pd[31] = new NeighbourID`, `pd[61] = famId`). **Identity preserved** (name, member
  count, budget, family number, bodystrings) — internal `GUID`s/`NeighbourID`s legitimately
  change on re-import (fresh `GenerateImportGUID` / lowest-free NID).

(b) **Occupied-house eviction:** Create family A (2 members) occupying a known house
`H` (e.g. `SetFamilyForHouse(H, famA, false)` where `HouseH.iff` exists in the clone,
e.g. house 5). Build the FAM for family B as a house import targeting `H`: export family
B (2 members), then patch its FAMI `house` field to `H` in the temp copy, stage it, drive
poll. Assertions:
- Family A loses the house: `A.HouseNumber == 0`, `A.Budget == baselineA.Budget + A.baselineValueInArch` (net-worth preservation), `A.Unknown & 8 == 0`.
- Family A's characters deleted: no `User*.iff` for A's GUIDs beyond baseline; the two
  fresh files are B's.
- The imported family occupies `H`: new `FAMI.HouseNumber == H`, `FamilyForHouse[H]` is
  it, and `Houses/HouseNN.iff` now carries the imported FAM's `FAMI`/`FAMs` chunks
  (`house-NN-is-the-fam-file`).
- `LastImportHouse == H` (the client refresh gate).

---

## 5. Cross-agent contract seam (implementation signature)

The engine (`FreeSO`, `mac-port-rel`) and client (`Client/Simitone`, `mac-port`) split,
resolved by reflection through `TutorialEngine247` just like `CheckForNewImports`.

Engine (`TS1NeighborhoodProvider` — `tso.content/TS1/TS1NeighbourProvider.cs`):

```csharp
// EXISTING — keep the return contract stable (0 ok / -1 guard / -50 move)
public int ImportFamFile(string famPath, out int newFamilyId);   // :640
public int CheckForNewImports();                                  // :574  (gate removal)
public int LastImportHouse { get; }                               // :608
public bool IsTutorialHouse(short house);                         // :502

// NEW
/// <summary>Serialize a family to &lt;UserPath&gt;Export/. Returns path or null.</summary>
public string ExportFamily(ushort familyId);

/// <summary>Generic-import gate removal: import the first valid *.FAM in Import/.</summary>
// (implemented inside CheckForNewImports; no new public signature required)
```

Client (`Simitone.Client/Utils/TutorialEngine247.cs`):

```csharp
// EXISTING
public enum ImportPollResult { Nothing = 0, Imported = 1, Error = 2 }
public static ImportPollResult PollImports();        // :157
public static int GetLastImportHouse();              // :217

// NEW (reflection bridge, same Probe() pattern)
public static string ExportFamily(int familyId);     // returns written path / null
```

`CheckForNewImports`'s tri-state and `ImportFamFile`'s return code must not change so
the existing `uitutorial-lifecycle` and the `impexport` fixture can share the same
contract.

---

## 6. Disclosed residuals / gaps (be honest)

- **`Export/` mirror refresh** (step 25) remains unimplemented regardless of the new
  `ExportFamily`; the two are independent (the native's step-25 free-wrapper mirror vs
  a user-initiated export). Documented at `TS1NeighbourProvider.cs:993-998`.
- **Re-imported records keep the LEGACY port NBRS shape** — `ImportAddNeighbor`
  (`:1155-1167`, R252 residual) writes `Version 0xA`, `PersonMode 9`, 256-short
  `PersonData`, not the original 0x4/5/160-byte shape. So the round-trip assertion
  should NOT compare raw NBRS record bytes; compare semantic fields only. Pinning the
  record shape is a separate R252 follow-up.
- **Generic-import order** (one FAM per poll) is a port choice; the native
  `CycleThroughImports` is a UI picker, not a bulk auto-import. If multiple `.FAM` are
  present, the port consumes one per second. Documented.
- **Pets** in a generic FAM are skipped (only-human), matching the port's lack of pet
  machinery (`:815-824`).
- **Gtab/FINV serializations** are reconstructed (no shipped sample) — an export that
  round-trips `Gtab`/`FINV` is not byte-pinned; the fixture asserts semantic inventory
  carry only.
- **`game-data` template FAMs** are read-only; the fixture copies, never writes them.
- The **default `Config.Checks`** and existing pins are untouched; `impexport` is
  strictly opt-in.
