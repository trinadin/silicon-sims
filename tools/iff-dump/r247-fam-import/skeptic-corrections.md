# R247-fam-import — adversarial re-verification (skeptic pass)

Independent re-derivation from `The Sims Complete` (SHA256
`33c76da2…c06a5f`, confirmed) via fresh disassembly (capdis2.py ranges
0x46f460/0x472cc0/0x473b20/0x231d00–0x232260/0xadc50–0xaee2c/0xb3770/
0xaf1a0/0x767e0/0x4aa010/0x51c500), an independent bl-target scan over the
whole code section, an independent function-record extraction (7608 records,
r247 law reproduced), and independent re-parsing of
`UserData/Tutorial.FAM` + `UserData/Houses/House07.iff`. verify.py runs PASS;
its pins were re-checked against raw words (14 raw + ~80 in-disassembly, zero
mismatches) and its data pins/strings recomputed from the PEF.

## Per-claim verdicts

* **Claim 1 (CheckForNewImports) — CONFIRMED with 2 corrections** (C4 timer
  re-arm condition, C5 scan-call signature). Callers are exactly
  {0x472e74 TSOnTimerMsg, 0x4749b4 Init}; re-arm `li r5, 0x3e8` at 0x472e8c
  with `SubscribeTimerMsg(winmgr, this, 1000, 0)`; tick source is
  `timeGetTime` (0x18220) → **ms**, so the period really is 1 s. The
  +0x190 latch, model+0x160 cache + vt+0x170 refresh, literal
  `Tutorial.FAM` gate (blob 0x6d1c0+0xa2 via FileExists 0x4ae8e0),
  GetImportInfoForFile kind 0, the free-ImportFamily call, the
  "Couldn't import the family"/"Error" dialog on rc==0, the
  ReportMissingTextures / DoNbhdScreen(true) (iff ii+0x110 && ii+0x150)
  trio, and the dtor/latch-clear epilogue are all instruction-exact.
  CycleThroughImports: 1 caller (0x47180c), kind=1 at 0x4705dc,
  SetNewDirectoryEntryFilter at 0x470554 — confirmed.
* **Claim 2 (free ImportFamily / ImportInfo) — CONFIRMED with 2 low notes**
  (C6 ctor seeds +0x188; C7 evicted-export guard form). All struct offsets
  verified in ctor 0x231d00 (+0x138 = -1 default), filler 0x231fd0
  (+0x114 HouseInfo, +0x140 net worth, +0x144 = 0, +0x148 = *(fam+0x110),
  +0x14c magi-coins, +0x150 only-on-house-chunks, +0x188 = efi flag byte,
  +0x18c vector of 8-byte members), dtor 0x231c00 (8-byte stride,
  deletes elem+4 cTSBuffer*, HouseInfo dtor at +0x114). The int* out:
  `*outId = (*outNew)->+0x10c` at 0x231ef0, skipped together with the
  imported-family ExportFamily when id == 0xfa0 (single branch at
  0x231e90) — exactly "what is skipped" as decoded. Path built with
  ConstructUserDataPath(GetString(0x57)='Export/') (record name confirmed).
* **Claim 3 (Neighborhood::ImportFamily) — CONFIRMED except one real
  error**: the RelMatrix copy (C1). Everything else instruction-exact:
  chunk law and order (open → EXPi → FAMI/FAMs/FAMh → CTSS v1000 #1 →
  NBRS v1 → FINV v2 → SIMI v1 latch read → MakeNewFamily → id
  keep-if-free-else-0 → name/money/handle move → eviction → house-character
  deletion → member loop → Gtab load/pairs/save → fixups → guards → flags →
  tutorial latch + type-attr sweep → Close → file shuffle → UpdateAll →
  Save → portrait pass). Eviction law, Neighbor+0xfa (s16), the 0x138
  bit ops (evicted clear bit28; newFam set bit29/clear bit28 or clear
  bit29; FAMI bit31 → |= 1), uChr string 13 discriminator with strtable
  'dog'/+0xec and 'cat'/+0xf0, pets gate (s16)app+0x66 & 0x40 with
  RemoveFromFamily(previous-neighbor), AddNewCharacter return convention
  (0 = success, Neighbor* through out param; nonzero = error code),
  template GUIDs (0→0x7fd96b54, 1→0x4a71df92, 2→0x7bea0977, out-of-range →
  human — verified at 0xb37b0..0xb37f4), FINV pointer-record carry,
  STR# 200 clone, CTSS pid+0x7d0 catalog + fallback
  CatalogResource::Load(iff, pid, 1), rename shortname + ' - ' + *(cat+0xc)
  + GetBodyStrings(1), Gtab 12-byte pairs (rec→live), persistent fields at
  +0x74+2*id, guards' polarities, nbhd+0x12c/+0x12e latch, type-attr sweep
  (bit 0x2 of (s16)*(sel->+0x60)->+0xb6 → GetTypeAttrBlock → Clear),
  .tmp rename/move/delete with -50 rollback, family-only DeleteFileA,
  Save(nbhd, *(app)->+0x44) AFTER the move, portrait gate
  0x44 < hi+0x40 < 0x46 && hi+0x24 > 0. `SIMI` tag/version verified through
  the TOC slots (‑0x7318 → 'SIMI', ‑0x731c → 1).
* **Claim 4 (guards) — CONFIRMED** with one documentation gap (C3: guard
  refusals leak the already-applied model mutations; only the file move has
  a rollback, and Save is never reached on failure).
* **Claim 5 (Tutorial.FAM end state) — CONFIRMED for everything provable
  from the staged bytes; one claim downgraded to UNPROVABLE** (C2: "SIMI
  global 26 ≠ 0"). Independently verified from the files: FAM SHA256
  dc6eb8f6…, 317738 bytes, 37 chunks in the stated order with the stated
  sizes; EXPi famId 1, ids [48,47,0×6], trailing flag byte 01; FAMI house 7,
  funds 665 (0x299), members 2, raw words +04=5, +0c=17225, +14=17, tail
  GUID words 0x5620aa32/0x5c685132 (BE reading); uChr resource ids 47 and
  48; FAMs name "Newbie"; CTSS = Betty/Bob name+description sets. Byte
  identity with pristine House07.iff holds for **every** common chunk
  (SIMI, HOUS, FLRm, WALm, all 14 Arry, objt, ObjM, HOUS#2, THMB, BMP_×2)
  — decode.md only claimed/compared 7 tags; only `rsmp` differs. The
  end-state chain (Import/ emptied, family Newbie in house 7 with id 1
  fresh / 0 played, +0x12c=7/+0x12e=0, two User#####.iff, Neighborhood.iff
  saved, Export/ refresh, portraits, screen reload) follows from the
  confirmed laws.

## Corrections (severity-ordered)

**C1 (HIGH) — RelMatrix copy: the source row is indexed by the OLD FAM id,
not the new id, and the loop covers ALL member id pairs.**
decode.md §3/§6/§7.3d say `n = rec->+0x0c.GetArraySize(newid);
live->+0x0c.SetArraySize(newid, n); live->SetValue(newid, j,
rec->GetValue(newid, j))`. The binary (0xae6dc..0xae78c) is, per member i
(with r20 = rec(NBRS record)->+0xc, r22 = live->+0xc, r29 walking newIds,
r30 walking efi.ids):

```
for j in 0..7 while (s16)efi.ids[j] != 0:      # ALL ids, not just member i's
    old = efi.ids[j]; new = newIds[j]
    n = rec->+0x0c.GetArraySize(old)           # OLD id on the FAM side
    if n == 0: live->+0x0c.RemoveArray(new)    # branch decode.md omits
    else:
        live->+0x0c.SetArraySize(new, n)
        for k in 0..n-1:
            live->+0x0c.SetValue(new, k, rec->+0x0c.GetValue(old, k))
```

This inner loop is nested INSIDE the per-member GUID loop, so each live
neighbor receives every imported member's relationship row (row index =
relationship target), each translated old→new. A port written from
decode.md would index the source rows by the fresh NeighbourIDs (which
exist only after creation) and lose the n==0 removal — wrong for the normal
case where FAM ids (47/48) differ from the new ids.

**C2 (MEDIUM) — "SIMI global 26 (= sim+0x44) is nonzero" is presented in §4
as a file fact but is not provable from the staged bytes.** The SIMI body's
word at +0x44 is 0; mapping stream→object requires the ReconBuilder
descriptor decode that r247 itself lists as UNRESOLVED. The claim is
inherited from r247-tut-lifecycle (behavioral) and is internally consistent
— the import (0x8d720 at 0xadea4) uses the same loader and the same
object offset as LoadHouse, and the FAM's SIMI is byte-identical to
House07's — but verify.py does not pin it either (its walkthrough asserts
g26 != 0 as a law constant). §4 wording should be downgraded to
"inherited from r247-tut-lifecycle; not byte-proven here". Port impact:
none (the port reads the same field from the same loader).

**C3 (MEDIUM) — guard refusals leave the in-memory neighborhood mutated;
decode.md's "refused" framing hides this.** The lot-table guard
(0xae8a4), the house-claimed / id-0 guards (0xae8f4..0xae99c), and the
AddNewCharacter failures (mid-loop at 0xae268) all fire AFTER character
deletion (0xae0c4), eviction mutation (0xae088..0xae0c0), and member
creation (0xae114..0xae608). On refusal the original returns −1/−50 with
characters deleted and a half-built family still in the model; there is no
model rollback anywhere — the only rollback is the house-file .tmp rename,
and `Save__12NeighborhoodFl` is never reached on any failure path. The
free wrapper then reports "Couldn't import the family" over a dirty model.
A port must either replicate this (bit-compatibility) or consciously clean
up; decode.md should state which behavior §7.3h specifies.

**C4 (LOW-MEDIUM) — timer re-arm condition is incomplete in decode.md.**
TSOnTimerMsg unsubscribes at entry (0x472ce4) and re-arms 1000 ms at
0x472e8c ONLY when `win->+0x16c == 0`. When +0x16c != 0 it takes the
DoInternetUpdate path (0x472cf4) and exits WITHOUT calling
CheckForNewImports and WITHOUT re-arming (DoInternetUpdate sets +0x16c = 1
at 0x46e940 and re-arms 100 ms at 0x46e974/0x46ee98). §1.1's "the check
runs once per second while the neighborhood screen is open" should read
"…while no internet update is active (+0x16c == 0)". Near-moot offline, but
it is the re-arm condition the port must encode.

**C5 (LOW) — the `*.FAM` presence scan is a ONE-argument call; the
`*.FAM` is part of the path string, and the r141 symbol name is a
mis-attribution.** At 0x46f4bc..0x46f4d0 the wildcard is APPENDED to the
path ('<datadir>UserData/Import/' + '*.FAM'); the call at 0x46f4d8 sets
only r3 (r4 is stale after the append call, which consumes r4 at entry —
0x843b0 reads r4 immediately), so the callee's signature is
`f(const cTSString&)`, not `cTSDirectory::…(path, pattern)`. Internally it
is FindFirstFileA(path, &fd) (0x2d700) + FindClose → bool. The scan LAW
(any *.FAM in UserData/Import/) is unchanged; decode.md §1.2's
`DoesAnyEntryExistThatMatchesPattern(pathA, '*.FAM')` two-arg form is not
what the binary does.

**C6 (LOW) — ImportInfo ctor seeds +0x188 from app->+0x64** (0x231d9c:
`lbz r0, 0x64(r4); stb r0, 0x188(r30)`), before GetImportInfoForFile
overwrites it with the EXPi flag byte. decode.md §2.1 attributes +0x188
only to the filler. Also, in Neighborhood::ImportFamily `*outNew` is
written (0xaded8) and the family-id assign (0xadf28) happens BEFORE the
null check (0xadf2c) — decode.md's law lists the null check first; only
the second "if null" occurrence matches the binary (immaterial if
MakeNewFamily never returns null, but the stated order is wrong).

**C7 (LOW) — evicted-family export guard form.** The wrapper's second
export is guarded by `fam = GetFamily(famB_id); fam && fam->+0x10c != 0`
(0x231f04..0x231f18), not literally `famB_id ∉ {−1, 0}`; famB_id is
snapshotted from *outEvicted before the member-portrait loop. Same behavior
in practice (GetFamily(−1) returns null); noted for exactness.

**C8 (COSMETIC) — verify.py issues.** (a) Six of the seven "fixtures"
(auto_import_predicate, refresh_condition, family_id_collision,
house_guards, member_types, reset_import_end_state, import_disposition)
are tautologies: pure-Python restatements of the decode that assert on
their own outputs and cannot falsify anything about the binary. The
evidentiary content of verify.py is entirely in its 135 instruction pins +
22 data pins + 16 record pins + the Tutorial.FAM/House07.iff file fixtures
— all of which survived independent re-derivation. (b) Fixture (e) defines
`TEMPLATE_GUIDS = {0: 0x7FD96B54, 1: 0x4A71DF92, 0x7BEA0977: 2}` — a
malformed (unused) mapping; the `guid_for` dict below it is the correct
one.

## Additional confirmations worth recording

* Function-record law reproduced: 7608 records; 0xadc50 =
  ImportFamily__12Neighborhood… (4572 B), 0xaf1a0 = ExportFamily… (2248 B),
  0xb1050 = UpdateFamilyFriendsCount — the r141 corrections claimed by
  decode.md are real.
* Call-site completeness (bl scan over 0x8e90..0x5c22f0):
  CheckForNewImports = exactly 2 callers; CycleThroughImports = 1;
  Neighborhood::ImportFamily = 1 (the free wrapper); free ImportFamily =
  7 callers (VC check 0x46f5cc, VC cycle 0x470f20, UL 0x464fd4, +4 others);
  AddNewCharacter called at the three dispatch sites 0xae1d4/0xae23c/
  0xae258 plus 0xfa644/0x230924/0x233804; ExportFamily at 0x231ee4/0x231f6c
  inside the wrapper.
* 0xadc50 zeroes both out-params at entry (0xadc80/0xadc88) — *outEvicted
  is always written (null when nobody to evict).
* Tutorial.FAM vs House07.iff: byte-identity extends to ALL common chunks
  (incl. 14 Arry and both BMP_); only rsmp differs. Stronger than decoded.
* FAMI member-GUID words confirmed 0x5620aa32/0x5c685132 (BE reading of
  LE fields) — role still unresolved, as decode.md says.
