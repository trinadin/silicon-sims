# R154 — ENGINE WIRING: does the ORIGINAL BINARY ever call tree 9761 / 39200, and which engine events invoke trees at all?

Binary: `game-data/The Sims/The Sims Complete` (PPC PEF, Complete Collection).
All code addresses are FILE offsets into sec0 (raw code, file
0x8E90..0x5C22E8). Data addresses are offsets into the unpacked sec1 image
(`r104/sec1-unpacked.bin`; CFM TOC r2 = sec1 base + 0x8000). Symbols: r153
(14,578 functions, `r153-symbols.txt/.json`). Scripts: `r154-anchor-scan.py`,
`r154-tree-machinery.py` (this round, reusable). Dumps:
`r154-anchor-9761.txt`, `r154-engine-trees.txt`.

## VERDICT UP FRONT

**NO. The engine binary never references tree id 9761 ('Convert Interests to
0-1000') or 39200 ('Add Hot Date Interests') — not as code immediates, not as
data words, not inside any dispatch table. The engine is NOT a caller of
either BHAV; if either tree runs, a SCRIPT ran it.** This closes the
R153 residual #1 from the engine side.

Second result: **the engine never hardcodes ANY numeric tree id.** Every tree
invocation in the binary resolves its id at runtime from exactly three
sources: (1) the object's ObjFnTable resource (the TTAB-analog) indexed by an
engine-event number (ObjEntryPoint), (2) a tree NAME string via
`Behavior::GetTreeID(StackString)` lookup, or (3) a script-supplied id from
the VM stack (the Gosub/interaction prims). At all 46 direct call sites of
the tree runners the id register is table/name/data-fed; no `li` of a tree
id exists anywhere. The complete map of engine events that invoke trees is
in §3.

## 1. TASK 1 — ANCHOR SCAN: zero real hits

Scan space and forms (r154-anchor-scan.py → r154-anchor-9761.txt):

* sec0, every 4-aligned word decoded as a D-form instruction whose 16-bit
  immediate field equals 9761 (0x2621) or 39200 (0x9920). STRONG forms
  (li/addi rA=0, lis, ori, oris, xori, xoris, andi., andis., cmpwi, cmplwi,
  mulli, addic) are the ones that carry values; D-form load/store
  displacements and bc/bl displacement fields are coincidence-prone and
  classified separately. 39200's signed form (cmpwi against -26336 after
  lha) uses the same raw field 0x9920 and is covered.
* 32-bit composites: lis+ori pairs — none.
* sec0 word-aligned literal pools 0x00002621 / 0x00009920 — none.
* sec1 unpacked image: raw halfwords and BE words — 2 halfword hits, both
  low halfwords of 32-bit POINTERS (see table).

Result: **8 raw-field hits, 0 STRONG.** Every hit is a coincidence:

| file addr | word | op | why it is not an id |
|---|---|---|---|
| 0x000537D0 | 4BFE2621 | bl | op-18 branch; 0x2621 = low 16 bits of the 24-bit LI displacement (target 0x35DF0, std::string assign region) |
| 0x000D3D00 | 4BF62621 | bl | same shape (target 0x36320, in WriteBackConstants) |
| 0x0038DEF0 | 48002621 | bl | target 0x390510 (VitaBoy::InitializeAll) |
| 0x004C20F0 | 48032621 | bl | target 0x4F4710 (cTSLanguageUtility) |
| 0x0052EF00 | 48062621 | bl | target 0x591520 (cTSWinTextEdit2) |
| 0x00358840 | 80629920 | lwz r3,-26336(r2) | TOC-relative load in SAnimator::Tick (slot = float-constant pointer), not a value |
| 0x00358864 | 80629920 | lwz r3,-26336(r2) | same slot, second use in the same cascade |
| 0x005C1760 | 00569920 | data | low halfword of pointer 0x00569920 (sec0-rel code 0x569920 = file 0x5727B0) in a pointer map |

sec1: halfword 0x9920 at sec1+0x002116 (low half of pointer 0x00079920) and
at sec1+0x01513A (low half of pointer 0x00569920, same pointer family as the
sec0 data hit). **No 0x2621 anywhere in sec1; no 0x00002621/0x00009920
32-bit words in either section.**

Table-run check: since no table contains 9761/39200 at all, there is no
dispatch-table neighborhood to dump — the negative is total (independently
reproduced by the parallel scan r154-engine-constscan.txt: "load-immediate
scan (addi/li + ori) for [9761, 39200]: total: 0").

## 2. TASK 2 — THE TREE-INVOCATION MACHINERY (byte-pinned)

### 2.1 The id resolver: ObjFnTable = the TTAB-analog, ids are DATA

* `ObjFnTable::GetTreeID(ObjEntryPoint ep)` @0x0FCCA0:
  `if (ep < 0) return 0; return entries.data()[ep];` — entries = a
  `{int size@+4; vector<ObjFunction> @+8}` of 4-byte ids
  (`blt` @0xFCCB4 word 4180002C; `lwzx r3, r3, r0` @0xFCCD8 word 7C6302AE
  after `rlwinm r0, r31, 2` @0xFCCD4 word 57E0103A).
* `cXObject::GetTreeID(ObjEntryPoint)` @0x170870:
  `return ObjFnTable::GetTreeID(this->ObjSelector->[+284]->GetFnTable(), ep)`
  (`lwz r3, 284(r3)` word 8063011C → 0x103120 `lwz r0,144(r3)` cached-table
  getter → `bl 0xFCCA0` @0x170890).
* The table is LOADED FROM THE OBJECT'S RESOURCES:
  `ObjFnTable::Load(iResFile)` @0x0FC710 → `ReconLoadObject<ObjFnTable>`
  @0x0FCF90. For old-format objects,
  `BuildFromOldEntries(ObjDefinition)` @0x0FC780 builds it from 30 OBJD
  halfword fields — one `GetFunction(table, EP)` per enum value
  (`li r4, 0..29` at 0xFC78C..0xFC9D8; each id stored via
  `sth r0, 0(r3)` from `lha r0, K(r31)`). The full EP enum is therefore
  0..29; field offsets in call order:
  EP0←+0x56, 1←+0x0A, 2←+0x5E, 3←+0x68, 4←+0x78, 5←+0x2A, 6←+0x7C,
  7←+0x64, 8←+0x9C, 9←+0x58, 10←+0x94, 11←+0x60, 12←+0x5A, 13←+0x6A,
  14←+0x6E, 15←+0x22, 16←+0x0C, 17←+0x18, 18←+0x30, 19←+0x32, 20←+0x34,
  21←+0x36, 22←+0x38, 23←+0x3A, 24←+0x3C, 25←+0x3E, 26←+0x40, 27←+0x42,
  28←+0x76, 29←+0xA0.

### 2.2 The runners

* `TreeSim::RunCheckTree(Behaviors*, short id, short*)` @0x153EC0 — the
  general "run tree id if not busy" gate (`lha r0, 36(r3)`; returns 0 when
  the flag is set), inner exec 0x1540D0.
* `TreeSim::RunOneTickTree(Behaviors*, short id, short*)` @0x153E60 —
  single-tick variant, delegates to RunCheckTree @0x153EE0-ish (bl 0x153EC0
  at +0xC).
* `TreeSim::Reset(Behaviors*)` @0x154320 — tears down and re-arms; callers
  pass the MAIN-tree id (see §3).
* `cXObject::RunTree(Behaviors*, short, const char* name, short*)`
  @0x0EFD30 — BY NAME: builds a StackString from the char* (bl 0x142230),
  `id = Behavior::GetTreeID(StackString)` (bl 0x3FF40 @0xEFD88), returns 0
  if id == 0, else `RunCheckTree(this, beh, id, ...)` @0xEFDA8. **Name →
  runtime lookup; never a literal id.**
* `cXObject::GosubObjectTree(cXObject*, short id, short, bool)` @0x0EFE00;
  `cXPerson::` override @0x109180 (delegates at +0x24, the ONLY direct
  caller). Dispatched VIRTUALLY: CFM tvector for the cXPerson version at
  sec1+0xDA80 (code sec0-rel 0x1002F0), referenced by the cXPerson vtable
  (base sec1+0x48EE0, r153-verified vptr) at sec1+0x48F1C →
  **vtable slot +0x3C (+60)**. Call sites do
  `lwz r12, K(r12)` + glue 0x5A29E0 (`lwz r0,0(r12); mtctr; lwz r2,4(r12);
  bctr`). 139 glue sites use final slot K=60 across all classes; the ones
  on object/person code paths (TryCallFunctionalTree+0x198 @0xF4328,
  TryCallNamedTree+0x318 @0xF99E8, TryIdleForInput+0x2EC @0x10A3AC,
  InitRoute+0x1A4 @0x10CF74, TryGotoRoutingSlot x3) pass ids taken from
  VM stack frames / routing data — script-visible, data-driven.
* cXPerson embeds a TreeSim base (the runners are called with the person as
  `this`; TreeSim fields +4/+36 live inside cXPerson).

### 2.3 The decisive census (r154-engine-trees.txt, full windows)

Direct bl census: RunTree 17 sites; GosubObjectTree(cXObject) 1 (the
cXPerson wrapper); RunOneTickTree 3; RunCheckTree 40; TreeSim::Reset 3;
GetTreeID(ObjFnTable) 66; cXObject::GetTreeID 7; GetFunction (builder) 31.

At EVERY runner site the id register (r6) is fed by one of:
* `addi r6, r3, 0` immediately after the paired `bl GetTreeID` (the uniform
  event pattern, e.g. Reset__8cXPerson @0x111AA4 word 38C30000, Cleanup,
  PostLoad, Pickup/Place/UserPickup/UserPlace/Turn/UpdateRooms/...), or
* `addi r6, r3, 0` after `bl 0x3FF40` Behavior::GetTreeID(name) — the
  by-name path (BroadcastMessage @0xE367C, TryCreateObject @0xF6470,
  RunTree itself @0xEFD8C), or
* `lha r6, 32(r30)` — the Interaction record's tree-id field
  (AddAction @0x10B00C word A8DE0020).

**No `li` into an id argument exists at any of the 46 runner sites.** The
only numeric literals near these calls are the EP enum constants (r4 at
GetTreeID sites, values 0..29) and runner params/flags (li r5,0 / r7,0).

## 3. TASK 3 — EVENT MAP: which engine events run trees, with byte cites

Every line = engine event handler → `li r4, EP` → GetTreeID → run. The EP
constant is the `li r4, K` immediately before the GetTreeID bl.

| EP | meaning (derived) | engine callers (site = GetTreeID bl) |
|----|-------------------|--------------------------------------|
| 0 | Init tree | Reset__8cXPerson+0x190 @0x111AA0; Reset__8cXObject+0x2D0 @0xCA8F0; Reset__10cXMTObject+0x88 @0xA2A88; PostLoad__8cXObject+0x214 @0xCA514; GetInitTreeVersion+0x80 @0x104710 |
| 1 | Main tree | Reset__8cXObject+0x1C4 @0xCA7E4 (via TreeSim::Reset @0xCA7F8); Cleanup__8cXPerson+0x41C @0x1085EC; Cleanup__8cXObject+0x9C @0xCA16C; GetMainTreeVersion+0x80 @0x104620 |
| 2 | PostLoad tree | PostLoad__8cXObject+0x15C @0xCA45C (run @0xCA480) |
| 3 | Cleanup tree | Cleanup__8cXObject+0x28/0x40 @0xCA0F8/0xCA110; Cleanup__8cXPerson+0x42C @0x1085FC |
| 4 | Queue/idle tree | TryIdleForInput+0x21C/0x28C/0x318 @0x10A2DC/0x10A34C/0x10A3D8; RemoveAction+0x100 @0x10AD30; AddAction+0x793 @0x10B6D4; Cleanup__8cXPerson+0x614/0xB54 @0x1087E4/0x108D24 |
| 5 | Test-intersection tree | TestIntersection+0x4D8/0x520 @0xC7008/0xC7050; ShouldIgnore+0x94 @0x170B44 |
| 6 | Wall-adjacency tree | UpdateWallAdjacency+0x2C/0x47C @0xC415C/0xC45AC; DoCommand+0x13F @0xE5090 |
| 7 | Room-update tree | UpdateRooms+0xE0 @0xE5DE0 |
| 8 | Dynamic-adjacency tree | UpdateDynAdjacency+0x168 @0xA2308; IsDynamic+0x17 @0xA24C8; cXMTObject UserPickup+0x27 @0xA2CB8, UserPlace+0x1BF @0xA31D0 |
| 9 | Place tree | Place__8cXObject+0x594 @0xC7BC4; Place__10cXMTObject+0x1EC @0xA367C; Pickup+0x2F4 @0xC8154; UpdateChairFacing+0x3A4 @0xC7594 |
| 10 | Pickup tree | Pickup__8cXObject+0x3C @0xC7E9C; Pickup__10cXMTObject+0x4C @0xA33DC |
| 11 | UserPlace tree | UserPlace__8cXObject+0x94/0x1C8 @0xC36F4/0xC3828; UserPlace__10cXMTObject+0x3F/0x173 @0xA3050/0xA3184 |
| 12 | UserPickup tree | UserPickup__8cXObject+0x3B/0x16F @0xC348C/0xC35C0; UserPickup__10cXMTObject+0x117/0x24B @0xA2DA8/0xA2EDC |
| 13 | (no static engine fetcher) | script surface only |
| 14 | functional-tree prim arm | TryCallFunctionalTree (below) |
| 15 | Portal/room-partition/routing-slot tree | AddObject+0x1F4 @0xE4BC4 (with li r5,1); DrawIso__8cXPortal+0x15C @0x11B7AC; GetOtherSide+0xF8 @0x11CB88; BuildRoomPartition x7 @0x16EF98..0x170458; TryGotoRoutingSlot+0x788 @0x10EE88 |
| 16..25 | functional-tree prim arms | TryCallFunctionalTree switch |
| 25 | (also) chair-facing tree | UpdateChairFacing+0x38C @0xC757C |
| 26 | Turn/facing tree | Turn+0x98 @0xC6198; Place+0x244 @0xC7874; Pickup+0x2AC @0xC810C; InitRoute+0x128/0x184 @0x10CEF8/0x10CF54; ConstructGoals+0x9D4 @0x16C5E4; ChooseStartingPoint+0x64 @0x16CC34; IsPersonSittingOnChairGoal+0x84 @0x170CC4; TryGotoRoutingSlot+0xA00 @0x10F100 |
| 27 | routing-slot alt / prim arm | TryGotoRoutingSlot+0xA18 @0x10F118 |
| 28, 29 | functional-tree prim arms | TryCallFunctionalTree switch |

The script-visible engine-event prim: `TryCallFunctionalTree(StackElem,
XPrimParam)` @0x0F4190 reads the prim operand (`lha r0, 0(r30)` @0xF4208),
switches (jump table @0xF4214), and maps operand values to EP constants
{14, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26(cond. on an object flag
test @0xF42B8), 27, 28, 29} — arms at 0xF4228..0xF42E0 (`li r28, EP`) —
then `id = ObjFnTable::GetTreeID(EP)` @0xF42FC and a VIRTUAL
GosubObjectTree (slot 60) @0xF4328. **Even here the EP enum is the only
hardcoded constant; the tree id itself stays data-driven.**

By-NAME runs (17 RunTree sites, all name-fed):
* CancelTutorial+0x4C @0xAD97C — literal name string: TOC slot
  (lwz r6,-23248(r2) word 80C2A530) → sec1+0x2530 = pointer 0x45604, name =
  0x45604+197 (`addi r6, r6, 197` word 38C600C5) = **"cancel tutorial"**.
* ShowTutorialInfo+0x30 @0xE2C20, Tut_CheckForEvents+0x8C/0x444
  @0x1565DC/0x156994 — names built at runtime into stack buffers (e.g. bl
  0x138C20 name-builder @0x1565B8).
* TryReach+0x1B0/0x250 @0x10C680/0x10C720 — name = inline char buffer at
  person+279 (`addi r6, r29, 279` word 38DD0117); person state.
* TryAnimate (SAnimator x3, Animator x2) — name pointer into the
  AnimateNewParam struct (e.g. r6 = r31+4304 @0x35FC98); animation data.
* CWinMagicBook (GetKidCharm/GetSpell x4, Init x2) — UI-built name
  structures on the stack.

Non-event id runs: BroadcastMessage(name, i) @0xE3668 loop — walks the
object list, `id = Behavior::GetTreeID(messageName)` per object
(bl 0x3FF40 @0xE3678), RunOneTickTree; TryCreateObject+0xB6C @0xF648C —
same by-name shape; AddAction+0xD7 @0x10B018 — id from the Interaction
record field +32 (`lha r6, 32(r30)`).

**Conclusion (model):** the original's tree-invocation model is fully
declarative. Objects carry an ObjFnTable (TTAB-analog: EP → id, from the
resource or built from 30 OBJD fields); engine events name an EP, never an
id; scripts reference ids/names through prims. The engine itself is a
caller of ZERO concrete BHAVs — a fortiori not of 9761/39200.

## 4. PORT COMPARISON (read-only cites)

The port (FreeSO VM) mirrors the same model: data-driven entry points,
engine-native events hardcode the EP NUMBER (never a tree id):

* `FreeSO/TSOClient/tso.simantics/Entities/VMEntity.cs:494` —
  `ExecuteEntryPoint(0, ...)` //Init; `:495` EP 8 (dynamic multitile);
  `:513` EP 1 //Main — the port's creation/init flow = original EP
  0 + 8 + 1 (cf. §3: Reset/PostLoad fetch EP 0; dyn-adjacency EP 8; Main
  EP 1). `:540` EP 3 + `:541` EP 1 in `Reset`.
* `FreeSO/TSOClient/tso.simantics/Utils/VMTS1Activator.cs:298` —
  `nobj.ExecuteEntryPoint(11, VM.Context, true)` — lot-load placement =
  original EP 11 (UserPlace). The interest mirror lives at
  `VMTS1Activator.cs:317-324` (`VMInterestTraits.IsZeroed` →
  `ApplyInitTraits`), and `Utils/VMTS1ActivatorNew.cs` (the runtime load
  path) has NO interest/EP hook at all — objects flow through
  `VMEntity.Init` (EP 0/8/1).
* Other engine-event EP runs: `VMWorldActivator.cs:143` (EP 11),
  `VMContext.cs:1216-1217/1269-1270` (EP 5, test-intersection — original
  EP 5), `Engine/VMRoutingFrame.cs:369`, `Engine/VMDirectControlFrame.cs:209`
  (EP 5), `Engine/VMThread.cs:319` (EP 1), `Engine/VMThread.cs:864` (EP 4,
  "queue skipped" — original EP 4), `NetPlay/Model/Commands/`:
  VMNetPlaceInventoryCmd.cs:120/133, VMNetBuyObjectCmd.cs:91,
  VMNetMoveObjectCmd.cs:29 (EP 11), VMBlueprintRestoreCmd.cs:63 (EP 2),
  VMNetDeleteObjectCmd.cs:26 (EP 12), VMNetUpgradeCmd.cs:23 (EP 0 reinit),
  `VM.cs:994` (EP 30 — house-unload region; beyond the original's
  BuildFromOldEntries 0..29, from the newer TTAB).
* `FreeSO/TSOClient/tso.simantics/Primitives/VMGenericTS1Call.cs:17-331` —
  the native "generic TS1 call" prim (modes 0..43; e.g. SwapMyAndStackObjects
  Slots :25, AddToFamily :44, DisableBuildBuy :78, ChangeToLotInTemp0 :97,
  SaveSimPersistentData :228). This is the port's native-function surface,
  NOT the original's TryCallFunctionalTree EP-switch; the port has no
  script-prim that runs an arbitrary EP (no Primitives/ handler calls
  ExecuteEntryPoint) — engine events are reached only through the native
  calls above.

**Where a 9761-style conversion COULD have lived:** in the original it
could only ever have been script-driven (no engine caller exists). The
port's corresponding seams are (a) `VMEntity.Init` after the EP-0 run
(VMEntity.cs:494) — the original runs the init tree LAST on the
character-file path (r153 §1.3b) and never converts after it; (b) the
`VMTS1Activator.cs:317-324` zero-guard mirror — deliberately restricted to
zeroed avatars (R150 net) after R153 removed the sentinel hook; (c) a
matter-transfer Reset counterpart, which the port does not have (disclosed
R153 residual #4). Since the engine never calls 9761/39200, a faithful port
must NOT wire them into any load/init path — script callers (if the corpus
sweep finds any) remain the only legitimate invocation path, and the R152
dual-dialect bridge stays display-neutral for raw-scale sims.

## 5. Corrections / notes for prior rounds

1. Decoder notes for re-readers: op-31 `or` prints operands as (rS, rA_dest,
   rB); `or rX, rY, rY` = `mr rX, rY`. Branch names printed by
   ppc_decode.py use a BO-keyed dict that mislabels some conditions — use
   raw encodings: 0x4180=blt, 0x4181=bgt, 0x4182=beq, 0x4082=bne,
   0x4081=ble (consistent with r153's bgt correction).
2. r153-virtual-callers census covered vtable slots +68/+72 only; the
   GosubObjectTree slot is +0x3C (+60) on the same vtable (this round).
3. The r153 "init tree" of a character-file person (§1.3b) is EP 0 fetched
   by the Reset/PostLoad machinery (EP 0 rows in §3), consistent with the
   r153 load orders.

## 6. Residuals (honest)

1. EP names are DERIVED from the engine functions that fetch them; the
   enum's original C++ identifiers are not in the binary (no debug strings
   for the enum). The 0..29 order from BuildFromOldEntries is exact.
2. The jump table inside TryCallFunctionalTree (TOC slot at r2-22964) was
   not symbolically dumped; the arm EP constants {14,16..29} are
   byte-pinned, but the operand→arm mapping (identity vs permuted) is
   inferred from the arm sequence. Does not affect the round verdict.
3. GosubObjectTree slot-60 glue sites on non-object classes are
   class-agnostic coincidences (offset reuse); only the object/VM-path
   sites were typed. Irrelevant to the verdict.
4. Runtime frequencies of the by-name tutorial/animator runs are not
   statically determinable (as before).

---

## R154 RESCAN ERRATUM (ids)

The anchor/const scans in this report targeted the byte-swapped ids
9761/39200 (0x2621/0x9920). Re-run with the TRUE ids 8486/8345
(0x2126/0x2099 — r154-anchor-9761.txt RESCAN section,
r154-engine-constscan-trueids.txt): **the verdict is unchanged and now
correctly founded** — 0 STRONG/WEAK instruction references, only
branch-displacement and unaligned-data coincidences. The structural
finding (the engine hardcodes NO numeric tree id; all 46 tree-runner
sites use the data-driven ObjFnTable::GetTreeID) is id-independent and
stands.
