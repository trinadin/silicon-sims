# R250 — the native HAND-OFF AND SERVING law: winner acceptance → running
# interaction → chair sit/routing → failure semantics; and the check-vs-run
# distinction

All addresses are executable FILE OFFSETS (code section starts at file 0x8e90;
stored data pointers are section-relative; TOC = sec1+0x8000, so `TOC-k` =
sec1+0x8000-k). Container facts per r244; data-section unpack per r104. Game
data (Objects.far chair trees) is quoted as instruction/text facts only.
Nothing outside this directory was written.

**Headline answer to the port's open question: the native does NOT re-run the
check tree at hand-off.** TryGosubFoundAction (0x10fdc0) re-finds the TTAB
entry by the winner number, calls GosubObjectTree (a virtual — the push of the
action tree onto the person's SimAntics stack), records the Interaction with
SetCurrentAction, and returns 3. Zero guard/test invocations. The check tree
runs exactly twice per interaction lifetime in other places: once during
autonomous gathering (TestInteraction, r249) and once when a QUEUED action is
dequeued for serving (TryIdleForInput, below). For the autonomous gosub path
the winner is trusted as-is.

## 0. The execution substrate (everything else hangs off this)

The person's SimAntics VM state, by object offset:

* **TreeSim** embedded at the head of cXPerson; its TreeStack member at
  person+4: `TreeStack::GetStackSize` = *(this+0xc) (helper 0x152560 =
  `lwz r3,4(r3)`), so the live frame count is at **person+0x10**.
  Frames are the real StackElem records pushed by `Push__9TreeStackFPC9StackElemPCs`
  (0x152b40). `GetCurElem__7TreeSimFv` (0x153650) = top frame.
* **gosub bookkeeping stack** (stack "B") at **person+0xab0**: {+0xab4 count,
  +0xab8 frames base}, records of 0xc bytes `{obj+, size-at-push, bool}` —
  pushed by GosubObjectTree, popped by StackJustPopped. Pure bookkeeping; the
  VM never executes these.
* **current action descriptor** = a 0x3c-byte Interaction at **person+0xa08**
  (fields: +0xa0c actor's action word, +0xa10 target, +0xa14 gosub target,
  +0xa30 name CTGString, +0xa3c unique id, +0xa40 flags); **previous action**
  saved copy at person+0xa44..0xa7f (including its own string at +0xa6c and
  flags at +0xa7c).
* **action queue ring** at person+0x820 (8 × 0x3c Interaction slots), head
  **+0xa00**, tail **+0xa04** (GetIndAction 0x10abc0: `slot = 0x820 +
  ((head+i)&7)*0x3c`; count = tail-head).
* **route state**: RoutingSlot copy at person+0xaa0, state count +0xaa4, state
  array base +0xaa8, 0x9c bytes per state (TryGotoRoutingSlot core).
* `TreeSim+0x24` (person+0x24) = error latch; `+0x18` = last transition
  (0xfe = "tree returned false"); `+0x1c` = "last primitive returned false"
  byte; `+0` = per-RunCheckTree instruction budget.

### 0.1 The per-tick driver

`cSimulator::SimulateOneTick` (0x139600) → `TickAllObjects__10cSimulatorFv`
(0x139400). Per object (module+0x20 array, count module+0x1c), using the
module flags word at module+0x2c[index]:

1. flags PPC-bit18 set → `0xe1eb0(module, index, 4, 0)` (event dispatch);
2. flags **bit15 clear → skip** (not tick-enabled);
3. if this is a *filtered* pass (r30 != 0) and flags **bit14 clear → skip**;
4. flags high half > 0 → decrement (multi-tick sleep scheduler) and skip;
5. else **virtual `Simulate(tick)` via vtable+0x28 slot +0x1c** (0x139548) —
   for a person this is `Simulate__8cXPersonFl` (0x10bff0);
6. after Simulate: `GetError__7TreeSimFv` (0x154800) != 0 →
   `HandleError__8cXObjectFv` (0xd2600).

`Simulate__8cXObjectFl` (0xf0d00, the super call at 0x10c01c) calls
**`TreeSim::Simulate` (0x153790)**: the one-node-per-tick executor:

```
loop:
    r = DoNodeAction(this, GetCurElem(this))
    if r == 1: return 1            # primitive in progress — resume next tick
    if r == 2:                     # hard error from the node
        this->0x24 = *(s16*)<failing behavior>
        Error(this, ...)           # vtable+0x28 slot 0xc
        return 1
    # r == 0: node completed (or subtree pushed, or a frame returned) — loop
```

`DoNodeAction__7TreeSimFP9StackElem` (0x1538c0) node cases (donodeaction.txt):

* **sentinel/stack frame** (elem->fnTable == 0): underflow-check, `Pop`,
  then **virtual StackJustPopped** (vtable+0x28 slot +0x10) at 0x153c50;
  continue into the parent frame.
* **primitive node** (node opcode < 0x100): dispatch **`TryElement` via
  vtable+0x28 slot +0x8** (0x153af0). Result law (0x153afc):
  * **0** → elem->nodeId = node->falseBranchByte2; TreeSim+0x18 =
    falseBranch; TreeSim+0x1c = 1; elem+8 = 0 → return 0 ("false" taken);
  * **1** → same with trueBranch byte3, TreeSim+0x1c = 0 → return 0 ("true");
  * **2** → **return 1 — YIELD: the primitive is still in progress; the VM
    stays on this node and re-executes it next tick** (0x153bbc);
  * **3** → continue (the primitive pushed a subtree) → return 0;
  * **-1/other** → return 2 — hard error.
* **subtree node** (opcode >= 0x100): gosub via 0x1540d0, return 0.
* **exit node** (elem nodeId 0xfb..0xff): underflow-check, `Pop`, **virtual
  StackJustPopped**, then follow the PARENT frame's next-node pointer with the
  popped tree's exit code: parent nodeId = parent-node->byte2 if exit code ==
  **0xfe (returned false)** else ->byte3 (0x153c60..0x153d50); TreeSim+0x18 =
  the new node. A missing branch (byte 0xfd) logs message 0x44c; an unresolvable
  case returns 2 (error).

`StackJustPopped__8cXPersonFv` (0x109050, stackjustpopped.txt) — the
interaction-finished hook, called on EVERY frame pop:

```
sizeA = TreeSim::GetStackSize(person)          # person+0x10
if person+0xab4 == 0: return                   # no gosub bookkeeping
top = framesB[countB-1]
if sizeA == top.size-1 or sizeA < top.size:    # the recorded tree finished
    top.obj->0xc6 -= 1                         # gosub refcount--
    for rec in framesB:                        # last bool-flagged record wins
        if rec.flag: person+0xa14 = rec.obj    # currentAction+0xc
    pop framesB (0x115860)
if framesB empty:
    SetCurrentAction(person, fresh empty Interaction)   # action OVER
```

When the action tree's last frame returns, the sentinel is reached, the B
record is popped, and the current action is cleared to the empty Interaction —
the person is idle again and the brain BHAV (re-entered through its
IdleForInput/FindBestAction prims) selects the next action. **No queue
auto-advance here**: the queue is drained by the brain's IdleForInput prim (B,
below), not by StackJustPopped.

## A. TryGosubFoundAction (0x10fdc0) — the accepted winner → running interaction

Instruction-exact flow (trygosubfoundaction.txt; all of it re-verified):

1. `oid = *(s16*)(stack+4)`; if oid == 0 → log error string 0x15 → return 0.
2. `obj = module->objects[oid-1]` (person+0xdc → ObjectModule; count at +0x1c,
   array at +0x20; oid is 1-based). Missing → return 0.
3. `ttable = obj->ObjSelector(+0x11c)->GetTreeTable()` (0x102fe0); null →
   return 0.
4. **Entry search**: linear scan for `entry->+0x18 == *(s16*)(person+0x72)`
   (u32 compare at 0x10fe88). Not found → log error string 0x16 → return 0.
   person+0x72 is the winner action number TryFindBestAction stored (r249).
5. **`GosubObjectTree` — a VIRTUAL call**: `lwz r12, 0x28(r31);
   lwz r12, 0x3c(r12); bl` (0x10fef4) with
   `(person, obj, params=&{0,0,0,0}, treeId = *(s16*)(entry+2), false)`.
   **The tree that RUNS is TTAB entry+2, not entry+0x18** — entry+0x18 is the
   autonomy/ad match key, entry+2 the action tree (TreeTableEntry::DoStream
   order at 0x1551dc: Recon16(+2) first, then Recon16(+0)).
   Failure (0 in the low byte) → **return -1**.
6. Otherwise build `Interaction(person, obj, actionNumber=person+0x72,
   arg4=2)` via ctor 0x9ca90 (interaction-ctors.txt): +0x10 = person+0x72
   (ad number, re-finds the entry later), +0x20 = entry+2 (run tree), +0x1c =
   2 (later mirrored to person attribute slot 33 by SetCurrentAction),
   +0x24 = GetAttenuationValue(entry, isVisitor).
7. `SetStackVars(interaction, &{0,0,0,0})` (0x9ca30) — the four s16 temps at
   Interaction+0x14 are ZERO.
8. `SetCurrentAction(person, interaction)`; destroy the temp string; **return
   3** (the prim's "true" to the brain BHAV).

**There is NO TestInteraction, NO re-run of the entry's test/check tree, and
no serving gate anywhere in 0x10fdc0.** The only guards are identity/lookup
(oid, tree table, entry match, tree exists).

### A.1 GosubObjectTree (vtable+0x3c → 0x109180) and its core (0xefe00)

The cXPerson secondary vtable (stored at person+0x28, ptr at TOC-0x70dc =
sec1+0xf24 → vtable sec1+0x48ee0; person-vtable.txt):

| slot | function |
|---|---|
| +0x08 | TryElement__8cXPerson (primitive dispatch) |
| +0x0c | Error__8cXObjectFs |
| +0x10 | StackJustPopped__8cXPersonFv |
| +0x14 | HandleBreakpoint |
| +0x1c | Simulate__8cXPersonFl |
| +0x3c | **GosubObjectTree__8cXPersonFP8cXObjectPssb** |
| +0x40 | Cleanup__8cXPersonFP8cXObject |

`GosubObjectTree__8cXPersonFP8cXObjectPssb` (0x109180) =
(person, obj, short* params, short treeId, bool flag):

1. call the base impl **0xefe00** (`GosubObjectTree__8cXObjectFP8cXObjectPssb`,
   gosub-gate-efe00.txt); 0 → return 0.
2. `size = TreeSim::GetStackSize(person)`; build temp record
   `{obj, size, flag}`; if flag: `currentAction+0xa14 = obj`.
3. push the record onto stack B (0x115a80 vector insert).
4. `obj->0xc6 += 1` — the target object's gosub reference counter (always).
   Return 1.

Base impl 0xefe00:

1. `ClearIdleStatus__12ObjectModuleFi(module, obj->0xf0)` — the target stops
   being reported as idle.
2. `0x1540d0(person, fnTable = *(objSelector+0xc), params, treeId)` — the
   TreeSim gosub core: resolve `GetTree__8BehaviorFs(fnTable, treeId)` (null →
   Error via slot +0xc → return 0); negative tree ids route through the
   Behavior-file remap (-0x7fff/-0x7ffe = glob/middle file); build the new
   StackElem (tree id, params, frame flags) and `Push__9TreeStackFPC9StackElemPCs`.
3. `GetCurElem()->+4 = obj->0xf0` (objectId stamped on the frame).
   Return 1.

**The tree is only PUSHED here. No node executes during hand-off.** Nodes run
starting with the next `TreeSim::Simulate` tick (section 0.1). Note the
stack-a frame count snapshot stored in the B record is the mechanism
StackJustPopped uses to detect "my tree finished".

### A.2 SetCurrentAction (0x108e60) — recording the current action

Pure state copy (setcurrentaction.txt); it runs NO tree:

```
if currentAction.target (+0xa10) != 0:
    move currentAction (+0xa08..+0xa40) to previousAction (+0xa44..+0xa7c),
    including the CTGString (0x5714d0 operator=, +0xa30 -> +0xa6c)
    savedFlags = flags with PPC-bit26 cleared (rlwinm 0x1b,0x19);
    if person->byte_1c (+0x1c) != 0: savedFlags |= 0x20   # was-interrupted
copy the NEW Interaction (+0..+0x3c) into person+0xa08 (word/lfd/lha/lfs +
CTGString operator= into +0xa30)
if new.target (+0xa10) != 0:
    person+0x5d8 = 0; person+0x5da = 0            # clear route-progress flags
    if Interaction.uniqueId (+0x34) == 0: SetUniqueID__11InteractionFv (0x9c910)
person attr slot 33 (+0x5ce) = (s16)Interaction+0x1c   # ctor arg4 (2 = autonomous gosub)
0x1161a0(person+0xa80)                       # action-string/UI side update
if person+0x61e != 0: person+0x61e = 0; ComputeRect(...)   # dirty rect
person+0x61c = 0; person+0x61a = 0
```

So "current action" = the descriptor the engine and UI read for what the sim
is doing; the executing entity is the tree frame stack. Either can be observed
without the other.

## B. From queue ring to running interaction — who serves

### B.1 AddAction (0x10af40) — queueing may start the action INLINE

Two paths, selected by the TTAB entry flags mask 0x4 (PPC bit28/29 boundary;
`rlwinm. 0x1d,0x1d` at 0x10af7c — the AddAction re-test gate, addaction.txt):

* **mask 0x4 SET — synchronous serve**: build a fresh ObjTestSim(person,
  target, autonomous=0), **run TestInteraction** (0x1062d0) on the incoming
  Interaction; if the passed-flag (Interaction flags PPC bit3) is clear →
  return 0 (dropped). Otherwise: save attr33, set attr33 = Interaction+0x1c,
  **`RunOneTickTree__7TreeSimFP8BehaviorssPs` (0x153e60 → the RunCheckTree
  worker 0x153ec0) with (fnTable = *(target->ObjSelector+0xc), objectId =
  target+0xf0, treeId = Interaction+0x20 = entry+2, params =
  &Interaction+0x14)** — the action tree's first tick executes INSIDE
  AddAction — restore attr33, SetUniqueID, return 1. The Interaction is
  consumed, never queued.
* **mask 0x4 CLEAR — queue**: SetUniqueID; optional name inherit from the
  current action (flags bit25, mask 0x40); priority-insert/rebuild of the
  8-slot ring ordered by Interaction+0x1c (the priority int the constructor
  received); evicted actions (ring overflow) get their **target's exit tree**
  run (`GetTreeID(ObjFnTable, ObjEntryPoint=4)` via 0xfcca0, then 0x153ec0)
  before being dropped. Return 1.

(`RunOneTickTree` 0x153e60 is a 32-byte tail call into 0x153ec0, whose own
function record names it `RunCheckTree__7TreeSimFP8BehaviorPCcPs`,
runonetick-worker.txt. It pushes a sentinel StackElem {treeId = -1}, gosubs
the requested tree via 0x1540d0, then loops DoNodeAction until the sentinel is
the current frame; every node result == 1/2 just logs message 0x44f and loops
(synchronous serving never yields); on sentinel: `Pop` + **virtual
StackJustPopped**, then while `TreeSim+0x18 == 0xfe` (a false-return
transition) fire the virtual Error; restore globals; return 1 if a
false-return transition happened else 0.)

### B.2 TryIdleForInput (prim 0x11, 0x10a0c0) — the queue DRAIN

The brain BHAV idles in this primitive; each re-entry serves at most one
queued action (tryidleforinput.txt):

1. `ShouldInterrupt__8cXPersonFv` (0x10aa00); param bounds check (error 8).
2. With no interrupt and `params[paramIdx] == 0`: set obj+0x5a bits and
   return 1 — keep idling.
3. Interrupt path: if `XPrimParam+2 == 0` → return 1.
4. **ring empty (head == tail, person+0xa00 == +0xa04) → return 1**.
5. `XPrimParam+8 == 1` and ring[head].flags bit30 (mask 2) clear → return 1
   (not yet servable).
6. **DEQUEUE**: copy ring[(head)&7] to a local Interaction; `head += 1`
   (raw increment at 0x10a26c; masking is at access time).
7. Local.target (+8 of the copy) == 0 → dtors, return 2.
8. **RE-TEST**: fresh ObjTestSim(person, target, 0) + TestInteraction on the
   copy. The Interaction-flags bit3 "passed" bit decides:
   * **failed** (or tree id Interaction+0x20 == 0): run the **target's exit
     tree** `GetTreeID(fn,4)` + 0x153ec0(person, fn+0xc, objectId, 0), dtors,
     **return 2 — the action is DROPPED. No re-queue, no idem, no failure
     counter.**
   * **passed**: **`GosubObjectTree` via vtable+0x3c** (0x10a394) with
     (target, params = &local+0x14 (the four s16 temps), treeId = local+0x20,
     bool=false).
     * gosub failed → target exit tree, return 2 (dropped);
     * gosub ok → **SetCurrentAction(person, &local)** (0x10a428),
       `params[paramIdx] = 0`, `StackElem+8 = 1` (elem scratch = "serving"),
       `GetCurElem+4 = target id`, obj+0x5a |= 0x40, dtors, **return 3**.

So the serving contract for a queued action: pop head → re-test → gosub →
record current → let the VM tick it. Exactly ONE guard re-run exists in the
whole serving path, and it is HERE (dequeued actions only). For the
autonomous winner (A) there is none.

### B.3 Scheduling

Everything runs on the object-tick thread: TickAllObjects → Simulate →
TreeSim::Simulate, one primitive attempt per tick, forever-yielding primitives
(route, animate) re-entered each tick. AddAction's sync path and the
RunCheckTree-based side trees (test trees, exit trees, cleanup trees) run
inline inside the calling primitive — they must not block (and the worker
loops rather than yields if they try).

## C. The sit/routing serving path for a chair interaction

### C.1 The chair's own trees (Objects.far member `ChairsLR1Tile.iff`)

chair-sit-tree.txt has the full dumps. Facts:

* OBJDs ×4 ("Chair - Living Room - Cheap/Moderate/Expensive 1/2");
  TTAB id 33024 ("Chair - Living Room - Cheap 1 tree table"): on-disk header
  `{u16 version=1, u16 count=1}` — **ONE advertised entry ("Sit")**, payload
  in the TTAB version-9 ReconCmprInt compressed stream (UNRESOLVED, §UNRESOLVED).
* BHAVs: `main` (16), `Sit` (272), `Sit in Chair test` (528), `sit function`
  (1040), `sit core` (784), `stand function` (1552), `snap out of chair`
  (2832), `init cheap/common/moderate/expensive`, `CT - sit comfort` (1296),
  `inc comfort` (3344). Instruction format (sig 0x8002): header 12 bytes
  {u16 ver, u16 count, u8 type, u8 args, u16 locals, u16 flags, u16 pad};
  instructions 12 bytes {u16 opcode LE, u8 true-ptr, u8 false-ptr, 8-byte
  operand} (SimsLib BHAV.cs layout; opcode >= 0x100 = gosub tree#).
* **`sit core` (784) is the sit action tree.** Decoded primitive sequence:
  ```
  [0]  prim 0x2d GoToRoutingSlot {01 00 02 00 ...}   T->[1]  F->255 (RETURN FALSE)
  [1]  Expression                                    T->[25] F->255
  [2-5]  Expressions on StackObject attr 1/4/16/0x40 (sit-style selectors)
  [7-10] prim 0x2c AnimateSim {1|3|2|4, flags 0x4000}     # sit-DOWN animation,
                                                            wait-for-completion
  [12] prim 0x2c AnimateSim {0}                           # seated pose/idle
  [13] prim 0x2e Snap {06 00 03 00 ...}                   # snap into the chair
  [15-23] PlaySound / TestObjectType (chair subtype GUIDs)
  [24] prim 0x2e Snap {00 00 03 00 04 00}                 # snap variant
  [25-33] person-data writes (comfort/posture bookkeeping)
  [34] GOSUB 398 (semi-global space)
  [35-39] more person-data writes; tree return
  ```
  The FALSE exit of GoToRoutingSlot (instruction 0 false-ptr 255) IS the
  routing-failure path: the sit tree returns false, which becomes the 0xfe
  transition (section D).
* `sit function` (1040) gosubs 4099 then `NotifyOutOfIdle` (0x31);
  `Sit` (272) gosubs 4100; `main` (16) gosubs 280 and 4109; `stand function`
  (1552) gosubs 370 and **4107**. Ids 4096..8191 are the SEMI-GLOBAL tree
  space (TS1 SimAntics addressing); the chair's semi-global file binding is
  per-OBJD GUID (file identity: UNRESOLVED, below).
* The chair's SLOT chunk (id 32768, 652 bytes at member offset 334260) is the
  routing-slot geometry source; the engine materializes it as the object's
  RoutingSlot array consumed by prim 0x2d (below).

### C.2 Prim 0x2d GoToRoutingSlot — engine side

**Wrapper** `TryGotoRoutingSlot__8cXPersonFP9StackElemP10XPrimParam`
(0x10f760, gotoroutingslot-prim.txt):

* if `XPrimParam+8 != 0` → tail-call the core with slot = NULL (the core then
  discovers/uses its own state; used for resume entries);
* else resolve the RoutingSlot:
  * mode `XPrimParam+2 == 0`: slot index = `elem.params[param0]`
    (StackElem param selected by XPrimParam+0);
  * mode 1: slot index = XPrimParam+0 literal;
  * mode 2: MODULE-level slot (module+0xa8 array, count module+0xa4);
  * bounds errors log message 0x25 and return 0 (prim FALSE).
  * object slots: array at **obj+0x144**, count at **obj+0x140**, stride
    **0x3c** (0x10f884);
* flag fixups on slot+0x1c: keep the masked bits (`rlwinm 0x12,0x10`), and if
  XPrimParam byte4 bit0 == 0 set flag 0x4000;
* tail-call the **core** `TryGotoRoutingSlot__8cXPersonFP9StackElemP11RoutingSlot`
  (0x10e700).

**Core** (0x10e700, 4112 bytes; gotoroutingslot-core.txt) — a cross-tick
STATE MACHINE whose continuation marker lives in `StackElem+8` (the primitive
owns this slot; TryIdleForInput uses the same mechanism for its own scratch):

* first entry (StackElem+8 == 0): object lookup from elem param (1-based,
  module array), `IsInWorld` gate (0xc9760) — off-world → return 0;
  construct `XRoute(person, obj, slot)` on the stack (ctor 0x170f90 at
  0x10e7a8), apply posture/engagement flag fixups to the slot copy
  (`person+0x614` bit0 → route slot flags |= 0x800; existing state + slot
  flag bit14 checks), then commit a 0x9c-byte route state into the person's
  state array (person+0xaa4 count, +0xaa8 base; stride 0x9c at 0x10e818) and
  init it (0x1164d0).
* **`InitRoute__8cXPersonFP6XRoute` (0x10cdd0, called at 0x10f18c)** builds
  the walk: `FindPath__6XRouteFR8TileList` (0x16d190 — A* over the lot tile
  graph; ConstructGoals 0x16bc10, BuildGoalList 0x16e810,
  ChooseStartingPoint 0x16cbb0, SetDirectionForGoalSearch 0x16cde0),
  plus `MoveOutOfWay`/`AskOthersToMove__8cXPersonFP6XRoute` (0x10e210,
  called at 0x10ef94) when another sim blocks a waypoint, and portal
  machinery for cross-lot walks (FindBestPortal 0x11be10,
  InitPortalRoute 0x11c650, BeginningPortalTree 0x11b9b0,
  FailedPortalTree 0x11bcc0).
* each subsequent tick: advance the phase (`ShouldInterrupt` polled at
  0x10edc4 — user input aborts the route), recompute facing from the waypoint
  list at person+0xa98 (8-byte waypoints; the |dx| vs |dy| doubling-compare
  direction law at 0x10ea34-0x10eb18 → direction 2..7 stored at person+0x4c,
  normalized `& 7`, then applied via vtable slot +0x7c), step the walk, and
  when the route completes clear the "blocked-by" handshake
  (states whose +0x80/+0x90 reference this person/action: 0x10ec0c-0x10ec94;
  the symmetric `AbortActionById` call 0x10ac30 at 0x10eca8 — section D).
* **failure branch**: route impossible/blocked-for-good →
  `Backtrace__8cXObjectFv` + `EndFollow__9SAnimatorFv` animation cleanup
  (0x10eb78-0x10eb90); if `person attr0 (+0x58c) == 0` set route flag
  state+0x88 = 1 and **failure code 7** (0x10eb5c-0x10eb70 — matches
  r249-cfg §3); `person+0x5da = 1` when this is the last route state and
  +0x5d8 == 0; notify dependent routes; **abort the current action via
  0x10ac30** when state+0x80 != 0; pop the state; the prim returns 0 (FALSE)
  → `sit core` instruction 0 takes its FALSE exit → the sit tree returns
  false → the 0xfe transition (section D).
* success: the prim returns 1 (TRUE) the tick the sim arrives at the slot;
  `sit core` proceeds to AnimateSim/Snap as above.

Portable summary of "winner accepted → sim is sitting":

```
accept (r249 law) → TryGosubFoundAction:
    find (object, TTAB entry) by winner number          [no checks]
    push entry+2 tree frame onto the person's VM        [GosubObjectTree]
    record Interaction as current action                [SetCurrentAction]
per tick (TickAllObjects → TreeSim::Simulate):
    run the sit tree one primitive-attempt per tick:
    GoToRoutingSlot: resolve slot (obj+0x144[idx*0x3c]) → XRoute →
        FindPath (A*) → walk waypoint-by-waypoint, facing between waypoints,
        AskOthersToMove when blocked; yields (return 2) every tick in progress
    on arrival: AnimateSim (sit-down anim, wait flag), Snap into the slot,
        person-data writes (posture/comfort), return true
    tree returns → sentinel pop → StackJustPopped → current action cleared
```

## D. Failure semantics

* **Action tree returns false** (any node's sub-tree returns false — exit
  code 0xfe in TreeSim+0x18): the frame pops; StackJustPopped pops the B
  record and (when the stack empties) clears the current action to the empty
  Interaction. RunCheckTree additionally fires the virtual **Error** while the
  0xfe transition latches (0x154020-0x15404c). **Error__8cXObjectFs (0xd2d60)**
  = set `obj+0x114` flags bit23 (mask 0x100, clear-then-set idiom at 0xd2d7c)
  and dispatch `0xe1eb0(module, objectId, 4, 1)` (event code 4). The `code`
  argument is discarded. No counter, no idem.
* **Routing impossible** (no free slot / no path): GoToRoutingSlot returns 0;
  the sit tree takes its false exit and returns false → the above. The core
  additionally runs the animation cleanup, sets the failure code (7) and
  route flag, notifies dependent routes, and may cancel the pending action
  via 0x10ac30.
* **Queue-level failure**: dequeued action fails its re-test (TryIdleForInput
  step 8) → target exit tree + **silent drop** (return 2). AddAction's sync
  path fails TestInteraction → return 0 (caller decides; callers generally
  abandon). Ring overflow → evicted actions run their target exit tree, then
  drop.
* **0x10ac30 (unnamed `CancelActionById`)** — abort-10ac30.txt: given
  (person, actionUniqueId): if the CURRENT action's unique id (+0xa3c)
  matches: attr33 = 0, current flags bit23 |= 0x100, and unless
  `ShouldInterrupt` the engine queues the special interrupt Interaction
  `Interaction(person, person, 0x21, 0x32)` via AddAction (tree arg 0x21=33,
  0x32=50). Otherwise scan the ring; queued matches: run their TARGET's exit
  tree (entry 4) on the target's own TreeSim and drop them; rebuild ring.
* **Cleanup__8cXPersonFP8cXObject (0x1081d0)** — the heavy interrupt (object
  deleted, sim leaving, etc.): `TreeSim::Reset(person, fnTable, entry 1)`
  (0x154320) to unwind the VM; run the person's OWN exit tree
  `GetTreeID(fn, 3)` (0x1085f8) via RunCheckTree; **attr0 (posture, +0x58c) =
  0** and clears +0x5dc/+0x602/+0x8e/+0x5c; SetCurrentAction(empty);
  purge ring entries targeting the deleted object (running each target's exit
  tree, 0x108d48); clear route-state handshakes referencing the object; clear
  obj+0x114 flag bits. (cleanup-exit-tree.txt, cleanup-delobj-current.txt,
  cleanup-ring-purge.txt.)

So: **native failure = (optionally the target's exit tree) + silent drop +
Error-flag/event on tree-level false. No retry, no re-gather (the brain's
next FindBestAction tick IS the re-gather), no failure counters.**

## E. The check-vs-run distinction

Exactly three tree-execution contexts exist, all sharing one VM:

1. **Test/check trees** — the SAME TTAB action tree executed in test mode:
   `TestInteraction` (0x1062d0, r249) runs the target's tree via RunCheckTree
   (0x153ec0) with the actor's temps copied in, result → person+0xae, and
   never mutates world state meaningfully because the primitives run in
   test/eval mode (the person "idles for input" while evaluating). Runs:
   (a) per candidate during autonomous gathering; (b) per queued action at
   dequeue (B.2); (c) AddAction's sync path. Also the "sit condition"
   checks like `Sit in Chair test` (528) are ordinary expressions — they run
   inside the action tree too, re-validating at run time *in tree logic*, not
   by engine mandate.
2. **Action trees** — pushed by GosubObjectTree at hand-off (A) or serving
   (B.2), executed tick-wise by TreeSim::Simulate. The engine runs NO guard
   between accept and execution.
3. **Side trees** — exit trees (ObjFnTable entry 4: run on drop/evict/abort),
   the person's cleanup tree (entry 3), main (entry 1 for Reset) — run
   synchronously via RunCheckTree inside engine decisions.

TTAB entry field roles (pinned via consumers): entry+0 = test/GUI action
number (0 → TestInteraction auto-passes, 0x1064f8); entry+2 = the action tree
id the engine gosubs at hand-off and serving; entry+0x14 = flags (bit masks:
0x4 = AddAction re-test gate; 0x2/0x8/0x40 = queue/name/lane flags);
entry+0x18 = the autonomy/ad match key (winner number, person+0x72);
entry+0x1c = queue priority; entry+0xc/+0x10 = attenuation enum/value;
entry+0x52.. ad rows; entry name string (v7+).

## Portable pseudocode (native hand-off + serving law)

```
# ---- hand-off (autonomous winner; brain BHAV calls GosubFoundAction) ----
def gosub_found_action(person, stack):
    oid = stack[4]
    if oid == 0: return 0
    obj = module.objects[oid-1];  if obj is None: return 0
    tt  = obj.selector.tree_table; if tt is None: return 0
    entry = first e in tt.entries where e.ad_number == person.action_number
    if entry is None: return 0
    # THE WHOLE GUARD. No TestInteraction. No re-check.
    if not gosub_object_tree(person, obj, params=[0,0,0,0],
                             tree_id=e.action_number, flag=False):
        return -1
    inter = Interaction(person, obj, action_number=person.action_number,
                        prio=2, tree_id=e.action_number, temps=[0,0,0,0])
    set_current_action(person, inter)
    return 3

def gosub_object_tree(person, obj, params, tree_id, flag):
    module.clear_idle_status(obj.id)
    fn  = obj.selector.fn_table
    tree = fn.get(tree_id)
    if tree is None: obj.error(); return False
    record = (obj, size=len(person.vm_frames), flag)
    if flag: person.current_action.gosub_target = obj
    person.vm_frames.push(make_frame(tree, params))
    person.gosub_stack.push(record)        # 0xc-byte bookkeeping
    obj.gosub_refs += 1                    # obj+0xc6
    return True

def set_current_action(person, inter):
    if person.current_action.target is not None:
        person.previous_action = copy(person.current_action)
        if person.byte_1c: person.previous_action.flags |= 0x20
    person.current_action = copy(inter)
    if person.current_action.target is not None:
        person.f5d8 = 0; person.f5da = 0
        if person.current_action.uid == 0: person.current_action.uid = new_uid()
    person.attr[33] = inter.prio_s16

# ---- serving (queue drain; brain idles in IdleForInput) ----
def idle_for_input(person, elem, prm):
    if person.queue.head == person.queue.tail: return 1        # keep idling
    inter = person.queue[person.queue.head & 7]
    person.queue.head += 1                                     # raw ++
    if inter.target is None: return 2
    if not test_interaction(person, inter):                    # THE re-test
        run_exit_tree(inter.target, entry_point=4); return 2   # silent drop
    if inter.tree_id == 0:
        run_exit_tree(inter.target, 4); return 2
    if not gosub_object_tree(person, inter.target, inter.temps,
                             inter.tree_id, flag=False):
        run_exit_tree(inter.target, 4); return 2
    set_current_action(person, inter)
    elem.params[prm.idx] = 0; elem.scratch8 = 1
    return 3

# ---- per-tick VM ----
def tree_sim_tick(person):                       # one primitive per tick
    r = do_node_action(person, person.vm_frames.top)
    if r == CONTINUE: return                     # next node this tick? loop
    if r == YIELD:   return                      # prim unfinished: next tick
    if r == ERROR:   person.error_latch = True; fire_error(person)

# ---- chair sit tree (Objects.far ChairsLR1Tile.iff, BHAV 784 'sit core') ----
# [0] GoToRoutingSlot(slot=chair.slots[...])     false -> tree returns FALSE
# [7..12] AnimateSim(sit-down anims, wait)  [13/24] Snap into slot
# [25..33] person-data writes; return
```

## What is safe to base a C# reimplementation on

Instruction-proven and fixture-backed (verify.py: 88 code pins, 118 checks
PASS):

* TryGosubFoundAction: entry re-found by entry+0x18 == person+0x72; the run
  tree is **entry+2**; hand-off = GosubObjectTree (virtual vtable+0x3c) +
  SetCurrentAction; returns 3/-1/0; **zero guard re-runs**.
* GosubObjectTree pushes the frame, stamps objectId on the frame (+4), pushes
  the 0xc-byte B record, increments obj+0xc6, clears the target's idle status;
  base impl 0xefe00; gosub core 0x1540d0 (GetTree + Push).
* SetCurrentAction: previous-action save (+0xa44 block, string move, bit26
  0x20 interrupted marker gated on person+0x1c), copy to +0xa08, attr33 =
  Interaction+0x1c, +0x5d8/+0x5da cleared, SetUniqueID when +0x34 == 0.
* StackJustPopped: pop-B condition `sizeA == top.size-1 or sizeA < top.size`,
  obj+0xc6--, last-flagged-frame object → currentAction+0xc, empty-B →
  SetCurrentAction(empty).
* TickAllObjects gate (bit15 enabled, bit14 filtered-pass, timer decrement)
  → virtual Simulate (slot +0x1c of the +0x28 vtable) → TreeSim::Simulate
  (0x153790) one-node law; DoNodeAction result map
  {0 false-branch, 1 true-branch, 2 yield, 3 gosub, -1 error} and exit-node
  0xfe=false / else=true branch-follow + StackJustPopped on every pop.
* TryIdleForInput drain: head==tail → 1; pop, raw head++, TestInteraction
  re-test; fail → target exit tree (entry 4) + drop (2); pass → GosubObjectTree
  + SetCurrentAction (3); StackElem+8 = 1; params[paramIdx] = 0.
* AddAction: mask-4 sync serve (TestInteraction + RunOneTickTree first tick
  inline) vs priority ring insert; overflow evictions run target exit trees.
* Error = obj+0x114 bit23 + event(4); no counters.
* 0x10ac30 CancelActionById law incl. the interrupt Interaction (0x21, 0x32)
  re-queued via AddAction.
* Routing: prim 0x2d wrapper slot addressing (obj+0x144/0x140 stride 0x3c;
  module+0xa8/0xa4), XRoute ctor, route-state machine (StackElem+8, 0x9c-byte
  states at person+0xaa4/+0xaa8), ShouldInterrupt polling, AskOthersToMove,
  failure code 7 + route flag +0x88 + 0x10ac30 abort, animation cleanup.
* Cleanup: VM Reset (entry 1), person exit tree (entry 3), attr0 posture
  cleared, SetCurrentAction(empty), ring purge with target exit trees.
* TTAB on-disk: header {u16 version, u16 count}; per-entry DoStream order
  {+2 action, +0 test, +4, +0x14 flags, +0x18 adNumber, +0xc attenCode,
  +0x10 attenValue, +0x1c, +0x20, ad rows 6×s16, v7+ name string}; v9
  payloads use ReconCmprInt compression.

## Portable hand-off contract (the answer)

**The native does NOT re-run the guard tree at hand-off.** For an accepted
autonomous winner the port must, exactly:

1. resolve the winner object (1-based id from stack+4) and its TTAB;
2. find the entry whose ad/action number (entry+0x18) equals the stored
   winner number (person+0x72 analogue);
3. gosub the entry's action tree (entry+2) onto the sim's VM with the four
   params zeroed, stamping the target object id on the frame — **without
   executing any check**;
4. record the Interaction (actor, target, ad number, tree id, prio=2) as the
   current action, clearing the route-progress flags and assigning a unique
   id;
5. let the per-tick VM execute the tree (one primitive attempt per tick;
   routing/animation prims yield until done);
6. when the tree returns, clear the current action (empty Interaction) —
   failure means exactly "returned false" (optionally the target's exit tree
   + Error flag), never a re-run of the guard.

For QUEUED actions the single re-run of the check tree happens at dequeue
(TryIdleForInput), and its failure path is: run the target's exit tree, drop
silently. A chair-sit winner therefore needs only: enqueue-or-gosub with tree
= entry+2, then the tree's own `GoToRoutingSlot → AnimateSim → Snap` sequence
serves it; the port's synchronous re-run of the check/serving tree before
enqueue has no native counterpart on this path and must not gate the enqueue.

## UNRESOLVED (with reason)

* **TTAB v9 compressed payload**: `TreeTable::Load` (0x1558d0) + ReconBuffer
  `ReconCmprInt` (0x11f080) bit-level scheme compression (Scheme via
  TOC-0x5868) was not decoded, so the cheap chair's literal entry values
  (action number 272 vs a semi-global entry tree; weight; ad rows) are not
  read out of the file. The entry layout is pinned from DoStream; the
  semi-global binding (OBJD GUID → file) also not chased.
* **Chair semi-global file identity**: trees 4107/4109/4099/4100/398/370
  referenced by the chair file live in the 4096..8191 (semi-global) space;
  no Global.far member carries chunk ids 4099-4109 (SofaGlobals uses 32-step
  ids), so the chair base semiglobal file was not located in the mount.
* **TryGotoRoutingSlot core internals** (0x10e700): phase table (TOC-0x5928),
  all 0x9c-byte state fields, and the exact FindPath termination handshake
  are outlined (section C.2), not field-exact; the walk/portable porting
  contract for FindPath internals (A* heuristic, waypoint format at
  person+0xa98) is deferred — the observable law (yield each tick, face
  between waypoints, AskOthersToMove when blocked, fail = return 0) is what
  the hand-off contract needs.
* **person+0x5d8/+0x5da** exact name/role (route progress flags written by
  the routing core, cleared by SetCurrentAction; r249-cfg noted slot 38/39
  writers) — behavior pinned, name not.
* **TreeSim+0x24 error latch reset site** (HandleError 0xd2600 clears it) —
  not fully traced.
* **0x1161a0** (called from SetCurrentAction and 0x110808; takes
  person+0xa80) — action-string side update; internals not decoded.

## Files

* `verify.py` — SHA-256 + 88 NEW code pins + data/vtable fixtures + 7 law
  fixtures; PASS from any cwd; ruff clean; writes only `verified-state.json`.
* `verified-state.json` — its output.
* `trygosubfoundaction.txt` (from r249, base evidence), `setcurrentaction.txt`,
  `gosubobjecttree.txt`, `gosub-gate-efe00.txt`, `interaction-ctors.txt`,
  `stackjustpopped.txt` — hand-off + recording.
* `runonetick-worker.txt` (RunCheckTree), `treesim-tick-153790.txt`
  (TreeSim::Simulate), `donodeaction.txt`, `error-xobject.txt`,
  `tickallobjects.txt`, `person-vtable.txt` — execution substrate.
* `tryidleforinput.txt` (from r249), `addaction.txt` (from r249),
  `shouldinterrupt.txt`, `abort-10ac30.txt` — queue serving + abort.
* `gotoroutingslot-prim.txt`, `gotoroutingslot-core.txt` — routing primitive.
* `cleanup-exit-tree.txt`, `cleanup-delobj-current.txt`,
  `cleanup-ring-purge.txt` — failure/interrupt paths.
* `chair-sit-tree.txt` — the chair TTAB + BHAV instruction dumps.
* `treetable-load.txt`, `treetable-recon.txt`, `ttabentry-dostream.txt` —
  TTAB resource law.
