# R247 — the FAMILY-IMPORT machinery: CheckForNewImports, CycleThroughImports, ImportInfo, and Neighborhood::ImportFamily (the consumer of the Reset Tutorial flow)

Companion to `r247-tut-lifecycle` (which decoded ResetTutorial: it copies
`UserData/Tutorial.FAM` → `UserData/Import/Tutorial.FAM`, clears read-only,
and lands on the neighborhood screen). This round decodes what happens next.
All addresses are executable FILE OFFSETS (code section starts at file 0x8e90;
stored data pointers section-relative; TOC base = data + 0x8000; data section
unpacked 0x7bf80 + zero-fill BSS). Function records use the r247 law
`start = marker+8 − size − 12` (layout `[marker u32][pad u32][size u32][00
u8][len u8][name]`); 7608 named records were extracted this round, which
corrects several r141-index mis-attributions (notably 0xaf1a0 is
`ExportFamily`, NOT a rename helper, and 0xb1050 is
`UpdateFamilyFriendsCount`). Nothing outside this directory was written; no
game asset copied.

**Skeptic pass**: this decode was independently re-derived and confirmed with
corrections C1–C8 (see `skeptic-corrections.md` and `skeptic-response.md`).
C1 (the RelMatrix copy law, corrected in §3/§6/§7), C2 (SIMI global 26 is
decode-derived, not a file fact — §4), C3 (guard refusals leak model
mutations — §5/§7), C4 (timer re-arm only when win+0x16c == 0 — §1.1), C5
(the `*.FAM` scan is a one-arg call with the wildcard already in the path —
§1.2), C6/C7 (ctor +0x188 seed, id-assign-before-null-check, evicted-export
guard form), C8 (verify.py fixtures rewritten to bind to pinned binary
words). §7 is the skeptic-verified canonical operation order.
Applying C8 also caught two transcription slips, fixed throughout
and recorded as F1/F2 in `skeptic-response.md`: the pets-gate bit is
app+0x66 & 0x20 (rlwinm 0x1a,0x1a = MSB-index 26, not 25), and the dog
template GUID is 0x4a70df92 (lis 0x4a71 + addi −0x206e), not 0x4a71df92.

## 0. Cast of characters

| Function | Start (file) | Size | Notes |
| --- | --- | --- | --- |
| `CheckForNewImports__18cWinNeighborhoodVCFv` | 0x46f460 | 536 | the Tutorial.FAM auto-import |
| `CycleThroughImports__18cWinNeighborhoodVCFv` | 0x4704a0 | 3312 | the import-bin browser UI |
| `GetImportInfoForFile__FRC16StackString<260>P10ImportInfoi` | 0x231fd0 | 656 | ImportInfo filler (record at 0x232264) |
| `__ct__10ImportInfoFv` / `__dt__10ImportInfoFv` | 0x231d00 / 0x231c00 | 188/208 | struct ctor/dtor |
| `__dt__12ImportMemberFv` | 0x2322b0 | 84 | member = {CTGString, cTSBuffer*} |
| `ImportFamily__FP10ImportInfoPi` | 0x231df0 | 416 | free driver |
| `ImportFamily__12NeighborhoodFRC16StackString<260>PP6FamilyPP6Family` | 0xadc50 | 4572 | THE law |
| `LoadFamily__6FamilyFP8iResFilei` | 0x767e0 | 580 | FAMI/FAMs/FAMh reader |
| `AddNewCharacter__12NeighborhoodFPP8NeighborQ221MakeNewCharacterParam15eCharacterTypess` | 0xb3770 | 836 | fresh character factory |
| `ExportFamily__12NeighborhoodFRC16StackString<260>P6Family` | 0xaf1a0 | 2248 | Export/ refresher (record at 0xafa6c) |
| `DeleteCharacter__12NeighborhoodFP8Neighbor` | 0xb2510 | 356 | house-clear sweep |
| `UpdateFamilyFriendsCount` / `UpdateFamilyNumbers` | 0xb1050 / 0xb56e0 | — | post-import fixups |
| `GetFamilyMembers__FiRstd::vector<i>&` | 0x233890 | 196 | portrait pass |
| `Load__15GUIDTranslationFP8iResFile` / `Save` | 0x8a040 / 0x89fd0 | 48/48 | 'Gtab' chunk |
| `MoveFileA__14CTGFileManagerFPCcPCc` / `DeleteFileA` | 0x54a0f0 / 0x54a3e0 | — | file shuffle |
| `FileExists__7cTSFileFRC9cTSString` | 0x4ae8e0 | 108 | Tutorial.FAM gate |
| `DoesAnyEntryExistThatMatchesPattern__12cTSDirectoryFRC9cTSString` | 0x4aa010 | 136 | `*.FAM` scan |
| `strlen` (raw loop) | 0x59cd30 | 24 | helper |
| `.GetCTGFileManager__Fv` (singleton `lwz r3, TOC−0x6064`) | 0x550000 | 4 | holder BSS 0x97950 |
| `ReconLoadObject<13ExpFamilyInfo>` | 0xb8250 | 92 | 'EXPi' chunk |
| `ReconLoadObject<6Family>` | 0x770c0 | 92 | 'FAMI' chunk |

New global chains (instruction-pinned):

* `*(TOC−0x46a4)` = **0x6d1c0**, a static C-string blob for the neighborhood
  UI: +0x94 `"$family"`, **+0x9c `"*.FAM"`**, **+0xa2 `"Tutorial.FAM"`**,
  **+0xaf `"Couldn't import the family"`**, **+0xca `"Error"`** (plus web
  strings `neighborhood.html`, `Error %d`, `load` usage …).
* `*(TOC−0x778c)` = app holder; `app+0x24` = Neighborhood (r247);
  **`app+0x28` = ObjectFolder** (used by the type-attr sweep below);
  **`app+0x44` = long arg passed to `Save__12NeighborhoodFl`**;
  **`app+0x64` (byte) = pets-enabled flag** read by ImportFamily;
  **`(s16)app+0x66 & 0x20` = pets-enabled option bit** read in the member loop.
* `*(TOC−0x6064)` = BSS 0x97950 — the CTGFileManager holder.
* `Neighborhood` layout additions: **+0x14c count / +0x150 array** of live
  `Neighbor*` (1-based NeighbourID = index+1); **+0x158/+0x15c** family bin
  (count/array, known from r247); **+0x170 count / +0x174 array** of 8-byte
  lot-table entries `{u32 house; u32 flag}`; **+0x190/+0x194** =
  `std::vector<unsigned long>` of character-inventory entries
  `{ptr → {s16 id; SimInventory* inv}}`.
* `Family` layout additions: **+0x10c id (u32)**, **+0x110 house (u32)**,
  +0x114 ?, **+0x118/+0x11c money pair**, **+0x138 flags** (bit31 = dirty,
  copied from FAMI at 0xaea34; bit29 = occupies-house; bit28), **+0x13c
  second house field** (checked alongside +0x110), **+0x140 'FAMh' handle**,
  +0x144 list. The object base is the embedded StringBuffer (name), as with
  Neighborhood.
* `Neighbor` fields used: **+0x00 NeighbourID (s16)**, **+0x08 ObjSelector***,
  **+0x0c RelMatrix**, **+0x18 persistent-field-list head**,
  **+0xfa house (s16)**.
* `ExpFamilyInfo` (the 'EXPi' chunk object): **+0x00 byte** (echo of
  app+0x64, re-stored into ImportInfo+0x188), **+0x02 s16 family id**
  (arg to `LoadFamily`), **+0x04 s16[8] member/uChr ids** (0-terminated).
* String table (data 0x45604, `*(TOC−0x5ad0)`): +0x01 `User%05d`, +0x0a
  `.iff`, +0x0f `Characters/`, +0x38 `*.FAM`, +0x57 `Townie`, +0xd5
  `%sHouses/House%02d.iff`, **+0xec `dog`**, **+0xf0 `cat`**, **+0xf4
  `" - "`**, **+0xf8 `.tmp`**, +0x100 `.FAM`. GetString table (0x60 entries):
  7 = `UserData/`, **0x57 = `Export/`**, **0x58 = `UserData/Import/`**.

## 1. CheckForNewImports / CycleThroughImports (item 1)

### 1.1 Who runs them, and when

Exact caller sets (bl-target scan over the whole code section):

* `CheckForNewImports__18cWinNeighborhoodVCFv` (0x46f460) — 2 callers:
  * **0x472e74 in `cWinNeighborhoodVC::TSOnTimerMsg`** (0x472cc0). The timer
    handler unsubscribes at entry (0x472ce4) and, **only when
    `win->+0x16c == 0`** (0x472ce8..0x472cf0; C4), runs its work and
    **re-arms: `SubscribeTimerMsg(winmgr, this, 0x3e8 /*1000 ms*/, 0)`**
    (0x472e8c..0x472e94, tick source `timeGetTime` = ms). When +0x16c != 0
    it takes the `DoInternetUpdate` branch (0x472cf8) and exits **without**
    calling CheckForNewImports and **without** re-arming (DoInternetUpdate
    sets +0x16c = 1 at 0x46e940 and re-arms at a 100 ms cadence
    0x46e974/0x46ee98). So the import check runs **once per second while no
    internet update is active**. A +0x191 one-shot byte (cleared at
    0x472d20, first tick only) gates the extra first-tick work
    (CheckNeighborhoodFileLocked, DoFirstTimeCelebs).
  * **0x4749b4 in `cWinNeighborhoodVC::Init`** (0x473b20) — once when the
    screen is built (this is the call that fires right after
    `DoNbhdScreen(true)` from ResetTutorial).
  * Screen activation (function containing 0x4740fc) clears the latch byte
    `this+0x190 = 0`, sets `this+0x191 = 1`, and starts the same 1000 ms
    timer (0x4740f8..0x474118).
* `CycleThroughImports__18cWinNeighborhoodVCFv` (0x4704a0) — 1 caller:
  **0x47180c in `RegularModeBtnHandler__18cWinNeighborhoodVCFi`** (0x4711d0),
  the import-UI button.
* Unleashed/Magicland/Downtown/Studiotown variants exist with the same core
  (`CheckForNewImports__18cWinNeighborhoodULFv` 0x464d60 — three pattern
  directories scanned instead of one; `CycleCommunityImports`/
  `CycleResidentialImports`; `cWinMagicland`/`cWinDowntown`/
  `cWinStudiotown` versions). All funnel into the same
  `ImportFamily__FP10ImportInfoPi`. The classic-game (VC) flow above is what
  the tutorial uses.

### 1.2 CheckForNewImports law (0x46f460)

```
CheckForNewImports(win):
  if win->+0x190 != 0: return                      # suppress latch
  pathA = FindDataDirectory(app) + GetString(0x58)   # '<datadir>UserData/Import/'
  pathA += '*.FAM'                                   # globals blob +0x9c (r31=0x6d1c0)
  # C5: the presence scan is a ONE-argument call (0x46f4d8) on the path
  # string with the wildcard already appended — r4 is stale after the
  # append (0x843b0 consumes r4 at entry). The r141 record name
  # "cTSDirectory::…(path, pattern)" is a mis-attribution; the body is
  # FindFirstFileA(path, &fd) (0x2d700) + INVALID_HANDLE test + FindClose
  # -> bool (0x4aa010).
  present = DoesAnyEntryExistThatMatchesPattern(pathA)
  model = *(win+0x130)
  if (u8)model->+0x160 != (u8)present:
      model->+0x160 = present
      model->vt+0x170(model)                         # enable/disable import UI
  pathB = FindDataDirectory(app) + GetString(0x58) + 'Tutorial.FAM'  # blob +0xa2
  if not FileExists(pathB): goto done                # 0x4ae8e0
  # auto-import the ONE tutorial file
  buf = StringBuffer(pathB)
  ImportInfo ii; ok = GetImportInfoForFile(buf, &ii, kind=0)
  if ok:
      rc = ImportFamily(buf, &ii, &outId)            # free fn, 0x231df0
      if rc != 0:                                    # free fn returns 1 on success
          importedAny = 1
          if ii.+0x110 != 0 and ii.+0x150 != 0:      # has house chunks && house != 0
              reloadScreen = 1
      else:
          app.MessageDialog("Couldn't import the family", "Error", 0, 0)
  if importedAny: app.ReportMissingTextures()
  if reloadScreen: app.DoNbhdScreen(true)            # rebuild neighborhood screen
done:
  ii.~ImportInfo(); win->+0x190 = 0
```

Answers to the item-1 questions: the trigger is the neighborhood screen's
1-second timer plus Init; it scans **`<datadir>UserData/Import/`** for
**`*.FAM`** (result cached into the UI model byte +0x160, change-dispatched
through vtable slot 0x170); but the only file it ever AUTO-imports is one
literally named **`Tutorial.FAM`** (FileExists gate). Other .FAM files in
Import/ are consumed by the interactive browser:

### 1.3 CycleThroughImports law (0x4704a0, sketched)

Builds `cTSDirectory(<Import path>)`, sets a directory-entry filter from the
`*.FAM` blob string (0x470554 `SetNewDirectoryEntryFilter`), and iterates the
entries. Per file: `ToChar` → StringBuffer → `ImportInfo` ctor →
**`GetImportInfoForFile(path, &ii, kind=1)`** (0x4705dc; kind 1 = also parse
the house chunks). The FAM's ExpFamilyInfo flag byte (ii+0x188) is compared
against `app+0x64` for cross-version compatibility before display; ii+0x110/
+0x150 drive the "moves into a house" presentation. Buttons: action 1 =
import (`ImportFamily__FP10ImportInfoPi` at 0x470f20; success → flag screen
refresh, failure → the same "Couldn't import the family"/"Error" dialog),
action 2 = rebuild the info panel via `LoadUIStrings(9, GetString(9), …)`.

## 2. ImportInfo, GetImportInfoForFile, and free ImportFamily (item 2)

### 2.1 ImportInfo layout (ctor 0x231d00 / dtor 0x231c00 / filler 0x231fd0)

| off | type | meaning |
| --- | --- | --- |
| +0x000 | StringBuffer(260) | the .FAM path (copied last, 0x142110) |
| +0x110 | u8 | has-house flag: 1 iff `GetHouseInfo(iff, …)` parsed the FAM's SIMI/THMB |
| +0x114 | HouseInfo (dtor +0x114) | house facts from the FAM (r247 §G: +0x14 = SIMI g58 etc.) |
| +0x138 | int | −1 default; **−2 when house chunks present** |
| +0x13c | CTGString | family name (from FAM CTSS string 1) |
| +0x140 | int | family net worth (`GetNetWorth`) |
| +0x144 | int | 0 |
| +0x148 | int | house number (read from Family+0x110) |
| +0x14c | int | magi-coins (`GetMagiCoins`) |
| +0x150 | int | house number again — only rewritten (to Family+0x110) when house chunks present |
| +0x188 | u8 | **ctor seeds it from app->+0x64** (0x231d9c); GetImportInfoForFile overwrites it with the EXPi flag byte (cross-version check vs app+0x64) |
| +0x18c/+0x190/+0x194 | growable array | ImportMember list: 8-byte records `{CTGString name; cTSBuffer* portrait}` (dtor 0x2322b0) |

**The `int*` (r4) of `ImportFamily__FP10ImportInfoPi` is the OUT new-family-id**
— it receives `Family->+0x10c` of the imported family so the UI can select it;
never written when the id is the reserved 0xfa0.

### 2.2 GetImportInfoForFile (0x231fd0)

```
GetImportInfoForFile(path, ii, kind) -> bool:
  IFFResFile2 iff; if not iff.Open(path, write=1) or not iff.ValidFile(): return false
  ExpFamilyInfo efi; if not efi.Load(iff): return false        # 0xb7490 ('EXPi')
  ii->+0x188 = efi.byte
  Family fam(null, 0); if not fam.LoadFamily(iff, efi.famId): return false
  ii->name = fam.GetName(); ii->+0x140 = fam.GetNetWorth()
  ii->+0x148 = *(fam+0x110); ii->+0x14c = fam.GetMagiCoins()
  for i in 0..7 while (s16)efi.ids[i] != 0:                    # max 8 members
      push ImportMember {name: CTGString, portrait: nil}
      CatalogResource::Load(&last.name, iff, efi.ids[i], 1)    # member name text
      LoadBuffer(nsSMResFac, iff, efi.ids[i], &last.portrait, 4)  # portrait BMP_
  ii->+0x110 = 0
  if GetHouseInfo(iff, &ii->+0x114, kind, &flag):              # 0x2324b0, r247 §G
      ii->+0x110 = 1; ii->+0x138 = -2; ii->+0x150 = *(fam+0x110)
  ii->path = path; return true
```

### 2.3 Free ImportFamily (0x231df0)

```
ImportFamily(path, ii, int* outId) -> bool:
  nbhd = *( *(TOC-0x778c) )->+0x24
  rc = Neighborhood::ImportFamily(nbhd, path, &famA, &famB)   # 0xadc50
  if rc != 0: return false            # 0 on success, negative error codes
  famB_id = famB ? famB->+0x10c : -1              # the EVICTED family
  for i in 0..famA->CountMembers():               # famA = the new bin family
      PersonFinder::GenerateBitmaps(GetHandle(famA.GetIndexedMember(i)), 0x1f, 1)
      CQuickTimePlayer::UpdateAll()
  id = famA->+0x10c
  if id != 0xfa0:                                 # 4000 = reserved (Strays) id
      path2 = ConstructUserDataPath(GetString(0x57)='Export/', null)
      if fam = nbhd->GetFamily(id): ExportFamily(nbhd, path2, fam)
      *outId = id
  # evicted-family re-export (C7): literal guard is GetFamily(famB_id)
  # non-null AND that family's +0x10c != 0 (0x231f04..0x231f18); famB_id is
  # snapshotted from *outEvicted before the portrait loop
  if fam = nbhd->GetFamily(famB_id) and fam->+0x10c != 0:
      ExportFamily(nbhd, path3, fam)
  return true
```

So after every import the **`UserData/Export/`** mirror of the affected bin
families is refreshed (ExportFamily, 2248 B) — the export file is the
`.FAM`-in/`.FAM`-out loop's other half. **Error path**: any negative rc from
the real ImportFamily → free fn returns 0 → the screen shows
`MessageDialog("Couldn't import the family", "Error", style 0, 0)`.

## 3. Neighborhood::ImportFamily (0xadc50) — the law (item 3)

Signature `(this, const StackString<260>& famPath, Family** outNewFam,
Family** outEvictedFam)`; returns 0 = success, negative = error.

```
Neighborhood::ImportFamily(nbhd, famPath, &outNew, &outEvicted):
  iff.Open(famPath, write=1); fail -> return (open rc)
  ReconLoadObject<ExpFamilyInfo>(efi, iff, 'EXPi' 0x45585069, 0, 0); fail -> return rc
  efi.byte = app->+0x64                                  # 0xadd0c stb before load
  Family fam; if not fam.LoadFamily(iff, (s16)efi.famId): return -1
  # family display name: 'CTSS' stringset, version 1000, string 1
  CTSS.SetInfo(iff, 1000, 'CTSS', 0, -1); if CTSS.Load(1,0) ok: fam.SetName(CTSS.GetString(1))
  NBRS.SetInfo/SetNew… (0xb88a0) with 'NBRS' 0x4e425253, version 1 -> live Neighbor records; fail -> return rc
  FINV loader (0xb8950) with 'FINV' 0x46494e56, version 2 -> {count+4, array+8} of {s16 id; SimInventory*}
  cSimulator sim; ReconLoadObject<cSimulator>(sim, iff, 'SIMI', 1, 0)      # r247 consts
  tutFlag = ((s16)*(sim+0x44) != 0)                    # SIMI global 26 != 0 → 0xb58 byte
  newFam = MakeNewFamily(nbhd); *outNew = newFam                  # written at 0xaded8
  # --- family id: allocate-or-keep (0xadedc..0xadf2c) ---
  if no existing bin family has +0x10c == fam.LoadFamily-id:  newFam->+0x10c = id   # 0xadf28
  else: newFam->+0x10c stays 0                                  # NO replace-on-collision
  if newFam null -> return -1                                   # null check AFTER assign (C6)
  newFam.SetName(fam.GetName())
  newFam->+0x118 = fam->+0x118; newFam->+0x11c = fam->+0x11c    # money
  newFam->+0x140 = fam->+0x140; fam->+0x140 = 0                 # 'FAMh' handle moved
  house = r26 = fam->+0x110                                     # FAMI house number
  # --- evict current occupant (0xadff0..0xae0c0) ---
  if house == 0: outEvicted = first bin family with +0x10c == 0 else null
  else:          outEvicted = first bin family with +0x110 == house or +0x13c == house
  if outEvicted and house != 0:
      outEvicted->+0x110 = 0                                    # loses the house
      outEvicted->+0x118 = outEvicted.GetNetWorth(); outEvicted->+0x11c = 0
      outEvicted->+0x138 = clear bit28                          # 0xae0bc rlwinm 0x1d,0x1b
  # --- delete the house's current characters (0xae0c4..0xae110) ---
  if house != 0:
      for nbr in nbhd->+0x150[]: if (s16)nbr->+0xfa == house: DeleteCharacter(nbhd, nbr)
  # --- recreate the members (0xae114..0xae608) ---
  for i in 0..7 while (s16)efi.ids[i] != 0:
      pid = efi.ids[i]
      uchr.SetInfo(iff, pid, 'uChr' 0x75436872, 0, -1); uchr.Load(1, 1)
      name13 = uchr.GetString(13)                               # member kind/name
      if name13 == 'dog'  (strtable+0xec) and appbit+0x66&0x20: nbr = AddNewCharacter(nbhd,&out,1,pid,0,-1)   # type 1
      elif name13 == 'dog' and not appbit: RemoveFromFamily(nbhd, outPrev); continue   # pets disabled quirk
      elif name13 == 'cat' (strtable+0xf0) and appbit+0x66&0x20: nbr = AddNewCharacter(nbhd,&out,2,pid,0,-1)  # type 2
      elif name13 == 'cat' and not appbit: RemoveFromFamily(nbhd, outPrev); continue
      else: nbr = AddNewCharacter(nbhd, &out, 0, pid, 0, -1)    # human
      if nbr == null (error code in r23): return rc             # e.g. -1 template missing
      uchrIDs[i] = (s16)nbr->+0x00                              # NEW NeighbourID
      newFam.AddMember(nbr.GetGUID())
      # FINV inventory carry
      if i < FINV.count: el = FINV.array[i]; (s16)*el = nbrID; (s16)*el stays the key
         scan nbhd->+0x194 vector for entry with (s16)*entry == nbrID:
            found -> destroy old SimInventory (0x401b10), replace pointer
            else   -> vector insert {el} into nbhd->+0x190 list (0x36320)
      # character file: clone uChr strings into the new character's selector
      sel = nbr->+0x08 (ObjSelector)
      ss2 = clone of uchr (GetNativeString/SetString over 1..Count)
      ss2.SetInfo(sel, 200, 'STR#', 0, 0); ss2.Save()           # STR# resource 200
      # catalog name/description
      cat = sel.GetCatalogResource()
      ctss.SetInfo(iff, pid + 2000 (0x7d0), 'CTSS', 0, -1); ctss.Load(1,0)
      if ok: cat.name = ctss.GetString(1); cat.sub = ctss.GetString(2)
      else:  CatalogResource::Load(cat, iff, pid, 1)
      cat.Save(sel, (s16)*(sel->+0x60)->+0x52)                  # 0x5d3d0
      # rename the character file: '<shortname> - <name>'
      buf = sel.GetShortFilename(); buf += ' - ' (strtable+0xf4); buf += *(cat+0xc)
      sel.SetObjectName(buf); sel.GetBodyStrings(1)
  # --- GUID translation (0xae608..0xae804) ---
  gt.Load(iff)                                                  # 'Gtab' chunk 0x47746162
  for i in 0..7 while (s16)efi.ids[i] != 0:
      rec  = NBRS.array[ efi.ids[i] - 1 ]                       # FAM-side record by uChr id
      live = nbhd->+0x150[ uchrIDs[i] - 1 ]                     # live neighbor by new id
      if not rec or not live: continue
      gt.AddPair(GetGUID(rec), GetGUID(live))                   # 0x8ae50, 12-byte records
      # C1 — RelMatrix (0xae6e8..0xae78c): the inner loop runs over ALL
      # member id pairs (r29 walks the newIds array base, r30 the efi.ids
      # array base, both reset per outer iteration). Source rows are
      # indexed by the OLD FAM id (efi.ids[j]); dest rows by the NEW id
      # (uchrIDs[j]); n == 0 removes the destination row.
      for j in 0..7 while (s16)efi.ids[j] != 0:
          old = efi.ids[j]; new = uchrIDs[j]
          n = rec->+0x0c.GetArraySize(old)                      # OLD id on the FAM side
          if n == 0: live->+0x0c.RemoveArray(new)               # 0xae710..0xae718
          else:
              live->+0x0c.SetArraySize(new, n)                  # 0xae720..0xae72c
              for k in 0..n-1:
                  live->+0x0c.SetValue(new, k, rec->+0x0c.GetValue(old, k))
      live->+0x18 = rec->+0x18                                  # persistent-field head
      for f in GetPersistentDataFields(rec->+0x18, -1):         # 0xa4600
          live->(0x74 + 2*f.id) (s16) = rec->(0x74 + 2*f.id)
  # --- persist + fixups (0xae804..0xae8a0) ---
  if house != 0: if gt.Save(iff) != 0: return rc                # GUID map written into the FAM
  free gt
  UpdateFamilyFriendsCount(nbhd, newFam); UpdateFamilyNumbers(nbhd)
  # --- house occupancy bookkeeping (0xae8a4..0xaea28) ---
  if house != 0:
      entry = lot table (nbhd+0x170/+0x174) row with +0 == house; must exist and +4 == 1
      else -> return -1
      if any family still has +0x110 == house or +0x13c == house -> return -1
      newFam->+0x110 = house                                    # 0xae9a8
      newFam->+0x138 = set bit29 (4), clear bit28 (8)           # occupies-house
  else:
      if any existing family has +0x10c == 0 -> return -1       # 0xae908, bin has a hole
      newFam->+0x110 = 0
      if newFam->+0x10c == 0 was NOT assigned... (handled above)
      newFam->+0x138 = clear bit29 (0xaea1c..24)
  if FAMI flag bit31 (loaded fam+0x138 bit0): newFam->+0x138 |= 1
  # --- tutorial bookkeeping (0xaea48..0xaeaec) ---
  if tutFlag:                       # FAM SIMI global 26 != 0
      nbhd->+0x12c (s16) = house; nbhd->+0x12e = 0
      for each selector in app->+0x28 ObjectFolder:            # CountSelectors/GetNthSelector
          if (s16)*(sel->+0x60)->+0xb6 has bit (mask 0x2):
              blk = ObjectFolder.GetTypeAttrBlock(sel.GetGUID())
              if blk: blk.Clear()                              # 0xfc460
  elif nbhd->+0x12c == house: nbhd->+0x12c = 0
  iff.Close()
  # --- consume the Import/ file (0xaeaf4..0xaecac) ---
  fm = GetCTGFileManager()                                      # 0x550000
  if house != 0:
      housePath = sprintf('%sHouses/House%02d.iff', ExtractDirectory(nbhd path), house)  # strtable+0xd5
      if not MoveFileA(housePath, housePath + '.tmp' (strtable+0xf8)): return -50
      if not MoveFileA(famPath, housePath):                     # THE FAM BECOMES THE HOUSE FILE
          MoveFileA(housePath + '.tmp', housePath)              # rollback
          return -50
      DeleteFileA(housePath + '.tmp')                           # old house gone
  else:
      DeleteFileA(famPath)                                      # family-only import: consume
  CQuickTimePlayer::UpdateAll()
  Save(nbhd, *(app)->+0x44)                                     # neighborhood saved NOW
  # --- portrait refresh for the moved-in family (0xaecc0..0xaedcc) ---
  if house != 0:
      hi = HouseInfo(); if GetHouseInfo(house, hi, 0):
          if 0x44 < hi.+0x40 < 0x46 and hi.+0x24 > 0:           # SIMI kind 0x45 + family present
              members = GetFamilyMembers(hi.+0x24, &vec)        # 0x233890
              for m in members: PersonFinder::GenerateBitmaps(m, 0x1f, 1)
      hi.~HouseInfo()
  return 0
```

Answers to the item-3 questions:

* **FAMI/FAMs**: `Family::LoadFamily(iff, id)` (0x767e0) sets `Family+0x10c
  = id` immediately, reads the **'FAMI'** chunk via `ReconLoadObject<6Family>`
  (0x770c0; the body echoes the tag reversed, "IMAF"), takes the display name
  from the **'FAMs'** stringset (string 1) and grabs a **'FAMh'** handle into
  +0x140. New id allocation is **keep-if-free, otherwise 0** — there is NO
  replace-on-collision with an existing family id; the 0xfa0/4000 id is
  legitimate but is excluded from the Export refresh and the out-id write in
  the free wrapper (reserved id — consistent with the Strays family).
* **NBRS**: loaded as TEMP `Neighbor` records ('NBRS', version 1) and
  **recreated, not merged**: every live character whose house field
  (`Neighbor+0xfa`) equals the FAMI house is `DeleteCharacter`ed first, then
  fresh neighbors are created with new NeighbourIDs. Per member, the FAM
  record's **RelMatrix rows — indexed by the OLD FAM member ids, copied into
  rows indexed by the NEW ids, one full pass over ALL member id pairs per
  member (C1), with RemoveArray(new) when the source row is empty —** the
  **+0x18 persistent-field head, and the persistent s16 fields at
  +0x74+2·id** are copied onto the live neighbor, and a **GUID old→new
  pair** is appended to the GUIDTranslation ('Gtab', loaded from the FAM,
  saved back before Close).
* **Character IFFs**: yes — `AddNewCharacter` (0xb3770) picks a TEMPLATE
  neighbor by GUID (0=human 0x7fd96b54, 1=dog 0x4a70df92, 2=cat
  0x7bea0977), formats a path from strtable `Characters/` + `User%05d` +
  `.iff` (0xb38c8 sprintf → `UserData/Characters/User#####.iff`), creates it
  through the StubObject selector machinery, and registers it via
  `AddNewNeighbor__12NeighborhoodFP11ObjSelectors` (0xb4d50). The import then
  writes the character's uChr strings into that file as **STR# resource 200**
  and the CTSS-derived name/description into its catalog resource, and
  renames the object to `"<shortname> - <name>"`. The **uChr** resources are
  keyed by the FAMI member ids (47/48 for Tutorial.FAM); string **13** is the
  kind string (`dog`/`cat`/anything-else=human).
* **House chunks**: SIMI/HOUS/FLRm/WALm/Arry/objt/ObjM/THMB/BMP_ are NOT
  parsed chunk-by-chunk on import — the FAM **file itself** is moved over
  `Houses/HouseNN.iff` where NN = the FAMI house number (+0x110). The old
  house file is renamed `+ '.tmp'`, deleted after the move succeeds, and
  restored on failure (return −50). Family-only imports just
  `DeleteFileA(famPath)`. Either way **the Import/ file never survives**.
* **uChr/CTSS handling**: uChr per member (kind string 13 → character type;
  strings cloned to STR# 200; portraits `BMP_` per member loaded by
  GetImportInfoForFile for the UI); CTSS version 1000 string 1 = family
  name; CTSS at resource id+2000 = per-member name/description pair.
* **Afterward**: `UpdateFamilyFriendsCount`, `UpdateFamilyNumbers`, an
  immediate `Save__12NeighborhoodFl(nbhd, *(app)->+0x44)`, the +0x12c/+0x12e
  tutorial-house latch (see §4), the selector type-attr sweep when tutFlag,
  and a portrait regeneration pass for the moved-in family gated on
  `HouseInfo+0x40 ∈ (0x44, 0x46)` (i.e. == 0x45) and `HouseInfo+0x24 > 0`.

## 4. Tutorial.FAM end state (item 4)

File facts (verified against `game-data/The Sims/UserData/Tutorial.FAM`,
SHA256 `dc6eb8f660ea5ce50f2764df29ec792d400a9660b278ae891b13782ae91d42fb`,
317738 bytes, IFF FILE 2.5, chunk sizes INCLUDE the 8-byte header):
37 chunks — `SIMI(0x192) HOUS FLRm WALm Arry×14 objt ObjM HOUS(0x76) THMB
BMP_×2 | FAMI(0x7c) EXPi(0x6b) uChr×2 (+BMP_×2) NBRS(0x1a9a) XXXX FAMs(0xdf)
CTSS×2 rsmp XXXX`. The house chunks are **byte-identical** to the shipped
pristine `Houses/House07.iff` for **every common chunk** (SIMI, HOUS, FLRm,
WALm, all 14 Arry, objt, ObjM, HOUS#2, THMB, both BMP_); only the FAM-only
`rsmp` has no counterpart (skeptic-confirmed).
EXPi decodes (little-endian fields after the `iPXE` echo): famId = **1**,
member ids = **[48, 47, 0×6]**; FAMI (after the `IMAF` echo): house = **7**,
funds = **665 (0x299)**, member count 2, two member GUIDs
(0x5620aa32, 0x5c685132).

**SIMI global 26 (C2 — decode-derived, not a file fact):** the claim "the
tutorial FAM's SIMI carries global 26 ≠ 0 (sim+0x44), so the import takes
the tutFlag branch (0xadeb0)" is **inherited from r247-tut-lifecycle**
(behavioral evidence: only data/script ever sets g26, and a fresh tutorial
run must arm the spawner) and is internally consistent here — the import
reads the same field through the same loader (`ReconLoadObject<cSimulator>`,
0x8d720 at 0xadea4) that LoadHouse uses, and the FAM's SIMI is
byte-identical to the pristine tutorial house's. It is **not byte-provable
from the staged file**: the SIMI body is a ReconBuilder stream (the raw word
at body+0x44 is 0), so mapping stream → object field requires the recon
descriptor decode that r247 lists as UNRESOLVED. verify.py accordingly does
not pin a g26 value; it pins only the read site (0xadeb0 `lha r3, 0x49c(r1)`
= sim+0x44).

Walking the law by hand for the original, after Options → Reset Tutorial
succeeds (`UserData/Import/Tutorial.FAM` created, read-only cleared,
`DoNbhdScreen(true)`):

1. The neighborhood screen's Init/1-second-timer (re-armed only while
   `win+0x16c == 0`, C4) runs CheckForNewImports;
   `*.FAM` present → import UI enabled; `Import/Tutorial.FAM` exists →
   auto-import with kind 0.
2. `Neighborhood::ImportFamily` with house = 7:
   * temp family loads as id 1, name from CTSS#1 ("Newbie"), funds 665.
   * **Fresh neighborhood** (no bin family 1, house 7 empty): newFam gets
     id **1**; no eviction; house 7 has no characters to delete.
   * **Played neighborhood** (user family 1 living in house 7): id 1 is
     taken → the imported family enters the bin with **id 0**; the old
     family 1 is evicted (loses +0x110 house, keeps net worth, flag bit28
     cleared); all characters with `+0xfa == 7` are deleted; the imported
     "Newbie" family takes house 7 with fresh members.
   * Two new characters (`Characters/User#####.iff`, fresh GUIDs, new
     NeighbourIDs) are created for uChr 47/48 from the human template
     0x7fd96b54, each receiving its uChr strings as STR# 200, its CTSS
     name/description, and the NBRS records' RelMatrix rows — per C1, every
     member's FAM row (keyed by the OLD ids 47/48) copied into the row keyed
     by the member's NEW id — plus the persistent fields. FINV inventories
     (if any) are carried into the neighborhood inventory vector keyed by
     the new ids.
   * `newFam->+0x110 = 7`; tutFlag (SIMI g26 ≠ 0 — see the C2 note above:
     decode-derived from r247, not byte-proven from the staged file) →
     `nbhd->+0x12c = 7`, `+0x12e = 0`, and every ObjectFolder selector with
     attribute-flag bit set gets its ObjectTypeAttrBlock cleared.
   * Files: `Houses/House07.iff` → `House07.iff.tmp` → deleted;
     `Import/Tutorial.FAM` **moved onto `Houses/House07.iff`** (so House07.iff
     afterwards IS the FAM file — house chunks identical to pristine plus the
     FAMI/EXPi/uChr/NBRS/CTSS/Gtab extras that house loading ignores). The
     'Gtab' GUID map is written into it before the move.
   * `Neighborhood.iff` saved immediately (family bin + character list).
   * Portraits regenerated for the new family (both passes).
3. Back in the free wrapper: family 1 (or the evicted family, on the played
   path) is re-exported to `UserData/Export/…`; `*outId` = 1 (fresh path).
4. CheckForNewImports: ii+0x110 = 1 and ii+0x150 = 7 → **DoNbhdScreen(true)**
   reloads the screen; ReportMissingTextures runs.
5. Downstream (r247, unchanged): entering house 7 loads the pristine SIMI
   with g26 = 1 → the LoadHouse tail spawns the Tutorial object
   (GUID 0xc3249a1d) since `+0x12a < 3`; generic calls 0/8/9 later complete
   the tutorial; `HouseInfo+0x14` = the house SIMI's g58 keeps the
   "tutorial house" move-in refusal armed for House07.

## 5. Guards — what refuses an import (item 5)

| Guard | Site | Refusal |
| --- | --- | --- |
| FAM open/ValidFile fails | 0xadca0..0xadcc0 / 0x232000 | returns open rc / false |
| 'EXPi' load fails | 0xadd18 (0xb8250 rc) | returns rc |
| LoadFamily (FAMI) fails | 0xadd58 | −1 |
| 'NBRS' load fails | 0xade0c (0xb88a0 rc) | returns rc |
| MakeNewFamily returns null | 0xadf2c (null check AFTER the out-write and id assign, C6) | −1 |
| AddNewCharacter error (e.g. template neighbor missing, −1) | 0xae260 | returns the member's error code |
| House import: lot-table entry missing or entry flag ≠ 1 | 0xae8a4..0xae8f0 | −1 |
| House import: a family still claims the house (+0x110/+0x13c) | 0xae920..0xae99c | −1 (after eviction this only fires on anomalies) |
| Family-only import: another family already has id 0 | 0xae908 | −1 |
| '.tmp' rename fails / FAM→house move fails | 0xaebbc / 0xaec24 | rollback, −50 |
| GUIDTranslation Save fails (house imports) | 0xae814 | returns rc |
| Whole-import failure → free fn 0 → UI | 0x231e28 | "Couldn't import the family" / "Error" dialog |

Note: an **occupied house is NOT a refusal** — the occupying family is
evicted in-place (loss of house, keeps funds) and its characters deleted.
The refusals are structural (lot table, unresolvable id-0 collision,
template/lot missing, file errors).

**C3 — refusals fire AFTER the model is already mutated, and nothing rolls
it back.** The character deletion (0xae0c4), the eviction mutation
(0xae088..0xae0c0), and member creation (0xae114..0xae608) all run BEFORE
the lot-table guard (0xae8a4), the house-claimed / id-0 guards
(0xae8f4..0xae99c), and any mid-loop AddNewCharacter failure (0xae268). On
refusal the original returns −1 with characters deleted and a half-built
family still in the in-memory neighborhood; the only rollback anywhere is
the house-file `.tmp` rename (file level), and `Save__12NeighborhoodFl` is
never reached on any failure path — the free wrapper then reports
"Couldn't import the family" over a dirty model. A port must either
replicate this leak (bit-compatibility) or consciously clean up; §7 step 20
specifies replicate.

## 6. Portable pseudocode

```
def reset_tutorial(app):                       # r247 §B; produces the input below
    ...

def check_for_new_imports(win):                # 1 s timer + screen Init
    if win.suppress_latch: return
    entries = list_userdata_import('*.FAM')
    if win.model.import_present != bool(entries):
        win.model.import_present = bool(entries); win.model.refresh()
    if not exists(datadir + 'UserData/Import/Tutorial.FAM'): return
    ii = get_import_info_for_file('.../Import/Tutorial.FAM', kind=0)
    if ii:
        ok = import_family(ii.path, ii)        # free wrapper
        if ok:
            if ii.has_house and ii.house: app.do_nbhd_screen(reload=True)
        else:
            app.message_dialog("Couldn't import the family", "Error")

def import_family_neighborhood(nbhd, fam_path):
    iff = iff_open_rw(fam_path)
    efi = recon_load(iff, 'EXPi')              # famId + up to 8 member ids
    fam = family_load(iff, efi.famId)          # 'FAMI' + 'FAMs' name + 'FAMh'
    fam.name = iff_stringset('CTSS', 1000).get(1)
    nbrs_fam = load_neighbors(iff, 'NBRS')     # temp records
    finv = load_inventory(iff, 'FINV')         # [{id, SimInventory*}]
    g26 = recon_load(iff, 'SIMI').global[26]   # tutorial latch
    new_fam = nbhd.make_new_family()
    new_fam.id = efi.famId if efi.famId not in nbhd.family_ids else 0
    new_fam.money = fam.money; new_fam.handle = fam.handle
    house = fam.house                          # FAMI house number
    evicted = nbhd.find_family(house=house) if house else None
    if evicted: evicted.lose_house()           # keeps funds
    if house: nbhd.delete_characters_in_house(house)   # Neighbor+0xfa == house
    nbr_ids = []
    for pid in efi.member_ids:
        kind = {'dog': 1, 'cat': 2}.get(iff_string(iff, 'uChr', pid).get(13), 0)
        if kind and not app.pets_enabled:
            nbhd.remove_from_family(prev_nbr); continue
        nbr = add_new_character(nbhd, template_guid[kind], base_record=pid)
        # creates Characters/User%05d.iff via StubObject + AddNewNeighbor
        new_fam.add_member(nbr.guid)
        carry_inventory(finvc, nbr.id)         # replace-or-append
        nbr.selector.save_strings(clone_uChr_as_STR200)
        nbr.selector.save_catalog(ctss_name_desc(pid))
        nbr.selector.set_object_name(shortname + ' - ' + catname)
        rec = fam_records[pid - 1]
        gt.add(old=rec.guid, new=nbr.guid)
        nbr_ids.append(nbr.id)
        # C1 — all member id pairs, source rows keyed by OLD FAM ids:
        for old, new in zip(efi.member_ids, nbr_ids):
            n = rec.relmatrix.row_size(old)
            if n == 0:
                nbr.relmatrix.remove_row(new)
            else:
                nbr.relmatrix.set_row_size(new, n)
                for k in range(n):
                    nbr.relmatrix[new][k] = rec.relmatrix[old][k]
        copy_persistent_fields(rec -> nbr)
    if house:
        gt.save(iff)                           # 'Gtab' persisted into the FAM
    update_family_friends_count(nbhd, new_fam); update_family_numbers(nbhd)
    if house:
        if not lot_table_ok(nbhd, house) or house_claimed(nbhd, house): return -1
        new_fam.house = house; new_fam.flags |= OCCUPIES
    new_fam.flags |= fam.flags & 1
    if g26: nbhd.tutorial_house = house; nbhd.tutorial_house2 = 0
            clear_all_selector_type_attr_blocks()
    iff.close()
    if house:
        if not move(house_path, house_path + '.tmp'): return -50
        if not move(fam_path, house_path): restore(); return -50
        delete(house_path + '.tmp')            # FAM becomes the house file
    else:
        delete(fam_path)                       # consumed
    nbhd.save()
    if house: regenerate_portraits(nbhd.get_family_members(new_fam.id))
    return 0
```

## 7. What the port must reproduce — CANONICAL OPERATION ORDER
(skeptic-verified 25-step contract; C1/C3/C5 corrections folded in)

Driver context first:

0a. **ResetTutorial** (r247, unchanged): confirm dialog → save gate → copy
    `UserData/Tutorial.FAM` → `UserData/Import/Tutorial.FAM` (fail if
    exists) → clear read-only → `DoNbhdScreen(true)`.
0b. **The screen must poll**: `cWinNeighborhoodVC::Init` runs
    CheckForNewImports once; `TSOnTimerMsg` re-runs it and re-arms a 1000 ms
    timer — **only while `win+0x16c == 0`** (no internet update active; C4).
    Presence of any `*.FAM` in `UserData/Import/` (one-arg wildcard-in-path
    scan, C5) toggles the import UI; presence of `Tutorial.FAM` triggers the
    automatic import with no user interaction.

`Neighborhood::ImportFamily` (0xadc50), in exact order:

1. **Zero both out-params** (0xadc80/0xadc88) — `*outEvicted` is always
   written (null when nobody to evict).
2. **Open** the FAM read-write (`IFFResFile2::Open`, magic-checked); fail →
   return open rc.
3. **'EXPi'** recon load → `{flag byte (re-seeded from app+0x64), famId,
   memberIds[8]}`; fail → return rc.
4. **`LoadFamily(famId)`** (0x767e0): id → `Family+0x10c`; 'FAMI' recon
   (house 7, funds 665, member GUIDs); 'FAMs' string 1 = name; 'FAMh'
   handle → +0x140; fail → −1.
5. **'CTSS' v1000 string 1** → temp family name (only if the load succeeds).
6. **'NBRS' v1** → TEMP Neighbor records (fail → return rc); **'FINV' v2**
   → `{id, SimInventory*}` inventory records.
7. **'SIMI' v1** → temp `cSimulator`; latch byte = ((s16)sim+0x44
   [global 26] ≠ 0).
8. **`MakeNewFamily`**; write `*outNew` (0xaded8).
9. **Id allocation** (0xadedc..0xadf28): scan the bin; assign famId to
   `+0x10c` only if unused, else the new family keeps **id 0** (no
   replace-on-collision) — written BEFORE the null check (0xadf2c, C6);
   null → −1.
10. **Copy** name / money (+0x118, +0x11c) / 'FAMh' handle (moved);
    snapshot `house` = loaded `Family+0x110`.
11. **Eviction selection**: house == 0 → first bin family with id 0; else
    first family with `+0x110 == house` or `+0x13c == house`; write
    `*outEvicted`; evict it (clear +0x110, `GetNetWorth` → +0x118,
    +0x11c = 0, clear flag bit28).
12. **Delete every live character** whose `(s16)Neighbor+0xfa == house`.
13. **Member loop** (≤ 8, while `efi.ids[i] != 0`): read 'uChr' resource
    `pid`, string 13; `dog` → type 1, `cat` → type 2, else 0 (pets gated by
    `(s16)app+0x66 & 0x20`; a gated-out pet removes the previous member and
    skips); `AddNewCharacter` (template GUID by type; creates
    `Characters/User%05d.iff`, new GUID/NeighbourID); nonzero return →
    **return the error code, model left mutated (C3)**; else record the new
    id, `AddMember(guid)`, carry the FINV inventory record (replace existing
    id-keyed entry or vector-append), clone uChr strings into the
    character's selector as **STR# 200**, write the 'CTSS' name/description
    (resource `pid + 2000`; fallback `CatalogResource::Load(iff, pid)`),
    rename the object `"<shortname> - <name>"` + GetBodyStrings.
14. **'Gtab' load** from the FAM.
15. **Per member**: append the GUID pair (old record GUID → new GUID).
16. **RelMatrix, per member over ALL member id pairs (C1 — the
    skeptic-corrected law)**: for every `j` with `efi.ids[j] != 0`:
    `n = rec.RelMatrix.GetArraySize(efi.ids[j])` (OLD id); `n == 0` →
    `live.RelMatrix.RemoveArray(newIds[j])`; else
    `SetArraySize(newIds[j], n)` and copy values
    `GetValue(old, k) → SetValue(new, k)`. Then copy `+0x18` and the
    persistent s16 fields at `+0x74 + 2·id`.
17. **'Gtab' save** back into the FAM — house imports only; rc ≠ 0 →
    return rc.
18. **Fixups**: free the translation; `UpdateFamilyFriendsCount`;
    `UpdateFamilyNumbers`.
19. **Guards** (after the mutations — C3): house import → the lot table
    (`nbhd+0x170/+0x174`) must contain the house with entry flag == 1, and
    no family may still claim `+0x110/+0x13c == house` — else **return −1
    with the model left mutated** (deleted characters, half-built family,
    `Save` never reached); family-only import → any existing id-0 family →
    −1, else clear +0x110 / flag bit29.
20. **House assignment**: `newFam->+0x110 = house`; flags set bit29, clear
    bit28; FAMI flag bit31 → `|= 1`.
21. **Tutorial latch**: latch byte set → `nbhd->+0x12c (s16) = house`,
    `+0x12e = 0`, and clear the ObjectTypeAttrBlock of every ObjectFolder
    selector with attribute bit 0x2 set; else clear +0x12c if it equals
    `house`.
22. **Close** the FAM.
23. **File shuffle** (house import): rename `Houses/HouseNN.iff` →
    `+ '.tmp'`; move the FAM file over `Houses/HouseNN.iff` (the FAM
    becomes the house file); delete the `.tmp`. Any failure → restore and
    return **−50** (this is the ONLY rollback). Family-only import →
    `DeleteFileA(famPath)`. Then `CQuickTimePlayer::UpdateAll`.
24. **Save** `nbhd` (`Save__12NeighborhoodFl(nbhd, *(app)->+0x44)`) —
    reached only on success.
25. **Portrait pass** (house imports): `GetHouseInfo(house)`; if
    `0x44 < HouseInfo+0x40 < 0x46` and `+0x24 > 0`, `GenerateBitmaps` for
    every member of `GetFamilyMembers(+0x24)`. Return 0.

Free-wrapper tail: regenerate portraits for the new family's members;
re-export the imported family (and the evicted one, guarded by
`GetFamily(id) && id != 0`, C7) to `UserData/Export/` via
`ConstructUserDataPath('Export/')` — both skipped for reserved id
0xfa0/4000, which also suppresses the `*outId` write; the UI then applies
the §1 dialog/refresh laws.

End state (Tutorial.FAM): `UserData/Import/` empty again; `House07.iff` is
the FAM file itself (pristine tutorial house chunks + family chunks); family
"Newbie" occupies house 7 (id 1 fresh / id 0 played, old family 1 survives
unnumbered); r247's spawner/completion machinery takes over from the house
SIMI the import just installed.

## UNRESOLVED

* **SIMI global 26 value in the staged Tutorial.FAM (C2)**: not
  byte-provable — the SIMI body is a ReconBuilder stream (raw body+0x44 is
  0); the "g26 ≠ 0" tutorial-latch claim is inherited from
  r247-tut-lifecycle and decode-derived only (see §4).
* **FAMI/EXPi recon-stream field semantics beyond house/famId/ids/funds**:
  the streams are little-endian u32/u16 fields after a reversed-tag echo
  (house = first FAMI field = 7; EXPi = s16 famId + s16[8] ids), but the
  remaining FAMI words (+04 = 5, +0c = 17225, +14 = 17) are unnamed until
  the ReconBuilder format decode (same blocker r247 recorded for SIMI).
* **The app flag bytes**: `app+0x64` (byte, copied into EXPi/ImportInfo and
  compared by CycleThroughImports for version compatibility) and
  `(s16)app+0x66 & 0x20` (pets-enabled gate in the member loop) are pinned
  by instruction; their option-screen writers were not traced.
* **Lot-table entry semantics** (nbhd+0x170/+0x174 `{house, flag}`): the
  import requires `flag == 1` for the target house; what populates/means
  flag (house file exists?) was not chased.
* **The +0x190/+0x191 latch pair**: +0x190 = 0 and +0x191 = 1 are written on
  screen activation (0x4740fc) and +0x190 gates/re-arms CheckForNewImports;
  no writer of +0x190 = 1 was found in the VC window code (suppress flag,
  possibly set by a subclass or obsolete path).
* **Which selectors the type-attr sweep hits**: the test bit is
  `(s16)*(sel->+0x60)->+0xb6 & 0x2` — the field's meaning (character
  selector marker?) not named.
* **The exact FAMI member-GUID role** (0x5620aa32/0x5c685132): presumably
  the exported members' original GUIDs (matching NBRS records, remapped via
  'Gtab'), not cross-checked record-by-record.
* UL/Magicland/Downtown/Studiotown Check/Cycle variants were confirmed to
  share the core (same free ImportFamily, same dialog strings) but only the
  VC variant was decoded instruction-exactly.

## Files

* `verify.py` — SHA pin + 230 instruction pins + 23 data pins + 16
  function-record pins + 7 binary-bound
  fixtures (C8 rewrite: branch-tree re-execution and instruction-word field
  decoding for the template-GUID selector, uChr type dispatch, RelMatrix
  register wiring, guard ordering/leak, one-arg scan call; plus the
  Tutorial.FAM/House07.iff file fixtures with full common-chunk byte
  identity); PASS from any cwd; ruff-clean; writes
  `verified-fam-import.json` only.
* `skeptic-corrections.md` — the independent re-derivation (C1–C8 evidence).
* `skeptic-response.md` — provenance of this revision.
* capdis2.py excerpts: `checkfornewimports-vc.txt`,
  `checkfornewimports-ul.txt`, `checkfornewimports-ml.txt`,
  `vc-timer-callsite.txt`, `vc-init-callsite.txt`,
  `cyclethroughimports-import.txt`, `importinfo-ctor-dtor.txt`,
  `importinfo-filler.txt`, `importmember-dtor.txt`, `importfamily-free.txt`,
  `nbhd-importfamily.txt` (0xadc50..0xaeb90),
  `nbhd-importfamily-tail.txt` (0xaeb8c..0xaee30), `loadfamily.txt`,
  `addnewcharacter.txt`, `gt-load.txt`, `fm-getter.txt`.
