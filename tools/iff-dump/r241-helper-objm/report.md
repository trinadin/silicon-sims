# R241 helper — original OBJM user-event (opcode35) continuation layout

Assignment B: recover how the ORIGINAL game's saved OBJM object frames store the
prepare/dispatch/complete continuation of opcode35 (picture-in-picture / user
event), compare with the maintained fork's import path, and propose (not apply)
a minimal import fix.

Original executable: `game-data/The Sims/The Sims Complete`, SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`. All
original addresses below are PEF **file offsets** (r141 symbol-index entries
minus the historical +2; re-verified by decoding known bytes — e.g. TryUserEvent
index `000fac72` → code at file `0xfac70`, matching r240-pip/primitive.txt).
No proprietary binaries or images are stored in this directory; only derived
scripts, logs, and JSON.

---

## 1. Concrete findings (ranked by confidence and user impact)

### F1 (VERIFIED) — the original OBJM serializes the opcode35 phase per stack frame

The original `TreeStack::ReconStream` (file `0x153080`, 924 bytes) serializes,
per frame, an s32 at frame offset **+8** (on-disk position: after params/locals,
before the code-owner short). The original `TryUserEvent` (opcode35,
file `0xfac70`, matches r240-pip/primitive.txt) uses that same word,
`StackElem+8`, as its phase latch:

- `0xfac94`: `lwz r0, 8(r4)` — read phase (r4 = StackElem).
- `0xfac8c..0xfacc0`: phase 0 → `SetGlobal(11, 1)`, `stw 1, 8(r26)`, return 2 (yield).
- `0xfaccc..0xfad0c`: phase 1 → `stw 2, 8(r26)` (dispatch), resolve StackObject
  from `StackElem+4` against the ObjectModule object table
  (`r25->0xdc->0x1c count / 0x20 array`), error 23 if missing, then
  `DoPictureInPicture`, return 2 (yield).
- `0xfaf60`: any other phase → return 1 (true).

Therefore the saved s32 at frame+8 IS the saved continuation (0/1/2 for
opcode35). The maintained OBJM reader already parses it
(`OBJMStackFrame.PrimitiveState`), but the importer drops it (F3).

Empirical proof from real saves: an independent parser (fixture, section 4)
decoded all 648 OBJM chunks in `UserData*/Houses/House*.iff` — 283,433
objects, 547,889 frames, zero errors, zero overshoots past any object's
ReconMark. It found **32 frames with `primitive_state` 1 or 2** (8 phase-1,
24 phase-2; 4 unique frames duplicated across the UserData copies of the same
neighborhood). One of them is a literally verified PIP continuation:

```
UserData*/Houses/House56.iff obj 292: stack_object 333, tree 4110, node 1,
  primitive_state 2, code_owner_objtype 146
→ (BHAV 4110, instruction 1) is a catalogued opcode35 site in
  GameData/Objects/Objects.far!Social2.iff   (r240-pip/corpus.json)
```

i.e. a house saved while a picture-in-picture user event was mid-continuation.
The other phase-1/2 frames sit at (tree 281, node 4), (tree 4355, node 11),
(tree 4105, node 0) — no opcode35 site there, which is expected: frame+8 is a
GENERIC per-primitive state slot (also observed values 3 and 15 from other
yielding primitives). Any import mapping must therefore be gated on the
current instruction actually being opcode 35 (see section 5).

### F2 (VERIFIED) — full original frame layout and version gates

Recovered from `TreeStack::ReconStream` disassembly (section 3) and
cross-validated by accessors and by a whole-corpus parse. Stream version =
the OBJM header version (u32 at body offset 4; observed corpus values
51/56/62/63/66/68/69/72/73 — every real save is ≥ 25, so only the "new" layout
ships in practice):

On-disk per frame (all values through the ReconCmprInt bit reader):

| # | type | frame offset | meaning |
|---|------|--------------|---------|
| 1 | s16 | +4 | stack object ID (skipped only when version == 25) |
| 2 | s16 | +0 | tree ID (bit15 = runtime flag, not persisted; `GetTreeID` `0x1524b0` masks it) |
| 3 | s16 | +2 | instruction pointer / node (`TreeSim::DoNodeAction` `0x1538c0` range-checks < 0xFB) |
| 4 | u8 | +6 | locals count |
| 5 | u8 | +7 | params count (`GetLocals` `0x153000`: locals = frame + 2×count(+7) + 0x10) |
| 6 | s16×n | +0x10 | params |
| 7 | s16×m | | locals |
| 8 | s32 | +8 | **primitive state — the opcode35 phase latch** |
| 9 | s16 | +0xc (low half) | code-owner OBJT type index (BehaviorFinder virtual; runtime frame+0xc = resolved code-owner object, used by TryUserEvent at `0xfae64` for STR#305 caption lookup) |

A second s32 precedes the frames in the stream (stack array byte size; decoded
**512 for all 283,433 stacks** — the "512 usually" note in the maintained
OBJM.cs).

The maintained `OBJMStackFrame(IffFieldEncode)` reader parses exactly this
order with exactly these widths — it is CORRECT. The gap is purely in the
importer (F3).

### F3 (VERIFIED) — the maintained importer drops the phase

`VMTS1ActivatorNew.ConvertThread` (section 2) builds `VMStackFrameMarshal`s
from OBJM frames and never copies `frame.PrimitiveState`. `TS1UserEventPhase`
defaults to 0, so every imported opcode35 continuation re-executes from the
prepare step (or, where the surrounding stack is rejected by
`IsRestorableStack`, the whole continuation is dropped and the object resets).
Native saves keep the phase (marshal version 40), so the gap is import-only.
This matches r240-pip/primitive-law.md's stated residual: "imported original
object stack phase is not separately reconstructed here."

### F4 (VERIFIED) — OBJM container framing around the frames

Body of an OBJM chunk (after the 64-byte IFF chunk header):

```
[0..4)   pad u32 (0)
[4..8)   version u32 LE          (51..73 observed; == stream version)
[8..12)  'MjbO' magic (LE u32 0x4f626a4d)
[12]     compression byte == 1   (ReconCmprInt bit packing from here on)
[13..]   id/type compressed s16 pairs until id == 0 (terminator = present bit 0)
[byte-align] then per object i:
    raw s32 LE  "skip"  -> mark_i = 12 + skip = end of object i's bits
    object i compressed data (the OBJMInstance fields of OBJM.cs, incl. stack)
    [at mark_i the next skip int / final mark follows]
after the last object:
    final mark raw s32 LE == 0 (all 648 files)
    tutorial owner compressed s16 (nonzero in 24 files)
    (observed: one further trailing byte in most files, typically 0xa3 —
     alignment/padding detail, does not affect frame parsing)
```

The r239 "ReconMark trailer" and the skip pointers are accounted for: the
per-object int IS the mark (self/next chain: int@mark_i → mark_{i+1}, verified
chain-wise; final mark int = 0). Existing skip pointers land on those marks
(exactly as r239-tutorial established).

### F5 (VERIFIED) — compression scheme tables

ReconCmprInt (`0x11f080`): 1 present bit (0 → value 0), 2-bit selector, then
the value in `width` bits, sign-extended via `or` with `0xFFFFFFFF << (width-1)`
(Precision ctor `0x11f7f0` proves the mask; initializer `0x11f570..0x11f7a4`
builds the tables). Schemes (TOC-relative globals, r2 = TOC):

- Recon16 (`0x11e9c0`, global at TOC-0x5868): widths **[5, 8, 13, 16]**
- Recon32 (`0x11e7e0`, global at TOC-0x5864): widths **[6, 11, 21, 32]**
- Recon8 (`0x11ebe0`, global at TOC-0x586c): widths **[2, 4, 6, 8]**
- (fourth table TOC-0x5870 = [2, 13, 21, 32], not used by these readers)

Independently corroborated byte-for-byte by the maintained
`IffFieldEncode.cs` (`widths = {5,8,13,16}`, `widths32 = {6,11,21,32}`,
`widthsByte = {2,4,6,8}`, same sign-extension idiom) — two independent
derivations agree.

---

## 2. Maintained import path (exact source references)

All paths under `/Users/nathannoom/Developer/Games/The Sims/simitone-fork/FreeSO/`:

- `TSOClient/tso.files/Formats/IFF/Chunks/OBJM.cs`
  - `OBJM.Read` 670-731: header, id table, skip ints, final mark, tutorial owner.
  - `OBJMInstance` ctor 484-645: object prefix, `stackFrames`/`stackFlags` 538-539.
  - `OBJMStackFrame(IffFieldEncode)` 355-379: stack object, tree, node, counts,
    params, locals, **`PrimitiveState = iop.ReadInt32()` (line 377)**,
    `CodeOwnerObjType` (line 378). Reader-side layout matches the original exactly.
- `TSOClient/tso.simantics/Utils/VMTS1ActivatorNew.cs`
  - `ConvertThread` 240-271: builds `VMStackFrameMarshal` per OBJM frame
    (StackObject/RoutineID/InstructionPointer/Args/Locals at 254-270) —
    **`PrimitiveState` is not mapped; `TS1UserEventPhase` stays 0.**
  - `GetRoutine(frame)` 73-87 (resolves RoutineID ≥8192 semiglobal, ≥4096
    house resource, else globals) and `routine.Instructions[ip]` usage at 352-353.
  - `IsRestorableStack` 91-…, applied at 280-301 (drops non-restorable stacks).
- `TSOClient/tso.simantics/Marshals/Threads/VMStackFrameMarshal.cs`
  - field `TS1UserEventPhase` line 19; read `Version >= 40` line 49;
    written line 66 (native saves preserve the phase once set).
- `TSOClient/tso.simantics/Engine/VMStackFrame.cs` line 71 (runtime field);
  copied by `VMRoutingFrame`/`VMDirectControlFrame` save paths (1250/714).
- `TSOClient/tso.simantics/Primitives/VMSpecialEffect.cs` 17-43: the restored
  prepare/dispatch/complete phase machine consuming `TS1UserEventPhase`.
- `TSOClient/tso.simantics/VMContext.cs` 346-351: opcode 35 → VMSpecialEffect.
- JIT note: the phase lives on `VMStackFrame` and is consumed inside the
  registered primitive handler; JIT4 compiled trees call the same handler, so
  no JIT frame serialization change is involved in the import fix (consistent
  with r240-pip/primitive-law.md "Save version 40 appends one byte …").

## 3. Original addresses and offset-verification notes

| address (file) | symbol (r141 index = addr+2) | role |
|---|---|---|
| `0x153080` (924 B) | `ReconStream__9TreeStackFP11ReconBufferlP14BehaviorFinder` | the frame serializer (layout above) |
| `0xfac70` (776 B) | `TryUserEvent__8cXObjectFP9StackElemP10XPrimParam` | opcode35; phase latch at StackElem+8; StackElem+4 = stack object |
| `0x1524b0` | `GetTreeID__9StackElemCFv` | proves frame+0 = tree ID (bit15 masked) |
| `0x153000` / `0x153040` | `GetLocals__9StackElemFv` / `GetParams__9StackElemFv` | prove +6/+7 count bytes, params at +0x10 before locals |
| `0x1538c0` | `DoNodeAction__7TreeSimFP9StackElem` | proves frame+2 = node/IP (< 0xFB); frame+0 bit15 branch; frame+0xc = code owner object |
| `0x11e9c0` / `0x11e7e0` / `0x11ebe0` | `Recon16/32/8` | compressed readers, schemes in F5 |
| `0x11f080` | `ReconCmprInt__11ReconBufferFPlP6Scheme` | present bit + 2-bit selector + width bits + sign-extend |
| `0x11f7f0` | `__ct__9PrecisionFi` | entry = {width, 0xFFFFFFFF << (width-1)} |
| `0x11f570..0x11f7a4` | scheme initializer | builds the four tables; globals at TOC-0x5868/-0x5864/-0x586c/-0x5870 |
| `0xfc050` / `0xfbe90` | `StreamHeader` / `DoStream` (cXObjectStream) | object fourcc dispatch, version flow |
| `0xc5cc0` / `0xc59a0` | `ReconHeader` / `ReconStream` (cXObject) | object prefix field order; calls TreeStack at `0xc5b20` with the SAME version, finder = `*(module+0x80)` |
| `0xe7c70` | `DoStream__12ObjectModuleFP11ReconBufferl` | object list, per-object marks, tutorial owner (≥0x2f compression bool) |
| `0xe86e0` / `0xe94f0` / `0xe85xx` | `Save` / chunk open / load helper | OBJM write/read; 'MjbO' magic at `0xe8630-0xe863c` (read) and `0xe8754` (write) |
| `0x11e570` / `0x11e510` | `ReconMark` / `ReadToNextMark` | 4-byte marks; r239 trailer contract confirmed |
| `0xc5850` | `ReconSlots` | slots = s16 count + count×(s16,s16), version ≥ 5 (post-stack) |
| `0x11fe80` | `DoStream__9RelMatrixFP11ReconBufferl` | post-stack relationship container (count i32, negative = delta form) |

Version gates inside `TreeStack::ReconStream` (verified from branches):
version ≤ 10 → nothing read; version ≥ 25 → new layout; version > 25 → the
leading stack-object s16. All real OBJM versions (51+) take the full layout.

PEF/TOC notes: section table starts at file offset 44 for this image; code
vaddr ↔ file verified via the r240-pip anchor ("TOC[-0x4f58] → code+0x59b7a0 =
file 0x5a4630", i.e. file = vaddr + 0x8e90); scheme tables are in the packed
data section (container `0x5c22f0`, packed `0x6d834`, unpacked `0x7bf80`,
unpack verified exactly) and were located through the Precision initializer
rather than raw TOC dereference (PEF constants hold relocations, not raw
pointers).

## 4. Independent fixture

In this directory (all derived text/scripts, no game assets):

- `disasm.py` — capstone-based big-endian PPC disassembler with symbol
  annotation (used for every decode quoted above).
- `pef_toc.py` — PEF section parser + packed-section unpacker; resolves the
  TOC-relative scheme globals; re-verifies the r232/r240-pip TOC anchors.
- `parse_objm_frames.py` — independent OBJM parser implementing the recovered
  layout; validates every object against its mark (no overshoot allowed),
  the mark chain, the final mark (= 0), stack byte size, and frame sanity
  (locals ≤ 20, params ≤ 12, node < 0xFB, owner < 0x1000).
- `run-output.txt` / `objm-frame-corpus.json` — full output and decoded corpus.

Abridged output (648 files = all `UserData*/Houses/House*.iff`):

```json
{
  "files": 648, "objm": 648,
  "objects": 283433, "stacks": 283433, "frames": 547889,
  "phase": { "2": 24, "1": 8 },
  "state_values": { "0": 547793, "3": 56, "2": 24, "15": 8, "1": 8 },
  "errors": [],
  "versions": { "51": 8, "56": 8, "62": 34, "63": 46, "66": 80, "68": 72,
                "69": 248, "72": 72, "73": 80 },
  "mark_self_ok": 283433, "mark_bad": 0,
  "owners_nonzero": 24,
  "final_marks": { "0": 648 },
  "sizes": { "512": 283433 }
}
```

Sample decoded continuation (also in `objm-frame-corpus.json`):

```json
{"file": "House56.iff", "obj": 292, "stack_object": 333, "tree": 4110,
 "node": 1, "locals_n": 3, "params_n": 0, "params": [],
 "locals": [4124, 255, 371], "primitive_state": 2, "code_owner_objtype": 146}
```

(tree 4110 node 1 = catalogued opcode35 site in `Social2.iff` — a saved
picture-in-picture continuation, phase 2 = dispatched, awaiting completion.)

Reproduce: `python3 tools/iff-dump/r241-helper-objm/parse_objm_frames.py`
(read-only; opens game data in place).

## 5. Proposed minimal fix (DESCRIPTION ONLY — not applied)

Single change, in `FreeSO/TSOClient/tso.simantics/Utils/VMTS1ActivatorNew.cs`,
inside the `ConvertThread` frame loop (lines 248-271), after the existing
`VMStackFrameMarshal` initializer:

- Look up the routine for the frame with the existing `GetRoutine(thread.Stack[i])`
  helper (line 73; same pattern as line 352). If the routine resolves and
  `InstructionPointer` indexes an existing instruction, and that instruction's
  opcode == 35 (the VMSpecialEffect primitive), and `frame.PrimitiveState` is
  1 or 2, set `thread.Stack[i].TS1UserEventPhase = (byte)frame.PrimitiveState`.
- Leave `PrimitiveState` unmapped in every other case (observed saved states 3
  and 15 belong to other primitives; `TS1UserEventPhase` only understands
  0/1/2, and VMSpecialEffect is TS1-only).
- Placement note: this must run on the frames that survive, and before/inside
  the `IsRestorableStack` decision only insofar as the gate needs the routine;
  frames already rejected there keep today's behavior. Defensive null checks
  follow the existing `GetRoutine` usage.

Effect: importing an original save whose object was saved mid-PIP-event
resumes at the correct phase — phase 1 re-dispatches (target/eligibility/caption),
phase 2 completes on the next tick — instead of restarting the primitive and
re-issuing `SetGlobal(11, 1)` and a second PIP request. The value then persists
in native saves via the existing marshal version-40 byte. No format, reader,
JIT, or VM changes are required; non-35 continuations are untouched.

## 6. Remaining uncertainties

1. The BehaviorFinder virtual (cSimulator vtable+8) was not disassembled; the
   2-byte width of the code-owner field is proven empirically (whole-corpus
   parse consistency + FSO's shipping reader), not from the finder's own code.
2. Post-stack fields (RelMatrix delta form, slots, sprite flags, multitile/
   portal/person branches, the trailing `UnknownInt`, and the trailing 0xa3
   byte after the tutorial owner) are not fully decoded; the fixture only
   requires each object's parse to stay ≤ its mark. The maintained OBJM.cs
   models these approximately already; none affect the frame layout.
3. The exact original semantic of frame+0 bit15 (a runtime tree flag merged
   around serialization) is characterized but not named; import does not need
   it (fresh frames take the serialized value).
4. The three other phase-1/2 frames (trees 281/4355/4105) were not resolved to
   their primitives; they demonstrate only that other primitives use frame+8,
   which motivates the opcode-35 gate in the fix.
5. The maintainer's own OBJT lookups (`objt.Entries[frame.CodeOwnerObjType - 1]`)
   assume a nonempty OBJT table; unrelated to this fix but adjacent if the
   gate's routine lookup is added for frames whose routine is missing
   (IsRestorableStack already rejects those first).
