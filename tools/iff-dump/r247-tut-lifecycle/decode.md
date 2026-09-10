# R247 — the tutorial LIFECYCLE family: ResetTutorial, TutorialCompleted, CancelTutorial, the Tutorial.FAM swap, the object spawner, and the HouseInfo+0x14 origin

Resolves the lifecycle family r239 explicitly deferred ("automatic
tutorial-family setup/reset, and tutorial completion generic calls still need
separate original-backed implementation"). Independently re-verified by an
adversarial skeptic pass (`skeptic-corrections.md`, verdict SAFE TO
IMPLEMENT after corrections 1–3, all applied here). All addresses are
executable FILE OFFSETS; r141 symbol-index addresses are +2 (index addr − 2 =
instruction stream start). Container facts as in r244/r245: code section file
0x8e90..0x5c22e8, stored data pointers section-relative (code word + 0x8e90 =
file), TOC base = data + 0x8000, data section unpacked 0x7bf80 with zero-fill
BSS to 0x989a4. Function-record law `start = marker+8 − size − 12` re-validated
(9257 records). Nothing outside this directory was written; no game asset
copied into the tree.

## 0. Cast of characters (this round)

| Function | Start (file) | Size | Notes |
| --- | --- | --- | --- |
| `ResetTutorial__8cSimsAppFv` | 0x258350 | 768 | the FAM-restore flow |
| `TutorialCompleted__12NeighborhoodFi` | 0xad9e0 | 556 | record end confirmed by its own marker at 0xadc10 (size 0x22c) |
| `CancelTutorial__12NeighborhoodFv` | 0xad930 | 116 | ignores `this`; operates via global chains |
| `ImportFamily__12NeighborhoodFRC16StackString<260>PP6FamilyPP6Family` | 0xadc50 | 4572 | unnamed in r141 index; reached from `ImportFamily__FP10ImportInfoPi` 0x231df0 |
| `ImportFamily__FP10ImportInfoPi` | 0x231df0 | 416 | the import driver |
| `LoadHouse__12NeighborhoodFii` | 0xafea0 | 2420 | contains the spawner |
| `GetHouseInfo__FiP9HouseInfoi` | 0x232d30 | 2308 | opens HouseNN.iff, tail-calls the inner overload |
| `GetHouseInfo__FP8iResFileP9HouseInfoiRb` | 0x2324b0 | 2000 | THMB thumbnails + tail call |
| `GetHouseFileInfo__12NeighborhoodFP8iResFilePiPiPiPiPiPl` | 0xb0a40 | 288 | SIMI chunk → HouseInfo facts, **the +0x14 writer** |
| `MessageDialog__8cSimsAppFPCcPCcUlb` | 0x253f00 | — | thin wrapper over vtable slot byte **0x114** |
| `DoSave__8cSimsAppFv` | 0x259390 | 304 | save gate used by ResetTutorial |
| `DoNbhdScreen__8cSimsAppFb` | 0x2555b0 | 304 | terminal action of ResetTutorial |
| `CopyFileA` | 0x330d0 | — | Mac FS glue chain ending in `CopyFork` 0x40f9b0 |
| `GetFileAttributesA` | 0x2caa0 | — | returns −1 on failure |
| `SetFileAttributesA` | 0x2c840 | — | sets attribute word |
| `Open__11IFFResFile2FRC16StackString<260>b` | 0x97170 | — | IFF FILE 2.5 magic check (`IFF FILE *.*:TYP`, data 0x44b48) |
| `ExtractDirectory__FRC16StackString<260>R16StackString<260>` | 0x84110 | — | directory-of-path |
| `GetHousePath__12NeighborhoodFiP16StackString<260>` | 0xb50f0 | — | path builder used by GetHouseInfo(int) |
| `ReconstituteLoadObject<10cSimulator>` | 0x8d720 | — | SIMI chunk → cSimulator (globals at obj+0x10, u16 each) |
| `ReconstituteLoadObject<12Neighborhood>` | 0xb83b0 | — | NGBH chunk → Neighborhood |
| `ReconstituteSaveObject<12Neighborhood>` | 0xb85c0 | — | Neighborhood → NGBH chunk |
| `FindDataDirectory__8cSimsAppFR9cTSString` | 0x257a60 | 244 | app data directory |
| `GetString`-style index→string | 0x87f60 | ~0x330 | 0x60-entry table, TOC−0x5bec (table 0x43b60) / TOC−0x5be8 (strings 0x43ce0) |

Global chains (all instruction-pinned):

* `*(TOC−0x778c)` = app holder (BSS 0x8510c) → `app`; **app+0x24 = Neighborhood**
  (TryGenericSimCall 0xf1f90, TSOnKeyDown 0x217acc, DoSave 0x2593c0,
  free-ImportFamily 0x231e18); **app+0x1c = cSimulator** (LoadHouse 0xafed4 —
  the r245 note's offsets confirmed).
* **`*(app)` = an app-core object; core+0x14 = ObjectModule, core+0x20 =
  cSimulator** (two derefs from the same slot: CancelTutorial
  0xad938..0xad950 → KillObject `this`; TutorialCompleted 0xadba0..0xadbb4 →
  SetGlobal `this`).
* `*(TOC−0x7790)` (BSS 0x92660) → app (ResetTutorial 0x2583fc FindDataDirectory
  `this`; r245 used +0xa4 for CPState).
* `cSimulator` layout law: **`SetGlobal(sim, i, v)` = `sth v, 0x10+i*2(sim)`**
  (0x138c20..0x138c2c) — the globals array is at sim+0x10, u16 elements.
  Therefore **global 26 lives at cSimulator+0x44**, global 53 = +0x7a,
  54 = +0x7c, 58 = +0x84, 69 = +0x9a.
* `Neighborhood` object: base offset +0 is a `StringBuffer` (ctor 0xb7040
  embeds it at +0 with buffer at +0xc and the shared string-env singleton
  `*(TOC−0x744c)` = data 0x42248 at +8) — that is why the object itself can be
  passed as a path (`IFFResFile2::Open((StackString&)nbhd, true)` in
  TutorialCompleted 0xadac8..0xadad4). Neighborhood+0x164 = current house
  number (u32).

## A. Callers and triggers (bl-target scans over the full code section)

Exact caller sets (scan law: word>>26 == 18, LK=1, ±32 MB branch arithmetic):

* **ResetTutorial (2 sites)**
  * **0x282a70 in `cWinOptions::TSOnCommand`** (0x281750, 5204 B). The window
    scans its command table at this+0xd8 (4-byte entries, sentinel this+0x180);
    slot index = `addze((ptr − base) >> 2)` (0x281c60..0x281c68). The sub-dispatch
    at 0x281d20/0x281d50-54 compares the slot index against 0x21 and branches to
    0x282a6c: `mr r3, r31(app); bl ResetTutorial`. **The Options screen's
    "Reset Tutorial" command is table slot 0x21 (33).** No extra guard — every
    guard (dialog, save, copy) lives inside ResetTutorial.
  * **0x24fab4 in `AppCheatCallback`** (0x24e470, 10728 B). Dispatch:
    `cmplwi r26(cheat id), 0x63; jump table *(TOC−0x5198)` (data 0x5323c).
    Case 0x2d = 0x24fab0 = `r3 = app; bl ResetTutorial` — no wrapper args.
    The cheat registry (vector holder TOC−0x5190 → BSS 0x92648; records 0x14 B,
    `SimsAppCheat` ctor 0x252840 `{id, type, name, usage, long}` at
    0x251600..0x2525d0) registers id 0x2d with name **"restore_tut"**
    (blob 0x534d8+0xd47; registration at 0x251d2c..0x251d44; verified by the
    id 0x2e neighbor = "crash" matching the KABOOM case). So the cheat word is
    **`restore_tut`** (type 0 = no parameters).
* **TutorialCompleted (3 sites, all in `TryGenericSimCall__8cXObjectFP9StackElemP10XPrimParam` 0xf1f20)**
  * The generic-sim-call primitive: index = (s16)operand@param+0 (0xf1f68),
    bound `cmplwi 0x2b`, jump table `*(TOC−0x59a0)` (data 0x485a8). Decoded
    entries: **generic 0 → TutorialCompleted(0)** (0xf1f88..0xf1f94),
    **generic 8 → TutorialCompleted(1)** (0xf1f9c..0xf1fa8),
    **generic 9 → TutorialCompleted(2)** (0xf1fb0..0xf1fbc). Each fetches
    `r3 = *(app+0x24)` (the global Neighborhood) and calls with r4 = 0/1/2.
    These are the "tutorial completion generic calls" r239 left open —
    Tutorial.iff's main 4096 lesson phases invoke them.
* **CancelTutorial (1 site)**
  * **0x217ad0 in `cDDDSimsView::TSOnKeyDown`** (function spans
    ~0x21758c..0x217e50; unnamed in r141 but bounded by TSOnKeyUp/
    TSOnCharacter records). The case: `r3 = *(app+0x24); bl CancelTutorial`
    then immediately `hl = *(winmgr+0x20c)` (the cTSWinTutorialHighlight
    singleton, r245) and — if nonnull — `hl->vt+0xa0(hl)` = **HideWindow**.
    So a keyboard shortcut in the house view cancels the tutorial and kills
    the highlight. **The key is ESC (0x1B)**: the keydown switch compares
    `cmpwi r30, 0x1b` at 0x217610 with `beq 0x217ac8` at 0x217614 (r30 =
    TSOnKeyDown's key-code argument; the neighboring case at 0x217608 is
    0x29). Skeptic-resolved; pins added.

## B. ResetTutorial (0x258350) — the complete law

```
ResetTutorial(app):
  # 1. confirm
  if app->vtable[0x114](app, str(TOC-0xbe4), str(TOC-0xbe0), style=1, flags=0) != 3: return
  # 2. save gate — abort ONLY if DoSave returns 0 (see DoSave law below)
  if (DoSave(app) & 0xff) == 0: return
  # 3. build both paths: FindDataDirectory(app) + dir + sep-fix + "Tutorial.FAM"
  src = <datadir> + GetString(7)='UserData/'        + sep + 'Tutorial.FAM'
  dst = <datadir> + GetString(0x58)='UserData/Import/' + sep + 'Tutorial.FAM'
  # 4. copy (fail if dst exists)
  if CopyFileA(src, dst, mustNotExist=1) == 0:
      dialog(TOC-0xbe8, TOC-0xbe0, style 0); return        # failure message
  attr = GetFileAttributesA(dst)
  if attr == 0xFFFFFFFF (INVALID): dialog(...); return
  if SetFileAttributesA(dst, attr & ~1) == 0:              # clear READONLY bit 0
      dialog(...); return
  # 5. land on the neighborhood screen (imports get picked up there)
  DoNbhdScreen(app, true)
```

Instruction-exact notes:

* The dialog virtual is slot byte 0x114 of the app vtable — the same slot
  `MessageDialog__8cSimsAppFPCcPCcUlb` wraps (0x253f40..0x253f5c builds two
  cTSStrings from its `const char*` and dispatches 0x114). Quit uses the same
  dispatch (0x257c70..0x257c94) and treats return **3 as the affirmative
  button**; ResetTutorial requires exactly 3. The strings at TOC−0xbe0/−0xbe4/
  −0xbe8 are TOC-resident cTSString objects (static zeros) constructed by
  `__sinit__:SimsApp_cpp` (0x25ce08/0x25cdf0/0x25ce20) from literal pointers
  held at TOC−0x51d8/−0x51d4/−0x51dc — which are BSS 0x927xx, i.e.
  runtime-localized. **The literal dialog text is not statically recoverable.**
* `DoSave` (0x259390) — the full law (skeptic-verified, tail at
  0x259468..0x2594a8):
  * `r31 = 1` only when the app dirty byte `app+0xbf` != 0 **AND**
    `GetHouseNumber() != 0`; **gate failure (nothing dirty, or not in a
    house) → `li r3, 1` at 0x2594a4 — DoSave returns 1 = PROCEED**, no
    prompt, no save.
  * Otherwise it prompts with slot-0x114 style **3** (strings TOC−0xbd4/
    −0xbd0): result **1 → `li r3, 0` at 0x259438 — return 0** (user aborts
    saving → ResetTutorial aborts); result **4** → if the byte at
    `*( *(TOC−0x7110) )` is set, return 1, else `SaveGame(app)`: failure →
    return 0, success → **clear the +0xbf dirty byte** (0x25949c..0x2594a0)
    and return 1; **any other result → return 1** (proceed without saving).
  * `+0xbf` is the save-DIRTY byte (cleared on successful save), not a
    "save-enabled" flag. ResetTutorial's caller-visible law is only:
    **abort the reset iff DoSave returns 0.**
* Path build (0x2583b0..0x258564, done twice): `FindDataDirectory(app,
  cTSString{})` → StringBuffer; `append(StringGet(7 or 0x58))`; then a
  separator fixup — the StringBuffer's +8 env object's vtable slot 8 returns
  the length; if `buf[len-1]` is neither '/' (0x2f) nor '\\' (0x5c) a '/' is
  appended; then `append("Tutorial.FAM")` (blob+0x1621 at data 0x54af9).
* `CopyFileA` (0x330d0) verified as a real copy: DOS2MacPath both paths, FSSpec
  both, source open + GetCatInfoNoName (directory bit 0x10 → fail), destination
  open — **flag arg 1 (r29) makes an already-existing destination fail**
  (0x331dc..0x331e8); fnfErr (−43) → create destination (0x33208..0x3321c);
  NewPtr(0x4000) buffer (0x33260..0x33264) then `CopyFork(srcRef, dstRef, buf,
  0x4000)` at 0x33298.
* `GetFileAttributesA` (0x2caa0) returns −1 (INVALID_FILE_ATTRIBUTES) on
  failure — the reset flow's `addis r0, r4, 1; cmplwi r0, 0xffff` (0x2585b4..0x2585bc)
  is exactly `attr == 0xFFFFFFFF`.
* `SetFileAttributesA` (0x2c840) is called with `attr & ~1` (rlwinm clears
  bit 0 at 0x2585ec) = **clear READ-ONLY**; nonzero return = success.
* On every failure path the flow re-issues the TOC−0xbe8 message dialog and
  returns; success ends in `DoNbhdScreen(app, true)` (0x25862c).

So ResetTutorial does NOT touch nhoodData/globals/objects directly and does
NOT delete any Tutorial object — it is purely the **pristine-FAM restore into
the import folder**, plus save and navigation. The shipped pristine file
exists in the owner's install: `UserData/Tutorial.FAM` (IFF FILE 2.5, first
chunk `SIMI`, i.e. a family-global snapshot), and `UserData/Import/` exists
(empty marker `_`).

## C. TutorialCompleted(int arg) (0xad9e0) — the completion law

```
TutorialCompleted(nbhd, arg):        # arg ∈ {0,1,2} from generic calls 0/8/9
  dir   = ExtractDirectory(nbhd->embedded-path-string)
  fname = sprintf('%sHouses/House%02d.iff', dir, nbhd->+0x164)   # current house
  iffA.Open(fname, write=1)                  # IFFResFile2::Open, magic-checked
  iffB.Open((StackString&)nbhd, write=1)     # the neighborhood file itself
  if either open fails: destruct both; return

  tempSim = cSimulator()
  tempNbhd = Neighborhood()
  if arg == 0:
      ReconstituteLoadObject<cSimulator>(tempSim, iffA, 'SIMI', 1, &pos)
      tempSim.SetGlobal(26, 0)                       # globals base +0x10
      ReconstituteSave... (iffA, 'SIMI', 1, &pos)    # 0x8d9b0 with loaded pos
  ReconstituteLoadObject<Neighborhood>(tempNbhd, iffB, 'NGBH', 1, &pos)
  tempNbhd->+0x12a (u16) = arg + 1
  tempNbhd->+0x12c (u16) = 0
  ReconstituteSaveObject<Neighborhood>(tempNbhd, iffB, 'NGBH', 1, &pos)
  if arg == 0:
      *( *(app->+0x20) ).SetGlobal(26, 0)            # LIVE simulator global 26
  nbhd->+0x12a (u16) = arg + 1                        # LIVE tutorial state
  nbhd->+0x12c (u16) = 0
  destructors; return
```

Instruction-exact notes:

* `arg+1` is computed once into r26 (`addi r26, r25, 1` at 0xad9fc) and stored
  as a halfword into the SAME Neighborhood object field pair in both copies:
  **+0x12a = arg+1, +0x12c = 0**. The stores at 0xadb74/0xadb80
  (`sth r26, 0x492(r1)` / `sth r0, 0x494(r1)`) are STACK-frame addresses
  against the temp Neighborhood at r1+0x368 (ctor at 0xadb04..0xadb08):
  0x368 + 0x12a = 0x492 and 0x368 + 0x12c = 0x494 — i.e. object fields
  +0x12a/+0x12c of the reconstituted object, NOT a "+0x492 NGBH-chunk
  offset". The live stores at 0xadbbc/0xadbc8 (`sth ..., 0x12a(r31)` /
  `sth ..., 0x12c(r31)`) hit the identical fields on the real object.
  Persistence: because the temp object was reconstituted from the NGBH chunk
  and is saved back with `ReconstituteSaveObject<12Neighborhood>` (0xb85c0),
  the same +0x12a/+0x12c pair is what lands in the neighborhood file.
* The SIMI patch runs **only for arg == 0** (branch at 0xadb0c). Both the
  temp-simulator patch (0xadb14..0xadb50: load 0x8d720 → SetGlobal(26,0) at
  0xadb38 → save 0x8d9b0 passing the loaded position from r1+0x40) and the
  LIVE simulator clear (0xadba0..0xadbb8, `lwz r3, 0x20(r3)` two derefs =
  app-core+0x20 = cSimulator) are arg==0-only.
* The NGBH patch is unconditional: `ReconstituteLoadObject<12Neighborhood>`
  (0xb83b0) fills the temp Neighborhood from the neighborhood file's NGBH
  chunk; the object's +0x12a/+0x12c fields are then written (see above) and
  `ReconstituteSaveObject<12Neighborhood>` (0xb85c0, wrapping
  ReconBuilder::Compact 0x11de80) re-serializes the chunk. The second
  IFFResFile2::Open passes the Neighborhood object itself as the path — the
  same idiom as `Neighborhood::Load` (0xb5a0c) — so the NGBH write targets
  the NEIGHBORHOOD file (whose path `InitGame` 0x889e4..0x88a30 builds as
  GetString(7) + GetString(0xE) = 'Neighborhood.iff'); only the house file
  (Houses/House%02d.iff) receives the arg-0 SIMI patch.
* `'SIMI'` = data word at 0x44864 (TOC−0x7318, value 0x53494d49); the version
  halfword 1 = data 0x44860 (TOC−0x731c).
* `IFFResFile2::Open` (0x97170) checks the IFF FILE 2.5 magic against the
  wildcard pattern **`IFF FILE *.*:TYP`** (16 bytes at data 0x44b48; 0x2a `*`
  bytes are wildcards, compare loop 0x9720c..0x97244).
* The format string `'%sHouses/House%02d.iff'` = strtable 0x45604+0xd5
  (TOC−0x5ad0). The `%s` arg = ExtractDirectory of the Neighborhood's embedded
  path string (0x84110 at 0xada5c); `%02d` = Neighborhood+0x164, the current
  house number (loaded into r27 at 0xada28).

Downstream behavior changes (the gates that consume these writes):

* **The spawner gate** (LoadHouse 0xb0540..0xb0554, below): the Tutorial
  object stops spawning once `nbhd->+0x12a >= 3`, i.e. after
  `TutorialCompleted(2)`. Values 1/2 (args 0/1) keep the spawner enabled.
* **Global 26** is the "tutorial session active in this house" latch: the
  native code only ever CLEARS it — `TutorialCompleted(0)` (twice:
  temp-file + live), `MoveOut__12NeighborhoodFii` (0xb1c08),
  `RemoveFromVacation__12NeighborhoodFii` (0xb166c). There is **no
  SetGlobal(26, nonzero) anywhere in the executable** (all 38 SetGlobal call
  sites scanned) — the initial 1 comes from data/script (the Tutorial.FAM
  import and/or the tutorial IFF setup tree; the parallel IFF analysis'
  "global[26]" gate). The port's "setup 4098 gates on nhoodData[1]==0"
  corresponds to the persisted pair this function writes.
* Neighborhood+0x12c is also maintained by
  `ImportFamily__12Neighborhood` (0xadc50): with its flag byte set it stores
  `+0x12c = import house id, +0x12e = 0` (0xaea48..0xaea5c); otherwise, if
  `+0x12c == house id` it clears +0x12c (0xaea64..0xaea74). LoadHouse reads
  +0x12a (0xb0550); `Save__12NeighborhoodFl` persists the pair.

## D. CancelTutorial (0xad930) — complete

```
CancelTutorial(nbhd):            # nbhd (this) is NEVER used
  module = *( *( *(TOC-0x778c) ) + 0x14 )        # app-core+0x14 = ObjectModule
  owner  = ObjectModule::GetTutorialObject()     # 0xe2d80 = return this+0xac
  if owner:
      owner->RunTree(owner->+0x11c->+0xc /*Behaviors*/, "cancel tutorial", 0, 0)
      ObjectModule::KillObject((s16)owner->+0xf0)   # owner's object id
```

* The tree name `"cancel tutorial"` = strtable 0x45604+0xc5 (r6 = r6+0xc5 at
  0xad978).
* `KillObject__12ObjectModuleFs` (0xe6a60) is the same remover LoadHouse uses
  for cross-house objects; the id source `owner->+0xf0` is the object-id
  field (same field as the OBJM-persisted owner id chain from r239 and the
  LoadHouse sweep at 0xb0524).
* Caller context adds the second half of the law: after CancelTutorial the
  key handler hides the tutorial highlighter (`winmgr->+0x20c`, slot 0xa0 =
  HideWindow) — 0x217ad4..0x217af0.

## E. Tutorial.FAM — every code path

The executable contains exactly three literal "Tutorial.FAM" strings:

1. **Data blob 0x54af9** (blob base 0x534d8 = TOC−0x5180, offset 0x1621) —
   used by **ResetTutorial** (B above): copies `UserData/Tutorial.FAM` →
   `UserData/Import/Tutorial.FAM` (both dir names from the GetString table:
   index 7 = `'UserData/'`, index 0x58 = `'UserData/Import/'`, decoded from
   the inline jump table at 0x87f84..0x88280; full table dump in
   verify.py fixtures). After the copy it clears the copy's read-only bit and
   shows the neighborhood screen.
2. **String table 0x6c84a** (and duplicate 0x6d262, one per neighborhood UI
   family): the cluster is `$family`, `*.FAM`, `*.rdt`, `*.cmt`,
   `Tutorial.FAM`, `Couldn't import the family` — the **import-family UI**
   string set (file-dialog filter `*.FAM` with default name `Tutorial.FAM`
   and the failure message).
3. No other reference exists (full-data scan).

The import pipeline that consumes it:

* The neighborhood screens poll for new imports:
  `CheckForNewImports__18cWinNeighborhoodVCFv` (call 0x46f5cc) and the UL/
  Magicland variants, plus `CycleThroughImports*` and the `import` cheat —
  all call `ImportFamily__FP10ImportInfoPi` (0x231df0).
* That builds `Neighborhood::ImportFamily(nbhd, ImportInfo, &outFamilies)`
  (0xadc50), which opens the .FAM with IFFResFile2::Open, reconstitutes the
  family, adds it to the neighborhood, and maintains the +0x12c house-id
  field (C above). The free function then (re)generates portraits for each
  member (PersonFinder::GenerateBitmaps) and special-cases family id
  **0xfa0 = 4000** (`cmpwi r29, 0xfa0` at 0x231e8c).
* A .FAM is IFF FILE 2.5 whose first chunk is `SIMI` — the family's simulator
  globals (verified on the owner's `UserData/Tutorial.FAM`: header
  `IFF FILE 2.5:TYPE FOLLOWED BY SIZE\0JAMIE DOORNBOS & MAXIS 1`, chunk
  `SIMI` size 0x192).
* "FAM load" therefore = **family import** (bin family + globals), not a
  house/state swap. The house state involved in the tutorial is the
  destination `House%02d.iff`, whose SIMI chunk carries the per-house latch
  (global 26) and the facts read into HouseInfo (G below). Only the CURRENT
  neighborhood is affected (paths derive from the live Neighborhood).

Timeline of the original flow: Options "Reset Tutorial" (or `restore_tut`)
→ pristine Tutorial.FAM copied into UserData/Import → neighborhood screen
(DoNbhdScreen(true)) → CheckForNewImports imports it into the bin → the user
moves it into a house → house SIMI global 26 = 1 (data/script side) →
entering that house spawns the Tutorial object (F below) → lessons run →
generic calls 0/8/9 → TutorialCompleted → global 26 = 0 + NGBH state = arg+1
→ after arg 2 the spawner is permanently off for that neighborhood.

## F. The tutorial object spawner (LoadHouse tail)

`LoadHouse__12NeighborhoodFii` (0xafea0), after `PostLoad` (0xe83d0):

1. Object sweep (0xb04c8..0xb053c): for every object `o` in ObjectModule's
   list (+0x8c count / +0x90 array): if `o->+0x11c->+0xb4` bits 1..2 (of the
   `>>1 & 3` extraction) == 1 AND `(s16)o->+0x606 == 0` AND
   `o->+0x612 != nbhd->+0x164` (belongs to another house) →
   `KillObject((s16)o->+0xf0)`.
2. **The spawn** (0xb0540..0xb05a0), all five guards in order:

```
if (*(*(TOC-0x727c)) != 0)            skip   # BSS 0x852c8 inhibit flag (cheat-toggled)
if ((s16)nbhd->+0x12a >= 3)           skip   # tutorial completion state
if ((s16)sim->global[26] == 0)        skip   # sim = *(app)+0x1c; global 26 = sim+0x44
if (ObjectModule::GetObjectByGUID(0xc3249a1d)) skip # already in world
sel = ObjectFolder::GetSelectorByGUID(0xc3249a1d)
if (sel == null)                      skip
ObjectModule::MakeNewOutOfWorldObject(sel)   # 0xe7040
```

* GUID constant: `lis r4, 0xC325; addi r4, r4, -0x65E3` = **0xc3249a1d**
  (0xb0568..0xb0570 and again 0xb0584..0xb0588).
* The object is created **out-of-world** (no tile) — it exists to run its
  trees (r239/r241/r245 machinery) and is addressed by the ownership/dialog
  systems.
* The inhibit flag: readers are LoadHouse and the **`tutorial` cheat**
  (id 0x3d, type 2, name blob+0xdea — registration 0x251fb8..0x251fc4; case
  0x250128). The case computes **`flag = (param == 0)`**:
  `clrlwi r0,r24,0x18; cntlzw r0,r0; rlwinm r0,r0,27,24,31; stw` — cntlzw
  yields 32 only for a zero parameter, so the stored word is 1 iff
  `param & 0xff == 0`. r24 is the parsed cheat parameter (`li r24, 0` init at
  0x24e524; special words blob+0x57e **"on"** → `li r24, 1` at 0x24e62c and
  blob+0x581 **"off"** → `li r24, 0` at 0x24e658; numeric parse otherwise).
  Spawn requires the flag to be 0, therefore **`tutorial on` (and `tutorial
  <n>`, n != 0) set the inhibit flag to 0 = spawn ALLOWED; `tutorial off`
  (and `tutorial 0`) set it to 1 = spawn DISABLED.** The holder is runtime
  BSS; its default value is not statically initialized (UNRESOLVED — likely 0
  from zero-fill BSS, which matches the tutorial being active on a fresh
  install).
* Who creates it otherwise: **nobody.** There is no create-object call for
  GUID 0xc3249a1d anywhere else; the house TEMPLATE carries no tutorial
  object — the spawn is 100% this LoadHouse tail, gated on the latch that the
  Tutorial.FAM import / lesson setup data provides.

## G. HouseInfo: layout, the +0x14 origin, and the move-in refusal

**Writer chain (the R197 open question, now closed):**

```
GetHouseInfo(int house, HouseInfo* hi, int kind):          # 0x232d30
    path = GetHousePath(nbhd, house, ...)                   # 0xb50f0 at 0x232d8c
    iff.Open(path, read=0)
    ...zoning pre-check (0x232dc4..0x232e08)...
    return GetHouseInfo(iff, hi, kind, zonflag)             # tail call 0x232e0c

GetHouseInfo(iResFile* iff, HouseInfo* hi, int kind, bool& flag):  # 0x2324b0
    release hi+0x00/+0x04/+0x08/+0x0c (thumbnail cTSBuffer*, vt slot 8)
    reload 'THMB' variants 0x200/0x264/0x464 (LoadBuffer + ScaleBitmap)
    GetHouseFileInfo(nbhd, iff, &hi->+0x1c, &hi->+0x14, &hi->+0x18,
                     &hi->+0x10, &hi->+0x20, &hi->+0x40)     # 0x232c68
```

`GetHouseFileInfo` (0xb0a40) reconstitutes the house file's `SIMI` chunk into
a TEMP cSimulator and then, reading the temp sim's globals array (base +0x10,
u16 each):

| out param | HouseInfo field | source | site |
| --- | --- | --- | --- |
| r6 | **+0x14 (int)** | **`(s16)global[58]`** (sim+0x84) | `lha r0, 0x84(r1); stw r0, 0(r26)` 0xb0ac4/0xb0acc |
| r5 | +0x1c price (int) | `fctiwz(f2 × (double)convert(sim+0xe4 word)) + u32[sim+0xdc] + u32[sim+0xe0]` where **f2 = the runtime float at `*(TOC−0x7274)` = BSS 0x852b0** (`lwz r7, -0x7274(r2)` at 0xb0af4 reassigns r7 BEFORE `lfs f2, 0(r7)` at 0xb0b14) | 0xb0ae8..0xb0b40 |
| r7 | +0x18 built (int) | `(s16)global[54]` (sim+0x7c) | 0xb0ab0..0xb0ab8 |
| r8 | +0x10 (int) | `1 if global[58]==0 and global[53]==0 else 0` (sim+0x84, +0x7a) | 0xb0ac4..0xb0ae8 |
| r9 | +0x20 (int) | `(s16)global[69]` (sim+0x9a) | 0xb0abc..0xb0ac0 |
| r10 | +0x40 (long) | the SIMI reconstitute position out-param (`mr r7, r30` at 0xb0a80 feeds the 0x8d720 call) — NOT a price input | 0xb0a80 |

**So HouseInfo+0x14 — the R197 "TUTORIAL lot flag" — IS the house file's
SIMI global[58], copied verbatim by GetHouseFileInfo.** It is re-derived on
every GetHouseInfo/LoadHouseInfo refresh; there is no other writer of the
field (the displacement scan over the whole code section shows stores to
HouseInfo+0x14 only via this out-param). No native code ever SETS global 58
nonzero; like global 26 it is data/script-provided (which pristine house
file carries 58 != 0 is a data question — see UNRESOLVED).

Full HouseInfo map (cross-referenced with r197 §2):

| off | type | meaning | writer |
| --- | --- | --- | --- |
| +0x00..+0x0c | cTSBuffer* ×4 | lot thumbnails | GetHouseInfo inner (release+reload) |
| +0x10 | int | `(g58==0 && g53==0)` — r197 consumed it as the "can-afford/refuse" precomputed flag | GetHouseFileInfo |
| +0x14 | int | **tutorial-house flag = (s16)house-SIMI global[58]** | GetHouseFileInfo |
| +0x18 | int | house built = (s16)global[54] | GetHouseFileInfo |
| +0x1c | int | purchase price (SIMI-derived, float-scaled) | GetHouseFileInfo |
| +0x24 | int | occupying family id, −1 = none (FAMI side) | filled outside this chain (r197) |

**The move-in refusal branch** (r197's `MoveInModeLotHandler__18cWinNeighborhoodVCFi`,
0x471d80): `lotData = nbhd->0xec[lot-1]->+0x18c`; the FIRST guard is
`lotData->+0x14 != 0` → AskDialog STR#132[16]/[17] "Tutorial House" /
"This is a tutorial house. Families may not move into it." (style 0), before
the family-valid, afford, occupied and price guards. With the origin now
known, the branch becomes reachable exactly when the entered/moved-into
house's `HouseNN.iff` SIMI chunk has global[58] != 0 — i.e. the lot the
original designates as the tutorial house. The port can populate its
`tutorial` lot field from that same source (HouseNN.iff SIMI g58) instead of
leaving the branch permanently inert.

**PORTABLE PSEUDOCODE**

```
# ResetTutorial
def resetTutorial(app):
    if app.messageDialog(STR.confirmCaption, STR.confirmBody, style=1) != 3: return
    if not app.doSave(): return
    src = pathJoin(app.findDataDirectory(), 'UserData/', 'Tutorial.FAM')
    dst = pathJoin(app.findDataDirectory(), 'UserData/Import/', 'Tutorial.FAM')
    if not copyFile(src, dst, failIfDstExists=True):
        app.messageDialog(STR.failCaption, STR.failBody, style=0); return
    attr = getFileAttributes(dst)
    if attr == INVALID or not setFileAttributes(dst, attr & ~READONLY):
        app.messageDialog(STR.failCaption, STR.failBody, style=0); return
    app.doNbhdScreen(enter=True)

# TutorialCompleted
def tutorialCompleted(nbhd, arg):        # arg in {0,1,2}
    f = fmt('%sHouses/House%02d.iff', dirOf(nbhd.path), nbhd.houseNumber)
    houseFile.openWrite(); nbhdFile.openWrite()
    if arg == 0:
        g = loadSimGlobals(houseFile)    # 'SIMI'
        g[26] = 0; saveSimGlobals(houseFile, g)
    rec = loadNbhdRecord(nbhdFile)       # 'NGBH' -> temp Neighborhood
    rec.tutorialState = arg + 1          # obj +0x12a u16 (stack 0x492)
    rec.tutorialHouse = 0                # obj +0x12c u16 (stack 0x494)
    saveNbhdRecord(nbhdFile, rec)        # 'NGBH'
    if arg == 0: liveSim.global[26] = 0
    nbhd.tutorialState = arg + 1         # obj +0x12a u16
    nbhd.tutorialHouse = 0               # obj +0x12c u16

# CancelTutorial
def cancelTutorial(_nbhd):
    owner = objectModule.tutorialObject
    if owner:
        owner.runTree('cancel tutorial')
        objectModule.killObject(owner.objectId)     # +0xf0
    # caller: tutorialHighlighter.hideWindow()

# spawner (LoadHouse tail)
def maybeSpawnTutorial(nbhd, sim, objectModule, objectFolder):
    if tutorialInhibitFlag: return
    if nbhd.tutorialState >= 3: return
    if sim.global[26] == 0: return
    if objectModule.getObjectByGUID(0xc3249a1d): return
    sel = objectFolder.getSelectorByGUID(0xc3249a1d)
    if sel: objectModule.makeNewOutOfWorldObject(sel)

# HouseInfo fill (per lot refresh)
def fillHouseInfo(hi, houseIff):
    g = loadSimGlobals(houseIff)         # 'SIMI' -> temp simulator
    hi.built    = g[54]
    hi.tutorial = g[58]                  # the move-in refusal flag
    hi.flag10   = 1 if (g[58] == 0 and g[53] == 0) else 0
    hi.price    = priceAdjust(g, hi)     # float-scaled 64-bit, see note
    hi.g69      = g[69]
```

## 7. What is safe to base a C# implementation on

Instruction-pinned and fixture-backed (verify.py PASS; 133 instruction pins
+ 13 data pins + 7 fixture groups, including the skeptic-pass pins):

* Caller sets: Options command-table slot 0x21 and the `restore_tut` cheat
  (id 0x2d) → ResetTutorial; generic calls 0/8/9 → TutorialCompleted(0/1/2)
  on the global Neighborhood; the view key handler → CancelTutorial +
  highlighter HideWindow.
* ResetTutorial: dialog(slot 0x114, style 1, result 3) → DoSave gate
  (**abort the reset iff DoSave returns 0**; gate failure/nothing-dirty
  returns 1 = proceed) → `UserData/Tutorial.FAM` copied over
  `UserData/Import/Tutorial.FAM` (fail-if-exists), read-only cleared,
  `DoNbhdScreen(true)`; every failure → the style-0 message.
* TutorialCompleted: house-file SIMI g26 = 0 (arg 0 only, temp sim) + live
  sim g26 = 0 (arg 0 only); **Neighborhood object +0x12a = arg+1 (u16),
  +0x12c = 0 — written on BOTH the NGBH-reconstituted temp (persisted via
  ReconstituteSaveObject into the neighborhood file) and the live object**;
  the 0x492/0x494 in the raw words are stack addresses (r1+0x368 base), not
  chunk offsets. State 3 disables the spawner.
* CancelTutorial: named tree "cancel tutorial" + KillObject(owner->+0xf0);
  triggered by ESC (0x1B) in the house view (cmpwi at 0x217610).
* Tutorial.FAM: IFF FILE 2.5, SIMI-first family file; restore = file copy
  into UserData/Import; import = neighborhood-screen CheckForNewImports →
  ImportFamily chain; import UI defaults to Tutorial.FAM.
* Spawner: LoadHouse-only, five ordered guards, GUID 0xc3249a1d literal,
  MakeNewOutOfWorldObject; the `tutorial` cheat computes
  inhibit = (param == 0), so `tutorial on`/`tutorial 1` = spawn ALLOWED and
  `tutorial off`/`tutorial 0` = spawn DISABLED.
* HouseInfo+0x14 = house-SIMI global[58] (GetHouseFileInfo); +0x10 =
  (g58==0 && g53==0); +0x18 = g54; +0x20 = g69; the move-in refusal fires on
  +0x14 != 0 before all other guards.
* SetGlobal law (sim+0x10, u16 stride) and the app-holder chains
  (+0x24 nbhd, core+0x14 module, core+0x20 sim, +0x1c sim).

UNRESOLVED (with reason):

* **Dialog literal texts** for the reset confirm/failure (TOC−0xbe0/−0xbe4/
  −0xbe8): runtime-localized cTSStrings (literal pointers are BSS 0x927xx
  filled from UIText at startup) — not statically recoverable. The button
  enumeration of style 1 is known only as "3 = proceed" (invariant across
  two Quit sites and ResetTutorial); Quit's style-3 mapping is
  {4 = save+proceed, 2 = proceed-without-saving, 3 = cancel}, matching
  DoSave's style-3 law above.
* **The runtime value of the spawner inhibit flag** \*(BSS 0x852c8): static
  zero-fill implies enabled, but its sinit writer (if any) was not found;
  only the cheat writer is pinned.
* **Pristine-data values of SIMI g58/g26** in the shipped HouseNN.iff files:
  the SIMI chunk body is a tagged recon stream (ReconBuilder), not a raw
  globals dump, so reading them requires the cSimulator recon-format decode
  (out of scope this round). The instruction-level COPY law is fully pinned;
  the parallel IFF agent's "global[26]" data claim is consistent with it.
* **The price float global** at BSS 0x852b0 (`*(TOC−0x7274)`): its writer
  was not found (no `lwz -0x7274(r2)` + store pair in the code section), so
  the multiplier's runtime value is unknown; the instruction path through it
  into +0x1c is pinned. r197's port already validates prices against SIMI
  PurchaseValue data-side.
* `ImportFamily__12Neighborhood`'s family-id 0xfa0 (4000) special case is
  pinned but its meaning (reserved/tutorial family id?) is unconfirmed.

## Files

* `verify.py` — SHA pin + 133 instruction pins (incl. the skeptic-pass
  ESC/DoSave/cheat-polarity/price-source pins) + 13 data pins + fixtures;
  PASS, runs from any cwd, ruff-clean; writes `verified-tut-lifecycle.json`
  only.
* `skeptic-corrections.md` — the independent adversarial re-derivation that
  found the three corrected items (cheat polarity, +0x12a framing, DoSave
  law) and resolved the ESC key.
* `skeptic-response.md` — decode → skeptic review → repair provenance.
* `dosave-tail.txt` — the DoSave return-value law disassembly.
* capdis2.py (r145) excerpts at the addresses cited above: `resettutorial.txt`,
  `tutorialcompleted.txt`, `canceltutorial.txt`, `options-oncommand.txt`,
  `options-dispatch.txt`, `trygenericsimcall-region.txt`,
  `viewonkeydown-region.txt`, `cheatcallback-region.txt`,
  `cheatcallback-full.txt`, `cheat-reg-2d.txt`, `cheat-reg-tutorial.txt`,
  `cheat-tutorial-case.txt`, `cheatregistry.txt`, `simsappcheat-ctor.txt`,
  `loadhouse-spawner.txt`, `loadhouse-12a.txt`, `loadhouse-head.txt`,
  `gethouseinfo-head.txt`, `ghi-pre.txt`, `ghi-end.txt`, `ghi-price.txt`,
  `ghi-inner-head.txt`, `gethousefileinfo-head.txt`, `importfamily-head.txt`,
  `importfamily-free.txt`, `importfamily-12a.txt`, `fn-330d0.txt`,
  `fn-330d0-mid.txt`, `fn-2caa0.txt`, `fn-2c840.txt`, `fn-87f60.txt`,
  `fn-97170.txt`, `fn-8d720.txt`, `setglobal.txt`, `messagedialog.txt`,
  `quit-114.txt`, `savegame-114.txt`, `simsapp-sinit-strings.txt`,
  `neighborhood-ctor-head.txt`, `nbhd-loadchunk.txt`, `nbhd-savechunk.txt`,
  `after-tutorialcompleted.txt`.
