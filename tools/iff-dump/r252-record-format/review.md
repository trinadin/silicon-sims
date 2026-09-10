# R252 — Adversarial review of the implementer's write/read ORIGINAL-format change

**Verdict: STRUCTURALLY CORRECT — BUT the P0 skin remap is likely INEFFECTIVE at the
lot-load/spawn boundary (UNCERTAIN→FIX-FIRST), and it is completely UNTESTED by the
new `recordfmt`/`ucasflow` checks. One real honesty gap (import path still writes
legacy format).**

I did not trust the implementation agent's self-report. I re-derived every claim from
the code + diff and re-ran the ground-truth `verify.py` (passes) plus my own inspection
of the shipped original bytes. Paths below are relative to
`FreeSO/` or `Client/` as labelled.

---

## Per-area verdicts

### 1. NBRS write — PASS

- Chunk-data Version `0x49`→`0x3E`: `FreeSO/TSOClient/tso.files/Formats/IFF/Chunks/NBRS.cs:82`
  writes `NbrsFormat.CHUNK_VERSION` (0x3E). Read path `NBRS.Read` (`NBRS.cs:46-70`) reads
  `Version` at `:51` with **no validation**, and `Write` at `:82` — so a port save at
  `0x49` **and** an original `0x3E` both load. Old port saves still parse. **CONFIRMED.**
- Per-record `Version` is set to `0x4` in the save path: `SimitoneNeighbourGenerator.cs:179`
  (`Version = NbrsFormat.RECORD_VERSION`). **CONFIRMED.**

  > Note: `NBRS.cs:146` leaves the *class default* `Version = 0xA`. Any `Neighbour` created
  > **without** going through `SimitoneNeighbourGenerator.AddNeighbor` still writes the legacy
  > shape. The family-import path does exactly that — see area 9.
- `Neighbour.Save` (`NBRS.cs:224-265`) handles Version 0x4 correctly: `if (Version==0xA)`
  writes `Unknown3` (`:228`, skipped for 0x4); person-data loop selects `size=(Version==0x4)?0xa0:0x200`
  (`:235`) and runs `size/2`=80 iterations (`:237-247`), writing `PersonData[0..79]` and
  never indexing past 80. **CONFIRMED; no out-of-bounds, no Unknown3 for 0x4.**
- The 80-short cut is **lossless for created records**: `MakePersonData`
  (`SimitoneNeighbourGenerator.cs:267`) allocates `short[88]` but writes ≤ index 70
  (`pd[60]`/`[61]`/`[65]` max), and no created-record consumer reads ≥80
  (grep of `PersonData[N]` literals maxes at 19 in the network persist, which is a
  TSO-runtime slot, not a TS1 created-record field). **CONFIRMED.**

### 2. FAMI — PASS

- `FAMI.Write` honours `Version`: `FAMI.cs:84` writes `io.WriteUInt32(Version)` (was hardcoded
  `9`). `:13` default is now `0x7`. A loaded v7 re-saves as v7; a legacy v9 round-trips as v9.
  **CONFIRMED.**
- The 16 trailing zero bytes are removed: the `for (i=0; i<4; i++) io.WriteInt32(0)` block is
  gone (`FAMI.cs:96-98` now just a comment). Data size = `40 + 4·guidCount`. **CONFIRMED.**
- Reader still tolerates v9/legacy: `FAMI.Read` (`:49-77`) reads `Version` (`:54`, no
  validation) then try/catches the 4 trailing int32s (`:68-75`) so a v7 no-trailer parses fine.
  **CONFIRMED.**
- No consumer expects the legacy trailer: the default suite reads the shipped v7 FAMIs
  fine (verified by `verify.py`: all six are byte-exact v7). **CONFIRMED.**

### 3. Name-stem — PASS (verified, not assumed)

- `info.CharStem = "user" + userid.ToString().PadLeft(5,'0')` is set in
  `PrepareTemplatePerson` (`SimitoneNeighbourGenerator.cs:35`) **before** `SaveNewNeighbour`
  does `NextSim++` (`TS1NeighbourProvider.cs:216` writes `"User"+NextSim+".iff"`). Since
  `PrepareTemplatePerson` reads `userid = neigh.NextSim` at `:30` and nothing mutates `NextSim`
  before `:68`, the captured stem and the on-disk file stem are **the same counter**, lowercase.
  It flows to `AddNeighbor` via the `name` parameter (`:82`/`:91`/`:160`/`:180`).
  **CONFIRMED — lowercase `userNNNNN`, matching the original (all 20 shipped records are
  lowercase; the character files are capital-U).**

### 4. Skin remap — FIX-FIRST (the P0 area is NOT actually fixed for shipped original saves)

**Write direction is correct:**
- `SkinToDisk` (`SimitoneNeighbourGenerator.cs:338-346`): `0(Light)→1`, `1(Medium)→3`,
  `2(Dark)→2`. Matches `lgt=1, drk=2, med=3`. `pd[60]=SkinToDisk(SkinTone)` (`:325`).
  **CONFIRMED.**
- It does **not** mutate the array; `MakePersonData` constructs a fresh `pd` and writes the
  disk value. **CONFIRMED.**

**Read function is correct in isolation:**
- `DiskSkinToAppearance` (`VMTS1ActivatorNew.cs:497-516`): for `recordVersion==0x4`
  `1→Light, 3→Medium, 2→Dark`; for legacy `0→Light, 1→Medium, 2→Dark`. Returns a value,
  never writes back. **CONFIRMED** against `AppearanceType {Light=0,Medium=1,Dark=2}`.

**BUT the version discrimination is fragile (the P0 break):**
- `RecordVersionFor` (`VMTS1ActivatorNew.cs:525-535`) picks the dialect by reading
  `person.PersonData[(int)VMPersonDataVariable.NeighborId]` (**index 31**) and looking it up
  in `NeighbourByID`; on any miss it falls back to the **legacy 0xA** dialect.
- **The original created-record format leaves `pd[31] = 0` on every record.** I confirmed this
  against the shipped `Neighborhood.iff` with the verified parser: all **20** created records
  have `pd[31]=0` while each carries its NeighbourID in the **separate** `i16 NeighbourID` field
  (e.g. `user00023` → `pd[31]=0`, `nid=19`). The outsider fields `pd[58]`, `pd[60]`, `pd[65]`
  are all in <80; `pd[31]` is simply never written by the original.
- `ConvertAvatar` runs at **lot-load** (`VMTS1ActivatorNew.cs:660`) from `inst.PersonData`
  (the house file's OBJM person data), and there is **no lot-load re-keying of pd[31]** anywhere
  (grep shows the only `SetPersonData(NeighborId,…)` write is `VMAvatar.InheritNeighbor`
  `VMAvatar.cs:1024`, which runs at *runtime spawn*, *after* `ConvertAvatar` has already fixed
  the marshal's `SkinTone`).
- **Consequence:** for a sim whose house person-data has `pd[31]=0` — which is the consistent
  reading of the original format — `RecordVersionFor` misses and returns `0xA`, so
  `DiskSkinToAppearance` applies the **legacy** dialect to the **disk 1/2/3** value:
  disk `1`(light)→Medium (WRONG), disk `2`(dark)→Dark (coincidence), disk `3`(medium)→Light
  (WRONG). **The remap does not actually correct the original-save skin decode.**
- **It is not a regression vs pre-R252** (the old cast also broke these), but the implementation
  agent's claim that the skin is now "read correctly from original-format records" is **not
  substantiated** and is likely false for lot-loaded sims.

**Corrected approach (1 of):** since the original never writes `pd[31]`, the record version can
NOT be reliably recovered from the person data at lot-load. The robust fix is to resolve the
version from the **NeighbourID** that the lot object actually corresponds to (or, simpler, since
R252 makes the port *write* original format going forward, default the unresolved case to **0x4**
rather than 0xA — but that trades off old-port-save compat). At minimum the fallback must not
silently assume the dialect that mis-decodes the canonical data.

### 5. Other `pd[60]` / skin consumers — PASS with one documented residual

- The only read of `pd[60]` into an `AppearanceType` is `VMTS1ActivatorNew.cs:467` (remapped).
  The CAS UI / Vitaboy / outfit lookups use the *logical* `avatar.SkinTone`/`Appearance` (set
  from the skin string `lgt/med/drk` — `VMAvatar.cs:326-328`, `SimTemplateCreateInfo` ctor
  `:239-247`), not `pd[60]`. **No other consumer assumes 0/1/2 on a NBRS read.**
- **`VMNetAvatarPersistState.cs:217`**: `avatar.SetPersonData(SkinColor, SkinTone)` writes the
  logical `AppearanceType` byte (0/1/2) into `pd[60]`. Any runtime skin change clobbers the disk
  dialect, so a later save emits 0/1/2. This is the **runtime-skin clobber residual** — correctly
  disclosed in implementation.md §8 as out-of-scope, but it means "modify an imported sim then
  re-save" will not round-trip the original encoding.

### 6. Autotest pin repoints — PASS (strengthened, not weakened)

- `AutotestCASFlow.cs:874` `nbrs-record-version-4`, `:875` `personmode-5-original`, `:876`
  `nbrs-name-stem`, `:889` `pd60-skin-original-encoding` (asserts `pd[60]==1` for the **lgt**
  fixture, matching the confirmed `lgt=1`). These assert the **original** format and are all
  value-correct against the canon. **CONFIRMED.**
- `recordfmt` is **genuinely opt-in**: `AutotestRunner.cs:76` (`Config.Checks`) contains **no**
  `recordfmt`/`ucasflow` (byte-identical to the baseline); `:351` adds `|| CheckEnabled("recordfmt")`
  to the CAS entry; `:597-601` surfaces an independent `recordfmt` verdict. **CONFIRMED.**

### 7. `recordfmt` check correctness — PASS with two caveats

- It drives a **real** save (the CAS `SaveFamily`→`CreateFamily`→`AddNeighbor` path) and reads a
  **fresh** `new IffFile(neighPath, true)` (`AutotestCASFlow.cs:801`). `member`, `pd`, `charStem`
  come from that fresh parse, and `charStem` is derived from the **actual new file on disk**
  (`:848-851`), so the name-stem assert is non-circular. **CONFIRMED.**
- `nbrs-raw-pd-bytes-a0` (`:984` / `RawScanNbrs:1083-1084`) is **partly tautological**: it
  computes `personDataBytes = Version==0x4 ? 0xa0 : 0x200` and asserts `0xa0==0xa0`. It does not
  independently measure the byte span; it just re-derives the size from the already-checked
  version. The parse-consumes-exactly check in `decode.md`/`verify.py` is the real proof, not this. **Weak.**
- The FAMI trailing-byte assert (`:1014`, `OriginalData.Length == 40+4·guidCount`) is a genuine
  raw byte-measurement. **Good.**
- The round-trip (`:1021-1047`) writes `iff` and re-parses via a fresh `IffFile(rtPath)`; it
  proves `NBRS.Save`/`Read` round-trip `pd[60]==1` etc. **Non-tautological and a real
  non-mutation proof for the codec** — but it does **not** exercise `DiskSkinToAppearance`
  (see area 4/8), so it cannot catch the P0 concern.

### 8. Round-trip — PASS for the codec, but it does NOT test the skin remap

The round-trip writes the parsed `iff` and re-reads it. It proves the NBRS/FAMI **codec** is
lossless and that the **reader** does not mutate `pd[60]`. However `DiskSkinToAppearance`/
`RecordVersionFor` live in `VMTS1ActivatorNew` and are **never called** by `recordfmt` or
`ucasflow` (both read the NBRS records directly; neither loads the created sim into a lot, so
`ConvertAvatar` never runs on it). So **the P0 skin remap is entirely untested** by the new suite.
This is a significant test-coverage gap that should be closed (e.g. assert the skin a spawned
lot-load yields, or unit-test `DiskSkinToAppearance`+`RecordVersionFor`).

### 9. Honesty of `implementation.md` — PASS for the enumerated residuals, **one real gap**

Disclosed and correct:
- NBRS count/record mismatch (`NBRS.cs:90`, r248 P2-4) — acknowledged.
- `rsmp` drop, `FAMI.Unknown=24` vs 1, `pd[67]/pd[68]` — acknowledged as out-of-scope.
- Value-level defaults (`pd[56]=-1,57=4,36=50,33=25,29=1,32=2`, randomized skills/interests,
  and the skills/interests 0..10 vs x100 dialect) — disclosed as a separate structural-vs-value
  divergence.
- Skin runtime clobber (`VMNetAvatarPersistState.cs:217`) — disclosed.
- The **name-stem sourcing decision** (CharStem from `NextSim`, NOT the neighbour id) is correctly
  documented, and it is **correct**: `user00023` has `NeighbourID=19`, `user00022` has `NeighbourID=20`,
  so the record `Name` is the file stem, not the neighbour id. **CONFIRMED.**

**Gap:** `TS1NeighbourProvider.ImportAddNeighbor` (`TS1NeighbourProvider.cs:1124-1166`) still
writes a **legacy** record — it creates `new Neighbour()` leaving `Version=0xA` (class default)
and sets `PersonMode = 9` (`:1160`). The import/family-recreate path therefore does NOT write the
original format (256-short person data, Unknown3, PM9), even though it already uses the lowercase
stem. implementation.md's edit list only covers `SimitoneNeighbourGenerator.AddNeighbor` and §8 does
not mention this remaining non-original-format writer. This is defensible as out-of-R252-scope (r247
family import), but it should be disclosed, and it means **not all neighbour-writes are byte-faithful.**

### 10. Default-suite risk — UNCERTAIN (reader safe; skin remap unproven)

- **Reader: safe.** Shipped original saves (chunk 0x3E, record 0x4, 80 shorts, PM5, stem name,
  FAMI v7 no-trailer) all parse through the already-flexible `Neighbour` ctor (`NBRS.cs:148-217`)
  and `FAMI.Read`. No parse regression.
- **FAMI write: safe/backward-compatible** (loaded v7 stays v7 no-trailer; reader tolerates both).
- **Skin remap: unproven.** If lot-load person data carries `pd[31]=0` (the consistent reading of
  the original format), `RecordVersionFor` falls back to the legacy 0xA dialect and mis-decodes
  disk 1/2/3. The default suite does assert skin? It does **not** — no default check validates an
  original sim's skin, so the suite **can still report 142/0 while rendering wrong skins** on the
  shipped lots. The reported green run therefore is **not** evidence that the skin remap works.

---

## Concrete bug list (prioritised)

**FIX-FIRST (P0) — tell the implementer:**

1. **Skin remap ineffective for lot-loaded original sims.**
   `RecordVersionFor` (`VMTS1ActivatorNew.cs:525-535`) resolves the dialect via `person.PersonData[31]`,
   which is **0** in every shipped original record. On a miss it falls back to `0xA`, so an original
   record's disk `1/2/3` is decoded with the legacy `0/1/2` map → light→Medium, medium→Light.
   *Fix direction:* resolve the record from the **NeighbourID** the lot object corresponds to
   (not `pd[31]`), or default the unresolved case to `0x4` (the format R252 now writes), or remove
   the version discrimination and always use the disk map now that the port writes original format.

2. **P0 remap is untested.** Neither `recordfmt` nor `ucasflow` ever calls `ConvertAvatar`/
   `DiskSkinToAppearance`. Add a test that loads the created family into a lot (or unit-test
   `DiskSkinToAppearance` + `RecordVersionFor`) so the P0 behaviour is actually pinned.

**Honesty/disclosure:**

3. **`ImportAddNeighbor` still writes legacy format** (`TS1NeighbourProvider.cs:1155-1163`:
   `Version=0xA` default, `PersonMode=9`, 256-short). Document it as a remaining non-original
   writer (or fix it to `Version=0x4`/`PersonMode=5`).

**Minor:**

4. `recordfmt`'s `nbrs-raw-pd-bytes-a0` is a tautology (re-derives `0xa0` from `Version==0x4`).
   Measure the byte span independently (e.g. record the codec's `io.Position` delta) or drop the
   redundant assert.

---

## Confirm / refute the headline claims

- **Name-stem sourcing = CONFIRMED.** `userNNNNN` lowercase from `NextSim`, not the neighbour id.
- **Write-direction skin map = CONFIRMED** (`0→1, 1→3, 2→2`; `1/3/2` on disk; no array mutation).
- **Read-direction pure function = CONFIRMED correct** in isolation.
- **P0 skin remap actually fixing original saves = REFUTED/UNPROVEN** — the version
  discrimination (`pd[31]`) cannot distinguish an original record on the lot-load path, so the
  fix likely does not take effect for shipped original loads, and it is untested.

---

## Honest confidence

**High confidence:** NBRS/FAMI writers, name-stem, pin repoints, recordfmt opt-in, FAMI reader
tolerance, the `pd[31]=0` fact on all 20 shipped records, and the fact that neither new check
exercises the skin remap. All verified against code + raw bytes (`verify.py` re-run, PASS).

**Medium confidence on the P0 consequence:** I could NOT parse a shipped `HouseNN.iff` person
object to confirm whether the original house files carry `pd[31]` (the OBJM chunk is compressed
and its person/portal/multitile framing depends on a world-object OBJT lookup not available to my
standalone parser). So the *trigger condition* (`lot pd[31]==0`) is strongly suggested but **not
definitively proven**. The failure mode and the untested gap, however, are certain.

**What I could NOT verify:** (a) the house-file `pd[31]` value; (b) that the default suite actually
spawns and visually renders the shipped lots in a way that would surface a wrong skin; (c) the
`ImportAddNeighbor` path is not exercised by the default check set (it is the import flow); (d) I did
not rebuild/run the app — all conclusions are from static analysis + the re-run of `verify.py` and
the recorded `recordfmt`/ucasflow/full-suite output in `implementation.md`.

**Net statement on the default suite:** the format change is **safe to parse** the shipped
original saves and **backward-compatible**; the one open risk is the skin rendering of lot-loaded
original sims, which the reported green run does not actually validate.
